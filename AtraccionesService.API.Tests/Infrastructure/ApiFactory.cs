using System.Net.Http.Headers;
using AtraccionesService.API;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AtraccionesService.API.Tests.Infrastructure;

/// <summary>
/// Inicia el host API real con validación JWT contra una clave local y casos de uso de Application simulados.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public FakeApplication Fake { get; } = new();

    protected virtual string EnvironmentName => "Development";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("Authentication:OAuth2:Authority", "");
        builder.UseSetting("Authentication:OAuth2:Issuer", TestTokens.Issuer);
        builder.UseSetting("Authentication:OAuth2:Audience", TestTokens.Audience);
        builder.UseSetting("Authentication:OAuth2:RequireHttpsMetadata", "false");
        builder.UseSetting("RateLimiting:PermitLimit", "10000");

        // Las pruebas HTTP sustituyen Application por dobles: no se migra ni siembra ninguna base.
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Atracciones", "Data Source=api-tests;Mode=Memory;Cache=Shared");
        builder.UseSetting("Database:ApplyMigrations", "false");
        builder.UseSetting("Database:Seed:Catalog", "false");
        builder.UseSetting("Database:Seed:Ecommerce", "false");

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters.IssuerSigningKey = TestTokens.Key);

            services.AddSingleton(Fake);
            services.AddSingleton<IAttractionService>(Fake);
            services.AddSingleton<IAvailabilityService>(Fake);
            services.AddSingleton<IReservationService>(Fake);
            services.AddSingleton<IUserProfileService>(Fake);
            services.AddSingleton<ICustomerService>(Fake);
            services.AddSingleton<IPurchaseService>(Fake);
            services.AddSingleton<IOrderService>(Fake);
            services.AddSingleton<IPaymentSimulationService>(Fake);
            services.AddSingleton<ILocalPermissionService>(Fake);
        });
    }

    public HttpClient CreateClient(string? token)
    {
        var client = CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}

public sealed class ProductionApiFactory : ApiFactory
{
    protected override string EnvironmentName => "Production";
}
