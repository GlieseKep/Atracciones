using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AtraccionesService.Contracts.Ecommerce;

/// <summary>Métodos admitidos por la pasarela simulada. No se aceptan datos bancarios.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))]
public enum PaymentMethod
{
    CARD,
    BANK_TRANSFER
}

/// <summary>Estados de pedido: PENDING_PAYMENT → PAID | CANCELLED; PAID → FULFILLED | CANCELLED | PARTIALLY_REFUNDED | REFUNDED.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    PENDING_PAYMENT,
    PAID,
    FULFILLED,
    CANCELLED,
    PARTIALLY_REFUNDED,
    REFUNDED
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentSimulationStatus>))]
public enum PaymentSimulationStatus
{
    PENDING,
    AUTHORIZED,
    SETTLED,
    REJECTED,
    FAILED,
    CANCELLED,
    PARTIALLY_REFUNDED,
    REFUNDED
}

/// <summary>
/// Cuerpo de <c>POST /attractions/{attractionId}/purchase</c>: compra directa de una franja concreta.
/// El precio lo calcula el servidor. Si se indica <see cref="PaymentMethod"/>, el pago simulado se procesa en la misma operación;
/// en caso contrario se crea un pedido <c>PENDING_PAYMENT</c> con retención temporal de cupos.
/// </summary>
public sealed class CreatePurchaseRequest
{
    [Required]
    public DateOnly? Date { get; set; }

    [Required]
    [RegularExpression(Common.ValidationPatterns.LocalTime, ErrorMessage = "time debe tener formato HH:mm.")]
    public string Time { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Quantity { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }
}

public sealed class PurchaseResponse
{
    public Guid PurchaseId { get; init; }

    public Guid OrderId { get; init; }

    public Guid AttractionId { get; init; }

    public DateOnly Date { get; init; }

    public string Time { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public Common.PriceDto UnitPrice { get; init; } = new();

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public OrderStatus Status { get; init; }

    public Guid? ReservationId { get; init; }

    public PaymentSummaryDto? Payment { get; init; }

    /// <summary>Fin de la retención temporal de cupos cuando el pago no se confirmó en la compra.</summary>
    public DateTimeOffset? HoldExpiresAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Resumen seguro de la simulación de pago: estado y referencia, sin datos financieros.</summary>
public sealed class PaymentSummaryDto
{
    public Guid Id { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public PaymentSimulationStatus Status { get; init; }

    public string? GatewayReference { get; init; }
}

/// <summary>
/// Cuerpo de <c>POST /orders</c>: crea un pedido <c>PENDING_PAYMENT</c> para una franja concreta sin carrito ni checkout.
/// </summary>
public sealed class CreateOrderRequest
{
    [Required]
    public Guid? AttractionId { get; set; }

    [Required]
    public DateOnly? Date { get; set; }

    [Required]
    [RegularExpression(Common.ValidationPatterns.LocalTime, ErrorMessage = "time debe tener formato HH:mm.")]
    public string Time { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Quantity { get; set; }
}

/// <summary>Respuesta de pedido (CORRECCIONES_CONTRATO.md §5.15, sin carrito).</summary>
public sealed class OrderResponse
{
    public Guid Id { get; init; }

    public Guid CustomerId { get; init; }

    public Guid? PurchaseId { get; init; }

    public Guid? ReservationId { get; init; }

    public OrderStatus Status { get; init; }

    public string Currency { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyList<OrderItemResponse> Items { get; init; } = [];

    public PaymentSummaryDto? PaymentSimulation { get; init; }
}

public sealed class OrderItemResponse
{
    public Guid Id { get; init; }

    public Guid AttractionId { get; init; }

    public DateOnly Date { get; init; }

    public string Time { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public Common.PriceDto UnitPrice { get; init; } = new();

    public string Status { get; init; } = string.Empty;
}

/// <summary>Respuesta de <c>GET /orders/{orderId}/events</c> (CORRECCIONES_CONTRATO.md §5.19).</summary>
public sealed class OrderEventsResponse
{
    public Guid OrderId { get; init; }

    public IReadOnlyList<OrderEventResponse> Events { get; init; } = [];
}

public sealed class OrderEventResponse
{
    public string EventType { get; init; } = string.Empty;

    public string? PreviousStatus { get; init; }

    public string NewStatus { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Cuerpo de <c>POST /orders/{orderId}/cancel</c> (CORRECCIONES_CONTRATO.md §5.20).</summary>
public sealed class CancelOrderRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Cuerpo de <c>POST /payments/simulations</c> (CORRECCIONES_CONTRATO.md §5.16). El cliente no envía
/// <c>status</c>, códigos de respuesta ni datos de tarjeta; cualquier propiedad adicional se rechaza.
/// </summary>
public sealed class CreatePaymentSimulationRequest
{
    [Required]
    public Guid? OrderId { get; set; }

    [Required]
    public PaymentMethod? PaymentMethod { get; set; }

    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "amount debe ser mayor que cero.")]
    [Common.TwoDecimalPlaces]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression(Common.ValidationPatterns.CurrencyCode, ErrorMessage = "currency debe ser un código ISO 4217.")]
    public string Currency { get; set; } = string.Empty;
}

public sealed class PaymentSimulationResponse
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public PaymentSimulationStatus Status { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string? GatewayReference { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
