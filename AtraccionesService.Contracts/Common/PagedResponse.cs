namespace AtraccionesService.Contracts.Common;

/// <summary>
/// Respuesta paginada con metadatos (CORRECCIONES_CONTRATO.md §3.5 y §4.9). Evita arrays sin límite.
/// </summary>
public sealed class PagedResponse<T>
{
    public int TotalItems { get; init; }

    public int ItemsPerPage { get; init; }

    public int CurrentPage { get; init; }

    public int TotalPages { get; init; }

    public IReadOnlyList<T> Data { get; init; } = [];
}
