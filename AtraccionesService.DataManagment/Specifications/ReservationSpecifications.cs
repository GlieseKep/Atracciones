using AtraccionesService.Domain.Reservations;

namespace AtraccionesService.DataManagment.Specifications;

/// <summary>
/// Filtros y orden de reservas. La regla de acceso (cliente propietario o atracción) la fija el método del repositorio
/// que recibe esta especificación; nunca un valor enviado por el cliente sin validar.
/// </summary>
public sealed record ReservationFilter(ReservationStatus? Status, DateOnly? FromDate, DateOnly? ToDate, bool SortDescending);
