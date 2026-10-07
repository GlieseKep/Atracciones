using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Application.Commands.Reservations;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Tests.Infrastructure;
using AtraccionesService.Domain.Ecommerce;
using static AtraccionesService.Application.Tests.Infrastructure.ApplicationTestHost;

namespace AtraccionesService.Application.Tests;

public sealed class IdempotencyAndCompositionTests : IDisposable
{
    private readonly ApplicationTestHost _host = new();

    private Task<ReservationResult> Reserve(Abstractions.Authorization.AuthenticatedUser user, Guid attractionId, Guid key, int tickets = 2) =>
        _host.Run<IReservationService, ReservationResult>(s => s.CreateAsync(
            new CreateReservationCommand(attractionId, ServiceDate, "09:30", tickets, "Ana", "ana@ejemplo.com", key, user), default));

    [Fact]
    public async Task Misma_clave_y_payload_devuelve_el_resultado_original_sin_repetir_efectos()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction(capacity: 10);
        var key = Guid.NewGuid();

        var first = await Reserve(user, id, key);
        var replay = await Reserve(user, id, key);

        Assert.Equal(first, replay);
        Assert.Single(_host.Database.Reservations);
        Assert.Equal(2, _host.Slot(id).ReservedQuantity);
        Assert.Equal(IdempotencyStatus.COMPLETED, Assert.Single(_host.Database.Idempotency.Values).Status);
    }

    [Fact]
    public async Task Misma_clave_con_otro_payload_produce_conflicto()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction();
        var key = Guid.NewGuid();
        await Reserve(user, id, key, tickets: 2);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => Reserve(user, id, key, tickets: 3));

        Assert.Equal(ConflictException.IdempotencyKeyReused, conflict.Code);
        Assert.Single(_host.Database.Reservations);
    }

    [Fact]
    public async Task La_clave_se_aisla_por_usuario_y_por_operacion()
    {
        var a = await _host.RegisterAsync("a");
        var b = await _host.RegisterAsync("b");
        var id = _host.SeedAttraction(capacity: 10);
        var key = Guid.NewGuid();

        await Reserve(a, id, key);
        await Reserve(b, id, key);
        var purchase = await _host.Run<IPurchaseService, PurchaseResult>(s =>
            s.CreateAsync(new CreatePurchaseCommand(id, ServiceDate, "09:30", 1, "CARD", key, a), default));

        Assert.Equal(3, _host.Database.Reservations.Count);
        Assert.Equal("PAID", purchase.Status);
    }

    [Fact]
    public async Task Solicitud_aun_en_proceso_produce_conflicto_in_progress()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction();
        var key = Guid.NewGuid();
        _host.Database.Idempotency[(Issuer, user.Subject, "create-reservation", key)] = new IdempotencyRecord(
            Issuer, user.Subject, "create-reservation", key,
            Services.Shared.IdempotencyService.Hash(new { AttractionId = id, Date = ServiceDate, Time = "09:30", TicketCount = 2, CustomerName = "Ana", CustomerEmail = "ana@ejemplo.com" }),
            IdempotencyStatus.IN_PROGRESS, null, null, StartTime, StartTime.AddHours(24));

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => Reserve(user, id, key));

        Assert.Equal(ConflictException.IdempotencyInProgress, conflict.Code);
    }

    [Fact]
    public async Task Clave_expirada_puede_reutilizarse_tras_la_limpieza()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction(capacity: 10);
        var key = Guid.NewGuid();
        var first = await Reserve(user, id, key);

        _host.Clock.Advance(TimeSpan.FromHours(25));
        var second = await Reserve(user, id, key);

        Assert.NotEqual(first.ReservationId, second.ReservationId);
        Assert.Equal(4, _host.Slot(id).ReservedQuantity);
    }

    [Fact]
    public async Task Un_fallo_revierte_toda_la_transaccion()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction(capacity: 1);
        var rollbacksBefore = _host.Database.Rollbacks;

        await Assert.ThrowsAsync<ConflictException>(() => Reserve(user, id, Guid.NewGuid(), tickets: 2));

        Assert.Equal(rollbacksBefore + 1, _host.Database.Rollbacks);
        Assert.Empty(_host.Database.Idempotency);
        Assert.Empty(_host.Database.InventoryMovements);
    }

    [Fact]
    public void AddApplication_resuelve_todos_los_casos_de_uso()
    {
        Assert.NotNull(_host.Resolve<IAttractionService>());
        Assert.NotNull(_host.Resolve<IAvailabilityService>());
        Assert.NotNull(_host.Resolve<IReservationService>());
        Assert.NotNull(_host.Resolve<IUserProfileService>());
        Assert.NotNull(_host.Resolve<ICustomerService>());
        Assert.NotNull(_host.Resolve<IPurchaseService>());
        Assert.NotNull(_host.Resolve<IOrderService>());
        Assert.NotNull(_host.Resolve<IPaymentSimulationService>());
        Assert.NotNull(_host.Resolve<Abstractions.Authorization.ILocalPermissionService>());
    }

    [Fact]
    public void Zona_horaria_invalida_se_detecta_al_validar_la_configuracion()
    {
        using var host = new ApplicationTestHost(new Dictionary<string, string?> { ["Application:TimeZone"] = "Marte/Olympus" });

        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            host.Resolve<Microsoft.Extensions.Options.IOptions<DependencyInjection.ApplicationOptions>>().Value);
    }

    public void Dispose() => _host.Dispose();
}
