namespace AtraccionesService.Domain.Ecommerce;

public enum InventoryMovementType
{
    CONFIRMED,
    RELEASED,
    CANCELLED
}

/// <summary>Cambio de cupos de una franja. <see cref="Quantity"/> es positivo al tomar cupos y negativo al liberarlos.</summary>
public sealed record InventoryMovement(
    Guid Id,
    Guid AttractionId,
    Guid AvailabilitySlotId,
    int Quantity,
    InventoryMovementType MovementType,
    Guid? PurchaseId,
    Guid? ReservationId,
    DateTimeOffset CreatedAt);

public enum IdempotencyStatus
{
    IN_PROGRESS,
    COMPLETED
}

/// <summary>
/// Registro de idempotencia; identidad única <c>issuer + subject + operation + key</c>.
/// Guarda el hash canónico del payload y la respuesta serializada para replay. Nunca almacena tokens ni secretos.
/// </summary>
public sealed record IdempotencyRecord(
    string Issuer,
    string Subject,
    string Operation,
    Guid Key,
    string RequestHash,
    IdempotencyStatus Status,
    Guid? ResourceId,
    string? ResponseBody,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>Registro de auditoría inmutable de mutaciones administrativas (sin tokens ni datos financieros).</summary>
public sealed record AuditEvent(
    Guid Id,
    Guid? ActorUserId,
    string ActorSubject,
    string Action,
    string ResourceType,
    string ResourceId,
    string? Reason,
    DateTimeOffset CreatedAt);
