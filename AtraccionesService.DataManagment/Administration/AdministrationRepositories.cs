using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.QueryModels;
using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.Domain.Ecommerce;

namespace AtraccionesService.DataManagment.Administration;

/// <summary>
/// Consultas agregadas y paginadas para administración. Los resultados omiten PII y datos financieros sensibles.
/// </summary>
public interface IAdminReportRepository
{
    Task<AdminDashboardSummary> GetSummaryAsync(ReportPeriod period, CancellationToken cancellationToken);

    Task<PagedResult<OrderQueryResult>> GetOrdersAsync(ReportPeriod period, PaginationRequest page, CancellationToken cancellationToken);

    Task<PagedResult<PaymentSimulationQueryResult>> GetPaymentsAsync(ReportPeriod period, PaginationRequest page, CancellationToken cancellationToken);

    Task<PagedResult<ReservationQueryResult>> GetReservationsAsync(ReportPeriod period, PaginationRequest page, CancellationToken cancellationToken);
}

/// <summary>Reembolsos simulados de pagos liquidados.</summary>
public interface IRefundSimulationRepository
{
    Task<RefundSimulation?> GetByIdAsync(Guid refundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefundSimulation>> GetByPaymentAsync(Guid paymentSimulationId, CancellationToken cancellationToken);

    /// <summary>Total reembolsado (PENDING o SETTLED) de un pago; sirve para no superar el importe liquidado.</summary>
    Task<decimal> GetRefundedAmountAsync(Guid paymentSimulationId, CancellationToken cancellationToken);

    Task AddAsync(RefundSimulation refund, CancellationToken cancellationToken);

    /// <summary>Registra el resultado de un reembolso pendiente.</summary>
    Task<bool> CompleteAsync(Guid refundId, RefundStatus status, DateTimeOffset processedAt, CancellationToken cancellationToken);
}
