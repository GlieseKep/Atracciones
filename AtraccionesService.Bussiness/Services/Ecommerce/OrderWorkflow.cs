using System.Text.Json;
using AtraccionesService.Application.Abstractions.Ecommerce;
using AtraccionesService.Application.DependencyInjection;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Reservations;
using Microsoft.Extensions.Options;

namespace AtraccionesService.Application.Services.Ecommerce;

public sealed record PlacedOrder(Purchase Purchase, Order Order, Reservation Reservation);

/// <summary>
/// Pasos compartidos por compra directa, pedidos y pagos. Se ejecutan dentro de la transacción del caso de uso que los invoca:
/// validar disponibilidad, tomar cupos, fijar el precio, crear reserva/compra/pedido y registrar eventos.
/// </summary>
public sealed class OrderWorkflow(
    IUnitOfWork unitOfWork,
    IPriceCalculator prices,
    IPaymentSimulationPolicy paymentPolicy,
    BusinessClock clock,
    IOptions<ApplicationOptions> options)
{
    public async Task<PlacedOrder> PlacePendingOrderAsync(
        CustomerContext customer, Guid attractionId, DateOnly date, TimeOnly time, int quantity, Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var attraction = await unitOfWork.Attractions.GetByIdAsync(attractionId, cancellationToken)
            ?? throw new NotFoundException("La atracción no existe.");
        var slot = await unitOfWork.Availability.GetSlotAsync(attractionId, date, time, cancellationToken)
            ?? throw new ConflictException(ConflictException.SlotUnavailable, "La atracción no opera en la fecha y hora seleccionadas.");

        if (!await unitOfWork.Availability.TryReserveQuantityAsync(slot.Id, quantity, cancellationToken))
        {
            throw new ConflictException(ConflictException.InsufficientAvailability, "No hay cupos suficientes para la franja solicitada.");
        }

        var unitPrice = prices.UnitPrice(attraction);
        var total = prices.Total(attraction, quantity);
        var reservation = Reservation.Create(attractionId, customer.Customer.Id, date, time, quantity, total,
            customer.Customer.BillingName ?? customer.User.Email, customer.Customer.BillingEmail ?? customer.User.Email,
            ReservationStatus.PENDING, now);
        var purchase = new Purchase(Guid.NewGuid(), customer.Customer.Id, attractionId, date, time, quantity, unitPrice, total, idempotencyKey, now);
        var order = Order.PlacePending(customer.Customer.Id, purchase.Id, reservation.Id,
            [new OrderItem(Guid.NewGuid(), attractionId, date, time, quantity, unitPrice, slot.Id)],
            now.AddMinutes(options.Value.HoldMinutes), now);

        await unitOfWork.Reservations.AddAsync(reservation, cancellationToken);
        await unitOfWork.Purchases.AddAsync(purchase, cancellationToken);
        await unitOfWork.Orders.AddAsync(order, cancellationToken);
        await unitOfWork.Inventory.AddAsync(
            new InventoryMovement(Guid.NewGuid(), attractionId, slot.Id, quantity, InventoryMovementType.CONFIRMED, purchase.Id, reservation.Id, now),
            cancellationToken);
        await unitOfWork.OrderEvents.AddAsync(
            new OrderEvent(Guid.NewGuid(), order.Id, "ORDER_CREATED", null, OrderStatus.PENDING_PAYMENT, now), cancellationToken);

        return new PlacedOrder(purchase, order, reservation);
    }

    /// <summary>
    /// Crea la simulación y procesa un intento determinista. Si se aprueba, liquida el pago, marca el pedido PAID y confirma
    /// la reserva. Un rechazo o fallo deja el pedido PENDING_PAYMENT para reintentos dentro del límite configurado.
    /// </summary>
    public async Task<PaymentSimulation> ProcessPaymentAsync(Order order, PaymentMethod method, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var previousAttempts = (await unitOfWork.Payments.GetByOrderAsync(order.Id, cancellationToken)).Count;
        if (previousAttempts >= options.Value.MaxPaymentAttemptsPerOrder)
        {
            throw new ConflictException("PAYMENT_ATTEMPTS_EXCEEDED", "Se alcanzó el número máximo de intentos de pago para el pedido.");
        }

        var payment = PaymentSimulation.Start(order.Id, method, order.Total, now);
        await unitOfWork.Payments.AddAsync(payment, cancellationToken);
        await AddPaymentEventAsync(payment, "PAYMENT_REQUESTED", now, cancellationToken);

        var outcome = paymentPolicy.Evaluate(order.Total, method, previousAttempts + 1);
        await unitOfWork.Payments.AddAttemptAsync(
            new PaymentAttempt(Guid.NewGuid(), payment.Id, previousAttempts + 1, outcome.Status, outcome.ResponseCode, outcome.ResponseMessage, now),
            cancellationToken);

        if (outcome.Status == PaymentStatus.AUTHORIZED)
        {
            payment.TransitionTo(PaymentStatus.AUTHORIZED, now);
            await AddPaymentEventAsync(payment, "PAYMENT_AUTHORIZED", now, cancellationToken);
            payment.TransitionTo(PaymentStatus.SETTLED, now);
            await AddPaymentEventAsync(payment, "PAYMENT_SETTLED", now, cancellationToken);
            await MarkPaidAsync(order, now, cancellationToken);
        }
        else
        {
            payment.TransitionTo(outcome.Status, now, outcome.ResponseMessage);
            await AddPaymentEventAsync(payment, $"PAYMENT_{outcome.Status}", now, cancellationToken);
        }

        await unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
        return payment;
    }

    /// <summary>Cancela un pedido pendiente: libera cupos, cancela la reserva asociada y registra eventos.</summary>
    public async Task CancelPendingOrderAsync(Order order, string reason, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var previous = order.TransitionTo(OrderStatus.CANCELLED, now, reason);
        await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
        await unitOfWork.OrderEvents.AddAsync(new OrderEvent(Guid.NewGuid(), order.Id, "ORDER_CANCELLED", previous, OrderStatus.CANCELLED, now), cancellationToken);

        foreach (var item in order.Items)
        {
            await unitOfWork.Availability.ReleaseQuantityAsync(item.AvailabilitySlotId, item.Quantity, cancellationToken);
            await unitOfWork.Inventory.AddAsync(
                new InventoryMovement(Guid.NewGuid(), item.AttractionId, item.AvailabilitySlotId, -item.Quantity, InventoryMovementType.RELEASED,
                    order.PurchaseId, order.ReservationId, now),
                cancellationToken);
        }

        if (order.ReservationId is { } reservationId
            && await unitOfWork.Reservations.GetByIdAsync(reservationId, cancellationToken) is { CanCancel: true } reservation)
        {
            reservation.Cancel(reason);
            await unitOfWork.Reservations.UpdateAsync(reservation, cancellationToken);
        }
    }

    private async Task MarkPaidAsync(Order order, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var previous = order.TransitionTo(OrderStatus.PAID, now);
        await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
        await unitOfWork.OrderEvents.AddAsync(new OrderEvent(Guid.NewGuid(), order.Id, "PAYMENT_SETTLED", previous, OrderStatus.PAID, now), cancellationToken);

        if (order.ReservationId is { } reservationId
            && await unitOfWork.Reservations.GetByIdAsync(reservationId, cancellationToken) is { Status: ReservationStatus.PENDING } reservation)
        {
            reservation.Confirm();
            await unitOfWork.Reservations.UpdateAsync(reservation, cancellationToken);
        }
    }

    private Task AddPaymentEventAsync(PaymentSimulation payment, string eventType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            status = payment.Status.ToString(),
            amount = payment.Amount.Amount,
            currency = payment.Amount.Currency,
            gatewayReference = payment.GatewayReference,
            failureReason = payment.FailureReason,
        });
        return unitOfWork.PaymentEvents.AddAsync(new PaymentEvent(Guid.NewGuid(), payment.Id, eventType, payload, now), cancellationToken);
    }
}
