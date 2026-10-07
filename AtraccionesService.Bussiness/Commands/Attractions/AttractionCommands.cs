using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.ResultModels;

namespace AtraccionesService.Application.Commands.Attractions;

public sealed record LocationData(
    string Address,
    string City,
    string Country,
    double? Latitude,
    double? Longitude,
    string? Type);

public sealed record OperatorData(int Id, string Name);

/// <summary>Datos completos y editables de una atracción.</summary>
public sealed record AttractionData(
    string Name,
    string LongDescription,
    string Duration,
    Money Price,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Badges,
    IReadOnlyList<LocationData> Locations,
    IReadOnlyList<string> PhotoUrls,
    OperatorData? Operator,
    string ProductType,
    IReadOnlyList<string> Includes,
    IReadOnlyList<string> SupportedLanguages,
    bool FreeCancellation);

/// <summary>Cambios parciales: <c>null</c> significa "sin cambios".</summary>
public sealed record AttractionPatch(
    string? Name,
    string? LongDescription,
    string? Duration,
    Money? Price,
    IReadOnlyList<string>? Categories,
    IReadOnlyList<string>? Badges,
    IReadOnlyList<LocationData>? Locations,
    IReadOnlyList<string>? PhotoUrls,
    OperatorData? Operator,
    string? ProductType,
    IReadOnlyList<string>? Includes,
    IReadOnlyList<string>? SupportedLanguages,
    bool? FreeCancellation);

public sealed record CreateAttractionCommand(AttractionData Data, Guid IdempotencyKey, AuthenticatedUser Actor);

public sealed record ReplaceAttractionCommand(Guid AttractionId, AttractionData Data, Guid IdempotencyKey, AuthenticatedUser Actor);

public sealed record PatchAttractionCommand(Guid AttractionId, AttractionPatch Patch, Guid IdempotencyKey, AuthenticatedUser Actor);

public sealed record DeleteAttractionCommand(Guid AttractionId, Guid IdempotencyKey, AuthenticatedUser Actor);
