using AtraccionesService.API.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Controllers;

/// <summary>
/// Base de los controladores: solo coordinan HTTP y casos de uso. Las rutas se declaran sin <c>/api/v1</c>;
/// el prefijo lo agrega <see cref="Routing.ApiRoutePrefixConvention"/>.
/// </summary>
// Sin [Produces]: forzaría application/json también en los errores, que deben ser application/problem+json.
[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected const string IdempotencyHeader = RequireIdempotencyKeyAttribute.HeaderName;

    protected static string ApiPath(string relativePath) => $"/{Routing.ApiRoutePrefixConvention.Prefix}/{relativePath}";
}
