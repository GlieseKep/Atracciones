namespace AtraccionesService.DataManagment.Specifications;

/// <summary>Compras directas de un cliente (regla de acceso <see cref="CustomerId"/>), opcionalmente por atracción y fecha de servicio.</summary>
public sealed record PurchaseSpecification(
    Guid CustomerId,
    Guid? AttractionId = null,
    DateOnly? ServiceDateFrom = null,
    DateOnly? ServiceDateTo = null,
    bool SortDescending = true);
