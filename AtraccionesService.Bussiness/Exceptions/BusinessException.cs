namespace AtraccionesService.Application.Exceptions;

/// <summary>
/// Base de las excepciones de Application. <see cref="Code"/> es un identificador estable que la API publica en ProblemDetails.
/// </summary>
public class BusinessException : Exception
{
    public BusinessException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Datos inválidos o faltantes (400).</summary>
public sealed class ValidationException : BusinessException
{
    public ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base("VALIDATION_ERROR", message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>Recurso inexistente o no visible para el usuario (404).</summary>
public sealed class NotFoundException : BusinessException
{
    public NotFoundException(string message)
        : base("NOT_FOUND", message)
    {
    }
}

/// <summary>Identidad ausente o inválida (401).</summary>
public sealed class UnauthorizedException : BusinessException
{
    public UnauthorizedException(string message)
        : base("UNAUTHORIZED", message)
    {
    }
}

/// <summary>Usuario autenticado sin permiso sobre la operación o recurso (403).</summary>
public sealed class ForbiddenException : BusinessException
{
    public const string ProfileNotRegistered = "PROFILE_NOT_REGISTERED";
    public const string UserInactive = "USER_INACTIVE";

    public ForbiddenException(string message, string code = "FORBIDDEN")
        : base(code, message)
    {
    }
}

/// <summary>
/// Estado, disponibilidad u operación incompatible (409). Códigos de idempotencia estables:
/// <see cref="IdempotencyKeyReused"/> e <see cref="IdempotencyInProgress"/>.
/// </summary>
public sealed class ConflictException : BusinessException
{
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string IdempotencyInProgress = "IDEMPOTENCY_IN_PROGRESS";
    public const string InsufficientAvailability = "INSUFFICIENT_AVAILABILITY";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string SlotUnavailable = "SLOT_UNAVAILABLE";

    public ConflictException(string code, string message)
        : base(code, message)
    {
    }
}

/// <summary>Pago simulado rechazado o inválido (422).</summary>
public sealed class PaymentSimulationException : BusinessException
{
    public PaymentSimulationException(string code, string message)
        : base(code, message)
    {
    }
}
