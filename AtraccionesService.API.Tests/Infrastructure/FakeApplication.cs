using System.Collections.Concurrent;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Attractions;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Application.Commands.Reservations;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Queries.Attractions;
using AtraccionesService.Application.Queries.Ecommerce;
using AtraccionesService.Application.Queries.Reservations;
using AtraccionesService.Application.ResultModels;

namespace AtraccionesService.API.Tests.Infrastructure;

/// <summary>
/// Doble en memoria de los casos de uso de Application. Solo modela lo necesario para observar el comportamiento HTTP
/// (ownership, idempotencia y errores); no reemplaza las pruebas de la capa Business.
/// </summary>
public sealed class FakeApplication :
    IAttractionService,
    IAvailabilityService,
    IReservationService,
    IUserProfileService,
    ICustomerService,
    IPurchaseService,
    IOrderService,
    IPaymentSimulationService,
    ILocalPermissionService
{
    public const int AvailableSpots = 10;

    public static readonly Guid ExistingAttractionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly ConcurrentDictionary<string, (string Hash, object Result)> _idempotency = new();
    private readonly ConcurrentDictionary<(string, string), RegisteredUserResult> _users = new();
    private readonly ConcurrentDictionary<Guid, (string Owner, OrderResult Order)> _orders = new();

    public ConcurrentBag<string> CatalogWriters { get; } = [];

    public ConcurrentQueue<object> Commands { get; } = new();

    public int ReservationsCreated;

    public Guid AddOrder(string ownerSubject, decimal total = 90m)
    {
        var order = new OrderResult(
            Guid.NewGuid(), Guid.NewGuid(), null, null, "PENDING_PAYMENT", "USD", total, Now, Now,
            [new OrderItemResult(Guid.NewGuid(), ExistingAttractionId, new DateOnly(2026, 12, 15), "09:30", 2, new Money("USD", total / 2), "PENDING")],
            null);
        _orders[order.Id] = (ownerSubject, order);
        return order.Id;
    }

    // ---- Autorización local ----

    public Task<bool> HasPermissionAsync(AuthenticatedUser user, string permission, CancellationToken cancellationToken) =>
        Task.FromResult(permission == LocalPermissions.CatalogWrite && CatalogWriters.Contains(user.Subject));

    // ---- Catálogo ----

    private static AttractionResult Attraction(Guid id) => new(
        id,
        new AttractionData(
            "Teleférico de Quito", "Recorrido en teleférico", "PT2H", new Money("USD", 45m),
            ["Aventura"], [], [new LocationData("Av. Occidental", "Quito", "EC", -0.19, -78.52, "MEETING_POINT")],
            [], new OperatorData(123, "Quito Tour Bus"), "SINGLE_TICKET", [], ["es"], true),
        new RatingResult(10, 4.5),
        null);

    public Task<SearchAttractionsResult> SearchAsync(SearchAttractionsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new SearchAttractionsResult([Attraction(ExistingAttractionId)], 1, null));

    public Task<IReadOnlyList<AttractionResult>> GetDetailsAsync(GetAttractionDetailsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AttractionResult>>(query.AttractionIds.Select(Attraction).ToList());

    public Task<PaginationResult<AttractionResult>> ListAsync(ListAttractionsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new PaginationResult<AttractionResult>([Attraction(ExistingAttractionId)], 1, query.Limit, query.Offset));

    public Task<AttractionResult> GetAsync(GetAttractionQuery query, CancellationToken cancellationToken) =>
        query.AttractionId == ExistingAttractionId
            ? Task.FromResult(Attraction(query.AttractionId))
            : throw new NotFoundException("La atracción no existe.");

    public Task<AttractionResult> CreateAsync(CreateAttractionCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        return Task.FromResult(Attraction(Guid.NewGuid()) with { Data = command.Data });
    }

    public Task ReplaceAsync(ReplaceAttractionCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        return Task.CompletedTask;
    }

    public Task<AttractionResult> PatchAsync(PatchAttractionCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        return Task.FromResult(Attraction(command.AttractionId));
    }

    public Task DeleteAsync(DeleteAttractionCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        return Task.CompletedTask;
    }

    public Task<AvailabilityResult> GetAsync(GetAttractionAvailabilityQuery query, CancellationToken cancellationToken) =>
        query.AttractionId == ExistingAttractionId
            ? Task.FromResult(new AvailabilityResult(query.Date, "America/Guayaquil", AvailableSpots,
                [new AvailabilitySlotResult("09:30", AvailableSpots), new AvailabilitySlotResult("14:00", 0)]))
            : throw new NotFoundException("La atracción no existe.");

    // ---- Reservas ----

    public Task<ReservationResult> CreateAsync(CreateReservationCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        var hash = $"{command.AttractionId}|{command.Date}|{command.Time}|{command.TicketCount}";
        var result = Idempotent(command.User, "create-reservation", command.IdempotencyKey, hash, () =>
        {
            Interlocked.Increment(ref ReservationsCreated);
            return new ReservationResult(Guid.NewGuid(), command.AttractionId, "CONFIRMED", command.Date, command.Time,
                command.TicketCount, new Money("USD", 45m * command.TicketCount));
        });
        return Task.FromResult(result);
    }

    public Task<ReservationResult> CancelAsync(CancelReservationCommand command, CancellationToken cancellationToken) =>
        throw new NotFoundException("La reserva no existe.");

    public Task<PaginationResult<ReservationResult>> ListAsync(GetReservationsQuery query, CancellationToken cancellationToken)
    {
        Commands.Enqueue(query);
        return Task.FromResult(new PaginationResult<ReservationResult>([], 0, query.Limit, query.Offset));
    }

    public Task<ReservationResult> GetAsync(GetReservationQuery query, CancellationToken cancellationToken) =>
        throw new NotFoundException("La reserva no existe.");

    // ---- Identidad y clientes ----

    public Task<RegisteredUserResult> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        var key = (command.User.Issuer, command.User.Subject);
        if (_users.TryGetValue(key, out var existing))
        {
            return Task.FromResult(existing with { Created = false });
        }

        var created = new RegisteredUserResult(Guid.NewGuid(), command.Email, "ACTIVE", Guid.NewGuid(), Now, true);
        _users[key] = created;
        return Task.FromResult(created);
    }

    public Task<UserResult> GetCurrentAsync(AuthenticatedUser user, CancellationToken cancellationToken) =>
        _users.TryGetValue((user.Issuer, user.Subject), out var registered)
            ? Task.FromResult(new UserResult(registered.Id, registered.Email, registered.Status, Now, Now))
            : throw new NotFoundException("El perfil local no existe.");

    Task<CustomerResult> ICustomerService.GetCurrentAsync(AuthenticatedUser user, CancellationToken cancellationToken) =>
        _users.TryGetValue((user.Issuer, user.Subject), out var registered)
            ? Task.FromResult(new CustomerResult(registered.CustomerId, registered.Id, null, null, null, null, null, Now, Now))
            : throw new NotFoundException("El cliente no existe.");

    public Task<CustomerResult> UpdateCurrentAsync(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        var b = command.Billing;
        return Task.FromResult(new CustomerResult(Guid.NewGuid(), Guid.NewGuid(), b.BillingName, b.BillingEmail, b.BillingAddress,
            b.TaxId, b.PaymentMethodReference, Now, Now));
    }

    // ---- Compra directa, pedidos y pagos ----

    public Task<PurchaseResult> CreateAsync(CreatePurchaseCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        if (command.Quantity > AvailableSpots)
        {
            throw new ConflictException(ConflictException.InsufficientAvailability, "No hay cupos suficientes para la franja solicitada.");
        }

        var unit = new Money("USD", 45m);
        var paid = command.PaymentMethod is not null;
        var payment = paid ? new PaymentSummaryResult(Guid.NewGuid(), command.PaymentMethod!, "SETTLED", "SIM-0001") : null;
        return Task.FromResult(new PurchaseResult(Guid.NewGuid(), Guid.NewGuid(), command.AttractionId, command.Date, command.Time,
            command.Quantity, unit, unit.Total * command.Quantity, "USD", paid ? "PAID" : "PENDING_PAYMENT", null, payment,
            paid ? null : Now.AddMinutes(15), Now));
    }

    public Task<OrderResult> CreateAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        var id = AddOrder(command.User.Subject, 45m * command.Quantity);
        return Task.FromResult(_orders[id].Order);
    }

    public Task<OrderResult> GetAsync(GetOrderQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(OwnedOrder(query.OrderId, query.User));

    public Task<OrderEventsResult> GetEventsAsync(GetOrderEventsQuery query, CancellationToken cancellationToken)
    {
        var order = OwnedOrder(query.OrderId, query.User);
        return Task.FromResult(new OrderEventsResult(order.Id, [new OrderEventResult("ORDER_CREATED", null, "PENDING_PAYMENT", Now)]));
    }

    public Task<OrderResult> CancelAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        var order = OwnedOrder(command.OrderId, command.User);
        if (order.Status != "PENDING_PAYMENT")
        {
            throw new ConflictException(ConflictException.InvalidStateTransition, "El pedido no puede cancelarse.");
        }

        var cancelled = order with { Status = "CANCELLED" };
        _orders[order.Id] = (command.User.Subject, cancelled);
        return Task.FromResult(cancelled);
    }

    public Task<PaymentSimulationResult> SimulateAsync(SimulatePaymentCommand command, CancellationToken cancellationToken)
    {
        Commands.Enqueue(command);
        var order = OwnedOrder(command.OrderId, command.User);
        if (order.TotalAmount != command.Amount || order.Currency != command.Currency)
        {
            throw new PaymentSimulationException("AMOUNT_MISMATCH", "El importe o la moneda no coinciden con el pedido.");
        }

        return Task.FromResult(new PaymentSimulationResult(Guid.NewGuid(), order.Id, command.PaymentMethod, "PENDING",
            command.Amount, command.Currency, "SIM-0002", Now));
    }

    private OrderResult OwnedOrder(Guid orderId, AuthenticatedUser user) =>
        _orders.TryGetValue(orderId, out var entry) && entry.Owner == user.Subject
            ? entry.Order
            : throw new NotFoundException("El pedido no existe.");

    private T Idempotent<T>(AuthenticatedUser user, string operation, Guid key, string hash, Func<T> execute)
        where T : notnull
    {
        var identity = $"{user.Issuer}|{user.Subject}|{operation}|{key}";
        if (_idempotency.TryGetValue(identity, out var previous))
        {
            return previous.Hash == hash
                ? (T)previous.Result
                : throw new ConflictException(ConflictException.IdempotencyKeyReused, "La Idempotency-Key ya se usó con otro payload.");
        }

        var result = execute();
        _idempotency[identity] = (hash, result);
        return result;
    }
}
