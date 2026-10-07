using AtraccionesService.DataManagment.Administration;
using AtraccionesService.DataManagment.QueryModels;
using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.DataManagment.Contracts.Catalog;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.Contracts.Ecommerce;
using AtraccionesService.DataManagment.Contracts.Events;
using AtraccionesService.DataManagment.Contracts.Identity;
using AtraccionesService.DataManagment.Exceptions;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Identity;
using AtraccionesService.Domain.Reservations;
using static AtraccionesService.Application.Tests.Infrastructure.InMemoryDatabase;

namespace AtraccionesService.Application.Tests.Infrastructure;

/// <summary>Implementación en memoria de los contratos de DataManagement, con transacción por snapshot.</summary>
public sealed class InMemoryUnitOfWork(InMemoryDatabase db) : IUnitOfWork
{
    private object? _snapshot;

    public IAttractionRepository Attractions { get; } = new AttractionRepository(db);
    public IAvailabilityRepository Availability { get; } = new AvailabilityRepository(db);
    public IReservationRepository Reservations { get; } = new ReservationRepository(db);
    public IUserRepository Users { get; } = new UserRepository(db);
    public ICustomerRepository Customers { get; } = new CustomerRepository(db);
    public IUserRoleRepository UserRoles { get; } = new UserRoleRepository(db);
    public IPurchaseRepository Purchases { get; } = new PurchaseRepository(db);
    public IOrderRepository Orders { get; } = new OrderRepository(db);
    public IPaymentSimulationRepository Payments { get; } = new PaymentRepository(db);
    public IIdempotencyRepository Idempotency { get; } = new IdempotencyRepository(db);
    public IInventoryMovementRepository Inventory { get; } = new InventoryRepository(db);
    public IOrderEventRepository OrderEvents { get; } = new EventRepository(db);
    public IPaymentEventRepository PaymentEvents { get; } = new EventRepository(db);
    public IAuditEventRepository AuditEvents { get; } = new EventRepository(db);

    // Contratos de administración: Application todavía no los usa (casos administrativos pendientes de aprobación).
    public IRoleRepository Roles { get; } = new UnusedAdministration();
    public IRefundSimulationRepository Refunds { get; } = new UnusedAdministration();
    public IAdminReportRepository AdminReports { get; } = new UnusedAdministration();

    public bool HasActiveTransaction => _snapshot is not null;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is not null)
        {
            throw new InvalidOperationException("Ya existe una transacción activa.");
        }

        _snapshot = db.Snapshot();
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        _snapshot = null;
        db.Commits++;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is not null)
        {
            db.Restore(_snapshot);
            db.Rollbacks++;
        }

        _snapshot = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static PagedResult<T> Page<T>(IEnumerable<T> source, PaginationRequest page)
    {
        var all = source.ToList();
        return PagedResult<T>.Create(all.Skip(page.Offset).Take(page.Limit).ToList(), all.Count, page);
    }

    private static NotSupportedException Unused() => new("Application no usa este contrato; se prueba en AtraccionesService.DataAcess.Tests.");

    private sealed class UnusedAdministration : IRoleRepository, IRefundSimulationRepository, IAdminReportRepository
    {
        Task<Role?> IRoleRepository.GetByIdAsync(Guid roleId, CancellationToken ct) => throw Unused();
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct) => throw Unused();
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct) => throw Unused();
        public Task AddAsync(Role role, CancellationToken ct) => throw Unused();
        Task<RefundSimulation?> IRefundSimulationRepository.GetByIdAsync(Guid refundId, CancellationToken ct) => throw Unused();
        public Task<IReadOnlyList<RefundSimulation>> GetByPaymentAsync(Guid paymentSimulationId, CancellationToken ct) => throw Unused();
        public Task<decimal> GetRefundedAmountAsync(Guid paymentSimulationId, CancellationToken ct) => throw Unused();
        public Task AddAsync(RefundSimulation refund, CancellationToken ct) => throw Unused();
        public Task<bool> CompleteAsync(Guid refundId, RefundStatus status, DateTimeOffset processedAt, CancellationToken ct) => throw Unused();
        public Task<AdminDashboardSummary> GetSummaryAsync(ReportPeriod period, CancellationToken ct) => throw Unused();
        public Task<PagedResult<OrderQueryResult>> GetOrdersAsync(ReportPeriod period, PaginationRequest page, CancellationToken ct) => throw Unused();
        public Task<PagedResult<PaymentSimulationQueryResult>> GetPaymentsAsync(ReportPeriod period, PaginationRequest page, CancellationToken ct) => throw Unused();
        public Task<PagedResult<ReservationQueryResult>> GetReservationsAsync(ReportPeriod period, PaginationRequest page, CancellationToken ct) => throw Unused();
    }

    private sealed class AttractionRepository(InMemoryDatabase db) : IAttractionRepository
    {
        public Task<Attraction?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(db.Attractions.TryGetValue(id, out var a) ? Copy(a) : null);

        public Task<IReadOnlyList<Attraction>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Attraction>>(ids.Where(db.Attractions.ContainsKey).Select(id => Copy(db.Attractions[id])).ToList());

        public Task<PagedResult<Attraction>> ListAsync(PaginationRequest page, CancellationToken ct) =>
            Task.FromResult(Page(db.Attractions.Values.OrderBy(a => a.Details.Name).Select(Copy), page));

        public Task<PagedResult<AttractionQueryResult>> ListSummariesAsync(PaginationRequest page, CancellationToken ct) =>
            Task.FromResult(Page(db.Attractions.Values.OrderBy(a => a.Details.Name).Select(a => new AttractionQueryResult(
                a.Id, a.Details.Name, a.Details.ProductType.ToString(), a.Details.Price.Currency, a.Details.Price.Amount, a.Rating?.Score, a.Rating?.NumberOfReviews)), page));

        public Task<PagedResult<Attraction>> SearchAsync(AttractionSearchCriteria c, PaginationRequest page, CancellationToken ct)
        {
            var query = db.Attractions.Values.Where(a =>
                (c.Currency is null || a.Details.Price.Currency == c.Currency)
                && (c.Cities.Count == 0 || a.Details.Locations.Any(l => c.Cities.Contains(l.City, StringComparer.OrdinalIgnoreCase)))
                && (c.Countries.Count == 0 || a.Details.Locations.Any(l => c.Countries.Contains(l.Country)))
                && (c.MinimumReviewScore is null || (a.Rating?.Score ?? 0) >= c.MinimumReviewScore)
                && (c.MinimumReviewCount is null || (a.Rating?.NumberOfReviews ?? 0) >= c.MinimumReviewCount)
                && (c.StartDate is null && c.EndDate is null || db.Slots.Values.Any(s =>
                    s.AttractionId == a.Id && s.AvailableSpots > 0
                    && (c.StartDate is null || s.Date >= c.StartDate) && (c.EndDate is null || s.Date <= c.EndDate))));

            query = c.Sort switch
            {
                AttractionSort.PriceAscending => query.OrderBy(a => a.Details.Price.Amount),
                AttractionSort.PriceDescending => query.OrderByDescending(a => a.Details.Price.Amount),
                AttractionSort.RatingDescending => query.OrderByDescending(a => a.Rating?.Score ?? 0),
                _ => query.OrderByDescending(a => a.Rating?.NumberOfReviews ?? 0).ThenBy(a => a.Details.Name),
            };
            return Task.FromResult(Page(query.Select(Copy), page));
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Attractions.ContainsKey(id));

        public Task AddAsync(Attraction attraction, CancellationToken ct)
        {
            db.Attractions.Add(attraction.Id, Copy(attraction));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Attraction attraction, CancellationToken ct)
        {
            db.Attractions[attraction.Id] = Copy(attraction);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct)
        {
            db.Attractions.Remove(id);
            foreach (var slot in db.Slots.Values.Where(s => s.AttractionId == id).ToList())
            {
                db.Slots.Remove(slot.Id);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class AvailabilityRepository(InMemoryDatabase db) : IAvailabilityRepository
    {
        public Task<AvailabilitySlot?> GetSlotByIdAsync(Guid slotId, CancellationToken ct) =>
            Task.FromResult(db.Slots.TryGetValue(slotId, out var s) ? Copy(s) : null);

        public Task<IReadOnlyList<AvailabilitySlot>> GetSlotsInRangeAsync(Guid attractionId, DateOnly from, DateOnly to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AvailabilitySlot>>(db.Slots.Values
                .Where(s => s.AttractionId == attractionId && s.Date >= from && s.Date <= to)
                .OrderBy(s => s.Date).ThenBy(s => s.Time).Select(Copy).ToList());

        public Task<bool> AddWithConcurrencyCheckAsync(AvailabilitySlot slot, CancellationToken ct)
        {
            if (db.Slots.Values.Any(s => s.AttractionId == slot.AttractionId && s.Date == slot.Date && s.Time == slot.Time))
            {
                return Task.FromResult(false);
            }

            db.Slots.Add(slot.Id, Copy(slot));
            return Task.FromResult(true);
        }

        public Task<bool> UpdateIfVersionMatchesAsync(AvailabilitySlot slot, long expectedVersion, CancellationToken ct)
        {
            if (!db.Slots.TryGetValue(slot.Id, out var current) || current.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            db.Slots[slot.Id] = new AvailabilitySlot(slot.Id, slot.AttractionId, slot.Date, slot.Time, slot.Capacity, slot.ReservedQuantity, expectedVersion + 1);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<AvailabilitySlot>> GetSlotsAsync(Guid attractionId, DateOnly date, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AvailabilitySlot>>(db.Slots.Values.Where(s => s.AttractionId == attractionId && s.Date == date).Select(Copy).ToList());

        public Task<AvailabilitySlot?> GetSlotAsync(Guid attractionId, DateOnly date, TimeOnly time, CancellationToken ct) =>
            Task.FromResult(db.Slots.Values.Where(s => s.AttractionId == attractionId && s.Date == date && s.Time == time).Select(Copy).FirstOrDefault());

        public Task AddAsync(AvailabilitySlot slot, CancellationToken ct)
        {
            db.Slots.Add(slot.Id, Copy(slot));
            return Task.CompletedTask;
        }

        public Task<bool> TryReserveQuantityAsync(Guid slotId, int quantity, CancellationToken ct)
        {
            var slot = Copy(db.Slots[slotId]);
            if (!slot.CanReserve(quantity))
            {
                return Task.FromResult(false);
            }

            slot.Reserve(quantity);
            db.Slots[slotId] = slot;
            return Task.FromResult(true);
        }

        public Task ReleaseQuantityAsync(Guid slotId, int quantity, CancellationToken ct)
        {
            var slot = Copy(db.Slots[slotId]);
            slot.Release(quantity);
            db.Slots[slotId] = slot;
            return Task.CompletedTask;
        }
    }

    private sealed class ReservationRepository(InMemoryDatabase db) : IReservationRepository
    {
        public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(db.Reservations.TryGetValue(id, out var r) ? Copy(r) : null);

        public Task<Reservation?> GetByIdForCustomerAsync(Guid id, Guid customerId, CancellationToken ct) =>
            Task.FromResult(db.Reservations.TryGetValue(id, out var r) && r.CustomerId == customerId ? Copy(r) : null);

        public Task<PagedResult<Reservation>> GetByCustomerAsync(Guid customerId, ReservationFilter f, PaginationRequest page, CancellationToken ct)
        {
            var query = db.Reservations.Values.Where(r => r.CustomerId == customerId
                && (f.Status is null || r.Status == f.Status)
                && (f.FromDate is null || r.Date >= f.FromDate)
                && (f.ToDate is null || r.Date <= f.ToDate));
            query = f.SortDescending ? query.OrderByDescending(r => r.Date).ThenByDescending(r => r.Time) : query.OrderBy(r => r.Date).ThenBy(r => r.Time);
            return Task.FromResult(Page(query.Select(Copy), page));
        }

        public Task<bool> HasActiveByAttractionAsync(Guid attractionId, DateOnly fromDate, CancellationToken ct) =>
            Task.FromResult(db.Reservations.Values.Any(r => r.AttractionId == attractionId && r.Date >= fromDate && r.Status != ReservationStatus.CANCELLED));

        public Task<bool> ExistsForCustomerAsync(Guid reservationId, Guid customerId, CancellationToken ct) =>
            Task.FromResult(db.Reservations.TryGetValue(reservationId, out var r) && r.CustomerId == customerId);

        public Task<PagedResult<ReservationQueryResult>> GetByAttractionAsync(Guid attractionId, ReservationFilter filter, PaginationRequest page, CancellationToken ct) =>
            throw Unused();

        public Task<bool> UpdateStatusAsync(Guid reservationId, ReservationStatus expected, ReservationStatus next, string? reason, CancellationToken ct) =>
            throw Unused();

        public Task AddAsync(Reservation reservation, CancellationToken ct)
        {
            db.Reservations.Add(reservation.Id, Copy(reservation));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Reservation reservation, CancellationToken ct)
        {
            db.Reservations[reservation.Id] = Copy(reservation);
            return Task.CompletedTask;
        }
    }

    private sealed class UserRepository(InMemoryDatabase db) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Users.TryGetValue(id, out var u) ? Copy(u) : null);

        public Task<User?> GetByIdentityAsync(string issuer, string subject, CancellationToken ct) =>
            Task.FromResult(db.Users.Values.Where(u => u.OAuthIssuer == issuer && u.OAuthSubject == subject).Select(Copy).FirstOrDefault());

        public Task<IReadOnlyList<User>> GetByEmailAsync(string email, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<User>>(db.Users.Values.Where(u => u.Email == email).Select(Copy).ToList());

        public Task AddAsync(User user, CancellationToken ct)
        {
            if (db.Users.Values.Any(u => u.OAuthIssuer == user.OAuthIssuer && u.OAuthSubject == user.OAuthSubject))
            {
                throw new ConcurrencyException("Violación de unicidad issuer + subject.");
            }

            db.Users.Add(user.Id, Copy(user));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken ct)
        {
            db.Users[user.Id] = Copy(user);
            return Task.CompletedTask;
        }
    }

    private sealed class CustomerRepository(InMemoryDatabase db) : ICustomerRepository
    {
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Customers.TryGetValue(id, out var c) ? Copy(c) : null);

        public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(db.Customers.Values.Where(c => c.UserId == userId).Select(Copy).FirstOrDefault());

        public Task<bool> BelongsToUserAsync(Guid customerId, Guid userId, CancellationToken ct) =>
            Task.FromResult(db.Customers.TryGetValue(customerId, out var c) && c.UserId == userId);

        public Task AddAsync(Customer customer, CancellationToken ct)
        {
            db.Customers.Add(customer.Id, Copy(customer));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Customer customer, CancellationToken ct)
        {
            db.Customers[customer.Id] = Copy(customer);
            return Task.CompletedTask;
        }
    }

    private sealed class UserRoleRepository(InMemoryDatabase db) : IUserRoleRepository
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(db.Permissions.TryGetValue(userId, out var p) ? new HashSet<string>(p) : new HashSet<string>());

        public Task<IReadOnlyList<UserRoleAssignment>> GetActiveAssignmentsAsync(Guid userId, CancellationToken ct) => throw Unused();

        public Task AssignAsync(UserRoleAssignment assignment, CancellationToken ct) => throw Unused();

        public Task<bool> RevokeAsync(Guid userId, Guid roleId, DateTimeOffset revokedAt, CancellationToken ct) => throw Unused();
    }

    private sealed class PurchaseRepository(InMemoryDatabase db) : IPurchaseRepository
    {
        public Task<Purchase?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Purchases.GetValueOrDefault(id));

        public Task<PagedResult<PurchaseQueryResult>> GetByCustomerAsync(PurchaseSpecification specification, PaginationRequest page, CancellationToken ct) =>
            throw Unused();

        public Task AddAsync(Purchase purchase, CancellationToken ct)
        {
            db.Purchases.Add(purchase.Id, purchase);
            return Task.CompletedTask;
        }
    }

    private sealed class OrderRepository(InMemoryDatabase db) : IOrderRepository
    {
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Orders.TryGetValue(id, out var o) ? Copy(o) : null);

        public Task<Order?> GetByIdForCustomerAsync(Guid id, Guid customerId, CancellationToken ct) =>
            Task.FromResult(db.Orders.TryGetValue(id, out var o) && o.CustomerId == customerId ? Copy(o) : null);

        public Task<Order?> GetByReservationIdAsync(Guid reservationId, CancellationToken ct) =>
            Task.FromResult(db.Orders.Values.Where(o => o.ReservationId == reservationId).Select(Copy).FirstOrDefault());

        public Task<PagedResult<OrderQueryResult>> GetByCustomerAsync(OrderSpecification specification, PaginationRequest page, CancellationToken ct) =>
            throw Unused();

        public Task<bool> UpdateStatusAsync(Guid orderId, OrderStatus expected, OrderStatus next, DateTimeOffset updatedAt, CancellationToken ct) =>
            throw Unused();

        public Task AddAsync(Order order, CancellationToken ct)
        {
            db.Orders.Add(order.Id, Copy(order));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Order order, CancellationToken ct)
        {
            db.Orders[order.Id] = Copy(order);
            return Task.CompletedTask;
        }
    }

    private sealed class PaymentRepository(InMemoryDatabase db) : IPaymentSimulationRepository
    {
        public Task<PaymentSimulation?> GetSimulationAsync(Guid simulationId, CancellationToken ct) =>
            Task.FromResult(db.Payments.TryGetValue(simulationId, out var p) ? Copy(p) : null);

        public Task<IReadOnlyList<PaymentAttempt>> GetAttemptsAsync(Guid simulationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PaymentAttempt>>(db.PaymentAttempts.Where(a => a.PaymentSimulationId == simulationId).OrderBy(a => a.AttemptNumber).ToList());

        public Task<IReadOnlyList<PaymentSimulation>> GetByOrderAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PaymentSimulation>>(db.Payments.Values.Where(p => p.OrderId == orderId).Select(Copy).ToList());

        public Task AddAsync(PaymentSimulation simulation, CancellationToken ct)
        {
            db.Payments.Add(simulation.Id, Copy(simulation));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PaymentSimulation simulation, CancellationToken ct)
        {
            db.Payments[simulation.Id] = Copy(simulation);
            return Task.CompletedTask;
        }

        public Task AddAttemptAsync(PaymentAttempt attempt, CancellationToken ct)
        {
            db.PaymentAttempts.Add(attempt);
            return Task.CompletedTask;
        }
    }

    private sealed class IdempotencyRepository(InMemoryDatabase db) : IIdempotencyRepository
    {
        public Task<IdempotencyRecord?> GetByIdentityAsync(string issuer, string subject, string operation, Guid key, CancellationToken ct) =>
            Task.FromResult(db.Idempotency.GetValueOrDefault((issuer, subject, operation, key)));

        public Task<IReadOnlyList<IdempotencyRecord>> GetByRequestHashAsync(string issuer, string subject, string operation, string requestHash, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IdempotencyRecord>>(db.Idempotency.Values
                .Where(r => r.Issuer == issuer && r.Subject == subject && r.Operation == operation && r.RequestHash == requestHash).ToList());

        public Task<bool> TryCreateInProgressAsync(IdempotencyRecord record, CancellationToken ct) =>
            Task.FromResult(db.Idempotency.TryAdd(KeyOf(record), record));

        public Task CompleteAsync(IdempotencyRecord record, CancellationToken ct)
        {
            db.Idempotency[KeyOf(record)] = record;
            return Task.CompletedTask;
        }

        public Task<int> RemoveExpiredAsync(DateTimeOffset now, CancellationToken ct)
        {
            var expired = db.Idempotency.Where(r => r.Value.ExpiresAt <= now).Select(r => r.Key).ToList();
            expired.ForEach(k => db.Idempotency.Remove(k));
            return Task.FromResult(expired.Count);
        }

        private static (string, string, string, Guid) KeyOf(IdempotencyRecord r) => (r.Issuer, r.Subject, r.Operation, r.Key);
    }

    private sealed class InventoryRepository(InMemoryDatabase db) : IInventoryMovementRepository
    {
        public Task AddAsync(InventoryMovement movement, CancellationToken ct)
        {
            db.InventoryMovements.Add(movement);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<InventoryMovement>> GetByReservationAsync(Guid reservationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<InventoryMovement>>(db.InventoryMovements.Where(m => m.ReservationId == reservationId).ToList());

        public Task<PagedResult<InventoryMovement>> GetByAttractionAsync(Guid attractionId, PaginationRequest page, CancellationToken ct) =>
            Task.FromResult(Page(db.InventoryMovements.Where(m => m.AttractionId == attractionId).OrderByDescending(m => m.CreatedAt), page));

        public Task<int> GetNetReservedQuantityAsync(Guid availabilitySlotId, CancellationToken ct) =>
            Task.FromResult(db.InventoryMovements.Where(m => m.AvailabilitySlotId == availabilitySlotId).Sum(m => m.Quantity));
    }

    private sealed class EventRepository(InMemoryDatabase db) : IOrderEventRepository, IPaymentEventRepository, IAuditEventRepository
    {
        public Task AddAsync(OrderEvent orderEvent, CancellationToken ct)
        {
            db.OrderEvents.Add(orderEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OrderEvent>> GetByOrderAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OrderEvent>>(db.OrderEvents.Where(e => e.OrderId == orderId).ToList());

        public Task AddAsync(PaymentEvent paymentEvent, CancellationToken ct)
        {
            db.PaymentEvents.Add(paymentEvent);
            return Task.CompletedTask;
        }

        public Task AddAsync(AuditEvent auditEvent, CancellationToken ct)
        {
            db.AuditEvents.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PaymentEvent>> GetBySimulationAsync(Guid paymentSimulationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PaymentEvent>>(db.PaymentEvents.Where(e => e.PaymentSimulationId == paymentSimulationId).ToList());

        public Task<PagedResult<AuditEvent>> GetByResourceAsync(string resourceType, string resourceId, PaginationRequest page, CancellationToken ct) =>
            Task.FromResult(Page(db.AuditEvents.Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId).OrderByDescending(a => a.CreatedAt), page));
    }
}
