namespace AtraccionesService.DataAcess.Entities.Catalog;

/// <summary>Recurso principal del catálogo. <c>_links</c> no se persiste.</summary>
public class AttractionEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string LongDescription { get; set; } = string.Empty;

    public string Duration { get; set; } = string.Empty;

    /// <summary>Precio embebido (owned): columnas <c>price_currency</c> y <c>price_amount</c>.</summary>
    public PriceValue Price { get; set; } = new();

    public string ProductType { get; set; } = string.Empty;

    public bool FreeCancellation { get; set; }

    public int? OperatorId { get; set; }

    public OperatorEntity? Operator { get; set; }

    /// <summary>Calificación agregada (ambas columnas nulas = sin calificación).</summary>
    public int? RatingReviewCount { get; set; }

    public double? RatingScore { get; set; }

    public string? UrlWeb { get; set; }

    public string? UrlApp { get; set; }

    public List<CategoryEntity> Categories { get; set; } = [];

    public List<BadgeEntity> Badges { get; set; } = [];

    public List<InclusionEntity> Inclusions { get; set; } = [];

    public List<LanguageEntity> Languages { get; set; } = [];

    public List<LocationEntity> Locations { get; set; } = [];

    public List<PhotoEntity> Photos { get; set; } = [];
}

public class PriceValue
{
    public string Currency { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

public class OperatorEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>Valor de catálogo reutilizable (tabla propia con nombre único).</summary>
public interface ICatalogValue
{
    int Id { get; set; }

    string Name { get; set; }
}

public class CategoryEntity : ICatalogValue
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class BadgeEntity : ICatalogValue
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class InclusionEntity : ICatalogValue
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class LanguageEntity : ICatalogValue
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class LocationEntity
{
    public Guid Id { get; set; }

    public Guid AttractionId { get; set; }

    public int Position { get; set; }

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Type { get; set; }
}

public class PhotoEntity
{
    public Guid Id { get; set; }

    public Guid AttractionId { get; set; }

    public int Position { get; set; }

    public string Url { get; set; } = string.Empty;
}

/// <summary>Franja reservable; <c>AvailableSpots</c> se calcula y no se persiste. <see cref="Version"/> es token de concurrencia.</summary>
public class AttractionAvailabilityEntity
{
    public Guid Id { get; set; }

    public Guid AttractionId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Time { get; set; }

    public int Capacity { get; set; }

    public int ReservedQuantity { get; set; }

    public long Version { get; set; }
}

public class ReservationEntity
{
    public Guid Id { get; set; }

    public Guid AttractionId { get; set; }

    public Guid CustomerId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Time { get; set; }

    public int TicketCount { get; set; }

    public PriceValue TotalPrice { get; set; } = new();

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>Razón interna; no se expone en el contrato actual.</summary>
    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
