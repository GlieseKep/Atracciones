namespace AtraccionesService.Application.ResultModels;

/// <summary>Reserva visible para su propietario. <see cref="Status"/>: PENDING, CONFIRMED o CANCELLED.</summary>
public sealed record ReservationResult(
    Guid ReservationId,
    Guid AttractionId,
    string Status,
    DateOnly Date,
    string Time,
    int TicketCount,
    Money TotalPrice);
