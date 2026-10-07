using AtraccionesService.Application.Abstractions.Authorization;

namespace AtraccionesService.Application.Queries.Reservations;

/// <summary>Historial de reservas del usuario autenticado; Application resuelve el cliente a partir de <see cref="User"/>.</summary>
public sealed record GetReservationsQuery(
    AuthenticatedUser User,
    int Limit,
    int Offset,
    string? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool SortDescending);

public sealed record GetReservationQuery(Guid ReservationId, AuthenticatedUser User);
