namespace AtraccionesService.Application.DependencyInjection;

/// <summary>Parámetros de negocio configurables (sección <c>Application</c>).</summary>
public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    /// <summary>Zona horaria IANA en la que se interpretan fechas y horas de las franjas.</summary>
    public string TimeZone { get; set; } = "America/Guayaquil";

    /// <summary>Máximo de entradas por reserva, compra o pedido.</summary>
    public int MaxTicketsPerOperation { get; set; } = 100;

    /// <summary>Duración de la retención de cupos de un pedido pendiente de pago.</summary>
    public int HoldMinutes { get; set; } = 15;

    /// <summary>Intentos de pago simulado permitidos por pedido.</summary>
    public int MaxPaymentAttemptsPerOrder { get; set; } = 3;

    /// <summary>Retención de las claves de idempotencia.</summary>
    public int IdempotencyRetentionHours { get; set; } = 24;

    /// <summary>Clave HMAC (Base64) para firmar <c>nextPage</c>. Si está vacía se genera una por proceso.</summary>
    public string? PageTokenSecret { get; set; }

    public int PageTokenLifetimeMinutes { get; set; } = 15;
}
