using AtraccionesService.Domain.Common;

namespace AtraccionesService.Domain.Ecommerce;

public enum OrderStatus
{
    PENDING_PAYMENT,
    PAID,
    FULFILLED,
    CANCELLED,
    PARTIALLY_REFUNDED,
    REFUNDED
}

/// <summary>Compra directa de una franja; origina como máximo un pedido.</summary>
public sealed record Purchase(
    Guid Id,
    Guid CustomerId,
    Guid AttractionId,
    DateOnly ServiceDate,
    TimeOnly ServiceTime,
    int Quantity,
    Money UnitPrice,
    Money Total,
    Guid RequestIdempotencyKey,
    DateTimeOffset CreatedAt);

public sealed record OrderItem(
    Guid Id,
    Guid AttractionId,
    DateOnly ServiceDate,
    TimeOnly ServiceTime,
    int Quantity,
    Money UnitPrice,
    Guid AvailabilitySlotId);

/// <summary>
/// Pedido con snapshot de precios. Transiciones: PENDING_PAYMENT → PAID | CANCELLED;
/// PAID → FULFILLED | CANCELLED | PARTIALLY_REFUNDED | REFUNDED; PARTIALLY_REFUNDED → PARTIALLY_REFUNDED | REFUNDED.
/// </summary>
public sealed class Order
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions = new Dictionary<OrderStatus, OrderStatus[]>
    {
        [OrderStatus.PENDING_PAYMENT] = [OrderStatus.PAID, OrderStatus.CANCELLED],
        [OrderStatus.PAID] = [OrderStatus.FULFILLED, OrderStatus.CANCELLED, OrderStatus.PARTIALLY_REFUNDED, OrderStatus.REFUNDED],
        [OrderStatus.PARTIALLY_REFUNDED] = [OrderStatus.PARTIALLY_REFUNDED, OrderStatus.REFUNDED],
    };

    private readonly List<OrderItem> _items;

    private Order(
        Guid id, Guid customerId, Guid? purchaseId, Guid? reservationId, OrderStatus status, Money total,
        IEnumerable<OrderItem> items, DateTimeOffset? holdExpiresAt, string? cancellationReason, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        CustomerId = customerId;
        PurchaseId = purchaseId;
        ReservationId = reservationId;
        Status = status;
        Total = total;
        _items = items.ToList();
        HoldExpiresAt = holdExpiresAt;
        CancellationReason = cancellationReason;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;

        if (_items.Count == 0)
        {
            throw new DomainException("EMPTY_ORDER", "Un pedido requiere al menos un elemento.");
        }
    }

    public Guid Id { get; }

    public Guid CustomerId { get; }

    public Guid? PurchaseId { get; }

    public Guid? ReservationId { get; }

    public OrderStatus Status { get; private set; }

    public Money Total { get; }

    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>Fin de la retención temporal de cupos mientras el pedido espera el pago.</summary>
    public DateTimeOffset? HoldExpiresAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Order PlacePending(
        Guid customerId, Guid? purchaseId, Guid? reservationId, IReadOnlyList<OrderItem> items, DateTimeOffset holdExpiresAt, DateTimeOffset now)
    {
        var currency = items.Select(i => i.UnitPrice.Currency).Distinct().ToList();
        if (currency.Count != 1)
        {
            throw new DomainException("MIXED_CURRENCY", "Todos los elementos del pedido deben usar la misma moneda.");
        }

        var total = new Money(currency[0], items.Sum(i => i.UnitPrice.Amount * i.Quantity));
        return new Order(Guid.NewGuid(), customerId, purchaseId, reservationId, OrderStatus.PENDING_PAYMENT, total, items,
            holdExpiresAt, null, now, now);
    }

    public static Order Restore(
        Guid id, Guid customerId, Guid? purchaseId, Guid? reservationId, OrderStatus status, Money total,
        IEnumerable<OrderItem> items, DateTimeOffset? holdExpiresAt, string? cancellationReason, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, customerId, purchaseId, reservationId, status, total, items, holdExpiresAt, cancellationReason, createdAt, updatedAt);

    public bool CanTransitionTo(OrderStatus next) => Transitions.TryGetValue(Status, out var allowed) && allowed.Contains(next);

    public bool IsHoldExpired(DateTimeOffset now) => Status == OrderStatus.PENDING_PAYMENT && HoldExpiresAt is { } expires && expires <= now;

    /// <summary>Aplica una transición permitida y devuelve el estado anterior.</summary>
    public OrderStatus TransitionTo(OrderStatus next, DateTimeOffset now, string? reason = null)
    {
        if (!CanTransitionTo(next))
        {
            throw new DomainException("INVALID_STATE_TRANSITION", $"Transición de pedido no permitida: {Status} → {next}.");
        }

        var previous = Status;
        Status = next;
        UpdatedAt = now;
        if (next != OrderStatus.PENDING_PAYMENT)
        {
            HoldExpiresAt = null;
        }

        if (next == OrderStatus.CANCELLED)
        {
            CancellationReason = Guard.NotBlank(reason, "reason", 500);
        }

        return previous;
    }
}

/// <summary>Registro inmutable de un cambio de estado del pedido.</summary>
public sealed record OrderEvent(
    Guid Id,
    Guid OrderId,
    string EventType,
    OrderStatus? PreviousStatus,
    OrderStatus NewStatus,
    DateTimeOffset CreatedAt);
