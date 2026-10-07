using System.Net;
using System.Net.Http.Json;
using AtraccionesService.API.Tests.Infrastructure;
using AtraccionesService.Application.Commands.Customers;
using static AtraccionesService.API.Tests.Infrastructure.HttpAssertions;

namespace AtraccionesService.API.Tests;

public sealed class EcommerceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string PurchaseUrl = $"/api/v1/attractions/{FakeApplication.ExistingAttractionId}/purchase";

    [Fact]
    public async Task Registro_usa_sub_y_email_del_token_y_no_duplica_el_perfil()
    {
        var client = factory.CreateClient(TestTokens.Create(subject: "nuevo-usuario", email: "nuevo@ejemplo.com"));
        var body = new { billingName = "Nuevo Usuario", billingEmail = "factura@ejemplo.com" };

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", body);
        var created = await first.AssertJsonAsync(HttpStatusCode.Created);
        Assert.Equal("nuevo@ejemplo.com", created.GetProperty("email").GetString());
        Assert.EndsWith("/api/v1/users/me", first.Headers.Location!.ToString(), StringComparison.Ordinal);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", body);
        var existing = await second.AssertJsonAsync(HttpStatusCode.OK);
        Assert.Equal(created.GetProperty("id").GetString(), existing.GetProperty("id").GetString());

        var command = factory.Fake.Commands.OfType<RegisterUserCommand>().First(c => c.User.Subject == "nuevo-usuario");
        Assert.Equal(TestTokens.Issuer, command.User.Issuer);

        var me = await (await client.GetAsync("/api/v1/users/me")).AssertJsonAsync(HttpStatusCode.OK);
        Assert.Equal("nuevo@ejemplo.com", me.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Registro_rechaza_un_sub_o_email_enviados_en_el_cuerpo()
    {
        var client = factory.CreateClient(TestTokens.Create(subject: "victima"));

        var response = await client.SendAsync(PostJson("/api/v1/auth/register", """{"sub":"otro-usuario","email":"x@ejemplo.com"}"""));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.DoesNotContain(factory.Fake.Commands.OfType<RegisterUserCommand>(), c => c.User.Subject == "otro-usuario");
    }

    [Fact]
    public async Task Registro_sin_email_verificado_en_el_token_recibe_400()
    {
        var client = factory.CreateClient(TestTokens.Create(subject: "sin-verificar", emailVerified: false));

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("ref con espacios")]
    public async Task Facturacion_rechaza_referencias_de_pago_que_parecen_datos_reales(string reference)
    {
        var client = factory.CreateClient(TestTokens.Create());
        var body = new { billingName = "Ana", billingEmail = "ana@ejemplo.com", billingAddress = "Quito", paymentMethodReference = reference };

        var response = await client.PutAsJsonAsync("/api/v1/customers/me", body);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Facturacion_valida_se_actualiza()
    {
        var client = factory.CreateClient(TestTokens.Create());
        var body = new { billingName = "Ana", billingEmail = "ana@ejemplo.com", billingAddress = "Quito", paymentMethodReference = "sim-card-01" };

        var json = await (await client.PutAsJsonAsync("/api/v1/customers/me", body)).AssertJsonAsync(HttpStatusCode.OK);

        Assert.Equal("sim-card-01", json.GetProperty("paymentMethodReference").GetString());
        Assert.True(json.TryGetProperty("updatedAt", out _));
    }

    [Fact]
    public async Task Compra_directa_con_pago_devuelve_201_y_location_del_pedido()
    {
        var client = factory.CreateClient(TestTokens.Create());

        var response = await client.SendAsync(Post(PurchaseUrl, new { date = "2026-12-15", time = "09:30", quantity = 2, paymentMethod = "CARD" }, Guid.NewGuid()));

        var json = await response.AssertJsonAsync(HttpStatusCode.Created);
        Assert.Equal("PAID", json.GetProperty("status").GetString());
        Assert.Equal(90m, json.GetProperty("totalAmount").GetDecimal());
        Assert.Contains($"/api/v1/orders/{json.GetProperty("orderId").GetString()}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compra_que_supera_los_cupos_disponibles_es_rechazada_antes_del_pago()
    {
        var client = factory.CreateClient(TestTokens.Create());

        var response = await client.SendAsync(Post(PurchaseUrl,
            new { date = "2026-12-15", time = "09:30", quantity = FakeApplication.AvailableSpots + 1, paymentMethod = "CARD" }, Guid.NewGuid()));

        var problem = await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("INSUFFICIENT_AVAILABILITY", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Compra_con_fecha_u_hora_invalida_recibe_400()
    {
        var client = factory.CreateClient(TestTokens.Create());

        var response = await client.SendAsync(PostJson(PurchaseUrl, """{"date":"2026-13-45","time":"9am","quantity":1}""", Guid.NewGuid()));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Usuario_no_puede_leer_ni_cancelar_pedidos_ajenos()
    {
        var orderId = factory.Fake.AddOrder("propietario");
        var intruder = factory.CreateClient(TestTokens.Create(subject: "intruso"));

        await (await intruder.GetAsync($"/api/v1/orders/{orderId}")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await intruder.GetAsync($"/api/v1/orders/{orderId}/events")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await intruder.SendAsync(Post($"/api/v1/orders/{orderId}/cancel", new { reason = "x" }, Guid.NewGuid())))
            .AssertProblemAsync(HttpStatusCode.NotFound);

        var owner = factory.CreateClient(TestTokens.Create(subject: "propietario"));
        var order = await (await owner.GetAsync($"/api/v1/orders/{orderId}")).AssertJsonAsync(HttpStatusCode.OK);
        Assert.Equal("PENDING_PAYMENT", order.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Pedido_se_crea_consulta_y_cancela_con_transicion_validada()
    {
        var client = factory.CreateClient(TestTokens.Create(subject: "comprador"));

        var createResponse = await client.SendAsync(Post("/api/v1/orders",
            new { attractionId = FakeApplication.ExistingAttractionId, date = "2026-12-15", time = "09:30", quantity = 2 }, Guid.NewGuid()));
        var created = await createResponse.AssertJsonAsync(HttpStatusCode.Created);
        var orderId = created.GetProperty("id").GetString();

        var events = await (await client.GetAsync($"/api/v1/orders/{orderId}/events")).AssertJsonAsync(HttpStatusCode.OK);
        Assert.Equal("PENDING_PAYMENT", events.GetProperty("events")[0].GetProperty("newStatus").GetString());

        var cancelled = await (await client.SendAsync(Post($"/api/v1/orders/{orderId}/cancel", new { reason = "Cambio de planes" }, Guid.NewGuid())))
            .AssertJsonAsync(HttpStatusCode.OK);
        Assert.Equal("CANCELLED", cancelled.GetProperty("status").GetString());

        var again = await client.SendAsync(Post($"/api/v1/orders/{orderId}/cancel", new { reason = "Otra vez" }, Guid.NewGuid()));
        var problem = await again.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("INVALID_STATE_TRANSITION", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Pago_simulado_rechaza_datos_de_tarjeta_y_status_enviados_por_el_cliente()
    {
        var orderId = factory.Fake.AddOrder("user-1");
        var client = factory.CreateClient(TestTokens.Create());

        var withCard = await client.SendAsync(PostJson("/api/v1/payments/simulations",
            $$"""{"orderId":"{{orderId}}","paymentMethod":"CARD","amount":90.00,"currency":"USD","cardNumber":"4111111111111111","cvv":"123"}""",
            Guid.NewGuid()));
        await withCard.AssertProblemAsync(HttpStatusCode.BadRequest);

        var withStatus = await client.SendAsync(PostJson("/api/v1/payments/simulations",
            $$"""{"orderId":"{{orderId}}","paymentMethod":"CARD","amount":90.00,"currency":"USD","status":"AUTHORIZED"}""",
            Guid.NewGuid()));
        await withStatus.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pago_simulado_valido_no_devuelve_datos_financieros()
    {
        var orderId = factory.Fake.AddOrder("user-1");
        var client = factory.CreateClient(TestTokens.Create());

        var response = await client.SendAsync(Post("/api/v1/payments/simulations",
            new { orderId, paymentMethod = "CARD", amount = 90.00m, currency = "USD" }, Guid.NewGuid()));

        var json = await response.AssertJsonAsync(HttpStatusCode.Created);
        Assert.Equal("PENDING", json.GetProperty("status").GetString());
        var names = json.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Equal(["id", "orderId", "paymentMethod", "status", "amount", "currency", "gatewayReference", "createdAt"], names);
    }

    [Fact]
    public async Task Pago_simulado_con_importe_distinto_recibe_422()
    {
        var orderId = factory.Fake.AddOrder("user-1");
        var client = factory.CreateClient(TestTokens.Create());

        var response = await client.SendAsync(Post("/api/v1/payments/simulations",
            new { orderId, paymentMethod = "CARD", amount = 1.00m, currency = "USD" }, Guid.NewGuid()));

        var problem = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("AMOUNT_MISMATCH", problem.GetProperty("code").GetString());
    }
}
