using AtraccionesService.Application.Abstractions.Authorization;

namespace AtraccionesService.Application.Commands.Reservations;

public sealed record CreateReservationCommand(
    Guid AttractionId,
    DateOnly Date,
    string Time,
    int TicketCount,
    string CustomerName,
    string CustomerEmail,
    Guid IdempotencyKey,
    AuthenticatedUser User);

public sealed record CancelReservationCommand(
    Guid ReservationId,
    string Reason,
    Guid IdempotencyKey,
    AuthenticatedUser User);
