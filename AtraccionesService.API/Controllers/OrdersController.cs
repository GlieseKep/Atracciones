using AtraccionesService.API.Authorization;
using AtraccionesService.API.Filters;
using AtraccionesService.API.Mapping;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Application.Queries.Ecommerce;
using AtraccionesService.Contracts.Ecommerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtraccionesService.API.Controllers;

/// <summary>Pedidos del usuario autenticado; Application aplica ownership y transiciones de estado.</summary>
[Route("orders")]
[Tags("Pedidos")]
public sealed class OrdersController(IOrderService orders, ICurrentUserService currentUser) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = ApiPolicies.Book)]
    [RequireIdempotencyKey]
    [EndpointSummary("Crear un pedido pendiente de pago")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await orders.CreateAsync(
            new CreateOrderCommand(
                request.AttractionId!.Value,
                request.Date!.Value,
                request.Time,
                request.Quantity,
                idempotencyKey,
                currentUser.GetRequiredUser()),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { orderId = result.Id }, result.ToResponse());
    }

    /// <summary>Incluye el estado y la referencia segura de la simulación de pago.</summary>
    [HttpGet("{orderId:guid}")]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Consultar un pedido propio")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await orders.GetAsync(new GetOrderQuery(orderId, currentUser.GetRequiredUser()), cancellationToken);
        return result.ToResponse();
    }

    [HttpGet("{orderId:guid}/events")]
    [Authorize(Policy = ApiPolicies.Read)]
    [EndpointSummary("Consultar el historial de estados de un pedido")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OrderEventsResponse>> Events(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await orders.GetEventsAsync(new GetOrderEventsQuery(orderId, currentUser.GetRequiredUser()), cancellationToken);
        return result.ToResponse();
    }

    [HttpPost("{orderId:guid}/cancel")]
    [Authorize(Policy = ApiPolicies.Cancel)]
    [RequireIdempotencyKey]
    [EndpointSummary("Cancelar un pedido")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrderResponse>> Cancel(
        Guid orderId,
        CancelOrderRequest request,
        [FromHeader(Name = IdempotencyHeader)] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await orders.CancelAsync(
            new CancelOrderCommand(orderId, request.Reason, idempotencyKey, currentUser.GetRequiredUser()),
            cancellationToken);
        return result.ToResponse();
    }
}
