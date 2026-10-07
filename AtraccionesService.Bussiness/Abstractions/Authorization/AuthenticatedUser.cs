namespace AtraccionesService.Application.Abstractions.Authorization;

/// <summary>
/// Identidad OAuth2 ya validada por la API. <see cref="Issuer"/> + <see cref="Subject"/> identifican al usuario;
/// nunca se obtienen del cuerpo de la solicitud.
/// </summary>
public sealed record AuthenticatedUser(
    string Issuer,
    string Subject,
    string? Email,
    bool? EmailVerified);

/// <summary>Resuelve la identidad autenticada de la operación en curso. La implementa el host.</summary>
public interface ICurrentUserService
{
    /// <summary>Devuelve el usuario autenticado o lanza <c>UnauthorizedException</c>.</summary>
    AuthenticatedUser GetRequiredUser();
}

/// <summary>Consulta roles/permisos locales del usuario. No gestiona scopes del issuer.</summary>
public interface ILocalPermissionService
{
    Task<bool> HasPermissionAsync(AuthenticatedUser user, string permission, CancellationToken cancellationToken);
}

/// <summary>Permisos locales consultados por la API además de los scopes OAuth2.</summary>
public static class LocalPermissions
{
    public const string CatalogWrite = "catalog:write";
}
