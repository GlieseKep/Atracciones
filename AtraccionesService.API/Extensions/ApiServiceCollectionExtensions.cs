using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AtraccionesService.API.Authorization;
using AtraccionesService.API.OpenApi;
using AtraccionesService.API.Options;
using AtraccionesService.API.Routing;
using AtraccionesService.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AtraccionesService.API.Extensions;

/// <summary>
/// Registro de los servicios propios del host API. Los casos de uso de Application y la persistencia
/// se registran desde sus propias capas en el composition root.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "ConfiguredOrigins";

    public static IServiceCollection AddAtraccionesApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OAuth2Options>(configuration.GetSection(OAuth2Options.SectionName));
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        services.Configure<OpenApiPublishingOptions>(configuration.GetSection(OpenApiPublishingOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

        services
            .AddControllers(options => options.Conventions.Add(new ApiRoutePrefixConvention()))
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => ConfigureJson(options.SerializerOptions));

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
        });

        services.AddOAuth2Authentication();
        services.AddAuthorization(ApiPolicies.Configure);
        services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, LocalPermissionAuthorizationHandler>();

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, config) =>
        {
            var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins)
                        .WithHeaders("Authorization", "Content-Type", "Idempotency-Key")
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                        .WithExposedHeaders("Location", "Retry-After");
                }
            });
        });

        services.AddApiRateLimiting();

        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<ContractDocumentTransformer>();
            options.AddOperationTransformer<ContractOperationTransformer>();
        });

        services.AddHealthChecks();

        return services;
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // additionalProperties: false — los campos desconocidos (p. ej. "sub" o datos de tarjeta) producen 400.
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.Converters.Add(new JsonStringEnumConverter());
    }

    private static void AddOAuth2Authentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<OAuth2Options>>((jwt, oauthOptions) =>
            {
                var oauth = oauthOptions.Value;
                if (!string.IsNullOrWhiteSpace(oauth.Authority))
                {
                    jwt.Authority = oauth.Authority;
                }

                jwt.RequireHttpsMetadata = oauth.RequireHttpsMetadata;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = oauth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = oauth.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub",
                };
            });
    }

    private static void AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var partition = context.User.FindFirstValue("sub") is { } subject
                    ? "sub:" + subject
                    : "ip:" + context.Connection.RemoteIpAddress;

                return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                    QueueLimit = 0,
                });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Demasiadas solicitudes",
                        Type = "urn:atracciones:problems:rate-limited",
                        Detail = "Se superó el límite de solicitudes. Reintente más tarde.",
                    },
                });
            };
        });
    }
}
