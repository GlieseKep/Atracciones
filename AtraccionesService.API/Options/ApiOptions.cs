namespace AtraccionesService.API.Options;

/// <summary>
/// Configuración del issuer OAuth2 (sección <c>Authentication:OAuth2</c>). Sin secretos: la API solo valida tokens.
/// </summary>
public sealed class OAuth2Options
{
    public const string SectionName = "Authentication:OAuth2";

    /// <summary>URL de metadatos OIDC del issuer; vacío solo en pruebas con claves locales.</summary>
    public string? Authority { get; set; }

    /// <summary>Valor esperado del claim <c>iss</c>.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Valor esperado del claim <c>aud</c>.</summary>
    public string Audience { get; set; } = string.Empty;

    public string AuthorizationUrl { get; set; } = string.Empty;

    public string TokenUrl { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Client id público usado por Swagger UI con PKCE (no es un secreto).</summary>
    public string? SwaggerClientId { get; set; }
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; set; } = 100;

    public int WindowSeconds { get; set; } = 60;
}

public sealed class OpenApiPublishingOptions
{
    public const string SectionName = "OpenApi";

    /// <summary>Publica OpenAPI/Swagger fuera de Development solo si se habilita explícitamente.</summary>
    public bool Enabled { get; set; }
}
