using AtraccionesService.Application.Abstractions.Authorization;

namespace AtraccionesService.Application.Commands.Ecommerce;

/// <summary>
/// Compra directa de una franja. Si <see cref="PaymentMethod"/> tiene valor, el pago simulado se procesa en la misma operación.
/// </summary>
public sealed record CreatePurchaseCommand(
    Guid AttractionId,
    DateOnly Date,
    string Time,
    int Quantity,
    string? PaymentMethod,
    Guid IdempotencyKey,
    AuthenticatedUser User);

public sealed record CreateOrderCommand(
    Guid AttractionId,
    DateOnly Date,
    string Time,
    int Quantity,
    Guid IdempotencyKey,
    AuthenticatedUser User);

public sealed record CancelOrderCommand(
    Guid OrderId,
    string Reason,
    Guid IdempotencyKey,
    AuthenticatedUser User);

/// <summary>
/// Solicita una simulación de pago. Application decide el resultado y registra intentos y eventos internamente.
/// </summary>
public sealed record SimulatePaymentCommand(
    Guid OrderId,
    string PaymentMethod,
    decimal Amount,
    string Currency,
    Guid IdempotencyKey,
    AuthenticatedUser User);
