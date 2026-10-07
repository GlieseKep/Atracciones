using AtraccionesService.API.Authorization;
using AtraccionesService.API.Filters;
using AtraccionesService.API.Mapping;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Attractions;
using AtraccionesService.Application.Queries.Attractions;
using AtraccionesService.Contracts.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Controllers;

/// <summary>Catálogo de atracciones. Las escrituras exigen <c>attractions:write</c> y el permiso local de catálogo.</summary>
[Route("atracciones")]
[Tags("Atracciones - Catálogo")]
public sealed class AttractionsController(IAttractionService attractions, ICurrentUserService currentUser) : ApiControllerBase
{
    [HttpPost("search")]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Búsqueda de atracciones")]
    public async Task<ActionResult<SearchAttractionsResponse>> Search(SearchAttractionsRequest request, CancellationToken cancellationToken)
    {
        var result = await attractions.SearchAsync(
            new SearchAttractionsQuery(
                request.Currency,
                request.Cities,
                request.Countries,
                request.Dates?.StartDate,
                request.Dates?.EndDate,
                request.Filters?.Rating?.MinimumReviewScore,
                request.Filters?.Rating?.MinimumReviewCount,
                request.NextPage,
                request.Rows,
                request.Sort?.By ?? "most_popular"),
            cancellationToken);

        return new SearchAttractionsResponse
        {
            Data = result.Items.Select(a => a.ToResponse(SelfLink(a.Id))).ToList(),
            Metadata = new SearchMetadata { TotalResults = result.TotalResults, NextPage = result.NextPage },
            RequestId = HttpContext.TraceIdentifier,
        };
    }

    [HttpPost("details")]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Obtener detalles estáticos de múltiples atracciones (batch)")]
    public async Task<ActionResult<SearchAttractionsResponse>> Details(DetailsRequest request, CancellationToken cancellationToken)
    {
        var result = await attractions.GetDetailsAsync(new GetAttractionDetailsQuery(request.Attractions, request.Languages), cancellationToken);

        return new SearchAttractionsResponse
        {
            Data = result.Select(a => a.ToResponse(SelfLink(a.Id))).ToList(),
            Metadata = new SearchMetadata { TotalResults = result.Count },
            RequestId = HttpContext.TraceIdentifier,
        };
    }

    [HttpGet]
    [Authorize(Policy = ApiPolicies.Read)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [EndpointSummary("Obtener el listado paginado de atracciones")]
    public async Task<ActionResult<PaginatedAttractionResponse>> List([FromQuery] ListAttractionsParameters parameters, CancellationToken cancellationToken)
    {
        var page = await attractions.ListAsync(new ListAttractionsQuery(parameters.Limit, parameters.Offset), cancellationToken);

        return new PaginatedAttractionResponse
        {
            Data = page.Items.Select(a => a.ToResponse(SelfLink(a.Id))).ToList(),
            Meta = new PaginationMeta
            {
                TotalItems = page.TotalItems,
                ItemCount = page.Items.Count,
                ItemsPerPage = page.Limit,
                TotalPages = page.TotalPages,
                CurrentPage = page.CurrentPage,
            },
        };
    }

    [HttpPost]
    [Authorize(Policy = ApiPolicies.CatalogWrite)]
    [RequireIdempotencyKey]
    [EndpointSummary("Registrar una nueva atracción")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AttractionResponse>> Create(
        CreateAttractionRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await attractions.CreateAsync(
            new CreateAttractionCommand(request.ToData(), idempotencyKey, currentUser.GetRequiredUser()),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result.ToResponse(SelfLink(result.Id)));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApiPolicies.Read)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [EndpointSummary("Obtener el detalle de una atracción")]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AttractionResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await attractions.GetAsync(new GetAttractionQuery(id), cancellationToken);
        return result.ToResponse(SelfLink(result.Id));
    }

    /// <summary>Reemplaza el recurso completo; las listas omitidas quedan vacías.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApiPolicies.CatalogWrite)]
    [RequireIdempotencyKey]
    [EndpointSummary("Reemplazar datos completos de una atracción")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Replace(
        Guid id,
        CreateAttractionRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        await attractions.ReplaceAsync(
            new ReplaceAttractionCommand(id, request.ToData(), idempotencyKey, currentUser.GetRequiredUser()),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Cambio parcial: solo se modifican las propiedades presentes y no nulas.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = ApiPolicies.CatalogWrite)]
    [RequireIdempotencyKey]
    [EndpointSummary("Actualizar parcialmente una atracción")]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AttractionResponse>> Patch(
        Guid id,
        UpdateAttractionRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await attractions.PatchAsync(
            new PatchAttractionCommand(id, request.ToPatch(), idempotencyKey, currentUser.GetRequiredUser()),
            cancellationToken);
        return result.ToResponse(SelfLink(result.Id));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApiPolicies.CatalogWrite)]
    [RequireIdempotencyKey]
    [EndpointSummary("Eliminar una atracción")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        await attractions.DeleteAsync(new DeleteAttractionCommand(id, idempotencyKey, currentUser.GetRequiredUser()), cancellationToken);
        return NoContent();
    }

    private static string SelfLink(Guid id) => ApiPath($"atracciones/{id}");
}
