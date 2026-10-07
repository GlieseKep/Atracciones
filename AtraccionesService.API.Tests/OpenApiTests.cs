using System.Net;
using System.Text.Json;
using AtraccionesService.API.Tests.Infrastructure;

namespace AtraccionesService.API.Tests;

public sealed class OpenApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>Operaciones aprobadas (paths relativos a <c>servers: /api/v1</c>).</summary>
    private static readonly string[] ExpectedOperations =
    [
        "POST /atracciones/search",
        "POST /atracciones/details",
        "GET /atracciones",
        "POST /atracciones",
        "GET /atracciones/{id}",
        "PUT /atracciones/{id}",
        "PATCH /atracciones/{id}",
        "DELETE /atracciones/{id}",
        "GET /atracciones/{id}/availability",
        "POST /atracciones/{id}/reservations",
        "POST /atracciones/reservations/{reservationId}/cancel",
        "GET /atracciones/reservations",
        "GET /atracciones/reservations/{reservationId}",
        "POST /auth/register",
        "GET /users/me",
        "GET /customers/me",
        "PUT /customers/me",
        "GET /customers/me/reservations",
        "POST /attractions/{attractionId}/purchase",
        "POST /orders",
        "GET /orders/{orderId}",
        "GET /orders/{orderId}/events",
        "POST /orders/{orderId}/cancel",
        "POST /payments/simulations",
    ];

    [Fact]
    public async Task Documento_publica_exactamente_las_operaciones_aprobadas_con_servers_api_v1()
    {
        var document = await GetDocumentAsync();

        Assert.Equal("/api/v1", document.GetProperty("servers")[0].GetProperty("url").GetString());

        var operations = document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(op => op.Name is "get" or "post" or "put" or "patch" or "delete")
                .Select(op => $"{op.Name.ToUpperInvariant()} {path.Name}"))
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(ExpectedOperations.OrderBy(x => x), operations);
        Assert.DoesNotContain(operations, op => op.Contains("/api/v1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Documento_declara_oauth2_authorization_code_y_scopes_por_operacion()
    {
        var document = await GetDocumentAsync();

        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("OAuth2Security");
        Assert.Equal("oauth2", scheme.GetProperty("type").GetString());
        var scopes = scheme.GetProperty("flows").GetProperty("authorizationCode").GetProperty("scopes");
        Assert.True(scopes.TryGetProperty("attractions:cancel", out _));
        Assert.False(scopes.TryGetProperty("attractions:webhooks", out _));

        var cancel = document.GetProperty("paths").GetProperty("/atracciones/reservations/{reservationId}/cancel").GetProperty("post");
        Assert.Equal("attractions:cancel", cancel.GetProperty("security")[0].GetProperty("OAuth2Security")[0].GetString());
        foreach (var status in new[] { "400", "401", "403", "404", "409", "429", "500", "503" })
        {
            Assert.True(cancel.GetProperty("responses").TryGetProperty(status, out _), $"Falta la respuesta {status}");
        }

        var key = cancel.GetProperty("parameters").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.True(key.GetProperty("required").GetBoolean());
        Assert.Equal("uuid", key.GetProperty("schema").GetProperty("format").GetString());
    }

    [Fact]
    public async Task OpenApi_no_se_publica_en_produccion()
    {
        await using var production = new ProductionApiFactory();

        var response = await production.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonElement> GetDocumentAsync()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }
}
