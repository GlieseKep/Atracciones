using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Queries.Ecommerce;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Extensions;
using AtraccionesService.DataAcess.Seed;
using AtraccionesService.DataAcess.Tests.Infrastructure;
using AtraccionesService.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Money = AtraccionesService.Domain.Common.Money;
using Microsoft.Extensions.DependencyInjection;

namespace AtraccionesService.DataAcess.Tests;

/// <summary>
/// Casos de uso reales de Application sobre la persistencia EF Core: verifican transacciones, idempotencia,
/// eventos y ownership de punta a punta (sin HTTP).
/// </summary>
public sealed class ApplicationIntegrationTests
{
    private static readonly DateOnly ServiceDate = new(2026, 12, 15);
    private static readonly AuthenticatedUser Buyer = new("https://issuer.test", "buyer", "buyer@ejemplo.com", true);

    public static IEnumerable<object[]> Providers() => TestDatabase.Providers();

    private static async Task<Guid> SeedCatalogAsync(TestDatabase db, decimal price, int capacity)
    {
        var attraction = Attraction.Create(new AttractionDetails(
            "Teleférico", "Descripción", "PT2H", new Money("USD", price), ["Aventura"], [],
            [new Location("Av. Occidental", "Quito", "EC", null, null, null)], [], null, ProductType.SINGLE_TICKET, [], ["es"], false));
        await db.WithUnitOfWork(async uow =>
        {
            await uow.Attractions.AddAsync(attraction, default);
            await uow.Availability.AddAsync(new AvailabilitySlot(Guid.NewGuid(), attraction.Id, ServiceDate, new TimeOnly(9, 30), capacity, 0, 0), default);
            return true;
        });
        return attraction.Id;
    }

    private static Task<RegisteredUserResult> RegisterAsync(TestDatabase db, AuthenticatedUser user) =>
        db.Run<IUserProfileService, RegisteredUserResult>(s =>
            s.RegisterAsync(new RegisterUserCommand(user, user.Email!, new BillingData("Comprador", null, null, null, null)), default));

    private static Task<PurchaseResult> PurchaseAsync(TestDatabase db, Guid attractionId, int quantity, string? method, Guid key, AuthenticatedUser? user = null) =>
        db.Run<IPurchaseService, PurchaseResult>(s => s.CreateAsync(
            new CreatePurchaseCommand(attractionId, ServiceDate, "09:30", quantity, method, key, user ?? Buyer), default));

    private static Task<int> ReservedAsync(TestDatabase db, Guid attractionId) =>
        db.WithContext(c => c.Availability.Where(s => s.AttractionId == attractionId).Select(s => s.ReservedQuantity).SingleAsync());

    [Theory, MemberData(nameof(Providers))]
    public async Task Compra_directa_pagada_persiste_pedido_reserva_pago_y_eventos(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var attractionId = await SeedCatalogAsync(db, 45m, 10);
        await RegisterAsync(db, Buyer);
        var key = Guid.NewGuid();

        var purchase = await PurchaseAsync(db, attractionId, 2, "CARD", key);
        var replay = await PurchaseAsync(db, attractionId, 2, "CARD", key);

        Assert.Equal("PAID", purchase.Status);
        Assert.Equal(90m, purchase.TotalAmount);
        Assert.Equal(purchase.OrderId, replay.OrderId);
        Assert.Equal(2, await ReservedAsync(db, attractionId));
        Assert.Equal(1, await db.WithContext(c => c.Orders.CountAsync()));
        Assert.Equal("CONFIRMED", await db.WithContext(c => c.Reservations.Select(r => r.Status).SingleAsync()));
        Assert.Equal(["ORDER_CREATED", "PAYMENT_SETTLED"],
            await db.WithContext(c => c.OrderEvents.OrderBy(e => e.EventType).Select(e => e.EventType).ToListAsync()));
        Assert.Equal(3, await db.WithContext(c => c.PaymentEvents.CountAsync()));
        Assert.Equal("COMPLETED", await db.WithContext(c => c.IdempotencyKeys.Where(k => k.Operation == "create-purchase").Select(k => k.Status).SingleAsync()));

        var order = await db.Run<IOrderService, OrderResult>(s => s.GetAsync(new GetOrderQuery(purchase.OrderId, Buyer), default));
        Assert.Equal("SETTLED", order.PaymentSimulation!.Status);
        Assert.Single(order.Items);
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Fallo_de_disponibilidad_revierte_la_transaccion_completa(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var attractionId = await SeedCatalogAsync(db, 45m, 1);
        await RegisterAsync(db, Buyer);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => PurchaseAsync(db, attractionId, 2, "CARD", Guid.NewGuid()));

        Assert.Equal(ConflictException.InsufficientAvailability, conflict.Code);
        Assert.Equal(0, await db.WithContext(c => c.Orders.CountAsync()));
        Assert.Equal(0, await db.WithContext(c => c.Purchases.CountAsync()));
        Assert.Equal(0, await db.WithContext(c => c.IdempotencyKeys.CountAsync()));
        Assert.Equal(0, await ReservedAsync(db, attractionId));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Pago_rechazado_conserva_intentos_y_la_cancelacion_libera_cupos(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var attractionId = await SeedCatalogAsync(db, 10.51m, 10);
        await RegisterAsync(db, Buyer);

        var order = await db.Run<IOrderService, OrderResult>(s => s.CreateAsync(
            new CreateOrderCommand(attractionId, ServiceDate, "09:30", 1, Guid.NewGuid(), Buyer), default));
        var rejected = await db.Run<IPaymentSimulationService, PaymentSimulationResult>(s => s.SimulateAsync(
            new SimulatePaymentCommand(order.Id, "CARD", 10.51m, "USD", Guid.NewGuid(), Buyer), default));

        Assert.Equal("REJECTED", rejected.Status);
        Assert.Equal(1, await db.WithContext(c => c.PaymentAttempts.CountAsync()));
        Assert.Equal("PENDING_PAYMENT", await db.WithContext(c => c.Orders.Select(o => o.Status).SingleAsync()));

        var cancelled = await db.Run<IOrderService, OrderResult>(s => s.CancelAsync(
            new CancelOrderCommand(order.Id, "Cambio de planes", Guid.NewGuid(), Buyer), default));
        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal(0, await ReservedAsync(db, attractionId));
        Assert.Equal("CANCELLED", await db.WithContext(c => c.Reservations.Select(r => r.Status).SingleAsync()));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Pedidos_ajenos_no_son_visibles_y_el_registro_no_duplica_perfiles(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var attractionId = await SeedCatalogAsync(db, 20m, 10);
        var other = new AuthenticatedUser("https://issuer.test", "other", "other@ejemplo.com", true);
        await RegisterAsync(db, Buyer);
        await RegisterAsync(db, other);
        var again = await RegisterAsync(db, Buyer);
        var purchase = await PurchaseAsync(db, attractionId, 1, null, Guid.NewGuid());

        Assert.False(again.Created);
        Assert.Equal(2, await db.WithContext(c => c.Users.CountAsync()));
        await Assert.ThrowsAsync<NotFoundException>(() => db.Run<IOrderService, OrderResult>(s => s.GetAsync(new GetOrderQuery(purchase.OrderId, other), default)));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Seeder_carga_catalogo_y_ecommerce_de_forma_idempotente(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var options = new SeedOptions { Catalog = true, Ecommerce = true, AvailabilityDays = 3 };

        for (var i = 0; i < 2; i++)
        {
            await db.Run<DatabaseSeeder, bool>(async seeder => { await seeder.SeedAsync(options, default); return true; });
        }

        Assert.Equal(3, await db.WithContext(c => c.Attractions.CountAsync()));
        Assert.Equal(3 * 3 * CatalogSeeder.SlotTimes.Length, await db.WithContext(c => c.Availability.CountAsync()));
        Assert.Equal(1, await db.WithContext(c => c.Users.CountAsync()));
        Assert.Equal("PAID", await db.WithContext(c => c.Orders.Select(o => o.Status).SingleAsync()));
        Assert.Equal(0, await db.WithContext(c => c.Roles.CountAsync()));

        // El catálogo sembrado es consumible por Application.
        var catalog = await db.Run<IAttractionService, PaginationResult<AttractionResult>>(s =>
            s.ListAsync(new Application.Queries.Attractions.ListAttractionsQuery(10, 0), default));
        Assert.Equal(3, catalog.TotalItems);
    }
}
