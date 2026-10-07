using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Entities.Ecommerce;
using AtraccionesService.DataAcess.Extensions;
using AtraccionesService.DataAcess.Tests.Infrastructure;
using AtraccionesService.DataManagment.Contracts.Catalog;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.Exceptions;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Common;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Identity;
using AtraccionesService.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AtraccionesService.DataAcess.Tests;

public sealed class RepositoryTests
{
    private static readonly DateOnly ServiceDate = new(2026, 12, 15);
    private static readonly TimeOnly ServiceTime = new(9, 30);

    public static IEnumerable<object[]> Providers() => TestDatabase.Providers();

    private static Attraction NewAttraction(string name = "Teleférico", string city = "Quito", decimal price = 45m, string[]? categories = null) =>
        Attraction.Create(new AttractionDetails(
            name, "Descripción", "PT2H", new Money("USD", price), categories ?? ["Aventura", "Naturaleza"], ["Nuevo"],
            [new Location("Av. Occidental", city, "EC", -0.19, -78.52, "MEETING_POINT"), new Location("Plaza Grande", city, "EC", null, null, null)],
            ["https://ejemplo.com/1.jpg", "https://ejemplo.com/2.jpg"], new OperatorInfo(77, "Operador"), ProductType.GUIDED_TOUR,
            ["Guía"], ["es", "en"], true));

    private static async Task<Guid> SeedSlotAsync(TestDatabase db, Guid attractionId, int capacity, DateOnly? date = null) =>
        await db.WithUnitOfWork(async uow =>
        {
            var slot = new AvailabilitySlot(Guid.NewGuid(), attractionId, date ?? ServiceDate, ServiceTime, capacity, 0, 0);
            await uow.Availability.AddAsync(slot, default);
            return slot.Id;
        });

    private static async Task<(User User, Customer Customer)> SeedCustomerAsync(TestDatabase db, string subject = "user-1") =>
        await db.WithUnitOfWork(async uow =>
        {
            var user = User.Register("https://issuer.test", subject, $"{subject}@ejemplo.com", TestDatabase.StartTime);
            var customer = Customer.CreateFor(user, TestDatabase.StartTime);
            await uow.Users.AddAsync(user, default);
            await uow.Customers.AddAsync(customer, default);
            return (user, customer);
        });

    [Theory, MemberData(nameof(Providers))]
    public async Task Migraciones_aplicadas_y_sin_cambios_de_modelo_pendientes(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);

        await db.WithContext(async context =>
        {
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.False(context.Database.HasPendingModelChanges());
            return true;
        });
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Atraccion_persiste_relaciones_reutiliza_valores_de_catalogo_y_reemplaza_al_actualizar(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var first = NewAttraction("Primera");
        var second = NewAttraction("Segunda", categories: ["Aventura", "Cultura"]);

        await db.WithUnitOfWork(async uow =>
        {
            await uow.Attractions.AddAsync(first, default);
            await uow.Attractions.AddAsync(second, default);
            return true;
        });

        var loaded = await db.WithUnitOfWork(uow => uow.Attractions.GetByIdAsync(first.Id, default));
        Assert.NotNull(loaded);
        Assert.Equal(first.Details.Price, loaded.Details.Price);
        Assert.Equal(["Aventura", "Naturaleza"], loaded.Details.Categories);
        Assert.Equal(first.Details.Locations, loaded.Details.Locations);
        Assert.Equal(first.Details.PhotoUrls, loaded.Details.PhotoUrls);
        Assert.Equal(new OperatorInfo(77, "Operador"), loaded.Details.Operator);
        Assert.Equal(3, await db.WithContext(c => c.Categories.CountAsync()));
        Assert.Equal(1, await db.WithContext(c => c.Operators.CountAsync()));

        loaded.Replace(loaded.Details with
        {
            Categories = ["Cultura"],
            Locations = [new Location("Nueva dirección", "Cuenca", "EC", null, null, null)],
            PhotoUrls = [],
        });
        await db.WithUnitOfWork(async uow =>
        {
            await uow.Attractions.UpdateAsync(loaded, default);
            return true;
        });

        var updated = await db.WithUnitOfWork(uow => uow.Attractions.GetByIdAsync(first.Id, default));
        Assert.Equal(["Cultura"], updated!.Details.Categories);
        Assert.Equal("Cuenca", Assert.Single(updated.Details.Locations).City);
        Assert.Empty(updated.Details.PhotoUrls);
        Assert.Equal(3, await db.WithContext(c => c.Locations.CountAsync()));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Eliminar_atraccion_borra_franjas_en_cascada_y_conserva_reservas_historicas(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var attraction = NewAttraction();
        await db.WithUnitOfWork(async uow => { await uow.Attractions.AddAsync(attraction, default); return true; });
        await SeedSlotAsync(db, attraction.Id, 10);
        var (_, customer) = await SeedCustomerAsync(db);
        var reservation = Reservation.Create(attraction.Id, customer.Id, new DateOnly(2026, 1, 10), ServiceTime, 1, new Money("USD", 45m),
            "Ana", "ana@ejemplo.com", ReservationStatus.CONFIRMED, TestDatabase.StartTime);
        await db.WithUnitOfWork(async uow => { await uow.Reservations.AddAsync(reservation, default); return true; });

        await db.WithUnitOfWork(async uow => { await uow.Attractions.DeleteAsync(attraction.Id, default); return true; });

        Assert.False(await db.WithUnitOfWork(uow => uow.Attractions.ExistsAsync(attraction.Id, default)));
        Assert.Equal(0, await db.WithContext(c => c.Availability.CountAsync()));
        Assert.NotNull(await db.WithUnitOfWork(uow => uow.Reservations.GetByIdAsync(reservation.Id, default)));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Identidad_oauth_y_cliente_por_usuario_son_unicos(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var (user, _) = await SeedCustomerAsync(db);

        await Assert.ThrowsAsync<ConcurrencyException>(() => db.WithUnitOfWork(async uow =>
        {
            await uow.Users.AddAsync(User.Register("https://issuer.test", "user-1", "otro@ejemplo.com", TestDatabase.StartTime), default);
            return true;
        }));
        await Assert.ThrowsAsync<ConcurrencyException>(() => db.WithUnitOfWork(async uow =>
        {
            await uow.Customers.AddAsync(Customer.CreateFor(user, TestDatabase.StartTime), default);
            return true;
        }));

        var found = await db.WithUnitOfWork(uow => uow.Users.GetByIdentityAsync("https://issuer.test", "user-1", default));
        Assert.Equal(user.Id, found!.Id);
        Assert.Null(await db.WithUnitOfWork(uow => uow.Users.GetByIdentityAsync("https://otro.test", "user-1", default)));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Reserva_de_cupos_es_condicional_y_no_sobrevende(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var attraction = NewAttraction();
        await db.WithUnitOfWork(async uow => { await uow.Attractions.AddAsync(attraction, default); return true; });
        var slotId = await SeedSlotAsync(db, attraction.Id, 3);

        Assert.True(await db.WithUnitOfWork(uow => uow.Availability.TryReserveQuantityAsync(slotId, 2, default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.Availability.TryReserveQuantityAsync(slotId, 2, default)));
        Assert.True(await db.WithUnitOfWork(uow => uow.Availability.TryReserveQuantityAsync(slotId, 1, default)));

        var full = await db.WithUnitOfWork(uow => uow.Availability.GetSlotAsync(attraction.Id, ServiceDate, ServiceTime, default));
        Assert.Equal(0, full!.AvailableSpots);
        Assert.Equal(2, full.Version);

        await db.WithUnitOfWork(async uow => { await uow.Availability.ReleaseQuantityAsync(slotId, 5, default); return true; });
        Assert.Equal(3, (await db.WithUnitOfWork(uow => uow.Availability.GetSlotAsync(attraction.Id, ServiceDate, ServiceTime, default)))!.AvailableSpots);
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Reservas_concurrentes_por_el_ultimo_cupo_solo_una_gana(DatabaseProvider provider)
    {
        if (provider != DatabaseProvider.SqlServer)
        {
            return; // SQLite en memoria serializa escrituras; la carrera real se verifica en SQL Server.
        }

        await using var db = await TestDatabase.CreateAsync(provider);
        var attraction = NewAttraction();
        await db.WithUnitOfWork(async uow => { await uow.Attractions.AddAsync(attraction, default); return true; });
        var slotId = await SeedSlotAsync(db, attraction.Id, 5);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => db.WithUnitOfWork(async uow =>
        {
            await uow.BeginTransactionAsync();
            var ok = await uow.Availability.TryReserveQuantityAsync(slotId, 1, default);
            await uow.CommitAsync();
            return ok;
        })));

        Assert.Equal(5, results.Count(r => r));
        Assert.Equal(0, (await db.WithUnitOfWork(uow => uow.Availability.GetSlotAsync(attraction.Id, ServiceDate, ServiceTime, default)))!.AvailableSpots);
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Idempotencia_impone_unicidad_completa_y_expira(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var key = Guid.NewGuid();
        var record = new IdempotencyRecord("https://issuer.test", "user-1", "create-order", key, "HASH", IdempotencyStatus.IN_PROGRESS, null, null,
            TestDatabase.StartTime, TestDatabase.StartTime.AddHours(24));

        Assert.True(await db.WithUnitOfWork(uow => uow.Idempotency.TryCreateInProgressAsync(record, default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.Idempotency.TryCreateInProgressAsync(record with { RequestHash = "OTRO" }, default)));
        Assert.True(await db.WithUnitOfWork(uow => uow.Idempotency.TryCreateInProgressAsync(record with { Operation = "cancel-order" }, default)));

        await db.WithUnitOfWork(async uow =>
        {
            await uow.Idempotency.CompleteAsync(record with { Status = IdempotencyStatus.COMPLETED, ResponseBody = "{\"id\":1}" }, default);
            return true;
        });
        var completed = await db.WithUnitOfWork(uow => uow.Idempotency.GetByIdentityAsync("https://issuer.test", "user-1", "create-order", key, default));
        Assert.Equal(IdempotencyStatus.COMPLETED, completed!.Status);
        Assert.Equal("{\"id\":1}", completed.ResponseBody);
        Assert.Equal(TestDatabase.StartTime.AddHours(24), completed.ExpiresAt);

        Assert.Equal(0, await db.WithUnitOfWork(uow => uow.Idempotency.RemoveExpiredAsync(TestDatabase.StartTime.AddHours(23), default)));
        Assert.Equal(2, await db.WithUnitOfWork(uow => uow.Idempotency.RemoveExpiredAsync(TestDatabase.StartTime.AddHours(24), default)));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Rollback_revierte_todo_lo_escrito_en_la_transaccion(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var user = User.Register("https://issuer.test", "temporal", "t@ejemplo.com", TestDatabase.StartTime);

        await db.WithUnitOfWork(async uow =>
        {
            await uow.BeginTransactionAsync();
            await uow.Users.AddAsync(user, default);
            Assert.NotNull(await uow.Users.GetByIdAsync(user.Id, default));
            await uow.RollbackAsync();
            return true;
        });

        Assert.Null(await db.WithUnitOfWork(uow => uow.Users.GetByIdAsync(user.Id, default)));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Eventos_son_inmutables(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.WithContext(async context =>
        {
            var audit = new AuditEventEntity { Id = Guid.NewGuid(), ActorSubject = "admin", Action = "x", ResourceType = "attraction", ResourceId = "1", CreatedAt = TestDatabase.StartTime };
            context.AuditEvents.Add(audit);
            await context.SaveChangesAsync();
            audit.Action = "alterado";
            return await context.SaveChangesAsync();
        }));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Busqueda_filtra_ordena_por_precio_y_pagina_en_el_motor(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var cheap = NewAttraction("Barata", city: "Quito", price: 10.50m);
        var expensive = NewAttraction("Cara", city: "QUITO", price: 99.99m);
        var other = NewAttraction("Otra ciudad", city: "Cuenca", price: 5m);
        await db.WithUnitOfWork(async uow =>
        {
            foreach (var a in new[] { cheap, expensive, other })
            {
                await uow.Attractions.AddAsync(a, default);
            }

            return true;
        });
        await SeedSlotAsync(db, cheap.Id, 5, new DateOnly(2026, 12, 1));

        AttractionSearchCriteria Criteria(AttractionSort sort, DateOnly? start = null) => new("USD", ["quito"], ["EC"], start, null, null, null, sort);

        var byPrice = await db.WithUnitOfWork(uow => uow.Attractions.SearchAsync(Criteria(AttractionSort.PriceDescending), new PaginationRequest(1, 0), default));
        Assert.Equal(2, byPrice.TotalItems);
        Assert.Equal("Cara", Assert.Single(byPrice.Items).Details.Name);

        var secondPage = await db.WithUnitOfWork(uow => uow.Attractions.SearchAsync(Criteria(AttractionSort.PriceDescending), new PaginationRequest(1, 1), default));
        Assert.Equal("Barata", Assert.Single(secondPage.Items).Details.Name);

        var withAvailability = await db.WithUnitOfWork(uow => uow.Attractions.SearchAsync(
            Criteria(AttractionSort.PriceAscending, new DateOnly(2026, 11, 1)), new PaginationRequest(10, 0), default));
        Assert.Equal("Barata", Assert.Single(withAvailability.Items).Details.Name);
    }

    [Fact]
    public void El_modelo_de_pagos_no_tiene_columnas_de_datos_de_tarjeta()
    {
        var forbidden = new[] { "card", "cvv", "pan", "expiry", "token" };
        var properties = typeof(PaymentSimulationEntity).GetProperties()
            .Concat(typeof(PaymentAttemptEntity).GetProperties())
            .Concat(typeof(PaymentEventEntity).GetProperties())
            .Select(p => p.Name.ToLowerInvariant());

        Assert.DoesNotContain(properties, name => forbidden.Any(f => name.Contains(f, StringComparison.Ordinal)));
    }
}
