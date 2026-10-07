using AtraccionesService.API.Authorization;
using AtraccionesService.API.Mapping;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Contracts.Common;
using AtraccionesService.Contracts.Identity;
using AtraccionesService.Contracts.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Controllers;

/// <summary>
/// Cliente y facturación del usuario autenticado. El <c>customerId</c> se resuelve desde la relación usuario-cliente;
/// no se aceptan identificadores de usuario o cliente en la solicitud.
/// </summary>
[Route("customers/me")]
[Tags("Clientes")]
public sealed class CustomersController(
    ICustomerService customers,
    IReservationService reservations,
    ICurrentUserService currentUser) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Obtener el cliente del usuario autenticado")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CustomerResponse>> Get(CancellationToken cancellationToken)
    {
        var result = await customers.GetCurrentAsync(currentUser.GetRequiredUser(), cancellationToken);
        return result.ToResponse();
    }

    /// <summary>Reemplaza todos los datos de facturación.</summary>
    [HttpPut]
    [Authorize(Policy = ApiPolicies.Book)]
    [EndpointSummary("Actualizar los datos de facturación")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CustomerResponse>> Update(UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await customers.UpdateCurrentAsync(
            new UpdateCustomerCommand(currentUser.GetRequiredUser(), request.ToBilling()),
            cancellationToken);
        return result.ToResponse();
    }

    [HttpGet("reservations")]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Consultar reservas del usuario autenticado")]
    public async Task<ActionResult<PagedResponse<ReservationResponse>>> Reservations(
        [FromQuery] ReservationListParameters parameters,
        CancellationToken cancellationToken)
    {
        var page = await reservations.ListAsync(parameters.ToQuery(currentUser.GetRequiredUser()), cancellationToken);
        return page.ToPagedResponse(r => r.ToResponse());
    }
}
