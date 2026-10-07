using AtraccionesService.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Middleware;

/// <summary>
/// Traduce excepciones de Application a respuestas RFC 7807 <c>application/problem+json</c>.
/// Las excepciones no controladas producen un 500 genérico sin stack trace.
/// </summary>
public sealed class ProblemDetailsExceptionMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                return;
            }

            var problem = Map(exception);
            if (problem.Status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Error no controlado procesando {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                logger.LogInformation("Solicitud rechazada con {Status} ({Code})", problem.Status, problem.Extensions["code"]);
            }

            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem,
                Exception = exception,
            });
        }
    }

    private static ProblemDetails Map(Exception exception)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "No autenticado"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acceso denegado"),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto"),
            PaymentSimulationException => (StatusCodes.Status422UnprocessableEntity, "Pago simulado rechazado"),
            BusinessException => (StatusCodes.Status409Conflict, "Regla de negocio incumplida"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor"),
        };

        var code = exception is BusinessException business ? business.Code : "INTERNAL_ERROR";
        var problem = exception is ValidationException { Errors.Count: > 0 } validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Type = $"urn:atracciones:problems:{code.ToLowerInvariant().Replace('_', '-')}";
        problem.Detail = exception is BusinessException ? exception.Message : "Se produjo un error inesperado.";
        problem.Extensions["code"] = code;
        return problem;
    }
}
