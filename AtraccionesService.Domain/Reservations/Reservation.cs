using AtraccionesService.Domain.Common;

namespace AtraccionesService.Domain.Reservations;

public enum ReservationStatus
{
    PENDING,
    CONFIRMED,
    CANCELLED
}

/// <summary>
/// Reserva de una franja. Transiciones: PENDING → CONFIRMED | CANCELLED; CONFIRMED → CANCELLED.
/// </summary>
public sealed class Reservation
{
    private Reservation(
        Guid id, Guid attractionId, Guid customerId, DateOnly date, TimeOnly time, int ticketCount, Money totalPrice,
        string customerName, string customerEmail, ReservationStatus status, string? cancellationReason, DateTimeOffset createdAt)
    {
        Id = id;
        AttractionId = attractionId;
        CustomerId = customerId;
        Date = date;
        Time = time;
        TicketCount = Guard.Positive(ticketCount, "ticketCount");
        TotalPrice = totalPrice;
        CustomerName = Guard.NotBlank(customerName, "customerName", 200);
        CustomerEmail = Guard.NotBlank(customerEmail, "customerEmail", 254);
        Status = status;
        CancellationReason = cancellationReason;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid AttractionId { get; }

    public Guid CustomerId { get; }

    public DateOnly Date { get; }

    public TimeOnly Time { get; }

    public int TicketCount { get; }

    public Money TotalPrice { get; }

    public string CustomerName { get; }

    public string CustomerEmail { get; }

    public ReservationStatus Status { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static Reservation Create(
        Guid attractionId, Guid customerId, DateOnly date, TimeOnly time, int ticketCount, Money totalPrice,
        string customerName, string customerEmail, ReservationStatus initialStatus, DateTimeOffset now)
    {
        if (initialStatus == ReservationStatus.CANCELLED)
        {
            throw new DomainException("INVALID_INITIAL_STATUS", "Una reserva no puede crearse cancelada.");
        }

        return new Reservation(Guid.NewGuid(), attractionId, customerId, date, time, ticketCount, totalPrice,
            customerName, customerEmail, initialStatus, null, now);
    }

    public static Reservation Restore(
        Guid id, Guid attractionId, Guid customerId, DateOnly date, TimeOnly time, int ticketCount, Money totalPrice,
        string customerName, string customerEmail, ReservationStatus status, string? cancellationReason, DateTimeOffset createdAt) =>
        new(id, attractionId, customerId, date, time, ticketCount, totalPrice, customerName, customerEmail, status, cancellationReason, createdAt);

    public bool CanCancel => Status is ReservationStatus.PENDING or ReservationStatus.CONFIRMED;

    public void Confirm()
    {
        if (Status != ReservationStatus.PENDING)
        {
            throw new DomainException("INVALID_STATE_TRANSITION", $"No se puede confirmar una reserva {Status}.");
        }

        Status = ReservationStatus.CONFIRMED;
    }

    public void Cancel(string reason)
    {
        if (!CanCancel)
        {
            throw new DomainException("INVALID_STATE_TRANSITION", $"No se puede cancelar una reserva {Status}.");
        }

        CancellationReason = Guard.NotBlank(reason, "reason", 500);
        Status = ReservationStatus.CANCELLED;
    }
}
