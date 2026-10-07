namespace AtraccionesService.DataManagment.Contracts.Common;

/// <summary>Límites configurables de paginación compartidos por Application y DataAccess.</summary>
public sealed record PaginationLimits(int DefaultLimit = 20, int MaxLimit = 100)
{
    public static readonly PaginationLimits Default = new();
}

/// <summary>
/// Página solicitada: <see cref="Offset"/> y <see cref="Limit"/>, con orden y filtro opcionales.
/// <see cref="Sort"/> usa la convención <c>campo</c> (ascendente) o <c>-campo</c> (descendente); cada repositorio
/// documenta los campos que admite. Application valida los valores antes de llegar aquí.
/// </summary>
public sealed record PaginationRequest(int Limit, int Offset, string? Sort = null, string? Filter = null)
{
    /// <summary>Ajusta la página a los límites: límite por defecto si no es positivo, tope máximo y offset no negativo.</summary>
    public PaginationRequest Normalize(PaginationLimits limits) => this with
    {
        Limit = Limit <= 0 ? limits.DefaultLimit : Math.Min(Limit, limits.MaxLimit),
        Offset = Math.Max(0, Offset),
    };

    /// <summary>Indica si <see cref="Sort"/> pide orden descendente.</summary>
    public bool SortDescending => Sort?.StartsWith('-') == true;

    /// <summary>Campo de orden sin el prefijo de dirección.</summary>
    public string? SortField => Sort?.TrimStart('-');
}

public interface IPaginatedResult<out T>
{
    IReadOnlyList<T> Items { get; }

    int TotalItems { get; }

    int PageNumber { get; }

    int PageSize { get; }

    int TotalPages { get; }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalItems, int PageSize = 0, int Offset = 0) : IPaginatedResult<T>
{
    public int PageNumber => PageSize <= 0 ? 1 : (Offset / PageSize) + 1;

    public int TotalPages => PageSize <= 0 ? (TotalItems > 0 ? 1 : 0) : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalItems, PaginationRequest page) =>
        new(items, totalItems, page.Limit, page.Offset);

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) => new(Items.Select(map).ToList(), TotalItems, PageSize, Offset);
}
