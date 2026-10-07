using AtraccionesService.Domain.Common;

namespace AtraccionesService.Domain.Catalog;

/// <summary>
/// Franja reservable de una atracción (fecha y hora locales). Una fila por atracción, fecha y hora.
/// <see cref="Version"/> permite concurrencia optimista en DataAccess.
/// </summary>
public sealed class AvailabilitySlot
{
    public AvailabilitySlot(Guid id, Guid attractionId, DateOnly date, TimeOnly time, int capacity, int reservedQuantity, long version)
    {
        if (capacity < 0 || reservedQuantity < 0 || reservedQuantity > capacity)
        {
            throw new DomainException("INVALID_CAPACITY", "La capacidad reservada no puede superar la capacidad total.");
        }

        Id = id;
        AttractionId = attractionId;
        Date = date;
        Time = time;
        Capacity = capacity;
        ReservedQuantity = reservedQuantity;
        Version = version;
    }

    public Guid Id { get; }

    public Guid AttractionId { get; }

    public DateOnly Date { get; }

    public TimeOnly Time { get; }

    public int Capacity { get; }

    public int ReservedQuantity { get; private set; }

    public long Version { get; private set; }

    public int AvailableSpots => Capacity - ReservedQuantity;

    public bool CanReserve(int quantity) => quantity > 0 && quantity <= AvailableSpots;

    public void Reserve(int quantity)
    {
        if (!CanReserve(quantity))
        {
            throw new DomainException("INSUFFICIENT_AVAILABILITY", "No hay cupos suficientes en la franja.");
        }

        ReservedQuantity += quantity;
        Version++;
    }

    public void Release(int quantity)
    {
        Guard.Positive(quantity, "quantity");
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        Version++;
    }
}
