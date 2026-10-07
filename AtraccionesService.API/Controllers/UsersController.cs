using AtraccionesService.API.Authorization;
using AtraccionesService.API.Mapping;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Contracts.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Controllers;

[Route("users")]
[Tags("Identidad")]
public sealed class UsersController(IUserProfileService profiles, ICurrentUserService currentUser) : ApiControllerBase
{
    [HttpGet("me")]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Obtener la identidad del usuario autenticado")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken cancellationToken)
    {
        var result = await profiles.GetCurrentAsync(currentUser.GetRequiredUser(), cancellationToken);
        return result.ToResponse();
    }
}
