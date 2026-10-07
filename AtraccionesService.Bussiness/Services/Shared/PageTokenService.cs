using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AtraccionesService.Application.DependencyInjection;
using AtraccionesService.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace AtraccionesService.Application.Services.Shared;

/// <summary>
/// Token opaco <c>nextPage</c> (CORRECCIONES_CONTRATO.md §4.6): contiene el offset, un hash de los criterios de búsqueda
/// y la expiración, firmado con HMAC-SHA256. Un token manipulado, expirado o usado con otros criterios produce 400.
/// </summary>
public sealed class PageTokenService
{
    private readonly byte[] _key;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _lifetime;

    public PageTokenService(TimeProvider clock, IOptions<ApplicationOptions> options)
    {
        _clock = clock;
        _lifetime = TimeSpan.FromMinutes(options.Value.PageTokenLifetimeMinutes);
        _key = string.IsNullOrWhiteSpace(options.Value.PageTokenSecret)
            ? RandomNumberGenerator.GetBytes(32)
            : Convert.FromBase64String(options.Value.PageTokenSecret);
    }

    public string Create(int offset, string criteriaHash)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new TokenPayload(offset, criteriaHash, _clock.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds()));
        return $"{Base64Url(payload)}.{Base64Url(HMACSHA256.HashData(_key, payload))}";
    }

    public int ReadOffset(string token, string criteriaHash)
    {
        var parts = token.Split('.');
        if (parts.Length == 2
            && TryFromBase64Url(parts[0], out var payload)
            && TryFromBase64Url(parts[1], out var signature)
            && CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(_key, payload)))
        {
            var data = JsonSerializer.Deserialize<TokenPayload>(payload);
            if (data is not null
                && data.Offset >= 0
                && data.Criteria == criteriaHash
                && DateTimeOffset.FromUnixTimeSeconds(data.ExpiresAt) > _clock.GetUtcNow())
            {
                return data.Offset;
            }
        }

        throw new ValidationException("nextPage es inválido, expiró o no corresponde a los criterios de búsqueda.");
    }

    public static string HashCriteria(object criteria) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(criteria, criteria.GetType())))[..16];

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryFromBase64Url(string value, out byte[] bytes)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        bytes = new byte[base64.Length];
        return Convert.TryFromBase64String(base64, bytes, out var written) && (bytes = bytes[..written]).Length > 0;
    }

    private sealed record TokenPayload(int Offset, string Criteria, long ExpiresAt);
}
