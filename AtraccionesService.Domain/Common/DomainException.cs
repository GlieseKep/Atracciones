namespace AtraccionesService.Domain.Common;

/// <summary>
/// Violación de un invariante de dominio. Application valida antes de invocar al dominio, por lo que esta excepción
/// actúa como última defensa y, si llega a producirse, indica un error de programación.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class Guard
{
    public static string NotBlank(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("REQUIRED", $"{field} es obligatorio.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException("TOO_LONG", $"{field} admite como máximo {maxLength} caracteres.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : NotBlank(value, field, maxLength);

    public static int Positive(int value, string field) =>
        value > 0 ? value : throw new DomainException("NOT_POSITIVE", $"{field} debe ser mayor que cero.");
}
