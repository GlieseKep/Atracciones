using AtraccionesService.DataManagment.Administration;
using AtraccionesService.DataManagment.Contracts.Catalog;
using AtraccionesService.DataManagment.Contracts.Ecommerce;
using AtraccionesService.DataManagment.Contracts.Events;
using AtraccionesService.DataManagment.Contracts.Identity;

namespace AtraccionesService.DataManagment.UnitOfWork;

/// <summary>
/// Coordina los repositorios de una operación y su transacción local. Es el único mecanismo transaccional:
/// no existe un <c>ITransactionManager</c> paralelo.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IAttractionRepository Attractions { get; }

    IAvailabilityRepository Availability { get; }

    IReservationRepository Reservations { get; }

    IUserRepository Users { get; }

    ICustomerRepository Customers { get; }

    IRoleRepository Roles { get; }

    IUserRoleRepository UserRoles { get; }

    IPurchaseRepository Purchases { get; }

    IOrderRepository Orders { get; }

    IPaymentSimulationRepository Payments { get; }

    IIdempotencyRepository Idempotency { get; }

    IInventoryMovementRepository Inventory { get; }

    IOrderEventRepository OrderEvents { get; }

    IPaymentEventRepository PaymentEvents { get; }

    IAuditEventRepository AuditEvents { get; }

    IRefundSimulationRepository Refunds { get; }

    IAdminReportRepository AdminReports { get; }

    bool HasActiveTransaction { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Crea una unidad de trabajo independiente (con su propio contexto) para operaciones fuera de una solicitud,
/// por ejemplo tareas en segundo plano. Quien la crea debe liberarla con <c>DisposeAsync</c>.
/// </summary>
public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
