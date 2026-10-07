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
/// Compra directa de una experiencia: Application valida disponibilidad, calcula el precio y crea pedido/pago en una
/// sola operación transaccional. No existe carrito ni sesión de checkout.
/// </summary>
[Route("attractions")]
[Tags("Compras")]
public sealed class PurchasesController(IPurchaseService purchases, ICurrentUserService currentUser) : ApiControllerBase
{
    [HttpPost("{attractionId:guid}/purchase")]
    [Authorize(Policy = ApiPolicies.Book)]
    [RequireIdempotencyKey]
    [EndpointSummary("Comprar una franja de una atracción")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<PurchaseResponse>> Purchase(
        Guid attractionId,
        CreatePurchaseRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await purchases.CreateAsync(
            new CreatePurchaseCommand(
                attractionId,
                request.Date!.Value,
                request.Time,
                request.Quantity,
                request.PaymentMethod?.ToString(),
                idempotencyKey,
                currentUser.GetRequiredUser()),
            cancellationToken);

        return Created(ApiPath($"orders/{result.OrderId}"), result.ToResponse());
    }
}
