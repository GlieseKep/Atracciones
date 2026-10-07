using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.Domain.Ecommerce;

namespace AtraccionesService.DataManagment.Contracts.Events;

// Repositorios de eventos de solo inserción: se persisten en la misma unidad de trabajo que el cambio que registran,
// rechazan identificadores duplicados y no ofrecen actualización ni borrado. La publicación externa (outbox) queda fuera.

/// <summary>Historial inmutable de estados del pedido.</summary>
public interface IOrderEventRepository
{
    Task AddAsync(OrderEvent orderEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderEvent>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <summary>Eventos de la pasarela simulada.</summary>
public interface IPaymentEventRepository
{
    Task AddAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentEvent>> GetBySimulationAsync(Guid paymentSimulationId, CancellationToken cancellationToken);
}

/// <summary>Auditoría inmutable de mutaciones administrativas.</summary>
public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    Task<PagedResult<AuditEvent>> GetByResourceAsync(string resourceType, string resourceId, PaginationRequest page, CancellationToken cancellationToken);
}
