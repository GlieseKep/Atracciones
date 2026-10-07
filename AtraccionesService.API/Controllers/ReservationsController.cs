using AtraccionesService.API.Authorization;
using AtraccionesService.API.Filters;
using AtraccionesService.API.Mapping;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Reservations;
using AtraccionesService.Application.Queries.Reservations;
using AtraccionesService.Contracts.Common;
using AtraccionesService.Contracts.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Controllers;

/// <summary>
/// Ciclo de vida de reservas. El propietario se obtiene del token; Application valida la pertenencia de cada reserva.
/// </summary>
[Route("atracciones")]
[Tags("Atracciones - Reservas")]
public sealed class ReservationsController(IReservationService reservations, ICurrentUserService currentUser) : ApiControllerBase
{
    [HttpPost("{id:guid}/reservations")]
    [Authorize(Policy = ApiPolicies.Book)]
    [RequireIdempotencyKey]
    [EndpointSummary("Crear una reserva de la atracción")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ReservationResponse>> Create(
        Guid id,
        ReservationRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await reservations.CreateAsync(
            new CreateReservationCommand(
                id,
                request.Date!.Value,
                request.Time,
                request.TicketCount,
                request.CustomerName,
                request.CustomerEmail,
                idempotencyKey,
                currentUser.GetRequiredUser()),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { reservationId = result.ReservationId }, result.ToResponse());
    }

    [HttpPost("reservations/{reservationId:guid}/cancel")]
    [Authorize(Policy = ApiPolicies.Cancel)]
    [RequireIdempotencyKey]
    [EndpointSummary("Cancelar una reserva")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ReservationResponse>> Cancel(
        Guid reservationId,
        CancelReservationRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await reservations.CancelAsync(
            new CancelReservationCommand(reservationId, request.Reason, idempotencyKey, currentUser.GetRequiredUser()),
            cancellationToken);
        return result.ToResponse();
    }

    [HttpGet("reservations")]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Historial paginado de reservas del usuario autenticado")]
    public async Task<ActionResult<PagedResponse<ReservationResponse>>> List(
        [FromQuery] ReservationListParameters parameters,
        CancellationToken cancellationToken)
    {
        var page = await reservations.ListAsync(parameters.ToQuery(currentUser.GetRequiredUser()), cancellationToken);
        return page.ToPagedResponse(r => r.ToResponse());
    }

    [HttpGet("reservations/{reservationId:guid}")]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Obtener detalle de una reserva propia")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ReservationResponse>> GetById(Guid reservationId, CancellationToken cancellationToken)
    {
        var result = await reservations.GetAsync(new GetReservationQuery(reservationId, currentUser.GetRequiredUser()), cancellationToken);
        return result.ToResponse();
    }
}

internal static class ReservationListParametersExtensions
{
    public static GetReservationsQuery ToQuery(this ReservationListParameters parameters, AuthenticatedUser user) => new(
        user,
        parameters.Limit,
        parameters.Offset,
        parameters.Status?.ToString(),
        parameters.FromDate,
        parameters.ToDate,
        SortDescending: parameters.Sort.StartsWith('-'));
}
