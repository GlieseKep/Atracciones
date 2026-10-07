using System.Net;
using AtraccionesService.API.Tests.Infrastructure;
using static AtraccionesService.API.Tests.Infrastructure.HttpAssertions;

namespace AtraccionesService.API.Tests;

public sealed class HostAndSecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Catalog = "/api/v1/atracciones";

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/atracciones/health")]
    public async Task Health_responde_200_sin_autenticacion_ni_detalles_internos(string path)
    {
        var response = await factory.CreateClient(token: null).GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/weatherforecast")]
    [InlineData("/api/v1/admin/dashboard")]
    [InlineData("/api/v1/admin/attractions")]
    [InlineData("/api/v1/carts/me")]
    [InlineData("/api/v1/auth/login")]
    public async Task Rutas_de_plantilla_admin_o_no_aprobadas_no_se_publican(string path)
    {
        var response = await factory.CreateClient(TestTokens.Create()).GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Solicitud_anonima_a_ruta_protegida_recibe_401_problem()
    {
        var response = await factory.CreateClient(token: null).GetAsync(Catalog);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_expirado_recibe_401()
    {
        var token = TestTokens.Create(expires: DateTime.UtcNow.AddMinutes(-10));

        var response = await factory.CreateClient(token).GetAsync(Catalog);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_con_firma_invalida_recibe_401()
    {
        var response = await factory.CreateClient(TestTokens.Create(wrongKey: true)).GetAsync(Catalog);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_de_otro_issuer_recibe_401()
    {
        var response = await factory.CreateClient(TestTokens.Create(issuer: "https://otro-issuer.test")).GetAsync(Catalog);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_sin_scope_requerido_recibe_403()
    {
        var response = await factory.CreateClient(TestTokens.Create(scopes: "attractions:book")).GetAsync(Catalog);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_con_scope_de_lectura_lista_atracciones_paginadas()
    {
        var response = await factory.CreateClient(TestTokens.Create(scopes: "attractions:read")).GetAsync($"{Catalog}?limit=5&offset=0");

        var json = await response.AssertJsonAsync(HttpStatusCode.OK);
        Assert.Equal(5, json.GetProperty("meta").GetProperty("itemsPerPage").GetInt32());
        var first = json.GetProperty("data")[0];
        Assert.Equal("SINGLE_TICKET", first.GetProperty("productType").GetString());
        Assert.Equal($"{Catalog}/{FakeApplication.ExistingAttractionId}", first.GetProperty("_links").GetProperty("self").GetString());
    }

    [Fact]
    public async Task Cancelar_reserva_sin_scope_attractions_cancel_recibe_403()
    {
        var client = factory.CreateClient(TestTokens.Create(scopes: "attractions:read attractions:book"));

        var response = await client.SendAsync(Post($"{Catalog}/reservations/{Guid.NewGuid()}/cancel", new { reason = "x" }, Guid.NewGuid()));

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ruta_inexistente_bajo_api_responde_404_problem()
    {
        var response = await factory.CreateClient(TestTokens.Create()).GetAsync($"{Catalog}/{Guid.NewGuid()}");

        var problem = await response.AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("NOT_FOUND", problem.GetProperty("code").GetString());
    }
}
