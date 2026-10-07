using AtraccionesService.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AtraccionesService.API.Authorization;

/// <summary>Scopes OAuth2 declarados en el contrato (CONTRATO.md + CORRECCIONES_CONTRATO.md §2.4).</summary>
public static class ApiScopes
{
    public const string Read = "attractions:read";
    public const string Book = "attractions:book";
    public const string Write = "attractions:write";
    public const string Cancel = "attractions:cancel";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Read] = "Leer catálogo, disponibilidad, perfil, reservas y pedidos propios",
        [Book] = "Hacer reservas, compras, pedidos y pagos simulados",
        [Write] = "Crear y mantener inventario",
        [Cancel] = "Cancelar reservas y pedidos",
    };
}

/// <summary>Políticas por operación: scope OAuth2 y, para escrituras de catálogo, permiso local.</summary>
public static class ApiPolicies
{
    public const string Read = "attractions.read";
    public const string Book = "attractions.book";
    public const string CatalogWrite = "attractions.write";
    public const string Cancel = "attractions.cancel";

    /// <summary>Scope requerido por cada política; lo usa también el documento OpenAPI.</summary>
    public static readonly IReadOnlyDictionary<string, string> Scopes = new Dictionary<string, string>
    {
        [Read] = ApiScopes.Read,
        [Book] = ApiScopes.Book,
        [CatalogWrite] = ApiScopes.Write,
        [Cancel] = ApiScopes.Cancel,
    };

    public static void Configure(AuthorizationOptions options)
    {
        foreach (var (policy, scope) in Scopes)
        {
            options.AddPolicy(policy, builder =>
            {
                builder.RequireAuthenticatedUser();
                builder.AddRequirements(new ScopeRequirement(scope));
                if (policy == CatalogWrite)
                {
                    builder.AddRequirements(new LocalPermissionRequirement(LocalPermissions.CatalogWrite));
                }
            });
        }
    }
}

public sealed record ScopeRequirement(string Scope) : IAuthorizationRequirement;

/// <summary>Valida el scope en los claims <c>scope</c> (separado por espacios) o <c>scp</c>.</summary>
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var granted = context.User.FindAll(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (granted.Contains(requirement.Scope, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed record LocalPermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Consulta los roles/permisos locales; un token válido no basta para operaciones administrativas.
/// Si Application aún no registra <see cref="ILocalPermissionService"/>, se deniega (fail-closed).
/// </summary>
public sealed class LocalPermissionAuthorizationHandler(
    ICurrentUserService currentUser,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<LocalPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, LocalPermissionRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (context.User.Identity?.IsAuthenticated != true
            || httpContext?.RequestServices.GetService<ILocalPermissionService>() is not { } permissions)
        {
            return;
        }

        if (await permissions.HasPermissionAsync(currentUser.GetRequiredUser(), requirement.Permission, httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
