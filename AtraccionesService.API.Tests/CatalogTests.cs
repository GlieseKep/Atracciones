using System.Net;
using AtraccionesService.API.Tests.Infrastructure;
using AtraccionesService.Application.Commands.Attractions;
using static AtraccionesService.API.Tests.Infrastructure.HttpAssertions;

namespace AtraccionesService.API.Tests;

public sealed class CatalogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Catalog = "/api/v1/atracciones";

    private static object ValidAttraction(string name = "Teleférico de Quito") => new
    {
        name,
        longDescription = "Recorrido en teleférico hasta el Rucu Pichincha.",
        duration = "PT2H",
        price = new { currency = "USD", total = 45.50m },
        categories = new[] { "Aventura" },
        locations = new[] { new { address = "Av. Occidental", city = "Quito", country = "EC" } },
        productType = "SINGLE_TICKET",
    };

    [Fact]
    public async Task Escritura_sin_permiso_local_recibe_403_aunque_el_token_tenga_scope()
    {
        var client = factory.CreateClient(TestTokens.Create(subject: "sin-permiso", scopes: "attractions:write"));

        var response = await client.SendAsync(Post(Catalog, ValidAttraction(), Guid.NewGuid()));

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Administrador_local_sin_scope_de_escritura_recibe_403()
    {
        factory.Fake.CatalogWriters.Add("admin-sin-scope");
        var client = factory.CreateClient(TestTokens.Create(subject: "admin-sin-scope", scopes: "attractions:read"));

        var response = await client.SendAsync(Post(Catalog, ValidAttraction(), Guid.NewGuid()));

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Crear_atraccion_con_scope_y_permiso_local_devuelve_201_con_location()
    {
        factory.Fake.CatalogWriters.Add("catalog-manager");
        var client = factory.CreateClient(TestTokens.Create(subject: "catalog-manager", scopes: "attractions:write"));
        var key = Guid.NewGuid();

        var response = await client.SendAsync(Post(Catalog, ValidAttraction("Mitad del Mundo"), key));

        var json = await response.AssertJsonAsync(HttpStatusCode.Created);
        Assert.Equal("Mitad del Mundo", json.GetProperty("name").GetString());
        Assert.EndsWith($"/api/v1/atracciones/{json.GetProperty("id").GetString()}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        var command = factory.Fake.Commands.OfType<CreateAttractionCommand>().Single(c => c.Data.Name == "Mitad del Mundo");
        Assert.Equal(key, command.IdempotencyKey);
        Assert.Equal("catalog-manager", command.Actor.Subject);
    }

    [Fact]
    public async Task Crear_atraccion_incompleta_devuelve_400_problem_con_errores()
    {
        factory.Fake.CatalogWriters.Add("catalog-manager");
        var client = factory.CreateClient(TestTokens.Create(subject: "catalog-manager", scopes: "attractions:write"));
        var invalid = new
        {
            name = "X",
            duration = "dos horas",
            price = new { currency = "usd", total = -1m },
            categories = Array.Empty<string>(),
            locations = Array.Empty<object>(),
        };

        var response = await client.SendAsync(Post(Catalog, invalid, Guid.NewGuid()));

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        foreach (var field in new[] { "Name", "LongDescription", "Duration", "Price.Currency", "Price.Total", "Categories", "Locations", "ProductType" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Falta el error de '{field}': {errors}");
        }
    }

    [Fact]
    public async Task Propiedades_desconocidas_se_rechazan_con_400()
    {
        factory.Fake.CatalogWriters.Add("catalog-manager");
        var client = factory.CreateClient(TestTokens.Create(subject: "catalog-manager", scopes: "attractions:write"));
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{Catalog}/{FakeApplication.ExistingAttractionId}")
        {
            Content = new StringContent("""{"name":"Nuevo nombre","ratings":{"score":5}}""", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Disponibilidad_requiere_fecha_y_devuelve_franjas_con_estado()
    {
        var client = factory.CreateClient(TestTokens.Create(scopes: "attractions:read"));

        var missingDate = await client.GetAsync($"{Catalog}/{FakeApplication.ExistingAttractionId}/availability");
        await missingDate.AssertProblemAsync(HttpStatusCode.BadRequest);

        var response = await client.GetAsync($"{Catalog}/{FakeApplication.ExistingAttractionId}/availability?date=2026-12-15");
        var json = await response.AssertJsonAsync(HttpStatusCode.OK);
        Assert.Equal("America/Guayaquil", json.GetProperty("timeZone").GetString());
        Assert.Equal("SOLD_OUT", json.GetProperty("times")[1].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Busqueda_con_rango_de_fechas_invertido_devuelve_400()
    {
        var client = factory.CreateClient(TestTokens.Create(scopes: "attractions:read"));

        var response = await client.SendAsync(Post($"{Catalog}/search",
            new { dates = new { startDate = "2026-12-31", endDate = "2026-12-01" } }));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }
}
