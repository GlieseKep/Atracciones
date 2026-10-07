using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.Application.Abstractions.Idempotency;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Reservations;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Mappers;
using AtraccionesService.Application.Queries.Reservations;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.Application.Validators;
using AtraccionesService.DataManagment.Contracts.Catalog;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Reservations;

namespace AtraccionesService.Application.Services.Catalog;

/// <summary>
/// Reserva independiente (<c>POST /atracciones/{id}/reservations</c>): se confirma al crearse, no genera pedido ni pago.
/// El propietario es el cliente resuelto desde la identidad autenticada.
/// </summary>
public sealed class ReservationService(
    IUnitOfWork unitOfWork,
    IIdempotencyService idempotency,
    CustomerResolver customers,
    SlotRequestValidator slotValidator,
    IPriceCalculator prices,
    BusinessClock clock) : IReservationService
{
    public async Task<ReservationResult> CreateAsync(CreateReservationCommand command, CancellationToken cancellationToken)
    {
        var time = slotValidator.Validate(command.Date, command.Time, command.TicketCount, "ticketCount", extra =>
        {
            extra.RequiredText(command.CustomerName, "customerName", 200);
            extra.Email(command.CustomerEmail, "customerEmail");
        });

        var payload = new { command.AttractionId, command.Date, command.Time, command.TicketCount, command.CustomerName, command.CustomerEmail };
        return await idempotency.ExecuteAsync(command.User, IdempotentOperations.CreateReservation, command.IdempotencyKey, payload, async ct =>
        {
            var customer = await customers.RequireAsync(command.User, ct);
            var attraction = await unitOfWork.Attractions.GetByIdAsync(command.AttractionId, ct)
                ?? throw new NotFoundException("La atracción no existe.");
            var slot = await unitOfWork.Availability.GetSlotAsync(command.AttractionId, command.Date, time, ct)
                ?? throw new ConflictException(ConflictException.SlotUnavailable, "La atracción no opera en la fecha y hora seleccionadas.");

            if (!await unitOfWork.Availability.TryReserveQuantityAsync(slot.Id, command.TicketCount, ct))
            {
                throw new ConflictException(ConflictException.InsufficientAvailability, "No hay cupos suficientes para la franja solicitada.");
            }

            var now = clock.UtcNow;
            var reservation = Reservation.Create(command.AttractionId, customer.Customer.Id, command.Date, time, command.TicketCount,
                prices.Total(attraction, command.TicketCount), command.CustomerName, command.CustomerEmail, ReservationStatus.CONFIRMED, now);

            await unitOfWork.Reservations.AddAsync(reservation, ct);
            await unitOfWork.Inventory.AddAsync(
                new InventoryMovement(Guid.NewGuid(), command.AttractionId, slot.Id, command.TicketCount, InventoryMovementType.CONFIRMED, null, reservation.Id, now),
                ct);
            return reservation.ToResult();
        }, cancellationToken);
    }

    public async Task<ReservationResult> CancelAsync(CancelReservationCommand command, CancellationToken cancellationToken)
    {
        new ValidationErrors().RequiredText(command.Reason, "reason", 500).ThrowIfAny();

        return await idempotency.ExecuteAsync(command.User, IdempotentOperations.CancelReservation, command.IdempotencyKey,
            new { command.ReservationId, command.Reason }, async ct =>
            {
                var customer = await customers.FindAsync(command.User, ct);
                var reservation = customer is null
                    ? null
                    : await unitOfWork.Reservations.GetByIdForCustomerAsync(command.ReservationId, customer.Customer.Id, ct);
                if (reservation is null)
                {
                    throw new NotFoundException("La reserva no existe.");
                }

                if (await unitOfWork.Orders.GetByReservationIdAsync(reservation.Id, ct) is not null)
                {
                    throw new ConflictException("RESERVATION_MANAGED_BY_ORDER",
                        "La reserva pertenece a un pedido; debe cancelarse mediante el pedido.");
                }

                if (!reservation.CanCancel)
                {
                    throw new ConflictException(ConflictException.InvalidStateTransition, $"No se puede cancelar una reserva {reservation.Status}.");
                }

                if (!clock.IsFuture(reservation.Date, reservation.Time))
                {
                    throw new ConflictException("RESERVATION_ALREADY_STARTED", "No se puede cancelar una reserva cuya franja ya comenzó.");
                }

                reservation.Cancel(command.Reason);
                await unitOfWork.Reservations.UpdateAsync(reservation, ct);

                var slot = await unitOfWork.Availability.GetSlotAsync(reservation.AttractionId, reservation.Date, reservation.Time, ct);
                if (slot is not null)
                {
                    await unitOfWork.Availability.ReleaseQuantityAsync(slot.Id, reservation.TicketCount, ct);
                    await unitOfWork.Inventory.AddAsync(
                        new InventoryMovement(Guid.NewGuid(), reservation.AttractionId, slot.Id, -reservation.TicketCount,
                            InventoryMovementType.RELEASED, null, reservation.Id, clock.UtcNow),
                        ct);
                }

                return reservation.ToResult();
            }, cancellationToken);
    }

    public async Task<PaginationResult<ReservationResult>> ListAsync(GetReservationsQuery query, CancellationToken cancellationToken)
    {
        PaginationValidator.Validate(query.Limit, query.Offset);
        ReservationStatus? status = null;
        new ValidationErrors()
            .When(query.Status is not null && !TryParseStatus(query.Status, out status), "status", "status no es un estado de reserva válido.")
            .When(query.FromDate is { } from && query.ToDate is { } to && to < from, "toDate", "toDate debe ser igual o posterior a fromDate.")
            .ThrowIfAny();

        var customer = await customers.FindAsync(query.User, cancellationToken);
        if (customer is null)
        {
            return new PaginationResult<ReservationResult>([], 0, query.Limit, query.Offset);
        }

        var page = await unitOfWork.Reservations.GetByCustomerAsync(
            customer.Customer.Id,
            new ReservationFilter(status, query.FromDate, query.ToDate, query.SortDescending),
            new PaginationRequest(query.Limit, query.Offset),
            cancellationToken);

        return new PaginationResult<ReservationResult>(page.Items.Select(r => r.ToResult()).ToList(), page.TotalItems, query.Limit, query.Offset);
    }

    public async Task<ReservationResult> GetAsync(GetReservationQuery query, CancellationToken cancellationToken)
    {
        var customer = await customers.FindAsync(query.User, cancellationToken);
        var reservation = customer is null
            ? null
            : await unitOfWork.Reservations.GetByIdForCustomerAsync(query.ReservationId, customer.Customer.Id, cancellationToken);

        // Las reservas ajenas se informan como inexistentes para no revelar su existencia.
        return reservation?.ToResult() ?? throw new NotFoundException("La reserva no existe.");
    }

    private static bool TryParseStatus(string value, out ReservationStatus? status)
    {
        var ok = Enum.TryParse<ReservationStatus>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed);
        status = ok ? parsed : null;
        return ok;
    }
}
