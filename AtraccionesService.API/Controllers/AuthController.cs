using System.ComponentModel.DataAnnotations;
using AtraccionesService.API.Authorization;
using AtraccionesService.API.Mapping;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Contracts.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Controllers;

/// <summary>
/// Aprovisionamiento del perfil local tras autenticarse en el issuer OAuth2. No existe login, logout ni emisión de tokens:
/// esas operaciones pertenecen al proveedor OAuth2 (Authorization Code + PKCE).
/// </summary>
[Route("auth")]
[Tags("Identidad")]
public sealed class AuthController(IUserProfileService profiles, ICurrentUserService currentUser) : ApiControllerBase
{
    /// <summary>
    /// Crea el usuario local y su cliente usando los claims verificados <c>sub</c> y <c>email</c>.
    /// Repetir la operación devuelve el perfil existente (200) sin duplicarlo.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Policy = ApiPolicies.Book)]
    [EndpointSummary("Aprovisionar el perfil local del usuario autenticado")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<RegisterProfileResponse>> Register(RegisterProfileRequest request, CancellationToken cancellationToken)
    {
        var user = currentUser.GetRequiredUser();
        if (string.IsNullOrWhiteSpace(user.Email) || user.EmailVerified == false || !new EmailAddressAttribute().IsValid(user.Email))
        {
            throw new Application.Exceptions.ValidationException(
                "El access token no contiene un claim 'email' verificado y válido.");
        }

        var result = await profiles.RegisterAsync(new RegisterUserCommand(user, user.Email, request.ToBilling()), cancellationToken);
        var response = result.ToResponse();

        return result.Created
            ? Created(ApiPath("users/me"), response)
            : Ok(response);
    }
}
