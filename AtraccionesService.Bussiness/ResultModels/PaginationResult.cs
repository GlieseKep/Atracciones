namespace AtraccionesService.Application.ResultModels;

/// <summary>Página de resultados independiente de HTTP.</summary>
public sealed record PaginationResult<T>(IReadOnlyList<T> Items, int TotalItems, int Limit, int Offset)
{
    public int TotalPages => Limit <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)Limit);

    public int CurrentPage => Limit <= 0 ? 1 : (Offset / Limit) + 1;
}

/// <summary>Importe con moneda ISO 4217.</summary>
public sealed record Money(string Currency, decimal Total);
