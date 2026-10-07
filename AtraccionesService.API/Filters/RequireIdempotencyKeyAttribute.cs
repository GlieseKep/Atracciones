using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AtraccionesService.API.Filters;

/// <summary>
/// Exige la cabecera <c>Idempotency-Key</c> con un UUID canónico (CORRECCIONES_CONTRATO.md §3.3).
/// La API valida presencia y formato; Application garantiza la semántica de replay y conflictos.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireIdempotencyKeyAttribute : Attribute, IActionFilter
{
    public const string HeaderName = "Idempotency-Key";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var values = context.HttpContext.Request.Headers[HeaderName];
        string? error = null;

        if (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
        {
            error = $"La cabecera '{HeaderName}' es obligatoria en esta operación.";
        }
        else if (values.Count > 1 || !Guid.TryParseExact(values[0], "D", out var key) || key == Guid.Empty)
        {
            error = $"La cabecera '{HeaderName}' debe contener un único UUID con formato xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.";
        }

        if (error is null)
        {
            return;
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            title: "Idempotency-Key inválida",
            type: "urn:atracciones:problems:invalid-idempotency-key",
            detail: error);
        problem.Extensions["code"] = "INVALID_IDEMPOTENCY_KEY";

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
