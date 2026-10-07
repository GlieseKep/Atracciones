using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.QueryModels;
using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Reservations;

namespace AtraccionesService.DataManagment.Contracts.Catalog;

public interface IAttractionRepository
{
    /// <summary>Agregado completo con ubicaciones, fotos, operador, categorías, insignias, incluidos e idiomas.</summary>
    Task<Attraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Attraction>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<PagedResult<Attraction>> ListAsync(PaginationRequest page, CancellationToken cancellationToken);

    /// <summary>Resúmenes sin relaciones. <c>Sort</c>: <c>name</c> (por defecto), <c>price</c> o <c>rating</c>, con <c>-</c> para descendente.</summary>
    Task<PagedResult<AttractionQueryResult>> ListSummariesAsync(PaginationRequest page, CancellationToken cancellationToken);

    Task<PagedResult<Attraction>> SearchAsync(AttractionSearchCriteria criteria, PaginationRequest page, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Attraction attraction, CancellationToken cancellationToken);

    /// <summary>Actualiza datos y reemplaza las colecciones asociadas.</summary>
    Task UpdateAsync(Attraction attraction, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IAvailabilityRepository
{
    Task<AvailabilitySlot?> GetSlotByIdAsync(Guid slotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AvailabilitySlot>> GetSlotsAsync(Guid attractionId, DateOnly date, CancellationToken cancellationToken);

    /// <summary>Franjas de una atracción entre dos fechas inclusive, ordenadas por fecha y hora.</summary>
    Task<IReadOnlyList<AvailabilitySlot>> GetSlotsInRangeAsync(Guid attractionId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<AvailabilitySlot?> GetSlotAsync(Guid attractionId, DateOnly date, TimeOnly time, CancellationToken cancellationToken);

    Task AddAsync(AvailabilitySlot slot, CancellationToken cancellationToken);

    /// <summary>
    /// Inserta la franja respetando la unicidad <c>atracción + fecha + hora</c>. Devuelve <c>false</c> sin efectos si ya existe.
    /// </summary>
    Task<bool> AddWithConcurrencyCheckAsync(AvailabilitySlot slot, CancellationToken cancellationToken);

    /// <summary>
    /// Persiste capacidad y cupos reservados solo si la versión almacenada es <paramref name="expectedVersion"/>
    /// (concurrencia optimista). Devuelve <c>false</c> si otra operación la modificó.
    /// </summary>
    Task<bool> UpdateIfVersionMatchesAsync(AvailabilitySlot slot, long expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Incrementa atómicamente los cupos reservados si quedan suficientes. Devuelve <c>false</c> sin cambios cuando no
    /// alcanzan (la implementación usa versión de fila o actualización condicional; no se asume bloqueo de fila).
    /// </summary>
    Task<bool> TryReserveQuantityAsync(Guid slotId, int quantity, CancellationToken cancellationToken);

    /// <summary>Libera cupos reservados previamente.</summary>
    Task ReleaseQuantityAsync(Guid slotId, int quantity, CancellationToken cancellationToken);
}

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<Reservation?> GetByIdForCustomerAsync(Guid reservationId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>Comprueba que la reserva pertenece al cliente.</summary>
    Task<bool> ExistsForCustomerAsync(Guid reservationId, Guid customerId, CancellationToken cancellationToken);

    Task<PagedResult<Reservation>> GetByCustomerAsync(Guid customerId, ReservationFilter filter, PaginationRequest page, CancellationToken cancellationToken);

    /// <summary>Reservas de una atracción sin datos personales del cliente (uso operativo/administrativo).</summary>
    Task<PagedResult<ReservationQueryResult>> GetByAttractionAsync(Guid attractionId, ReservationFilter filter, PaginationRequest page, CancellationToken cancellationToken);

    /// <summary>Indica si la atracción tiene reservas PENDING o CONFIRMED con fecha igual o posterior a <paramref name="fromDate"/>.</summary>
    Task<bool> HasActiveByAttractionAsync(Guid attractionId, DateOnly fromDate, CancellationToken cancellationToken);

    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);

    Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken);

    /// <summary>
    /// Cambia el estado solo si el actual es <paramref name="expected"/> (actualización condicional). La validez de la
    /// transición la decide el dominio antes de llamar. Devuelve <c>false</c> si el estado ya cambió.
    /// </summary>
    Task<bool> UpdateStatusAsync(Guid reservationId, ReservationStatus expected, ReservationStatus next, string? reason, CancellationToken cancellationToken);
}
