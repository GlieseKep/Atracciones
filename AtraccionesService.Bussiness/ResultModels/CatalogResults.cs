using AtraccionesService.Application.Commands.Attractions;

namespace AtraccionesService.Application.ResultModels;

public sealed record RatingResult(int NumberOfReviews, double Score);

public sealed record UrlResult(string? Web, string? App);

public sealed record AttractionResult(Guid Id, AttractionData Data, RatingResult? Ratings, UrlResult? Url);

public sealed record SearchAttractionsResult(IReadOnlyList<AttractionResult> Items, int TotalResults, string? NextPage);

/// <summary>Disponibilidad de una fecha local de la atracción (zona horaria IANA).</summary>
public sealed record AvailabilityResult(
    DateOnly Date,
    string TimeZone,
    int AvailableSpots,
    IReadOnlyList<AvailabilitySlotResult> Slots);

public sealed record AvailabilitySlotResult(string Time, int AvailableSpots);
