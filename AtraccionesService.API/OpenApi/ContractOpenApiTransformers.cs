using AtraccionesService.API.Authorization;
using AtraccionesService.API.Filters;
using AtraccionesService.API.Options;
using AtraccionesService.API.Routing;
using AtraccionesService.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace AtraccionesService.API.OpenApi;

/// <summary>
/// Alinea el documento generado con el contrato: <c>servers: /api/v1</c>, paths sin prefijo y esquema <c>OAuth2Security</c>
/// con flujo <c>authorizationCode</c>.
/// </summary>
public sealed class ContractDocumentTransformer(IOptions<OAuth2Options> oauthOptions) : IOpenApiDocumentTransformer
{
    public const string SecuritySchemeName = "OAuth2Security";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "API de Atracciones Turísticas",
            Version = "1.3.0",
            Description = "Microservicio de Atracciones del Marketplace Turístico: catálogo, disponibilidad, reservas, " +
                          "perfil de cliente, compra directa, pedidos y pagos simulados. Los errores siguen RFC 7807 " +
                          "(application/problem+json) y las mutaciones transaccionales exigen la cabecera Idempotency-Key.",
        };

        document.Servers = [new OpenApiServer { Url = "/" + ApiRoutePrefixConvention.Prefix, Description = "API Gateway - Ruta base" }];

        var prefix = "/" + ApiRoutePrefixConvention.Prefix;
        var paths = new OpenApiPaths();
        foreach (var (path, item) in document.Paths)
        {
            paths[path.StartsWith(prefix + "/", StringComparison.Ordinal) ? path[prefix.Length..] : path] = item;
        }

        document.Paths = paths;

        var oauth = oauthOptions.Value;
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SecuritySchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "OAuth2 Authorization Code; los clientes públicos deben usar PKCE.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(oauth.AuthorizationUrl),
                    TokenUrl = new Uri(oauth.TokenUrl),
                    Scopes = ApiScopes.Descriptions.ToDictionary(),
                },
            },
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Añade a cada operación su requisito de seguridad, las respuestas de error comunes (§3.1) y el formato de
/// <c>Idempotency-Key</c> (§3.3).
/// </summary>
public sealed class ContractOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly (string Status, string Description)[] SecurityResponses =
    [
        ("401", "Access token ausente, inválido o expirado."),
        ("403", "El token no tiene el scope requerido o el usuario no tiene permiso local."),
        ("429", "Se superó el límite de solicitudes."),
        ("500", "Error interno del servidor."),
        ("503", "Servicio no disponible temporalmente."),
    ];

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (!metadata.OfType<IAllowAnonymous>().Any())
        {
            var scopes = metadata.OfType<IAuthorizeData>()
                .Select(a => a.Policy)
                .Where(p => p is not null && ApiPolicies.Scopes.ContainsKey(p))
                .Select(p => ApiPolicies.Scopes[p!])
                .Distinct()
                .ToList();

            if (metadata.OfType<IAuthorizeData>().Any())
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(ContractDocumentTransformer.SecuritySchemeName, context.Document)] = scopes,
                });

                operation.Responses ??= new OpenApiResponses();
                foreach (var (status, description) in SecurityResponses)
                {
                    if (!operation.Responses.ContainsKey(status))
                    {
                        operation.Responses[status] = ProblemResponse(description, context.Document);
                    }
                }
            }
        }

        if (metadata.OfType<RequireIdempotencyKeyAttribute>().Any())
        {
            var header = operation.Parameters?
                .OfType<OpenApiParameter>()
                .FirstOrDefault(p => p.In == ParameterLocation.Header && p.Name == RequireIdempotencyKeyAttribute.HeaderName);
            if (header is not null)
            {
                header.Required = true;
                header.Description = "UUID único por intento lógico. Repetir la misma clave y payload devuelve la respuesta original; " +
                                     "la misma clave con otro payload produce 409 IDEMPOTENCY_KEY_REUSED.";
                header.Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Format = "uuid",
                    Pattern = ValidationPatterns.Uuid,
                };
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiResponse ProblemResponse(string description, OpenApiDocument? document)
    {
        var response = new OpenApiResponse { Description = description };
        if (document?.Components?.Schemas?.ContainsKey("ProblemDetails") == true)
        {
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference("ProblemDetails", document) },
            };
        }

        return response;
    }
}
