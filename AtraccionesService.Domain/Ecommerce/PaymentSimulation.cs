using AtraccionesService.Domain.Common;

namespace AtraccionesService.Domain.Ecommerce;

public enum PaymentMethod
{
    CARD,
    BANK_TRANSFER
}

public enum PaymentStatus
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
/// Pago simulado. Transiciones: PENDING → AUTHORIZED | REJECTED | FAILED; AUTHORIZED → SETTLED | CANCELLED | FAILED;
/// SETTLED → PARTIALLY_REFUNDED | REFUNDED. No contiene datos de tarjeta ni credenciales.
/// </summary>
public sealed class PaymentSimulation
{
    private static readonly IReadOnlyDictionary<PaymentStatus, PaymentStatus[]> Transitions = new Dictionary<PaymentStatus, PaymentStatus[]>
    {
        [PaymentStatus.PENDING] = [PaymentStatus.AUTHORIZED, PaymentStatus.REJECTED, PaymentStatus.FAILED],
        [PaymentStatus.AUTHORIZED] = [PaymentStatus.SETTLED, PaymentStatus.CANCELLED, PaymentStatus.FAILED],
        [PaymentStatus.SETTLED] = [PaymentStatus.PARTIALLY_REFUNDED, PaymentStatus.REFUNDED],
    };

    private PaymentSimulation(
        Guid id, Guid orderId, PaymentMethod method, PaymentStatus status, Money amount, string gatewayReference,
        DateTimeOffset createdAt, DateTimeOffset? processedAt, string? failureReason)
    {
        Id = id;
        OrderId = orderId;
        Method = method;
        Status = status;
        Amount = amount;
        GatewayReference = gatewayReference;
        CreatedAt = createdAt;
        ProcessedAt = processedAt;
        FailureReason = failureReason;
    }

    public Guid Id { get; }

    public Guid OrderId { get; }

    public PaymentMethod Method { get; }

    public PaymentStatus Status { get; private set; }

    public Money Amount { get; }

    public string GatewayReference { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public static PaymentSimulation Start(Guid orderId, PaymentMethod method, Money amount, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        return new PaymentSimulation(id, orderId, method, PaymentStatus.PENDING, amount, $"SIM-{id:N}"[..16].ToUpperInvariant(), now, null, null);
    }

    public static PaymentSimulation Restore(
        Guid id, Guid orderId, PaymentMethod method, PaymentStatus status, Money amount, string gatewayReference,
        DateTimeOffset createdAt, DateTimeOffset? processedAt, string? failureReason) =>
        new(id, orderId, method, status, amount, gatewayReference, createdAt, processedAt, failureReason);

    public PaymentStatus TransitionTo(PaymentStatus next, DateTimeOffset now, string? failureReason = null)
    {
        if (!Transitions.TryGetValue(Status, out var allowed) || !allowed.Contains(next))
        {
            throw new DomainException("INVALID_STATE_TRANSITION", $"Transición de pago no permitida: {Status} → {next}.");
        }

        var previous = Status;
        Status = next;
        ProcessedAt = now;
        FailureReason = next is PaymentStatus.REJECTED or PaymentStatus.FAILED ? failureReason : null;
        return previous;
    }
}

/// <summary>Intento de pago simulado (interno, sin endpoint público).</summary>
public sealed record PaymentAttempt(
    Guid Id,
    Guid PaymentSimulationId,
    int AttemptNumber,
    PaymentStatus Status,
    string ResponseCode,
    string ResponseMessage,
    DateTimeOffset CreatedAt);

/// <summary>Evento inmutable de la pasarela simulada; el payload solo contiene datos de la simulación.</summary>
public sealed record PaymentEvent(
    Guid Id,
    Guid PaymentSimulationId,
    string EventType,
    string Payload,
    DateTimeOffset CreatedAt);
