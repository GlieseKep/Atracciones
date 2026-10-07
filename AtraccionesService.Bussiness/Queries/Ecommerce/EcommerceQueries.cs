using AtraccionesService.Application.Abstractions.Authorization;

namespace AtraccionesService.Application.Queries.Ecommerce;

public sealed record GetOrderQuery(Guid OrderId, AuthenticatedUser User);

public sealed record GetOrderEventsQuery(Guid OrderId, AuthenticatedUser User);
