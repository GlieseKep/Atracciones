using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Entities.Ecommerce;
using AtraccionesService.DataAcess.Repositories.Ecommerce;
using AtraccionesService.DataAcess.Repositories.Generic;
using AtraccionesService.DataManagment.Administration;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.QueryModels;
using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.Domain.Common;
using AtraccionesService.Domain.Ecommerce;
using Microsoft.EntityFrameworkCore;

namespace AtraccionesService.DataAcess.Repositories.Administration;

/// <summary>
/// Reportes administrativos agregados y paginados, sin PII ni datos financieros sensibles.
/// Las sumas de importes se realizan en memoria sobre la proyección mínima porque SQLite no agrega <c>decimal</c>.
/// </summary>
public sealed class AdminReportRepository(AtraccionesDbContext context) : IAdminReportRepository
{
    public async Task<AdminDashboardSummary> GetSummaryAsync(ReportPeriod period, CancellationToken cancellationToken)
    {
        var orders = await context.Orders.AsNoTracking().Where(o => o.CreatedAt >= period.From && o.CreatedAt < period.To)
            .GroupBy(o => o.Status).Select(g => new StatusCount(g.Key, g.Count())).ToListAsync(cancellationToken);
        var payments = await context.PaymentSimulations.AsNoTracking().Where(p => p.CreatedAt >= period.From && p.CreatedAt < period.To)
            .GroupBy(p => p.Status).Select(g => new StatusCount(g.Key, g.Count())).ToListAsync(cancellationToken);
        var reservations = await context.Reservations.AsNoTracking().Where(r => r.CreatedAt >= period.From && r.CreatedAt < period.To)
            .GroupBy(r => r.Status).Select(g => new StatusCount(g.Key, g.Count())).ToListAsync(cancellationToken);

        var settled = PaymentStatus.SETTLED.ToString();
        var settledAmounts = await context.PaymentSimulations.AsNoTracking()
            .Where(p => p.Status == settled && p.CreatedAt >= period.From && p.CreatedAt < period.To)
            .Select(p => new { p.Currency, p.Amount })
            .ToListAsync(cancellationToken);

        return new AdminDashboardSummary(
            orders.OrderBy(s => s.Status).ToList(),
            payments.OrderBy(s => s.Status).ToList(),
            reservations.OrderBy(s => s.Status).ToList(),
            settledAmounts.GroupBy(p => p.Currency).Select(g => new CurrencyTotal(g.Key, g.Sum(p => p.Amount))).OrderBy(c => c.Currency).ToList());
    }

    public Task<PagedResult<OrderQueryResult>> GetOrdersAsync(ReportPeriod period, PaginationRequest page, CancellationToken cancellationToken) =>
        OrderRepository.PageAsync(
            context,
            context.Orders.AsNoTracking().Where(o => o.CreatedAt >= period.From && o.CreatedAt < period.To),
            descending: true,
            page,
            cancellationToken);

    public async Task<PagedResult<PaymentSimulationQueryResult>> GetPaymentsAsync(ReportPeriod period, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = context.PaymentSimulations.AsNoTracking()
            .Where(p => p.CreatedAt >= period.From && p.CreatedAt < period.To)
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Offset).Take(page.Limit)
            .Select(p => new PaymentSimulationQueryResult(p.Id, p.OrderId, p.PaymentMethod, p.Status, p.Currency, p.Amount,
                context.PaymentAttempts.Count(a => a.PaymentSimulationId == p.Id), p.CreatedAt))
            .ToListAsync(cancellationToken);
        return PagedResult<PaymentSimulationQueryResult>.Create(items, total, page);
    }

    public async Task<PagedResult<ReservationQueryResult>> GetReservationsAsync(ReportPeriod period, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = context.Reservations.AsNoTracking()
            .Where(r => r.CreatedAt >= period.From && r.CreatedAt < period.To)
            .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Offset).Take(page.Limit)
            .Select(r => new ReservationQueryResult(r.Id, r.AttractionId, r.Date, r.Time, r.TicketCount, r.Status, r.TotalPrice.Currency, r.TotalPrice.Amount))
            .ToListAsync(cancellationToken);
        return PagedResult<ReservationQueryResult>.Create(items, total, page);
    }
}

public sealed class RefundSimulationRepository(AtraccionesDbContext context) : GenericRepository<RefundSimulationEntity>(context), IRefundSimulationRepository
{
    private static readonly string[] CountedStatuses = [RefundStatus.PENDING.ToString(), RefundStatus.SETTLED.ToString()];

    public async Task<RefundSimulation?> GetByIdAsync(Guid refundId, CancellationToken cancellationToken)
    {
        var entity = await Set.AsNoTracking().FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<RefundSimulation>> GetByPaymentAsync(Guid paymentSimulationId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().Where(r => r.PaymentSimulationId == paymentSimulationId).ToListAsync(cancellationToken))
        .OrderBy(r => r.CreatedAt).Select(ToDomain).ToList();

    public async Task<decimal> GetRefundedAmountAsync(Guid paymentSimulationId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking()
            .Where(r => r.PaymentSimulationId == paymentSimulationId && CountedStatuses.Contains(r.Status))
            .Select(r => r.Amount)
            .ToListAsync(cancellationToken))
        .Sum();

    public Task AddAsync(RefundSimulation refund, CancellationToken cancellationToken) => AddAsync(new RefundSimulationEntity
    {
        Id = refund.Id,
        PaymentSimulationId = refund.PaymentSimulationId,
        Amount = refund.Amount.Amount,
        Currency = refund.Amount.Currency,
        Status = refund.Status.ToString(),
        Reason = refund.Reason,
        CreatedByUserId = refund.CreatedByUserId,
        CreatedAt = refund.CreatedAt,
        ProcessedAt = refund.ProcessedAt,
    }, cancellationToken);

    public async Task<bool> CompleteAsync(Guid refundId, RefundStatus status, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        var pending = RefundStatus.PENDING.ToString();
        var value = status.ToString();
        var updated = await Set
            .Where(r => r.Id == refundId && r.Status == pending)
            .ExecuteUpdateAsync(s =>
            {
                s.SetProperty(r => r.Status, value);
                s.SetProperty(r => r.ProcessedAt, (DateTimeOffset?)processedAt);
            }, cancellationToken);
        return updated == 1;
    }

    private static RefundSimulation ToDomain(RefundSimulationEntity e) => new(
        e.Id, e.PaymentSimulationId, new Money(e.Currency, e.Amount), Enum.Parse<RefundStatus>(e.Status), e.Reason, e.CreatedByUserId, e.CreatedAt, e.ProcessedAt);
}
