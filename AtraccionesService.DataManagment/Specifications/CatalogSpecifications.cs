namespace AtraccionesService.DataManagment.Specifications;

public enum AttractionSort
{
    MostPopular,
    PriceAscending,
    PriceDescending,
    RatingDescending
}

/// <summary>
/// Especificación de búsqueda del catálogo: filtros y orden (la paginación llega en <c>PaginationRequest</c>).
/// Con <see cref="StartDate"/>/<see cref="EndDate"/> solo se devuelven atracciones con alguna franja con cupos en el rango.
/// La implementación carga las relaciones necesarias para reconstruir el agregado completo.
/// </summary>
public sealed record AttractionSearchCriteria(
    string? Currency,
    IReadOnlyList<string> Cities,
    IReadOnlyList<string> Countries,
    DateOnly? StartDate,
    DateOnly? EndDate,
    double? MinimumReviewScore,
    int? MinimumReviewCount,
    AttractionSort Sort);
