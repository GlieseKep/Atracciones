using AtraccionesService.API.Authorization;
using AtraccionesService.API.Filters;
using AtraccionesService.API.Mapping;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Contracts.Ecommerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Controllers;

/// <summary>
/// Pasarela simulada. Application determina el resultado y registra <c>payment_attempts</c> y <c>payment_events</c>
/// internamente; no hay endpoints públicos para intentos ni eventos. No se aceptan datos de tarjeta.
/// </summary>
[Route("payments")]
[Tags("Pagos simulados")]
public sealed class PaymentsController(IPaymentSimulationService payments, ICurrentUserService currentUser) : ApiControllerBase
{
    [HttpPost("simulations")]
    [Authorize(Policy = ApiPolicies.Book)]
    [RequireIdempotencyKey]
    [EndpointSummary("Crear una simulación de pago para un pedido")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<PaymentSimulationResponse>> Simulate(
        CreatePaymentSimulationRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await payments.SimulateAsync(
            new SimulatePaymentCommand(
                request.OrderId!.Value,
                request.PaymentMethod!.Value.ToString(),
                request.Amount,
                request.Currency,
                idempotencyKey,
                currentUser.GetRequiredUser()),
            cancellationToken);

        // El estado del pago se consulta mediante el pedido.
        return Created(ApiPath($"orders/{result.OrderId}"), result.ToResponse());
    }
}
