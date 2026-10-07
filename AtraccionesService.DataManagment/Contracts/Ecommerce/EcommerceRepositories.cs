using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.QueryModels;
using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.Domain.Ecommerce;

namespace AtraccionesService.DataManagment.Contracts.Ecommerce;

/// <summary>
/// Compras directas. Su estado se deriva del pedido asociado (una compra origina como máximo un pedido), por lo que
/// no se expone una actualización de estado propia.
/// </summary>
public interface IPurchaseRepository
{
    Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<PurchaseQueryResult>> GetByCustomerAsync(PurchaseSpecification specification, PaginationRequest page, CancellationToken cancellationToken);

    Task AddAsync(Purchase purchase, CancellationToken cancellationToken);
}

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order?> GetByIdForCustomerAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken);

    Task<Order?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<PagedResult<OrderQueryResult>> GetByCustomerAsync(OrderSpecification specification, PaginationRequest page, CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task UpdateAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Cambia el estado solo si el actual es <paramref name="expected"/>. La transición la valida el dominio antes de llamar.
    /// </summary>
    Task<bool> UpdateStatusAsync(Guid orderId, OrderStatus expected, OrderStatus next, DateTimeOffset updatedAt, CancellationToken cancellationToken);
}

public interface IPaymentSimulationRepository
{
    Task<PaymentSimulation?> GetSimulationAsync(Guid simulationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentSimulation>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentAttempt>> GetAttemptsAsync(Guid simulationId, CancellationToken cancellationToken);

    Task AddAsync(PaymentSimulation simulation, CancellationToken cancellationToken);

    Task UpdateAsync(PaymentSimulation simulation, CancellationToken cancellationToken);

    /// <summary>Registra un intento; los intentos son inmutables y únicos por simulación y número.</summary>
    Task AddAttemptAsync(PaymentAttempt attempt, CancellationToken cancellationToken);
}

/// <summary>
/// Idempotencia con identidad única <c>issuer + subject + operation + key</c>.
/// </summary>
public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetByIdentityAsync(string issuer, string subject, string operation, Guid key, CancellationToken cancellationToken);

    /// <summary>Registros vigentes del mismo usuario y operación con el hash de solicitud indicado.</summary>
    Task<IReadOnlyList<IdempotencyRecord>> GetByRequestHashAsync(string issuer, string subject, string operation, string requestHash, CancellationToken cancellationToken);

    /// <summary>
    /// Inserta el registro <c>IN_PROGRESS</c> respetando la unicidad. Devuelve <c>false</c> si otra solicitud ya lo creó.
    /// </summary>
    Task<bool> TryCreateInProgressAsync(IdempotencyRecord record, CancellationToken cancellationToken);

    /// <summary>Marca el registro como <c>COMPLETED</c> con la respuesta reproducible, dentro de la unidad de trabajo.</summary>
    Task CompleteAsync(IdempotencyRecord record, CancellationToken cancellationToken);

    Task<int> RemoveExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IInventoryMovementRepository
{
    Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken);

    Task<IReadOnlyList<InventoryMovement>> GetByReservationAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<PagedResult<InventoryMovement>> GetByAttractionAsync(Guid attractionId, PaginationRequest page, CancellationToken cancellationToken);

    /// <summary>Cupos tomados netos de una franja según el histórico (confirmados menos liberados).</summary>
    Task<int> GetNetReservedQuantityAsync(Guid availabilitySlotId, CancellationToken cancellationToken);
}
