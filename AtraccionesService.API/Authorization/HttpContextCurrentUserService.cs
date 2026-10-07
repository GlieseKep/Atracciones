using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Exceptions;

namespace AtraccionesService.API.Authorization;

/// <summary>
/// Obtiene la identidad de los claims verificados del access token (<c>iss</c>, <c>sub</c>, <c>email</c>, <c>email_verified</c>).
/// Nunca usa identificadores enviados en el cuerpo.
/// </summary>
public sealed class HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public AuthenticatedUser GetRequiredUser()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedException("Se requiere un access token OAuth2 válido.");
        }

        var subjectClaim = principal.FindFirst("sub");
        if (subjectClaim is null || string.IsNullOrWhiteSpace(subjectClaim.Value))
        {
            throw new UnauthorizedException("El access token no contiene el claim 'sub'.");
        }

        var issuer = principal.FindFirst("iss")?.Value ?? subjectClaim.Issuer;
        var email = principal.FindFirst("email")?.Value;
        bool? emailVerified = bool.TryParse(principal.FindFirst("email_verified")?.Value, out var verified) ? verified : null;

        return new AuthenticatedUser(issuer, subjectClaim.Value, email, emailVerified);
    }
}
