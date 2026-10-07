using System.ComponentModel.DataAnnotations;

namespace AtraccionesService.Contracts.Catalog;

/// <summary>
/// Cuerpo de <c>POST /atracciones/search</c> (CORRECCIONES_CONTRATO.md §4.5 y §4.6).
/// </summary>
public sealed class SearchAttractionsRequest : IValidatableObject
{
    /// <summary>Moneda ISO 4217 en la que se expresan los precios.</summary>
    [RegularExpression(Common.ValidationPatterns.CurrencyCode, ErrorMessage = "currency debe ser un código ISO 4217.")]
    public string? Currency { get; set; }

    [MaxLength(20)]
    public List<string> Cities { get; set; } = [];

    /// <summary>Códigos ISO 3166-1 alfa-2.</summary>
    [MaxLength(20)]
    public List<string> Countries { get; set; } = [];

    public SearchDateRange? Dates { get; set; }

    public SearchFilters? Filters { get; set; }

    /// <summary>Token opaco emitido por una respuesta previa; los tokens manipulados o expirados producen 400.</summary>
    [StringLength(512)]
    public string? NextPage { get; set; }

    [Range(1, 100)]
    public int Rows { get; set; } = 20;

    public SearchSort? Sort { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var country in Countries)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(country, Common.ValidationPatterns.CountryCode))
            {
                yield return new ValidationResult($"'{country}' no es un código de país ISO 3166-1 alfa-2.", [nameof(Countries)]);
            }
        }

        if (Dates is { StartDate: { } start, EndDate: { } end } && end < start)
        {
            yield return new ValidationResult("dates.endDate debe ser igual o posterior a dates.startDate.", [nameof(Dates)]);
        }
    }
}

public sealed class SearchDateRange
{
    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }
}

public sealed class SearchFilters
{
    public RatingFilter? Rating { get; set; }
}

public sealed class RatingFilter
{
    [Range(0d, 5d)]
    public double? MinimumReviewScore { get; set; }

    [Range(0, int.MaxValue)]
    public int? MinimumReviewCount { get; set; }
}

public sealed class SearchSort
{
    /// <summary>Criterio de orden: <c>most_popular</c>, <c>price_asc</c>, <c>price_desc</c> o <c>rating_desc</c>.</summary>
    [RegularExpression("^(most_popular|price_asc|price_desc|rating_desc)$", ErrorMessage = "sort.by no es un criterio de orden permitido.")]
    public string By { get; set; } = "most_popular";
}

public sealed class SearchAttractionsResponse
{
    public IReadOnlyList<AttractionResponse> Data { get; init; } = [];

    public SearchMetadata Metadata { get; init; } = new();

    public string RequestId { get; init; } = string.Empty;
}

public sealed class SearchMetadata
{
    public int TotalResults { get; init; }

    public string? NextPage { get; init; }
}

/// <summary>Cuerpo de <c>POST /atracciones/details</c>.</summary>
public sealed class DetailsRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<Guid> Attractions { get; set; } = [];

    [MaxLength(10)]
    public List<string> Languages { get; set; } = [];
}
