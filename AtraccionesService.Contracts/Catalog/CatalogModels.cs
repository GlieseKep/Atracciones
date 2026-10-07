using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AtraccionesService.Contracts.Catalog;

/// <summary>Tipo de producto del catálogo (enum de CONTRATO.md).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProductType>))]
public enum ProductType
{
    SINGLE_TICKET,
    GUIDED_TOUR,
    PACKAGE
}

/// <summary>Coordenadas WGS84 en grados decimales (CORRECCIONES_CONTRATO.md §3.8).</summary>
public sealed class CoordinatesDto
{
    [Range(-90d, 90d)]
    public double Latitude { get; set; }

    [Range(-180d, 180d)]
    public double Longitude { get; set; }
}

/// <summary>Ubicación de una atracción (CORRECCIONES_CONTRATO.md §3.8).</summary>
public sealed class LocationDto
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Address { get; set; } = string.Empty;

    /// <summary>Nombre de la ciudad.</summary>
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string City { get; set; } = string.Empty;

    /// <summary>Código de país ISO 3166-1 alfa-2.</summary>
    [Required]
    [RegularExpression(Common.ValidationPatterns.CountryCode, ErrorMessage = "country debe ser un código ISO 3166-1 alfa-2.")]
    public string Country { get; set; } = string.Empty;

    public CoordinatesDto? Coordinates { get; set; }

    [StringLength(60)]
    public string? Type { get; set; }
}

public sealed class PhotoDto
{
    [Required]
    [Url]
    public string Url { get; set; } = string.Empty;
}

public sealed class OperatorDto
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Valoración agregada de la atracción (solo lectura).</summary>
public sealed class RatingDto
{
    public int NumberOfReviews { get; init; }

    public double Score { get; init; }
}

/// <summary>URLs opcionales de la atracción (CORRECCIONES_CONTRATO.md §4.3).</summary>
public sealed class UrlDto
{
    public string? Web { get; init; }

    public string? App { get; init; }
}

/// <summary>
/// Cuerpo de <c>POST /atracciones</c> y <c>PUT /atracciones/{id}</c> (CORRECCIONES_CONTRATO.md §2.2 y §3.11).
/// En <c>PUT</c> reemplaza el recurso completo: las listas omitidas quedan vacías.
/// </summary>
public sealed class CreateAttractionRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string LongDescription { get; set; } = string.Empty;

    /// <summary>Duración ISO 8601, por ejemplo <c>PT3H</c>.</summary>
    [Required]
    [RegularExpression(Common.ValidationPatterns.IsoDuration, ErrorMessage = "duration debe ser una duración ISO 8601, por ejemplo PT3H.")]
    public string Duration { get; set; } = string.Empty;

    [Required]
    public Common.PriceDto Price { get; set; } = new();

    [Required]
    [MinLength(1)]
    public List<string> Categories { get; set; } = [];

    public List<string> Badges { get; set; } = [];

    [Required]
    [MinLength(1)]
    public List<LocationDto> Locations { get; set; } = [];

    public List<PhotoDto> Photos { get; set; } = [];

    public OperatorDto? Operator { get; set; }

    [Required]
    public ProductType? ProductType { get; set; }

    public List<string> Includes { get; set; } = [];

    public List<string> SupportedLanguages { get; set; } = [];

    public bool FreeCancellation { get; set; }
}

/// <summary>
/// Cuerpo de <c>PATCH /atracciones/{id}</c> (CORRECCIONES_CONTRATO.md §2.3).
/// Cambios parciales: una propiedad omitida o <c>null</c> no se modifica. No es posible anular campos obligatorios.
/// Las propiedades de solo lectura (<c>id</c>, <c>ratings</c>, <c>url</c>, <c>_links</c>) no se aceptan.
/// </summary>
public sealed class UpdateAttractionRequest
{
    [StringLength(200, MinimumLength = 3)]
    public string? Name { get; set; }

    [StringLength(5000, MinimumLength = 1)]
    public string? LongDescription { get; set; }

    [RegularExpression(Common.ValidationPatterns.IsoDuration, ErrorMessage = "duration debe ser una duración ISO 8601, por ejemplo PT3H.")]
    public string? Duration { get; set; }

    public Common.PriceDto? Price { get; set; }

    [MinLength(1)]
    public List<string>? Categories { get; set; }

    public List<string>? Badges { get; set; }

    [MinLength(1)]
    public List<LocationDto>? Locations { get; set; }

    public List<PhotoDto>? Photos { get; set; }

    public OperatorDto? Operator { get; set; }

    public ProductType? ProductType { get; set; }

    public List<string>? Includes { get; set; }

    public List<string>? SupportedLanguages { get; set; }

    public bool? FreeCancellation { get; set; }
}

public sealed class AttractionResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string LongDescription { get; init; } = string.Empty;

    public string Duration { get; init; } = string.Empty;

    public Common.PriceDto Price { get; init; } = new();

    public IReadOnlyList<string> Categories { get; init; } = [];

    public IReadOnlyList<string> Badges { get; init; } = [];

    public IReadOnlyList<LocationDto> Locations { get; init; } = [];

    public IReadOnlyList<PhotoDto> Photos { get; init; } = [];

    public OperatorDto? Operator { get; init; }

    public ProductType ProductType { get; init; }

    public IReadOnlyList<string> Includes { get; init; } = [];

    public IReadOnlyList<string> SupportedLanguages { get; init; } = [];

    public bool FreeCancellation { get; init; }

    public RatingDto? Ratings { get; init; }

    public UrlDto? Url { get; init; }

    /// <summary>Hipervínculos generados por la API; no se persisten.</summary>
    [JsonPropertyName("_links")]
    public IReadOnlyDictionary<string, string> Links { get; init; } = new Dictionary<string, string>();
}

/// <summary>Respuesta de <c>GET /atracciones</c>.</summary>
public sealed class PaginatedAttractionResponse
{
    public IReadOnlyList<AttractionResponse> Data { get; init; } = [];

    public PaginationMeta Meta { get; init; } = new();
}

public sealed class PaginationMeta
{
    public int TotalItems { get; init; }

    public int ItemCount { get; init; }

    public int ItemsPerPage { get; init; }

    public int TotalPages { get; init; }

    public int CurrentPage { get; init; }
}

/// <summary>Parámetros de <c>GET /atracciones</c>.</summary>
public sealed class ListAttractionsParameters
{
    [Range(1, 100)]
    public int Limit { get; set; } = 10;

    [Range(0, int.MaxValue)]
    public int Offset { get; set; }
}
