namespace AtraccionesService.Application.ResultModels;

/// <summary>Estado y referencia segura de la simulación de pago; nunca incluye datos financieros.</summary>
public sealed record PaymentSummaryResult(
    Guid Id,
    string PaymentMethod,
    string Status,
    string? GatewayReference);

public sealed record PurchaseResult(
    Guid PurchaseId,
    Guid OrderId,
    Guid AttractionId,
    DateOnly Date,
    string Time,
    int Quantity,
    Money UnitPrice,
    decimal TotalAmount,
    string Currency,
    string Status,
    Guid? ReservationId,
    PaymentSummaryResult? Payment,
    DateTimeOffset? HoldExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record OrderItemResult(
    Guid Id,
    Guid AttractionId,
    DateOnly Date,
    string Time,
    int Quantity,
    Money UnitPrice,
    string Status);

public sealed record OrderResult(
    Guid Id,
    Guid CustomerId,
    Guid? PurchaseId,
    Guid? ReservationId,
    string Status,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderItemResult> Items,
    PaymentSummaryResult? PaymentSimulation);

public sealed record OrderEventResult(
    string EventType,
    string? PreviousStatus,
    string NewStatus,
    DateTimeOffset CreatedAt);

public sealed record OrderEventsResult(Guid OrderId, IReadOnlyList<OrderEventResult> Events);

public sealed record PaymentSimulationResult(
    Guid Id,
    Guid OrderId,
    string PaymentMethod,
    string Status,
    decimal Amount,
    string Currency,
    string? GatewayReference,
    DateTimeOffset CreatedAt);
