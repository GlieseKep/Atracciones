using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Attractions;
using AtraccionesService.Application.Commands.Reservations;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Queries.Attractions;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Tests.Infrastructure;
using static AtraccionesService.Application.Tests.Infrastructure.ApplicationTestHost;

namespace AtraccionesService.Application.Tests;

public sealed class CatalogServiceTests : IDisposable
{
    private readonly ApplicationTestHost _host = new();

    private static AttractionData ValidData(string name = "Mitad del Mundo", decimal price = 30m) => new(
        name, "Monumento en la línea ecuatorial", "PT3H", new Money("USD", price), ["Cultura"], [],
        [new LocationData("Av. Manuel Córdova Galarza", "Quito", "EC", -0.002, -78.455, "ATTRACTION_SITE")],
        ["https://ejemplo.com/foto.jpg"], new OperatorData(7, "Operador"), "GUIDED_TOUR", ["Guía"], ["es", "en-US"], true);

    [Fact]
    public async Task Crear_requiere_permiso_local_de_catalogo()
    {
        var user = await _host.RegisterAsync("sin-permiso");

        await Assert.ThrowsAsync<ForbiddenException>(() => _host.Run<IAttractionService, AttractionResult>(s =>
            s.CreateAsync(new CreateAttractionCommand(ValidData(), Guid.NewGuid(), user), default)));
        Assert.Empty(_host.Database.Attractions);
    }

    [Fact]
    public async Task Crear_persiste_la_atraccion_y_registra_auditoria()
    {
        var admin = await _host.RegisterAsync("catalog-manager", LocalPermissions.CatalogWrite);

        var created = await _host.Run<IAttractionService, AttractionResult>(s =>
            s.CreateAsync(new CreateAttractionCommand(ValidData(), Guid.NewGuid(), admin), default));

        Assert.True(_host.Database.Attractions.ContainsKey(created.Id));
        Assert.Equal("GUIDED_TOUR", created.Data.ProductType);
        var audit = Assert.Single(_host.Database.AuditEvents);
        Assert.Equal("attraction.create", audit.Action);
        Assert.Equal("catalog-manager", audit.ActorSubject);
    }

    [Fact]
    public async Task Crear_con_datos_invalidos_devuelve_errores_por_campo()
    {
        var admin = await _host.RegisterAsync("catalog-manager", LocalPermissions.CatalogWrite);
        var invalid = ValidData() with
        {
            Name = "X",
            Duration = "dos horas",
            Price = new Money("usd", -1m),
            Categories = [],
            Locations = [new LocationData("", "Quito", "Ecuador", 10, null, null)],
            ProductType = "CRUISE",
            SupportedLanguages = ["español"],
        };

        var error = await Assert.ThrowsAsync<ValidationException>(() => _host.Run<IAttractionService, AttractionResult>(s =>
            s.CreateAsync(new CreateAttractionCommand(invalid, Guid.NewGuid(), admin), default)));

        foreach (var field in new[] { "name", "duration", "price.currency", "price.total", "categories", "locations[0].address",
                     "locations[0].country", "locations[0].coordinates", "productType", "supportedLanguages" })
        {
            Assert.True(error.Errors.ContainsKey(field), $"Falta el error de '{field}'.");
        }
    }

    [Fact]
    public async Task Patch_solo_modifica_campos_enviados_y_valida_el_resultado()
    {
        var admin = await _host.RegisterAsync("catalog-manager", LocalPermissions.CatalogWrite);
        var id = _host.SeedAttraction();
        var emptyPatch = new AttractionPatch(null, null, null, null, null, null, null, null, null, null, null, null, null);

        var patched = await _host.Run<IAttractionService, AttractionResult>(s =>
            s.PatchAsync(new PatchAttractionCommand(id, emptyPatch with { Name = "Nuevo nombre" }, Guid.NewGuid(), admin), default));

        Assert.Equal("Nuevo nombre", patched.Data.Name);
        Assert.Equal(45m, patched.Data.Price.Total);
        await Assert.ThrowsAsync<ValidationException>(() => _host.Run<IAttractionService, AttractionResult>(s =>
            s.PatchAsync(new PatchAttractionCommand(id, emptyPatch with { Categories = [] }, Guid.NewGuid(), admin), default)));
        Assert.Equal("Nuevo nombre", _host.Database.Attractions[id].Details.Name);
    }

    [Fact]
    public async Task Eliminar_se_bloquea_si_hay_reservas_activas_futuras()
    {
        var admin = await _host.RegisterAsync("catalog-manager", LocalPermissions.CatalogWrite);
        var id = _host.SeedAttraction();
        await _host.Run<IReservationService, ReservationResult>(s => s.CreateAsync(
            new CreateReservationCommand(id, ServiceDate, "09:30", 1, "Ana", "ana@ejemplo.com", Guid.NewGuid(), admin), default));

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => _host.Run<IAttractionService>(s =>
            s.DeleteAsync(new DeleteAttractionCommand(id, Guid.NewGuid(), admin), default)));

        Assert.Equal("ATTRACTION_HAS_ACTIVE_RESERVATIONS", conflict.Code);
        Assert.True(_host.Database.Attractions.ContainsKey(id));
    }

    [Fact]
    public async Task Eliminar_sin_reservas_borra_la_atraccion()
    {
        var admin = await _host.RegisterAsync("catalog-manager", LocalPermissions.CatalogWrite);
        var id = _host.SeedAttraction();

        await _host.Run<IAttractionService>(s => s.DeleteAsync(new DeleteAttractionCommand(id, Guid.NewGuid(), admin), default));

        Assert.False(_host.Database.Attractions.ContainsKey(id));
        await Assert.ThrowsAsync<NotFoundException>(() => _host.Run<IAttractionService, AttractionResult>(s => s.GetAsync(new GetAttractionQuery(id), default)));
    }

    [Fact]
    public async Task Busqueda_pagina_con_token_opaco_y_rechaza_tokens_manipulados_o_de_otra_busqueda()
    {
        for (var i = 0; i < 5; i++)
        {
            _host.SeedAttraction(price: 10m + i, name: $"Atracción {i}");
        }

        SearchAttractionsQuery Query(string? next = null, string sort = "price_asc") =>
            new(null, [], ["EC"], null, null, null, null, next, 2, sort);

        var first = await _host.Run<IAttractionService, SearchAttractionsResult>(s => s.SearchAsync(Query(), default));
        Assert.Equal(5, first.TotalResults);
        Assert.Equal([10m, 11m], first.Items.Select(a => a.Data.Price.Total));
        Assert.NotNull(first.NextPage);

        var second = await _host.Run<IAttractionService, SearchAttractionsResult>(s => s.SearchAsync(Query(first.NextPage), default));
        Assert.Equal([12m, 13m], second.Items.Select(a => a.Data.Price.Total));

        var third = await _host.Run<IAttractionService, SearchAttractionsResult>(s => s.SearchAsync(Query(second.NextPage), default));
        Assert.Single(third.Items);
        Assert.Null(third.NextPage);

        var tampered = first.NextPage![..^2] + (first.NextPage![^1] == 'A' ? "BB" : "AA");
        await Assert.ThrowsAsync<ValidationException>(() => _host.Run<IAttractionService, SearchAttractionsResult>(s => s.SearchAsync(Query(tampered), default)));
        await Assert.ThrowsAsync<ValidationException>(() => _host.Run<IAttractionService, SearchAttractionsResult>(s => s.SearchAsync(Query(first.NextPage, "price_desc"), default)));

        _host.Clock.Advance(TimeSpan.FromMinutes(16));
        await Assert.ThrowsAsync<ValidationException>(() => _host.Run<IAttractionService, SearchAttractionsResult>(s => s.SearchAsync(Query(first.NextPage), default)));
    }

    [Fact]
    public async Task Detalles_conserva_el_orden_y_omite_inexistentes()
    {
        var a = _host.SeedAttraction(name: "A");
        var b = _host.SeedAttraction(name: "B");

        var result = await _host.Run<IAttractionService, IReadOnlyList<AttractionResult>>(s =>
            s.GetDetailsAsync(new GetAttractionDetailsQuery([b, Guid.NewGuid(), a], ["es"]), default));

        Assert.Equal([b, a], result.Select(r => r.Id));
    }

    [Fact]
    public async Task Disponibilidad_suma_cupos_y_no_ofrece_franjas_ya_iniciadas()
    {
        var today = new DateOnly(2026, 10, 6);
        var id = _host.SeedAttraction(capacity: 8, date: today, time: new TimeOnly(6, 0));
        var afternoon = new Domain.Catalog.AvailabilitySlot(Guid.NewGuid(), id, today, new TimeOnly(15, 0), 10, 3, 0);
        _host.Database.Slots[afternoon.Id] = afternoon;

        var availability = await _host.Run<IAvailabilityService, AvailabilityResult>(s =>
            s.GetAsync(new GetAttractionAvailabilityQuery(id, today), default));

        Assert.Equal("America/Guayaquil", availability.TimeZone);
        Assert.Equal(["06:00", "15:00"], availability.Slots.Select(x => x.Time));
        Assert.Equal(0, availability.Slots[0].AvailableSpots);
        Assert.Equal(7, availability.AvailableSpots);
    }

    public void Dispose() => _host.Dispose();
}
