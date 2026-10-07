namespace AtraccionesService.Contracts.Common;

/// <summary>
/// Expresiones regulares compartidas por los contratos HTTP (CORRECCIONES_CONTRATO.md §3.3, §3.7, §3.8, §3.9, §3.10).
/// </summary>
public static class ValidationPatterns
{
    /// <summary>Código de moneda ISO 4217 (tres letras mayúsculas).</summary>
    public const string CurrencyCode = "^[A-Z]{3}$";

    /// <summary>Código de país ISO 3166-1 alfa-2.</summary>
    public const string CountryCode = "^[A-Z]{2}$";

    /// <summary>Hora local de la franja en formato 24 h <c>HH:mm</c>.</summary>
    public const string LocalTime = "^([01][0-9]|2[0-3]):[0-5][0-9]$";

    /// <summary>Duración ISO 8601 (por ejemplo <c>PT2H30M</c> o <c>P1D</c>).</summary>
    public const string IsoDuration = @"^P(?!$)(\d+D)?(T(?=\d)(\d+H)?(\d+M)?)?$";

    /// <summary>UUID en formato canónico, usado por <c>Idempotency-Key</c>.</summary>
    public const string Uuid = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

    /// <summary>
    /// Referencia local de método de pago simulado: alfanumérica y nunca compuesta solo por dígitos,
    /// para impedir que se envíe un número de tarjeta.
    /// </summary>
    public const string PaymentMethodReference = @"^(?!\d+$)[A-Za-z0-9_-]{1,64}$";
}
