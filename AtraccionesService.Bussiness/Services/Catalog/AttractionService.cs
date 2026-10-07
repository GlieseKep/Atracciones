using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Idempotency;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Attractions;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Mappers;
using AtraccionesService.Application.Queries.Attractions;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.Application.Validators;
using AtraccionesService.DataManagment.Contracts.Catalog;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Ecommerce;
using Money = AtraccionesService.Domain.Common.Money;

namespace AtraccionesService.Application.Services.Catalog;

/// <summary>
/// Catálogo: búsqueda, detalle y mantenimiento. Las escrituras son administrativas: verifican el permiso local,
/// son idempotentes y registran auditoría.
/// </summary>
public sealed class AttractionService(
    IUnitOfWork unitOfWork,
    IIdempotencyService idempotency,
    PermissionGuard permissions,
    PageTokenService pageTokens,
    BusinessClock clock) : IAttractionService
{
    private static readonly IReadOnlyDictionary<string, AttractionSort> SortOptions = new Dictionary<string, AttractionSort>
    {
        ["most_popular"] = AttractionSort.MostPopular,
        ["price_asc"] = AttractionSort.PriceAscending,
        ["price_desc"] = AttractionSort.PriceDescending,
        ["rating_desc"] = AttractionSort.RatingDescending,
    };

    public async Task<SearchAttractionsResult> SearchAsync(SearchAttractionsQuery query, CancellationToken cancellationToken)
    {
        new ValidationErrors()
            .When(query.Rows < 1 || query.Rows > PaginationValidator.MaxLimit, "rows", $"rows debe estar entre 1 y {PaginationValidator.MaxLimit}.")
            .When(query.Currency is not null && !Money.IsValidCurrency(query.Currency), "currency", "currency debe ser un código ISO 4217.")
            .When(!SortOptions.ContainsKey(query.SortBy), "sort.by", "sort.by no es un criterio de orden permitido.")
            .When(query.Countries.Any(c => !AttractionValidator.IsCountry(c)), "countries", "Los países deben ser códigos ISO 3166-1 alfa-2.")
            .When(query.StartDate is { } s && query.EndDate is { } e && e < s, "dates", "endDate debe ser igual o posterior a startDate.")
            .When(query.MinimumReviewScore is < 0 or > 5, "filters.rating.minimumReviewScore", "Debe estar entre 0 y 5.")
            .When(query.MinimumReviewCount is < 0, "filters.rating.minimumReviewCount", "No puede ser negativo.")
            .ThrowIfAny();

        var criteria = new AttractionSearchCriteria(
            query.Currency, query.Cities, query.Countries, query.StartDate, query.EndDate,
            query.MinimumReviewScore, query.MinimumReviewCount, SortOptions[query.SortBy]);

        // El token queda ligado a los criterios y al tamaño de página: no puede reutilizarse con otra búsqueda.
        var criteriaHash = PageTokenService.HashCriteria(new { criteria, query.Rows });
        var offset = string.IsNullOrEmpty(query.NextPage) ? 0 : pageTokens.ReadOffset(query.NextPage, criteriaHash);

        var page = await unitOfWork.Attractions.SearchAsync(criteria, new PaginationRequest(query.Rows, offset), cancellationToken);
        var nextOffset = offset + page.Items.Count;
        var nextPage = page.Items.Count > 0 && nextOffset < page.TotalItems ? pageTokens.Create(nextOffset, criteriaHash) : null;

        return new SearchAttractionsResult(page.Items.Select(a => a.ToResult()).ToList(), page.TotalItems, nextPage);
    }

    public async Task<IReadOnlyList<AttractionResult>> GetDetailsAsync(GetAttractionDetailsQuery query, CancellationToken cancellationToken)
    {
        new ValidationErrors()
            .When(query.AttractionIds.Count is 0 or > PaginationValidator.MaxLimit, "attractions", $"Se aceptan entre 1 y {PaginationValidator.MaxLimit} identificadores.")
            .When(query.Languages.Any(l => !AttractionValidator.IsLanguage(l)), "languages", "Los idiomas deben ser códigos ISO 639-1.")
            .ThrowIfAny();

        var ids = query.AttractionIds.Distinct().ToList();
        var found = (await unitOfWork.Attractions.GetByIdsAsync(ids, cancellationToken)).ToDictionary(a => a.Id);

        // Se conserva el orden solicitado; los identificadores inexistentes se omiten del lote.
        return ids.Where(found.ContainsKey).Select(id => found[id].ToResult()).ToList();
    }

    public async Task<PaginationResult<AttractionResult>> ListAsync(ListAttractionsQuery query, CancellationToken cancellationToken)
    {
        PaginationValidator.Validate(query.Limit, query.Offset);
        var page = await unitOfWork.Attractions.ListAsync(new PaginationRequest(query.Limit, query.Offset), cancellationToken);
        return new PaginationResult<AttractionResult>(page.Items.Select(a => a.ToResult()).ToList(), page.TotalItems, query.Limit, query.Offset);
    }

    public async Task<AttractionResult> GetAsync(GetAttractionQuery query, CancellationToken cancellationToken) =>
        (await unitOfWork.Attractions.GetByIdAsync(query.AttractionId, cancellationToken) ?? throw NotFound()).ToResult();

    public async Task<AttractionResult> CreateAsync(CreateAttractionCommand command, CancellationToken cancellationToken)
    {
        await permissions.EnsureAsync(command.Actor, LocalPermissions.CatalogWrite, cancellationToken);
        AttractionValidator.Validate(command.Data);

        return await idempotency.ExecuteAsync(command.Actor, IdempotentOperations.CreateAttraction, command.IdempotencyKey, command.Data, async ct =>
        {
            var attraction = Attraction.Create(command.Data.ToDomain());
            await unitOfWork.Attractions.AddAsync(attraction, ct);
            await AuditAsync(command.Actor, "attraction.create", attraction.Id, ct);
            return attraction.ToResult();
        }, cancellationToken);
    }

    public async Task ReplaceAsync(ReplaceAttractionCommand command, CancellationToken cancellationToken)
    {
        await permissions.EnsureAsync(command.Actor, LocalPermissions.CatalogWrite, cancellationToken);
        AttractionValidator.Validate(command.Data);

        await idempotency.ExecuteAsync(command.Actor, IdempotentOperations.ReplaceAttraction, command.IdempotencyKey,
            new { command.AttractionId, command.Data }, async ct =>
            {
                var attraction = await unitOfWork.Attractions.GetByIdAsync(command.AttractionId, ct) ?? throw NotFound();
                attraction.Replace(command.Data.ToDomain());
                await unitOfWork.Attractions.UpdateAsync(attraction, ct);
                await AuditAsync(command.Actor, "attraction.replace", attraction.Id, ct);
                return true;
            }, cancellationToken);
    }

    public async Task<AttractionResult> PatchAsync(PatchAttractionCommand command, CancellationToken cancellationToken)
    {
        await permissions.EnsureAsync(command.Actor, LocalPermissions.CatalogWrite, cancellationToken);

        return await idempotency.ExecuteAsync(command.Actor, IdempotentOperations.PatchAttraction, command.IdempotencyKey,
            new { command.AttractionId, command.Patch }, async ct =>
            {
                var attraction = await unitOfWork.Attractions.GetByIdAsync(command.AttractionId, ct) ?? throw NotFound();
                var merged = attraction.Details.ToData().Merge(command.Patch);
                AttractionValidator.Validate(merged);
                attraction.Replace(merged.ToDomain());
                await unitOfWork.Attractions.UpdateAsync(attraction, ct);
                await AuditAsync(command.Actor, "attraction.patch", attraction.Id, ct);
                return attraction.ToResult();
            }, cancellationToken);
    }

    /// <summary>
    /// Eliminación física. Se bloquea con 409 si la atracción tiene reservas activas futuras, para no dejar reservas huérfanas.
    /// </summary>
    public async Task DeleteAsync(DeleteAttractionCommand command, CancellationToken cancellationToken)
    {
        await permissions.EnsureAsync(command.Actor, LocalPermissions.CatalogWrite, cancellationToken);

        await idempotency.ExecuteAsync(command.Actor, IdempotentOperations.DeleteAttraction, command.IdempotencyKey,
            new { command.AttractionId }, async ct =>
            {
                if (!await unitOfWork.Attractions.ExistsAsync(command.AttractionId, ct))
                {
                    throw NotFound();
                }

                if (await unitOfWork.Reservations.HasActiveByAttractionAsync(command.AttractionId, clock.Today, ct))
                {
                    throw new ConflictException("ATTRACTION_HAS_ACTIVE_RESERVATIONS",
                        "La atracción tiene reservas activas futuras y no puede eliminarse.");
                }

                await unitOfWork.Attractions.DeleteAsync(command.AttractionId, ct);
                await AuditAsync(command.Actor, "attraction.delete", command.AttractionId, ct);
                return true;
            }, cancellationToken);
    }

    private async Task AuditAsync(AuthenticatedUser actor, string action, Guid attractionId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Users.GetByIdentityAsync(actor.Issuer, actor.Subject, cancellationToken);
        await unitOfWork.AuditEvents.AddAsync(
            new AuditEvent(Guid.NewGuid(), user?.Id, actor.Subject, action, "attraction", attractionId.ToString(), null, clock.UtcNow),
            cancellationToken);
    }

    private static NotFoundException NotFound() => new("La atracción no existe.");
}
