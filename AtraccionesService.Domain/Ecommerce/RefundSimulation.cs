using AtraccionesService.Domain.Common;

namespace AtraccionesService.Domain.Ecommerce;

public enum RefundStatus
{
    PENDING,
    SETTLED,
    FAILED,
    CANCELLED
}

/// <summary>
/// Reembolso simulado de un pago liquidado. Requiere motivo y actor administrativo; no invoca proveedores externos.
/// </summary>
public sealed record RefundSimulation(
    Guid Id,
    Guid PaymentSimulationId,
    Money Amount,
    RefundStatus Status,
    string Reason,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt)
{
    public static RefundSimulation Request(Guid paymentSimulationId, Money amount, string reason, Guid createdByUserId, DateTimeOffset now) =>
        new(Guid.NewGuid(), paymentSimulationId, amount, RefundStatus.PENDING, Guard.NotBlank(reason, "reason", 500), createdByUserId, now, null);
}
