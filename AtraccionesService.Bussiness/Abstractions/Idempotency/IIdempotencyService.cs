using AtraccionesService.Application.Abstractions.Authorization;

namespace AtraccionesService.Application.Abstractions.Idempotency;

/// <summary>
/// Ejecuta una mutación una sola vez por identidad <c>issuer + subject + operation + key</c>.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Primera solicitud: ejecuta <paramref name="action"/> en la transacción y guarda su resultado.
    /// Repetición con el mismo payload: devuelve el resultado original sin repetir efectos.
    /// Misma clave con otro payload: <c>409 IDEMPOTENCY_KEY_REUSED</c>; repetición aún en proceso: <c>409 IDEMPOTENCY_IN_PROGRESS</c>.
    /// </summary>
    Task<T> ExecuteAsync<T>(
        AuthenticatedUser user,
        string operation,
        Guid key,
        object payload,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);
}

/// <summary>Nombres de operación usados en la identidad de idempotencia.</summary>
public static class IdempotentOperations
{
    public const string CreateAttraction = "create-attraction";
    public const string ReplaceAttraction = "replace-attraction";
    public const string PatchAttraction = "patch-attraction";
    public const string DeleteAttraction = "delete-attraction";
    public const string CreateReservation = "create-reservation";
    public const string CancelReservation = "cancel-reservation";
    public const string CreatePurchase = "create-purchase";
    public const string CreateOrder = "create-order";
    public const string CancelOrder = "cancel-order";
    public const string SimulatePayment = "simulate-payment";
}
