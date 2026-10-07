using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.DataAcess.Extensions;
using AtraccionesService.DataAcess.Tests.Infrastructure;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Identity;
using AtraccionesService.Domain.Reservations;
using Microsoft.Extensions.DependencyInjection;
using Money = AtraccionesService.Domain.Common.Money;

namespace AtraccionesService.DataAcess.Tests;

/// <summary>
/// Contratos ampliados de DataManagement implementados por DataAccess: concurrencia, consultas por especificación,
/// roles, reembolsos, reportes, eventos y fábrica de unidades de trabajo (SQLite y SQL Server).
/// </summary>
public sealed class ExtendedContractTests
{
    private static readonly DateOnly ServiceDate = new(2026, 12, 15);
    private static readonly TimeOnly ServiceTime = new(9, 30);
    private static readonly AuthenticatedUser Buyer = new("https://issuer.test", "buyer", "buyer@ejemplo.com", true);
    private static readonly ReportPeriod Period = new(TestDatabase.StartTime.AddDays(-1), TestDatabase.StartTime.AddDays(1));

    public static IEnumerable<object[]> Providers() => TestDatabase.Providers();

    /// <summary>Crea catálogo, perfil, una compra pagada y un pedido pendiente mediante los casos de uso reales.</summary>
    private static async Task<(Guid AttractionId, Guid SlotId, RegisteredUserResult Profile, PurchaseResult Paid, OrderResult Pending)> SeedAsync(TestDatabase db)
    {
        var attraction = Attraction.Create(new AttractionDetails(
            "Teleférico", "Descripción", "PT2H", new Money("USD", 45m), ["Aventura"], [],
            [new Location("Av. Occidental", "Quito", "EC", null, null, null)], [], null, ProductType.SINGLE_TICKET, [], ["es"], false));
        var slot = new AvailabilitySlot(Guid.NewGuid(), attraction.Id, ServiceDate, ServiceTime, 10, 0, 0);
        await db.WithUnitOfWork(async uow =>
        {
            await uow.Attractions.AddAsync(attraction, default);
            await uow.Availability.AddAsync(slot, default);
            return true;
        });

        var profile = await db.Run<IUserProfileService, RegisteredUserResult>(s =>
            s.RegisterAsync(new RegisterUserCommand(Buyer, Buyer.Email!, new BillingData("Comprador", null, null, null, null)), default));
        var paid = await db.Run<IPurchaseService, PurchaseResult>(s => s.CreateAsync(
            new CreatePurchaseCommand(attraction.Id, ServiceDate, "09:30", 2, "CARD", Guid.NewGuid(), Buyer), default));
        var pending = await db.Run<IOrderService, OrderResult>(s => s.CreateAsync(
            new CreateOrderCommand(attraction.Id, ServiceDate, "09:30", 1, Guid.NewGuid(), Buyer), default));
        return (attraction.Id, slot.Id, profile, paid, pending);
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Disponibilidad_con_unicidad_y_concurrencia_optimista(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var (attractionId, slotId, _, _, _) = await SeedAsync(db);

        var duplicate = new AvailabilitySlot(Guid.NewGuid(), attractionId, ServiceDate, ServiceTime, 50, 0, 0);
        Assert.False(await db.WithUnitOfWork(uow => uow.Availability.AddWithConcurrencyCheckAsync(duplicate, default)));
        Assert.True(await db.WithUnitOfWork(uow => uow.Availability.AddWithConcurrencyCheckAsync(
            new AvailabilitySlot(Guid.NewGuid(), attractionId, ServiceDate.AddDays(1), ServiceTime, 5, 0, 0), default)));

        var current = (await db.WithUnitOfWork(uow => uow.Availability.GetSlotByIdAsync(slotId, default)))!;
        var resized = new AvailabilitySlot(current.Id, current.AttractionId, current.Date, current.Time, 25, current.ReservedQuantity, current.Version);
        Assert.True(await db.WithUnitOfWork(uow => uow.Availability.UpdateIfVersionMatchesAsync(resized, current.Version, default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.Availability.UpdateIfVersionMatchesAsync(resized, current.Version, default)));

        var range = await db.WithUnitOfWork(uow => uow.Availability.GetSlotsInRangeAsync(attractionId, ServiceDate, ServiceDate.AddDays(7), default));
        Assert.Equal([25, 5], range.Select(s => s.Capacity));
        Assert.Equal(current.ReservedQuantity, await db.WithUnitOfWork(uow => uow.Inventory.GetNetReservedQuantityAsync(slotId, default)));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Reservas_por_atraccion_ownership_y_cambio_de_estado_condicional(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var (attractionId, _, profile, paid, _) = await SeedAsync(db);
        var reservationId = paid.ReservationId!.Value;

        Assert.True(await db.WithUnitOfWork(uow => uow.Reservations.ExistsForCustomerAsync(reservationId, profile.CustomerId, default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.Reservations.ExistsForCustomerAsync(reservationId, Guid.NewGuid(), default)));

        var byAttraction = await db.WithUnitOfWork(uow => uow.Reservations.GetByAttractionAsync(
            attractionId, new ReservationFilter(null, null, null, false), new PaginationRequest(10, 0), default));
        Assert.Equal(2, byAttraction.TotalItems);
        Assert.Equal(1, byAttraction.PageNumber);

        Assert.True(await db.WithUnitOfWork(uow => uow.Reservations.UpdateStatusAsync(
            reservationId, ReservationStatus.CONFIRMED, ReservationStatus.CANCELLED, "Prueba", default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.Reservations.UpdateStatusAsync(
            reservationId, ReservationStatus.CONFIRMED, ReservationStatus.CANCELLED, "Repetida", default)));
        var stored = await db.WithUnitOfWork(uow => uow.Reservations.GetByIdAsync(reservationId, default));
        Assert.Equal((ReservationStatus.CANCELLED, "Prueba"), (stored!.Status, stored.CancellationReason));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Pedidos_y_compras_por_cliente_con_especificacion_y_estado_condicional(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var (_, _, profile, paid, pending) = await SeedAsync(db);

        var orders = await db.WithUnitOfWork(uow => uow.Orders.GetByCustomerAsync(new OrderSpecification(profile.CustomerId), new PaginationRequest(10, 0), default));
        Assert.Equal(2, orders.TotalItems);
        Assert.All(orders.Items, o => Assert.Equal(1, o.ItemCount));

        var paidOnly = await db.WithUnitOfWork(uow => uow.Orders.GetByCustomerAsync(
            new OrderSpecification(profile.CustomerId, OrderStatus.PAID), new PaginationRequest(10, 0), default));
        Assert.Equal(paid.OrderId, Assert.Single(paidOnly.Items).Id);

        var purchases = await db.WithUnitOfWork(uow => uow.Purchases.GetByCustomerAsync(new PurchaseSpecification(profile.CustomerId), new PaginationRequest(10, 0), default));
        Assert.Equal(2, purchases.TotalItems);
        Assert.Contains(purchases.Items, p => p.OrderId == paid.OrderId && p.OrderStatus == "PAID");

        var updatedAt = TestDatabase.StartTime.AddMinutes(5);
        Assert.True(await db.WithUnitOfWork(uow => uow.Orders.UpdateStatusAsync(pending.Id, OrderStatus.PENDING_PAYMENT, OrderStatus.CANCELLED, updatedAt, default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.Orders.UpdateStatusAsync(pending.Id, OrderStatus.PENDING_PAYMENT, OrderStatus.PAID, updatedAt, default)));
        var order = await db.WithUnitOfWork(uow => uow.Orders.GetByIdAsync(pending.Id, default));
        Assert.Equal((OrderStatus.CANCELLED, (DateTimeOffset?)null), (order!.Status, order.HoldExpiresAt));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Pagos_intentos_eventos_e_idempotencia_por_hash(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var (_, _, _, paid, _) = await SeedAsync(db);
        var paymentId = paid.Payment!.Id;

        var simulation = await db.WithUnitOfWork(uow => uow.Payments.GetSimulationAsync(paymentId, default));
        var attempts = await db.WithUnitOfWork(uow => uow.Payments.GetAttemptsAsync(paymentId, default));
        var events = await db.WithUnitOfWork(uow => uow.PaymentEvents.GetBySimulationAsync(paymentId, default));

        Assert.Equal(PaymentStatus.SETTLED, simulation!.Status);
        Assert.Equal(1, Assert.Single(attempts).AttemptNumber);
        Assert.Equal(3, events.Count);

        var record = await db.WithContext(c => Task.FromResult(c.IdempotencyKeys.Single(k => k.Operation == "create-purchase")));
        var byHash = await db.WithUnitOfWork(uow => uow.Idempotency.GetByRequestHashAsync(record.Issuer, record.Subject, record.Operation, record.RequestHash, default));
        Assert.Equal(record.Key, Assert.Single(byHash).Key);
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Usuarios_por_email_cliente_por_usuario_roles_y_asignaciones(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var (_, _, profile, _, _) = await SeedAsync(db);

        Assert.Equal(profile.Id, Assert.Single(await db.WithUnitOfWork(uow => uow.Users.GetByEmailAsync("buyer@ejemplo.com", default))).Id);
        Assert.True(await db.WithUnitOfWork(uow => uow.Customers.BelongsToUserAsync(profile.CustomerId, profile.Id, default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.Customers.BelongsToUserAsync(profile.CustomerId, Guid.NewGuid(), default)));

        var role = Role.Create("catalog_manager", "Gestión de catálogo", ["catalog:write", "catalog:read"], TestDatabase.StartTime);
        await db.WithUnitOfWork(async uow => { await uow.Roles.AddAsync(role, default); return true; });
        Assert.Equal(["catalog:read", "catalog:write"], (await db.WithUnitOfWork(uow => uow.Roles.GetByNameAsync("catalog_manager", default)))!.Permissions);

        await db.WithUnitOfWork(async uow =>
        {
            await uow.UserRoles.AssignAsync(UserRoleAssignment.Assign(profile.Id, role.Id, null, "Alta inicial", TestDatabase.StartTime), default);
            return true;
        });
        Assert.Contains("catalog:write", await db.WithUnitOfWork(uow => uow.UserRoles.GetEffectivePermissionsAsync(profile.Id, default)));
        Assert.Single(await db.WithUnitOfWork(uow => uow.UserRoles.GetActiveAssignmentsAsync(profile.Id, default)));

        Assert.True(await db.WithUnitOfWork(uow => uow.UserRoles.RevokeAsync(profile.Id, role.Id, TestDatabase.StartTime.AddHours(1), default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.UserRoles.RevokeAsync(profile.Id, role.Id, TestDatabase.StartTime.AddHours(2), default)));
        Assert.Empty(await db.WithUnitOfWork(uow => uow.UserRoles.GetEffectivePermissionsAsync(profile.Id, default)));
        Assert.Equal(1, await db.WithContext(c => Task.FromResult(c.UserRoles.Count())));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Reembolsos_simulados_acumulan_y_se_completan_una_vez(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var (_, _, profile, paid, _) = await SeedAsync(db);
        var paymentId = paid.Payment!.Id;

        var first = RefundSimulation.Request(paymentId, new Money("USD", 30m), "Cliente insatisfecho", profile.Id, TestDatabase.StartTime);
        var second = RefundSimulation.Request(paymentId, new Money("USD", 15.50m), "Ajuste", profile.Id, TestDatabase.StartTime.AddMinutes(1));
        await db.WithUnitOfWork(async uow =>
        {
            await uow.Refunds.AddAsync(first, default);
            await uow.Refunds.AddAsync(second, default);
            return true;
        });

        Assert.Equal(45.50m, await db.WithUnitOfWork(uow => uow.Refunds.GetRefundedAmountAsync(paymentId, default)));
        Assert.True(await db.WithUnitOfWork(uow => uow.Refunds.CompleteAsync(second.Id, RefundStatus.FAILED, TestDatabase.StartTime.AddMinutes(2), default)));
        Assert.False(await db.WithUnitOfWork(uow => uow.Refunds.CompleteAsync(second.Id, RefundStatus.SETTLED, TestDatabase.StartTime.AddMinutes(3), default)));
        Assert.Equal(30m, await db.WithUnitOfWork(uow => uow.Refunds.GetRefundedAmountAsync(paymentId, default)));
        Assert.Equal([RefundStatus.PENDING, RefundStatus.FAILED], (await db.WithUnitOfWork(uow => uow.Refunds.GetByPaymentAsync(paymentId, default))).Select(r => r.Status));
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Reportes_administrativos_agregan_sin_datos_personales(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        await SeedAsync(db);

        var summary = await db.WithUnitOfWork(uow => uow.AdminReports.GetSummaryAsync(Period, default));
        Assert.Equal([("PAID", 1), ("PENDING_PAYMENT", 1)], summary.OrdersByStatus.Select(s => (s.Status, s.Count)));
        Assert.Equal([("CONFIRMED", 1), ("PENDING", 1)], summary.ReservationsByStatus.Select(s => (s.Status, s.Count)));
        Assert.Equal(90m, Assert.Single(summary.SettledRevenue).Amount);

        var orders = await db.WithUnitOfWork(uow => uow.AdminReports.GetOrdersAsync(Period, new PaginationRequest(1, 0), default));
        var payments = await db.WithUnitOfWork(uow => uow.AdminReports.GetPaymentsAsync(Period, new PaginationRequest(10, 0), default));
        var reservations = await db.WithUnitOfWork(uow => uow.AdminReports.GetReservationsAsync(Period, new PaginationRequest(10, 0), default));
        Assert.Equal((2, 1, 2), (orders.TotalItems, orders.Items.Count, orders.TotalPages));
        Assert.Equal(1, Assert.Single(payments.Items).AttemptCount);
        Assert.Equal(2, reservations.TotalItems);

        var outside = await db.WithUnitOfWork(uow => uow.AdminReports.GetOrdersAsync(
            new ReportPeriod(Period.To, Period.To.AddDays(1)), new PaginationRequest(10, 0), default));
        Assert.Equal(0, outside.TotalItems);
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Auditoria_e_inventario_paginados(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var (attractionId, _, _, _, _) = await SeedAsync(db);
        await db.WithUnitOfWork(async uow =>
        {
            for (var i = 0; i < 3; i++)
            {
                await uow.AuditEvents.AddAsync(new AuditEvent(Guid.NewGuid(), null, "admin", $"accion-{i}", "attraction", attractionId.ToString(), null,
                    TestDatabase.StartTime.AddMinutes(i)), default);
            }

            return true;
        });

        var audit = await db.WithUnitOfWork(uow => uow.AuditEvents.GetByResourceAsync("attraction", attractionId.ToString(), new PaginationRequest(2, 0), default));
        Assert.Equal((3, 2), (audit.TotalItems, audit.TotalPages));
        Assert.Equal("accion-2", audit.Items[0].Action);

        var movements = await db.WithUnitOfWork(uow => uow.Inventory.GetByAttractionAsync(attractionId, new PaginationRequest(10, 0), default));
        Assert.Equal(2, movements.TotalItems);
    }

    [Theory, MemberData(nameof(Providers))]
    public async Task Fabrica_crea_unidades_de_trabajo_independientes(DatabaseProvider provider)
    {
        await using var db = await TestDatabase.CreateAsync(provider);
        var user = User.Register("https://issuer.test", "background-job", "job@ejemplo.com", TestDatabase.StartTime);

        await using (var scope = db.Scope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
            await using var first = factory.Create();
            await using var second = factory.Create();
            Assert.NotSame(first, second);

            await first.BeginTransactionAsync();
            await first.Users.AddAsync(user, default);
            await first.CommitAsync();

            Assert.NotNull(await second.Users.GetByIdAsync(user.Id, default));
        }

        Assert.NotNull(await db.WithUnitOfWork(uow => uow.Users.GetByIdAsync(user.Id, default)));
    }
}
