using AtraccionesService.API.Extensions;
using AtraccionesService.API.Middleware;
using AtraccionesService.API.Options;
using AtraccionesService.Application.DependencyInjection;
using AtraccionesService.DataAcess.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AtraccionesService.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 1. Configuración y servicios (composition root: API, Application y DataAccess).
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddAtraccionesApi(builder.Configuration);
        builder.Services.AddApplication(builder.Configuration);
        builder.Services.AddDataAccess(builder.Configuration);

        var app = builder.Build();

        // Migraciones y datos de ejemplo solo en desarrollo; en producción se aplican scripts SQL manualmente.
        await app.Services.InitializeDatabaseAsync(app.Environment.IsDevelopment());

        // 2. Manejo global de excepciones (RFC 7807) y cuerpos ProblemDetails para 401/403/404/405 sin contenido.
        app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
        app.UseStatusCodePages();

        // 3. HTTPS y CORS restringido a orígenes configurados.
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);

        // 4-5. Autenticación OAuth2, límite de tasa por identidad y autorización por scopes/permisos.
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        // 7. OpenAPI/Swagger solo en Development o si se habilita explícitamente.
        var openApi = app.Services.GetRequiredService<IOptions<OpenApiPublishingOptions>>().Value;
        if (app.Environment.IsDevelopment() || openApi.Enabled)
        {
            var oauth = app.Services.GetRequiredService<IOptions<OAuth2Options>>().Value;
            app.MapOpenApi().AllowAnonymous().DisableRateLimiting();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "API de Atracciones v1");
                options.OAuthUsePkce();
                if (!string.IsNullOrWhiteSpace(oauth.SwaggerClientId))
                {
                    options.OAuthClientId(oauth.SwaggerClientId);
                }
            });
        }

        // 6. Health checks y controladores.
        app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();
        app.MapHealthChecks("/api/v1/atracciones/health").AllowAnonymous().DisableRateLimiting();
        app.MapControllers();

        await app.RunAsync();
    }
}
