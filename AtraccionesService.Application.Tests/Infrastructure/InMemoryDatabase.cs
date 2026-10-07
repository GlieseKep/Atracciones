using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Identity;
using AtraccionesService.Domain.Reservations;

namespace AtraccionesService.Application.Tests.Infrastructure;

/// <summary>
/// Almacenamiento en memoria equivalente a la base de datos para pruebas de Application.
/// Guarda copias independientes de las entidades: los cambios solo se persisten mediante los repositorios,
/// y una transacción revertida restaura el estado anterior.
/// </summary>
public sealed class InMemoryDatabase
{
    public Dictionary<Guid, Attraction> Attractions { get; private set; } = [];
    public Dictionary<Guid, AvailabilitySlot> Slots { get; private set; } = [];
    public Dictionary<Guid, Reservation> Reservations { get; private set; } = [];
    public Dictionary<Guid, User> Users { get; private set; } = [];
    public Dictionary<Guid, Customer> Customers { get; private set; } = [];
    public Dictionary<Guid, HashSet<string>> Permissions { get; private set; } = [];
    public Dictionary<Guid, Purchase> Purchases { get; private set; } = [];
    public Dictionary<Guid, Order> Orders { get; private set; } = [];
    public Dictionary<Guid, PaymentSimulation> Payments { get; private set; } = [];
    public List<PaymentAttempt> PaymentAttempts { get; private set; } = [];
    public List<PaymentEvent> PaymentEvents { get; private set; } = [];
    public List<OrderEvent> OrderEvents { get; private set; } = [];
    public List<InventoryMovement> InventoryMovements { get; private set; } = [];
    public List<AuditEvent> AuditEvents { get; private set; } = [];
    public Dictionary<(string, string, string, Guid), IdempotencyRecord> Idempotency { get; private set; } = [];

    public int Commits { get; set; }

    public int Rollbacks { get; set; }

    public object Snapshot() => new State(
        new(Attractions), new(Slots), new(Reservations), new(Users), new(Customers),
        Permissions.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value)),
        new(Purchases), new(Orders), new(Payments), [.. PaymentAttempts], [.. PaymentEvents], [.. OrderEvents],
        [.. InventoryMovements], [.. AuditEvents], new(Idempotency));

    public void Restore(object snapshot)
    {
        var s = (State)snapshot;
        (Attractions, Slots, Reservations, Users, Customers, Permissions, Purchases, Orders, Payments) =
            (s.Attractions, s.Slots, s.Reservations, s.Users, s.Customers, s.Permissions, s.Purchases, s.Orders, s.Payments);
        (PaymentAttempts, PaymentEvents, OrderEvents, InventoryMovements, AuditEvents, Idempotency) =
            (s.PaymentAttempts, s.PaymentEvents, s.OrderEvents, s.InventoryMovements, s.AuditEvents, s.Idempotency);
    }

    // ---- Copias independientes (equivalen a leer/escribir filas) ----

    public static Attraction Copy(Attraction a) => Attraction.Restore(a.Id, a.Details, a.Rating, a.Urls);

    public static AvailabilitySlot Copy(AvailabilitySlot s) => new(s.Id, s.AttractionId, s.Date, s.Time, s.Capacity, s.ReservedQuantity, s.Version);

    public static Reservation Copy(Reservation r) => Reservation.Restore(
        r.Id, r.AttractionId, r.CustomerId, r.Date, r.Time, r.TicketCount, r.TotalPrice, r.CustomerName, r.CustomerEmail,
        r.Status, r.CancellationReason, r.CreatedAt);

    public static User Copy(User u) => User.Restore(u.Id, u.OAuthIssuer, u.OAuthSubject, u.Email, u.Status, u.CreatedAt, u.UpdatedAt);

    public static Customer Copy(Customer c) => Customer.Restore(
        c.Id, c.UserId, c.BillingName, c.BillingEmail, c.BillingAddress, c.TaxId, c.PaymentMethodReference, c.CreatedAt, c.UpdatedAt);

    public static Order Copy(Order o) => Order.Restore(
        o.Id, o.CustomerId, o.PurchaseId, o.ReservationId, o.Status, o.Total, o.Items, o.HoldExpiresAt, o.CancellationReason, o.CreatedAt, o.UpdatedAt);

    public static PaymentSimulation Copy(PaymentSimulation p) => PaymentSimulation.Restore(
        p.Id, p.OrderId, p.Method, p.Status, p.Amount, p.GatewayReference, p.CreatedAt, p.ProcessedAt, p.FailureReason);

    private sealed record State(
        Dictionary<Guid, Attraction> Attractions,
        Dictionary<Guid, AvailabilitySlot> Slots,
        Dictionary<Guid, Reservation> Reservations,
        Dictionary<Guid, User> Users,
        Dictionary<Guid, Customer> Customers,
        Dictionary<Guid, HashSet<string>> Permissions,
        Dictionary<Guid, Purchase> Purchases,
        Dictionary<Guid, Order> Orders,
        Dictionary<Guid, PaymentSimulation> Payments,
        List<PaymentAttempt> PaymentAttempts,
        List<PaymentEvent> PaymentEvents,
        List<OrderEvent> OrderEvents,
        List<InventoryMovement> InventoryMovements,
        List<AuditEvent> AuditEvents,
        Dictionary<(string, string, string, Guid), IdempotencyRecord> Idempotency);
}
