using AtraccionesService.Domain.Ecommerce;

namespace AtraccionesService.DataManagment.Specifications;

/// <summary>
/// Pedidos de un cliente: <see cref="CustomerId"/> es la regla de acceso obligatoria. Orden por fecha de creación.
/// </summary>
public sealed record OrderSpecification(
    Guid CustomerId,
    OrderStatus? Status = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    bool SortDescending = true);

/// <summary>Periodo de un reporte administrativo (instantes UTC, límite superior exclusivo).</summary>
public sealed record ReportPeriod(DateTimeOffset From, DateTimeOffset To);
