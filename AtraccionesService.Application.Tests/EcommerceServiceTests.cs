using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Application.Commands.Reservations;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Queries.Ecommerce;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Tests.Infrastructure;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Reservations;
using static AtraccionesService.Application.Tests.Infrastructure.ApplicationTestHost;

namespace AtraccionesService.Application.Tests;

public sealed class EcommerceServiceTests : IDisposable
{
    private readonly ApplicationTestHost _host = new();

    private Task<PurchaseResult> Purchase(AuthenticatedUser user, Guid attractionId, int quantity = 2, string? method = "CARD", Guid? key = null) =>
        _host.Run<IPurchaseService, PurchaseResult>(s => s.CreateAsync(
            new CreatePurchaseCommand(attractionId, ServiceDate, "09:30", quantity, method, key ?? Guid.NewGuid(), user), default));

    private Task<OrderResult> CreateOrder(AuthenticatedUser user, Guid attractionId, int quantity = 2) =>
        _host.Run<IOrderService, OrderResult>(s => s.CreateAsync(
            new CreateOrderCommand(attractionId, ServiceDate, "09:30", quantity, Guid.NewGuid(), user), default));

    private Task<PaymentSimulationResult> Pay(AuthenticatedUser user, OrderResult order, decimal? amount = null, Guid? key = null) =>
        _host.Run<IPaymentSimulationService, PaymentSimulationResult>(s => s.SimulateAsync(
            new SimulatePaymentCommand(order.Id, "CARD", amount ?? order.TotalAmount, order.Currency, key ?? Guid.NewGuid(), user), default));

    [Fact]
    public async Task Compra_directa_con_pago_liquida_el_pedido_y_confirma_la_reserva_en_una_transaccion()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction(price: 45m, capacity: 10);
        var commitsBefore = _host.Database.Commits;

        var purchase = await Purchase(user, id, quantity: 2);

        Assert.Equal("PAID", purchase.Status);
        Assert.Equal(90m, purchase.TotalAmount);
        Assert.Equal("SETTLED", purchase.Payment!.Status);
        Assert.Null(purchase.HoldExpiresAt);
        Assert.Equal(2, _host.Slot(id).ReservedQuantity);
        Assert.Equal(ReservationStatus.CONFIRMED, _host.Database.Reservations[purchase.ReservationId!.Value].Status);
        Assert.Equal(["ORDER_CREATED", "PAYMENT_SETTLED"], _host.Database.OrderEvents.Select(e => e.EventType));
        Assert.Equal(["PAYMENT_REQUESTED", "PAYMENT_AUTHORIZED", "PAYMENT_SETTLED"], _host.Database.PaymentEvents.Select(e => e.EventType));
        Assert.Single(_host.Database.PaymentAttempts);
        Assert.Equal(commitsBefore + 1, _host.Database.Commits);
    }

    [Fact]
    public async Task Compra_sin_metodo_de_pago_queda_pendiente_con_retencion_temporal()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction();

        var purchase = await Purchase(user, id, method: null);

        Assert.Equal("PENDING_PAYMENT", purchase.Status);
        Assert.Equal(StartTime.AddMinutes(15), purchase.HoldExpiresAt);
        Assert.Null(purchase.Payment);
        Assert.Equal(ReservationStatus.PENDING, _host.Database.Reservations[purchase.ReservationId!.Value].Status);
    }

    [Fact]
    public async Task Compra_con_cupos_insuficientes_no_deja_rastros()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction(capacity: 1);

        await Assert.ThrowsAsync<ConflictException>(() => Purchase(user, id, quantity: 2));

        Assert.Empty(_host.Database.Orders);
        Assert.Empty(_host.Database.Purchases);
        Assert.Empty(_host.Database.Reservations);
        Assert.Empty(_host.Database.Payments);
        Assert.Empty(_host.Database.Idempotency);
        Assert.Equal(0, _host.Slot(id).ReservedQuantity);
    }

    [Fact]
    public async Task Pedido_pendiente_se_paga_mediante_simulacion()
    {
        var user = await _host.RegisterAsync();
        var order = await CreateOrder(user, _host.SeedAttraction(price: 20m));

        var payment = await Pay(user, order);

        Assert.Equal("SETTLED", payment.Status);
        Assert.StartsWith("SIM-", payment.GatewayReference);
        var reloaded = await _host.Run<IOrderService, OrderResult>(s => s.GetAsync(new GetOrderQuery(order.Id, user), default));
        Assert.Equal("PAID", reloaded.Status);
        Assert.Equal("SETTLED", reloaded.PaymentSimulation!.Status);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => Pay(user, order));
        Assert.Equal("ORDER_NOT_PAYABLE", conflict.Code);
    }

    [Fact]
    public async Task Pago_rechazado_deja_el_pedido_pendiente_y_limita_reintentos()
    {
        var user = await _host.RegisterAsync();
        var order = await CreateOrder(user, _host.SeedAttraction(price: 10.51m), quantity: 1);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var rejected = await Pay(user, order);
            Assert.Equal("REJECTED", rejected.Status);
        }

        Assert.Equal(OrderStatus.PENDING_PAYMENT, _host.Database.Orders[order.Id].Status);
        Assert.Equal(3, _host.Database.PaymentAttempts.Count);
        var exceeded = await Assert.ThrowsAsync<ConflictException>(() => Pay(user, order));
        Assert.Equal("PAYMENT_ATTEMPTS_EXCEEDED", exceeded.Code);
    }

    [Fact]
    public async Task Pago_con_importe_distinto_o_retencion_expirada_es_rechazado()
    {
        var user = await _host.RegisterAsync();
        var order = await CreateOrder(user, _host.SeedAttraction(price: 20m));

        var mismatch = await Assert.ThrowsAsync<PaymentSimulationException>(() => Pay(user, order, amount: 1m));
        Assert.Equal("AMOUNT_MISMATCH", mismatch.Code);

        _host.Clock.Advance(TimeSpan.FromMinutes(16));
        var expired = await Assert.ThrowsAsync<ConflictException>(() => Pay(user, order));
        Assert.Equal("ORDER_HOLD_EXPIRED", expired.Code);
        Assert.Empty(_host.Database.Payments);
    }

    [Fact]
    public async Task Cancelar_pedido_pendiente_libera_cupos_y_cancela_la_reserva()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction(capacity: 10);
        var order = await CreateOrder(user, id, quantity: 4);

        var cancelled = await _host.Run<IOrderService, OrderResult>(s =>
            s.CancelAsync(new CancelOrderCommand(order.Id, "Cambio de planes", Guid.NewGuid(), user), default));

        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal(0, _host.Slot(id).ReservedQuantity);
        Assert.Equal(ReservationStatus.CANCELLED, _host.Database.Reservations[order.ReservationId!.Value].Status);
        var events = await _host.Run<IOrderService, OrderEventsResult>(s => s.GetEventsAsync(new GetOrderEventsQuery(order.Id, user), default));
        Assert.Equal(["ORDER_CREATED", "ORDER_CANCELLED"], events.Events.Select(e => e.EventType));
    }

    [Fact]
    public async Task Pedido_pagado_no_se_cancela_y_su_reserva_no_se_cancela_por_separado()
    {
        var user = await _host.RegisterAsync();
        var purchase = await Purchase(user, _host.SeedAttraction());

        var orderConflict = await Assert.ThrowsAsync<ConflictException>(() => _host.Run<IOrderService, OrderResult>(s =>
            s.CancelAsync(new CancelOrderCommand(purchase.OrderId, "x", Guid.NewGuid(), user), default)));
        var reservationConflict = await Assert.ThrowsAsync<ConflictException>(() => _host.Run<IReservationService, ReservationResult>(s =>
            s.CancelAsync(new CancelReservationCommand(purchase.ReservationId!.Value, "x", Guid.NewGuid(), user), default)));

        Assert.Equal(ConflictException.InvalidStateTransition, orderConflict.Code);
        Assert.Equal("RESERVATION_MANAGED_BY_ORDER", reservationConflict.Code);
    }

    [Fact]
    public async Task Un_cliente_no_puede_consultar_pagar_ni_cancelar_pedidos_ajenos()
    {
        var owner = await _host.RegisterAsync("owner");
        var other = await _host.RegisterAsync("other");
        var order = await CreateOrder(owner, _host.SeedAttraction());

        await Assert.ThrowsAsync<NotFoundException>(() => _host.Run<IOrderService, OrderResult>(s => s.GetAsync(new GetOrderQuery(order.Id, other), default)));
        await Assert.ThrowsAsync<NotFoundException>(() => _host.Run<IOrderService, OrderEventsResult>(s => s.GetEventsAsync(new GetOrderEventsQuery(order.Id, other), default)));
        await Assert.ThrowsAsync<NotFoundException>(() => Pay(other, order));
        await Assert.ThrowsAsync<NotFoundException>(() => _host.Run<IOrderService, OrderResult>(s =>
            s.CancelAsync(new CancelOrderCommand(order.Id, "x", Guid.NewGuid(), other), default)));
        Assert.Equal(OrderStatus.PENDING_PAYMENT, _host.Database.Orders[order.Id].Status);
    }

    public void Dispose() => _host.Dispose();
}
