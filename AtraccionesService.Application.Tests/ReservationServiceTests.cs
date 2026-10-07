using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Reservations;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Queries.Reservations;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Tests.Infrastructure;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Reservations;
using static AtraccionesService.Application.Tests.Infrastructure.ApplicationTestHost;

namespace AtraccionesService.Application.Tests;

public sealed class ReservationServiceTests : IDisposable
{
    private readonly ApplicationTestHost _host = new();

    private Task<ReservationResult> Create(Abstractions.Authorization.AuthenticatedUser user, Guid attractionId, int tickets = 2,
        Guid? key = null, DateOnly? date = null, string time = "09:30") =>
        _host.Run<IReservationService, ReservationResult>(s => s.CreateAsync(
            new CreateReservationCommand(attractionId, date ?? ServiceDate, time, tickets, "Ana Pérez", "ana@ejemplo.com", key ?? Guid.NewGuid(), user), default));

    private Task<ReservationResult> Cancel(Abstractions.Authorization.AuthenticatedUser user, Guid reservationId, Guid? key = null) =>
        _host.Run<IReservationService, ReservationResult>(s => s.CancelAsync(
            new CancelReservationCommand(reservationId, "Cambio de planes", key ?? Guid.NewGuid(), user), default));

    [Fact]
    public async Task Crear_reserva_toma_cupos_calcula_el_total_y_queda_confirmada()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction(price: 45m, capacity: 10);

        var reservation = await Create(user, id, tickets: 3);

        Assert.Equal("CONFIRMED", reservation.Status);
        Assert.Equal(new Money("USD", 135m), reservation.TotalPrice);
        Assert.Equal(3, _host.Slot(id).ReservedQuantity);
        var movement = Assert.Single(_host.Database.InventoryMovements);
        Assert.Equal(InventoryMovementType.CONFIRMED, movement.MovementType);
    }

    [Fact]
    public async Task Cupos_insuficientes_producen_conflicto_sin_cambios()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction(capacity: 2);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => Create(user, id, tickets: 3));

        Assert.Equal(ConflictException.InsufficientAvailability, conflict.Code);
        Assert.Empty(_host.Database.Reservations);
        Assert.Equal(0, _host.Slot(id).ReservedQuantity);
        Assert.Empty(_host.Database.Idempotency);
    }

    [Fact]
    public async Task Franja_inexistente_produce_conflicto_y_atraccion_inexistente_404()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction();

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => Create(user, id, time: "18:00"));
        Assert.Equal(ConflictException.SlotUnavailable, conflict.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => Create(user, Guid.NewGuid()));
    }

    [Theory]
    [InlineData("2026-10-05", "09:30")]
    [InlineData("2026-10-06", "06:59")]
    [InlineData("2026-12-15", "9:30")]
    public async Task Fecha_pasada_u_hora_invalida_producen_error_de_validacion(string date, string time)
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction();

        await Assert.ThrowsAsync<ValidationException>(() => Create(user, id, date: DateOnly.Parse(date), time: time));
    }

    [Fact]
    public async Task Usuario_sin_perfil_aprovisionado_recibe_403()
    {
        var id = _host.SeedAttraction();

        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() => Create(User("sin-perfil"), id));

        Assert.Equal(ForbiddenException.ProfileNotRegistered, forbidden.Code);
    }

    [Fact]
    public async Task Cancelar_libera_cupos_y_no_permite_cancelar_dos_veces()
    {
        var user = await _host.RegisterAsync();
        var id = _host.SeedAttraction();
        var reservation = await Create(user, id, tickets: 4);

        var cancelled = await Cancel(user, reservation.ReservationId);

        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal(0, _host.Slot(id).ReservedQuantity);
        Assert.Contains(_host.Database.InventoryMovements, m => m.MovementType == InventoryMovementType.RELEASED && m.Quantity == -4);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => Cancel(user, reservation.ReservationId));
        Assert.Equal(ConflictException.InvalidStateTransition, conflict.Code);
    }

    [Fact]
    public async Task Un_usuario_no_puede_ver_ni_cancelar_reservas_ajenas()
    {
        var owner = await _host.RegisterAsync("owner");
        var other = await _host.RegisterAsync("other");
        var id = _host.SeedAttraction();
        var reservation = await Create(owner, id);

        await Assert.ThrowsAsync<NotFoundException>(() => Cancel(other, reservation.ReservationId));
        await Assert.ThrowsAsync<NotFoundException>(() => _host.Run<IReservationService, ReservationResult>(s =>
            s.GetAsync(new GetReservationQuery(reservation.ReservationId, other), default)));
        Assert.Equal(ReservationStatus.CONFIRMED, _host.Database.Reservations[reservation.ReservationId].Status);
    }

    [Fact]
    public async Task Historial_filtra_por_propietario_estado_y_pagina()
    {
        var owner = await _host.RegisterAsync("owner");
        var other = await _host.RegisterAsync("other");
        var id = _host.SeedAttraction(capacity: 50);
        for (var i = 0; i < 3; i++)
        {
            await Create(owner, id, tickets: 1);
        }

        var toCancel = await Create(owner, id, tickets: 1);
        await Cancel(owner, toCancel.ReservationId);
        await Create(other, id, tickets: 1);

        var confirmed = await _host.Run<IReservationService, PaginationResult<ReservationResult>>(s =>
            s.ListAsync(new GetReservationsQuery(owner, 2, 0, "CONFIRMED", null, null, true), default));
        var all = await _host.Run<IReservationService, PaginationResult<ReservationResult>>(s =>
            s.ListAsync(new GetReservationsQuery(owner, 10, 0, null, null, null, true), default));

        Assert.Equal(3, confirmed.TotalItems);
        Assert.Equal(2, confirmed.Items.Count);
        Assert.Equal(2, confirmed.TotalPages);
        Assert.Equal(4, all.TotalItems);
        await Assert.ThrowsAsync<ValidationException>(() => _host.Run<IReservationService, PaginationResult<ReservationResult>>(s =>
            s.ListAsync(new GetReservationsQuery(owner, 10, 0, "DONE", null, null, true), default)));
    }

    public void Dispose() => _host.Dispose();
}
