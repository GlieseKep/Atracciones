using AtraccionesService.Domain.Common;

namespace AtraccionesService.Domain.Catalog;

public enum ProductType
{
    SINGLE_TICKET,
    GUIDED_TOUR,
    PACKAGE
}

public sealed record Location(string Address, string City, string Country, double? Latitude, double? Longitude, string? Type);

public sealed record OperatorInfo(int Id, string Name);

public sealed record Rating(int NumberOfReviews, double Score);

public sealed record AttractionUrls(string? Web, string? App);

/// <summary>Valores editables de una atracción; se usan para crear y para reemplazar el recurso completo.</summary>
public sealed record AttractionDetails(
    string Name,
    string LongDescription,
    string Duration,
    Money Price,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Badges,
    IReadOnlyList<Location> Locations,
    IReadOnlyList<string> PhotoUrls,
    OperatorInfo? Operator,
    ProductType ProductType,
    IReadOnlyList<string> Includes,
    IReadOnlyList<string> SupportedLanguages,
    bool FreeCancellation);

/// <summary>Agregado del catálogo.</summary>
public sealed class Attraction
{
    private Attraction(Guid id, AttractionDetails details, Rating? rating, AttractionUrls? urls)
    {
        Id = id;
        Details = Validate(details);
        Rating = rating;
        Urls = urls;
    }

    public Guid Id { get; }

    public AttractionDetails Details { get; private set; }

    public Rating? Rating { get; }

    public AttractionUrls? Urls { get; }

    public static Attraction Create(AttractionDetails details) => new(Guid.NewGuid(), details, null, null);

    /// <summary>Reconstruye un agregado persistido.</summary>
    public static Attraction Restore(Guid id, AttractionDetails details, Rating? rating, AttractionUrls? urls) =>
        new(id, details, rating, urls);

    public void Replace(AttractionDetails details) => Details = Validate(details);

    private static AttractionDetails Validate(AttractionDetails details)
    {
        Guard.NotBlank(details.Name, "name", 200);
        Guard.NotBlank(details.LongDescription, "longDescription", 5000);
        Guard.NotBlank(details.Duration, "duration", 50);
        if (details.Categories.Count == 0)
        {
            throw new DomainException("CATEGORIES_REQUIRED", "La atracción requiere al menos una categoría.");
        }

        if (details.Locations.Count == 0)
        {
            throw new DomainException("LOCATIONS_REQUIRED", "La atracción requiere al menos una ubicación.");
        }

        if (!Enum.IsDefined(details.ProductType))
        {
            throw new DomainException("INVALID_PRODUCT_TYPE", "Tipo de producto no soportado.");
        }

        return details;
    }
}
