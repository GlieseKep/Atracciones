namespace AtraccionesService.Application.Queries.Attractions;

public sealed record SearchAttractionsQuery(
    string? Currency,
    IReadOnlyList<string> Cities,
    IReadOnlyList<string> Countries,
    DateOnly? StartDate,
    DateOnly? EndDate,
    double? MinimumReviewScore,
    int? MinimumReviewCount,
    string? NextPage,
    int Rows,
    string SortBy);

public sealed record GetAttractionDetailsQuery(IReadOnlyList<Guid> AttractionIds, IReadOnlyList<string> Languages);

public sealed record ListAttractionsQuery(int Limit, int Offset);

public sealed record GetAttractionQuery(Guid AttractionId);

public sealed record GetAttractionAvailabilityQuery(Guid AttractionId, DateOnly Date);
