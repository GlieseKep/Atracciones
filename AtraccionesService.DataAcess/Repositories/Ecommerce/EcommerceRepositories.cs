using System.Security.Cryptography;
using System.Text;
using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Entities.Ecommerce;
using AtraccionesService.DataAcess.Entities.Technical;
using AtraccionesService.DataAcess.Mapping;
using AtraccionesService.DataAcess.Repositories.Generic;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.Contracts.Ecommerce;
using AtraccionesService.DataManagment.Contracts.Events;
using AtraccionesService.DataManagment.Exceptions;
using AtraccionesService.DataManagment.QueryModels;
using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.Domain.Ecommerce;
using Microsoft.EntityFrameworkCore;

namespace AtraccionesService.DataAcess.Repositories.Ecommerce;

public sealed class PurchaseRepository(AtraccionesDbContext context) : GenericRepository<PurchaseEntity>(context), IPurchaseRepository
{
    public async Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken))?.ToDomain();

    public async Task<PagedResult<PurchaseQueryResult>> GetByCustomerAsync(PurchaseSpecification specification, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = Set.AsNoTracking().Where(p => p.CustomerId == specification.CustomerId);
        if (specification.AttractionId is { } attractionId)
        {
            query = query.Where(p => p.AttractionId == attractionId);
        }

        if (specification.ServiceDateFrom is { } from)
        {
            query = query.Where(p => p.ServiceDate >= from);
        }

        if (specification.ServiceDateTo is { } to)
        {
            query = query.Where(p => p.ServiceDate <= to);
        }

        query = specification.SortDescending
            ? query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            : query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Offset).Take(page.Limit)
            .Select(p => new PurchaseQueryResult(
                p.Id, p.AttractionId, p.ServiceDate, p.ServiceTime, p.Quantity, p.UnitPrice.Currency, p.TotalAmount,
                Context.Orders.Where(o => o.PurchaseId == p.Id).Select(o => (Guid?)o.Id).FirstOrDefault(),
                Context.Orders.Where(o => o.PurchaseId == p.Id).Select(o => o.Status).FirstOrDefault(),
                p.CreatedAt))
            .ToListAsync(cancellationToken);
        return PagedResult<PurchaseQueryResult>.Create(items, total, page);
    }

    public Task AddAsync(Purchase purchase, CancellationToken cancellationToken) => AddAsync(purchase.ToEntity(), cancellationToken);
}

public sealed class OrderRepository(AtraccionesDbContext context) : GenericRepository<OrderEntity>(context), IOrderRepository
{
    private IQueryable<OrderEntity> Orders => Set.AsNoTracking().Include(o => o.Items);

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        (await Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken))?.ToDomain();

    public async Task<Order?> GetByIdForCustomerAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken) =>
        (await Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken))?.ToDomain();

    public async Task<Order?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken) =>
        (await Orders.FirstOrDefaultAsync(o => o.ReservationId == reservationId, cancellationToken))?.ToDomain();

    public async Task<PagedResult<OrderQueryResult>> GetByCustomerAsync(OrderSpecification specification, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = Set.AsNoTracking().Where(o => o.CustomerId == specification.CustomerId);
        if (specification.Status is { } status)
        {
            var value = status.ToString();
            query = query.Where(o => o.Status == value);
        }

        if (specification.CreatedFrom is { } from)
        {
            query = query.Where(o => o.CreatedAt >= from);
        }

        if (specification.CreatedTo is { } to)
        {
            query = query.Where(o => o.CreatedAt < to);
        }

        return await PageAsync(Context, query, specification.SortDescending, page, cancellationToken);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken) => AddAsync(order.ToEntity(), cancellationToken);

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstAsync(o => o.Id == order.Id, cancellationToken);
        entity.Apply(order);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateStatusAsync(Guid orderId, OrderStatus expected, OrderStatus next, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var expectedValue = expected.ToString();
        var nextValue = next.ToString();
        var clearHold = next != OrderStatus.PENDING_PAYMENT;
        var updated = await Set
            .Where(o => o.Id == orderId && o.Status == expectedValue)
            .ExecuteUpdateAsync(s =>
            {
                s.SetProperty(o => o.Status, nextValue);
                s.SetProperty(o => o.UpdatedAt, updatedAt);
                if (clearHold)
                {
                    s.SetProperty(o => o.HoldExpiresAt, (DateTimeOffset?)null);
                }
            }, cancellationToken);
        return updated == 1;
    }

    /// <summary>Proyección paginada común a listados de cliente y reportes administrativos.</summary>
    internal static async Task<PagedResult<OrderQueryResult>> PageAsync(
        AtraccionesDbContext context, IQueryable<OrderEntity> query, bool descending, PaginationRequest page, CancellationToken cancellationToken)
    {
        query = descending ? query.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id) : query.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Offset).Take(page.Limit)
            .Select(o => new OrderQueryResult(o.Id, o.CustomerId, o.Status, o.Currency, o.TotalAmount,
                context.OrderItems.Count(i => i.OrderId == o.Id), o.CreatedAt, o.UpdatedAt))
            .ToListAsync(cancellationToken);
        return PagedResult<OrderQueryResult>.Create(items, total, page);
    }
}

/// <summary>Simulaciones e intentos de pago. Los intentos son inmutables una vez registrados.</summary>
public sealed class PaymentRepository(AtraccionesDbContext context) : GenericRepository<PaymentSimulationEntity>(context), IPaymentSimulationRepository
{
    public async Task<PaymentSimulation?> GetSimulationAsync(Guid simulationId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(p => p.Id == simulationId, cancellationToken))?.ToDomain();

    public async Task<IReadOnlyList<PaymentSimulation>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().Where(p => p.OrderId == orderId).ToListAsync(cancellationToken)).Select(p => p.ToDomain()).ToList();

    public async Task<IReadOnlyList<PaymentAttempt>> GetAttemptsAsync(Guid simulationId, CancellationToken cancellationToken) =>
        (await Context.PaymentAttempts.AsNoTracking().Where(a => a.PaymentSimulationId == simulationId).OrderBy(a => a.AttemptNumber).ToListAsync(cancellationToken))
        .Select(a => new PaymentAttempt(a.Id, a.PaymentSimulationId, a.AttemptNumber, Enum.Parse<PaymentStatus>(a.Status), a.ResponseCode, a.ResponseMessage, a.CreatedAt))
        .ToList();

    public Task AddAsync(PaymentSimulation simulation, CancellationToken cancellationToken)
    {
        var entity = new PaymentSimulationEntity();
        entity.Apply(simulation);
        return AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(PaymentSimulation simulation, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstAsync(p => p.Id == simulation.Id, cancellationToken);
        entity.Apply(simulation);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task AddAttemptAsync(PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        Context.PaymentAttempts.Add(new PaymentAttemptEntity
        {
            Id = attempt.Id,
            PaymentSimulationId = attempt.PaymentSimulationId,
            AttemptNumber = attempt.AttemptNumber,
            Status = attempt.Status.ToString(),
            ResponseCode = attempt.ResponseCode,
            ResponseMessage = attempt.ResponseMessage,
            CreatedAt = attempt.CreatedAt,
        });
        await SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Idempotencia con unicidad <c>Issuer + Subject + Operation + Key</c> impuesta por la base de datos.
/// </summary>
public sealed class IdempotencyRepository(AtraccionesDbContext context) : GenericRepository<IdempotencyKeyEntity>(context), IIdempotencyRepository
{
    public async Task<IdempotencyRecord?> GetByIdentityAsync(string issuer, string subject, string operation, Guid key, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(
            k => k.Issuer == issuer && k.Subject == subject && k.Operation == operation && k.Key == key, cancellationToken))?.ToDomain();

    public async Task<IReadOnlyList<IdempotencyRecord>> GetByRequestHashAsync(string issuer, string subject, string operation, string requestHash, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking()
            .Where(k => k.Issuer == issuer && k.Subject == subject && k.Operation == operation && k.RequestHash == requestHash)
            .ToListAsync(cancellationToken))
        .Select(k => k.ToDomain()).ToList();

    /// <summary>Inserta el registro; si el índice único lo rechaza (otra solicitud lo creó) devuelve <c>false</c> sin efectos.</summary>
    public async Task<bool> TryCreateInProgressAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        var entity = new IdempotencyKeyEntity
        {
            Id = Guid.NewGuid(),
            Issuer = record.Issuer,
            Subject = record.Subject,
            Operation = record.Operation,
            Key = record.Key,
            RequestHash = record.RequestHash,
            Status = record.Status.ToString(),
            ResourceId = record.ResourceId,
            ResponseBody = record.ResponseBody,
            CreatedAt = record.CreatedAt,
            ExpiresAt = record.ExpiresAt,
        };

        Set.Add(entity);
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (ConcurrencyException)
        {
            Context.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public async Task CompleteAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstAsync(
            k => k.Issuer == record.Issuer && k.Subject == record.Subject && k.Operation == record.Operation && k.Key == record.Key, cancellationToken);
        entity.Status = record.Status.ToString();
        entity.ResourceId = record.ResourceId;
        entity.ResponseBody = record.ResponseBody;
        entity.ResponseHash = record.ResponseBody is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record.ResponseBody)));
        await SaveChangesAsync(cancellationToken);
    }

    public Task<int> RemoveExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Set.Where(k => k.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
}

public sealed class InventoryMovementRepository(AtraccionesDbContext context) : GenericRepository<InventoryMovementEntity>(context), IInventoryMovementRepository
{
    public Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken) => AddAsync(new InventoryMovementEntity
    {
        Id = movement.Id,
        AttractionId = movement.AttractionId,
        AvailabilityId = movement.AvailabilitySlotId,
        Quantity = movement.Quantity,
        MovementType = movement.MovementType.ToString(),
        PurchaseId = movement.PurchaseId,
        ReservationId = movement.ReservationId,
        CreatedAt = movement.CreatedAt,
    }, cancellationToken);

    public async Task<IReadOnlyList<InventoryMovement>> GetByReservationAsync(Guid reservationId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().Where(m => m.ReservationId == reservationId).ToListAsync(cancellationToken)).Select(m => m.ToDomain()).ToList();

    public async Task<PagedResult<InventoryMovement>> GetByAttractionAsync(Guid attractionId, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = Set.AsNoTracking().Where(m => m.AttractionId == attractionId).OrderByDescending(m => m.CreatedAt).ThenBy(m => m.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Offset).Take(page.Limit).ToListAsync(cancellationToken);
        return PagedResult<InventoryMovement>.Create(items.Select(m => m.ToDomain()).ToList(), total, page);
    }

    public Task<int> GetNetReservedQuantityAsync(Guid availabilitySlotId, CancellationToken cancellationToken) =>
        Set.Where(m => m.AvailabilityId == availabilitySlotId).SumAsync(m => m.Quantity, cancellationToken);
}

/// <summary>Repositorios de eventos de solo inserción (pedido, pago y auditoría).</summary>
public sealed class OrderEventRepository(AtraccionesDbContext context) : GenericRepository<OrderEventEntity>(context), IOrderEventRepository
{
    public Task AddAsync(OrderEvent orderEvent, CancellationToken cancellationToken) => AddAsync(new OrderEventEntity
    {
        Id = orderEvent.Id,
        OrderId = orderEvent.OrderId,
        EventType = orderEvent.EventType,
        PreviousStatus = orderEvent.PreviousStatus?.ToString(),
        NewStatus = orderEvent.NewStatus.ToString(),
        CreatedAt = orderEvent.CreatedAt,
    }, cancellationToken);

    public async Task<IReadOnlyList<OrderEvent>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().Where(e => e.OrderId == orderId).ToListAsync(cancellationToken))
        .OrderBy(e => e.CreatedAt).Select(e => e.ToDomain()).ToList();
}

public sealed class PaymentEventRepository(AtraccionesDbContext context) : GenericRepository<PaymentEventEntity>(context), IPaymentEventRepository
{
    public Task AddAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken) => AddAsync(new PaymentEventEntity
    {
        Id = paymentEvent.Id,
        PaymentSimulationId = paymentEvent.PaymentSimulationId,
        EventType = paymentEvent.EventType,
        Payload = paymentEvent.Payload,
        CreatedAt = paymentEvent.CreatedAt,
    }, cancellationToken);

    public async Task<IReadOnlyList<PaymentEvent>> GetBySimulationAsync(Guid paymentSimulationId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().Where(e => e.PaymentSimulationId == paymentSimulationId).ToListAsync(cancellationToken))
        .OrderBy(e => e.CreatedAt)
        .Select(e => new PaymentEvent(e.Id, e.PaymentSimulationId, e.EventType, e.Payload, e.CreatedAt))
        .ToList();
}

public sealed class AuditEventRepository(AtraccionesDbContext context) : GenericRepository<AuditEventEntity>(context), IAuditEventRepository
{
    public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => AddAsync(new AuditEventEntity
    {
        Id = auditEvent.Id,
        ActorUserId = auditEvent.ActorUserId,
        ActorSubject = auditEvent.ActorSubject,
        Action = auditEvent.Action,
        ResourceType = auditEvent.ResourceType,
        ResourceId = auditEvent.ResourceId,
        Reason = auditEvent.Reason,
        CreatedAt = auditEvent.CreatedAt,
    }, cancellationToken);

    public async Task<PagedResult<AuditEvent>> GetByResourceAsync(string resourceType, string resourceId, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = Set.AsNoTracking()
            .Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId)
            .OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Offset).Take(page.Limit).ToListAsync(cancellationToken);
        return PagedResult<AuditEvent>.Create(
            items.Select(a => new AuditEvent(a.Id, a.ActorUserId, a.ActorSubject, a.Action, a.ResourceType, a.ResourceId, a.Reason, a.CreatedAt)).ToList(),
            total, page);
    }
}
