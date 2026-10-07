using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Idempotency;
using AtraccionesService.Application.DependencyInjection;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Ecommerce;
using Microsoft.Extensions.Options;

namespace AtraccionesService.Application.Services.Shared;

public sealed class IdempotencyService(
    IUnitOfWork unitOfWork,
    TransactionRunner transactions,
    TimeProvider clock,
    IOptions<ApplicationOptions> options) : IIdempotencyService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

    public Task<T> ExecuteAsync<T>(
        AuthenticatedUser user,
        string operation,
        Guid key,
        object payload,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var hash = Hash(payload);

        return transactions.ExecuteAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var existing = await unitOfWork.Idempotency.GetByIdentityAsync(user.Issuer, user.Subject, operation, key, cancellationToken);

            if (existing is not null && existing.ExpiresAt > now)
            {
                if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal))
                {
                    throw new ConflictException(ConflictException.IdempotencyKeyReused,
                        "La Idempotency-Key ya se utilizó con un payload distinto para esta operación.");
                }

                if (existing.Status == IdempotencyStatus.IN_PROGRESS || existing.ResponseBody is null)
                {
                    throw new ConflictException(ConflictException.IdempotencyInProgress,
                        "Una solicitud con la misma Idempotency-Key todavía se está procesando.");
                }

                return JsonSerializer.Deserialize<T>(existing.ResponseBody, SerializerOptions)!;
            }

            if (existing is not null)
            {
                // Clave expirada: solo se reutiliza después de limpiarla.
                await unitOfWork.Idempotency.RemoveExpiredAsync(now, cancellationToken);
            }

            var record = new IdempotencyRecord(
                user.Issuer, user.Subject, operation, key, hash, IdempotencyStatus.IN_PROGRESS, null, null,
                now, now.AddHours(options.Value.IdempotencyRetentionHours));

            if (!await unitOfWork.Idempotency.TryCreateInProgressAsync(record, cancellationToken))
            {
                throw new ConflictException(ConflictException.IdempotencyInProgress,
                    "Una solicitud con la misma Idempotency-Key todavía se está procesando.");
            }

            var result = await action(cancellationToken);

            await unitOfWork.Idempotency.CompleteAsync(
                record with
                {
                    Status = IdempotencyStatus.COMPLETED,
                    ResponseBody = JsonSerializer.Serialize(result, SerializerOptions),
                },
                cancellationToken);

            return result;
        }, cancellationToken);
    }

    /// <summary>SHA-256 del JSON canónico del payload (sin identidad ni clave).</summary>
    internal static string Hash(object payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(json));
    }
}
