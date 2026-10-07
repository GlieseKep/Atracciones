using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using AtraccionesService.Application.Exceptions;

namespace AtraccionesService.Application.Validators;

/// <summary>Acumula errores por campo y los lanza juntos como <see cref="ValidationException"/>.</summary>
internal sealed partial class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool HasErrors => _errors.Count > 0;

    public ValidationErrors Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            _errors[field] = messages = [];
        }

        messages.Add(message);
        return this;
    }

    public ValidationErrors When(bool condition, string field, string message) => condition ? Add(field, message) : this;

    public void ThrowIfAny(string message = "La solicitud contiene datos inválidos.")
    {
        if (HasErrors)
        {
            throw new ValidationException(message, _errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }

    public ValidationErrors RequiredText(string? value, string field, int maxLength, int minLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Add(field, $"{field} es obligatorio.");
        }

        var length = value.Trim().Length;
        return When(length < minLength || length > maxLength, field, $"{field} debe tener entre {minLength} y {maxLength} caracteres.");
    }

    public ValidationErrors OptionalText(string? value, string field, int maxLength) =>
        value is null ? this : RequiredText(value, field, maxLength);

    public ValidationErrors Email(string? value, string field, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required ? Add(field, $"{field} es obligatorio.") : this;
        }

        return When(!IsEmail(value), field, $"{field} no tiene un formato de correo válido.");
    }

    public static bool IsEmail(string value) =>
        value.Length <= 254 && MailAddress.TryCreate(value, out var address) && address.Address == value && value.Contains('.', StringComparison.Ordinal);

    public static bool TryParseSlotTime(string? value, out TimeOnly time)
    {
        time = default;
        return value is not null
            && SlotTimePattern().IsMatch(value)
            && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    [GeneratedRegex("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    private static partial Regex SlotTimePattern();
}
