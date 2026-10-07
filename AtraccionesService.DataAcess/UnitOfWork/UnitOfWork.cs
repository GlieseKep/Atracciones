using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Exceptions;
using AtraccionesService.DataAcess.Repositories.Administration;
using AtraccionesService.DataAcess.Repositories.Catalog;
using AtraccionesService.DataAcess.Repositories.Ecommerce;
using AtraccionesService.DataAcess.Repositories.Identity;
using AtraccionesService.DataManagment.Administration;
using AtraccionesService.DataManagment.Contracts.Catalog;
using AtraccionesService.DataManagment.Contracts.Ecommerce;
using AtraccionesService.DataManagment.Contracts.Events;
using AtraccionesService.DataManagment.Contracts.Identity;
using AtraccionesService.DataManagment.UnitOfWork;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AtraccionesService.DataAcess.UnitOfWork;

/// <summary>
/// Unidad de trabajo EF Core: comparte un <see cref="AtraccionesDbContext"/> por solicitud entre todos los repositorios y
/// controla la transacción local. La transacción de EF (<see cref="IDbContextTransaction"/>) no sale de DataAccess.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AtraccionesDbContext _context;
    private readonly IAsyncDisposable? _ownedScope;
    private IDbContextTransaction? _transaction;

    public EfUnitOfWork(AtraccionesDbContext context)
        : this(context, null)
    {
    }

    /// <param name="ownedScope">Ámbito propio creado por <see cref="EfUnitOfWorkFactory"/>; se libera con la unidad.</param>
    internal EfUnitOfWork(AtraccionesDbContext context, IAsyncDisposable? ownedScope)
    {
        _context = context;
        _ownedScope = ownedScope;
        Attractions = new AttractionRepository(context);
        Availability = new AvailabilityRepository(context);
        Reservations = new ReservationRepository(context);
        Users = new UserRepository(context);
        Customers = new CustomerRepository(context);
        Roles = new RoleRepository(context);
        UserRoles = new UserRoleRepository(context);
        Purchases = new PurchaseRepository(context);
        Orders = new OrderRepository(context);
        Payments = new PaymentRepository(context);
        Idempotency = new IdempotencyRepository(context);
        Inventory = new InventoryMovementRepository(context);
        OrderEvents = new OrderEventRepository(context);
        PaymentEvents = new PaymentEventRepository(context);
        AuditEvents = new AuditEventRepository(context);
        Refunds = new RefundSimulationRepository(context);
        AdminReports = new AdminReportRepository(context);
    }

    public IAttractionRepository Attractions { get; }
    public IAvailabilityRepository Availability { get; }
    public IReservationRepository Reservations { get; }
    public IUserRepository Users { get; }
    public ICustomerRepository Customers { get; }
    public IRoleRepository Roles { get; }
    public IUserRoleRepository UserRoles { get; }
    public IPurchaseRepository Purchases { get; }
    public IOrderRepository Orders { get; }
    public IPaymentSimulationRepository Payments { get; }
    public IIdempotencyRepository Idempotency { get; }
    public IInventoryMovementRepository Inventory { get; }
    public IOrderEventRepository OrderEvents { get; }
    public IPaymentEventRepository PaymentEvents { get; }
    public IAuditEventRepository AuditEvents { get; }
    public IRefundSimulationRepository Refunds { get; }
    public IAdminReportRepository AdminReports { get; }

    public bool HasActiveTransaction => _transaction is not null;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveTranslatedAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("Ya existe una transacción activa en esta unidad de trabajo.");
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var transaction = _transaction ?? throw new InvalidOperationException("No hay una transacción activa.");
        try
        {
            await _context.SaveTranslatedAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>Revierte la transacción y descarta las entidades rastreadas para que no se reintenten en el mismo contexto.</summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction is not null)
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_transaction is not null)
            {
                await _transaction.DisposeAsync();
            }

            _transaction = null;
            _context.ChangeTracker.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_ownedScope is not null)
        {
            await _ownedScope.DisposeAsync();
        }
    }
}

/// <summary>Crea unidades de trabajo con su propio ámbito de servicios y contexto (uso fuera de solicitudes HTTP).</summary>
public sealed class EfUnitOfWorkFactory(IServiceScopeFactory scopeFactory) : IUnitOfWorkFactory
{
    public IUnitOfWork Create()
    {
        var scope = scopeFactory.CreateAsyncScope();
        return new EfUnitOfWork(scope.ServiceProvider.GetRequiredService<AtraccionesDbContext>(), scope);
    }
}
