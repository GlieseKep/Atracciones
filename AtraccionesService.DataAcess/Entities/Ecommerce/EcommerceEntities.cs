using AtraccionesService.DataAcess.Entities.Catalog;

namespace AtraccionesService.DataAcess.Entities.Ecommerce;

/// <summary>Registro inmutable: el contexto rechaza actualizaciones y borrados.</summary>
public interface IAppendOnlyEntity;

public class PurchaseEntity
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid AttractionId { get; set; }

    public DateOnly ServiceDate { get; set; }

    public TimeOnly ServiceTime { get; set; }

    public int Quantity { get; set; }

    public PriceValue UnitPrice { get; set; } = new();

    public decimal TotalAmount { get; set; }

    public Guid RequestIdempotencyKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class OrderEntity
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Único y opcional: impide más de un pedido por compra.</summary>
    public Guid? PurchaseId { get; set; }

    public Guid? ReservationId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public DateTimeOffset? HoldExpiresAt { get; set; }

    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<OrderItemEntity> Items { get; set; } = [];
}

public class OrderItemEntity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid AttractionId { get; set; }

    public Guid AvailabilityId { get; set; }

    public DateOnly ServiceDate { get; set; }

    public TimeOnly ServiceTime { get; set; }

    public int Quantity { get; set; }

    public PriceValue UnitPrice { get; set; } = new();
}

/// <summary>Historial inmutable de estados del pedido.</summary>
public class OrderEventEntity : IAppendOnlyEntity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? PreviousStatus { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Pago simulado. No almacena números de tarjeta, CVV ni credenciales.</summary>
public class PaymentSimulationEntity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string GatewayReference { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? FailureReason { get; set; }

    public List<PaymentAttemptEntity> Attempts { get; set; } = [];

    public List<PaymentEventEntity> Events { get; set; } = [];
}

public class PaymentAttemptEntity : IAppendOnlyEntity
{
    public Guid Id { get; set; }

    public Guid PaymentSimulationId { get; set; }

    public int AttemptNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public string ResponseCode { get; set; } = string.Empty;

    public string ResponseMessage { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

public class PaymentEventEntity : IAppendOnlyEntity
{
    public Guid Id { get; set; }

    public Guid PaymentSimulationId { get; set; }

    public string EventType { get; set; } = string.Empty;

    /// <summary>JSON con datos de la simulación únicamente.</summary>
    public string Payload { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Reembolso simulado (sin proveedor externo). Reservado para los casos administrativos pendientes de aprobación.</summary>
public class RefundSimulationEntity
{
    public Guid Id { get; set; }

    public Guid PaymentSimulationId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}

public class InventoryMovementEntity : IAppendOnlyEntity
{
    public Guid Id { get; set; }

    public Guid AttractionId { get; set; }

    public Guid AvailabilityId { get; set; }

    public int Quantity { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public Guid? PurchaseId { get; set; }

    public Guid? ReservationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Auditoría inmutable; metadata sanitizada sin tokens ni datos financieros.</summary>
public class AuditEventEntity : IAppendOnlyEntity
{
    public Guid Id { get; set; }

    public Guid? ActorUserId { get; set; }

    public string ActorSubject { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string ResourceId { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

