using AtraccionesService.API.Authorization;
using AtraccionesService.API.Mapping;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Queries.Attractions;
using AtraccionesService.Contracts.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AtraccionesService.API.Controllers;

[Route("atracciones/{id:guid}/availability")]
[Tags("Atracciones - Catálogo")]
public sealed class AvailabilityController(IAvailabilityService availability) : ApiControllerBase
{
    /// <param name="id">Identificador de la atracción.</param>
    /// <param name="date">Fecha local de la atracción (<c>yyyy-MM-dd</c>).</param>
    [HttpGet]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Consultar disponibilidad de cupos")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AvailabilityResponse>> Get(Guid id, [FromQuery, BindRequired] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await availability.GetAsync(new GetAttractionAvailabilityQuery(id, date), cancellationToken);
        return result.ToResponse();
    }
}
