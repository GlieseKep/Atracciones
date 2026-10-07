using System.Net;
using AtraccionesService.API.Tests.Infrastructure;
using AtraccionesService.Application.Queries.Reservations;
using static AtraccionesService.API.Tests.Infrastructure.HttpAssertions;

namespace AtraccionesService.API.Tests;

public sealed class ReservationsAndIdempotencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string ReservationsUrl = $"/api/v1/atracciones/{FakeApplication.ExistingAttractionId}/reservations";

    private static object Reservation(int tickets = 2) => new
    {
        date = "2026-12-15",
        time = "09:30",
        ticketCount = tickets,
        customerName = "Ana Pérez",
        customerEmail = "ana@ejemplo.com",
    };

    [Fact]
    public async Task Reserva_sin_idempotency_key_recibe_400()
    {
        var response = await factory.CreateClient(TestTokens.Create()).SendAsync(Post(ReservationsUrl, Reservation()));

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal("INVALID_IDEMPOTENCY_KEY", problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("no-es-un-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Reserva_con_idempotency_key_invalida_recibe_400(string key)
    {
        var response = await factory.CreateClient(TestTokens.Create()).SendAsync(Post(ReservationsUrl, Reservation(), rawKey: key));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Repetir_la_misma_idempotency_key_no_duplica_la_reserva()
    {
        var client = factory.CreateClient(TestTokens.Create(subject: "idem-user"));
        var key = Guid.NewGuid();
        var before = factory.Fake.ReservationsCreated;

        var first = await (await client.SendAsync(Post(ReservationsUrl, Reservation(), key))).AssertJsonAsync(HttpStatusCode.Created);
        var second = await (await client.SendAsync(Post(ReservationsUrl, Reservation(), key))).AssertJsonAsync(HttpStatusCode.Created);

        Assert.Equal(first.GetProperty("reservationId").GetString(), second.GetProperty("reservationId").GetString());
        Assert.Equal(before + 1, factory.Fake.ReservationsCreated);
        Assert.Equal("CONFIRMED", first.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Misma_idempotency_key_con_otro_payload_recibe_409()
    {
        var client = factory.CreateClient(TestTokens.Create(subject: "idem-user-2"));
        var key = Guid.NewGuid();
        await client.SendAsync(Post(ReservationsUrl, Reservation(2), key));

        var response = await client.SendAsync(Post(ReservationsUrl, Reservation(3), key));

        var problem = await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reserva_con_datos_invalidos_recibe_400()
    {
        var invalid = new { date = "2026-12-15", time = "25:99", ticketCount = 0, customerName = "", customerEmail = "no-es-correo" };

        var response = await factory.CreateClient(TestTokens.Create()).SendAsync(Post(ReservationsUrl, invalid, Guid.NewGuid()));

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        foreach (var field in new[] { "Time", "TicketCount", "CustomerName", "CustomerEmail" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Falta el error de '{field}': {errors}");
        }
    }

    [Fact]
    public async Task Historial_usa_la_identidad_del_token_y_devuelve_metadatos_de_paginacion()
    {
        var client = factory.CreateClient(TestTokens.Create(subject: "history-user", scopes: "attractions:read"));

        var json = await (await client.GetAsync("/api/v1/atracciones/reservations?limit=5&offset=10&status=CONFIRMED&sort=date"))
            .AssertJsonAsync(HttpStatusCode.OK);

        Assert.Equal(3, json.GetProperty("currentPage").GetInt32());
        Assert.Equal(5, json.GetProperty("itemsPerPage").GetInt32());
        Assert.True(json.TryGetProperty("totalItems", out _));
        Assert.True(json.TryGetProperty("totalPages", out _));
        var query = factory.Fake.Commands.OfType<GetReservationsQuery>().Last(q => q.User.Subject == "history-user");
        Assert.Equal("CONFIRMED", query.Status);
        Assert.False(query.SortDescending);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("offset=-1")]
    [InlineData("sort=nombre")]
    [InlineData("fromDate=2026-12-31&toDate=2026-01-01")]
    public async Task Historial_con_parametros_fuera_de_rango_recibe_400(string query)
    {
        var client = factory.CreateClient(TestTokens.Create(scopes: "attractions:read"));

        var response = await client.GetAsync($"/api/v1/customers/me/reservations?{query}");

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reserva_ajena_o_inexistente_recibe_404()
    {
        var response = await factory.CreateClient(TestTokens.Create()).GetAsync($"/api/v1/atracciones/reservations/{Guid.NewGuid()}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }
}
