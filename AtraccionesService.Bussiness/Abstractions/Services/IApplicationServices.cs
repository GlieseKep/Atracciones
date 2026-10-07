using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Commands.Attractions;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Application.Commands.Reservations;
using AtraccionesService.Application.Queries.Attractions;
using AtraccionesService.Application.Queries.Ecommerce;
using AtraccionesService.Application.Queries.Reservations;
using AtraccionesService.Application.ResultModels;

namespace AtraccionesService.Application.Abstractions.Services;

// Casos de uso expuestos por Application y consumidos por la API.
// Las implementaciones se definen en PLAN_IMPLEMENTACION_BUSINESS.md.

public interface IAttractionService
{
    Task<SearchAttractionsResult> SearchAsync(SearchAttractionsQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<AttractionResult>> GetDetailsAsync(GetAttractionDetailsQuery query, CancellationToken cancellationToken);

    Task<PaginationResult<AttractionResult>> ListAsync(ListAttractionsQuery query, CancellationToken cancellationToken);

    Task<AttractionResult> GetAsync(GetAttractionQuery query, CancellationToken cancellationToken);

    Task<AttractionResult> CreateAsync(CreateAttractionCommand command, CancellationToken cancellationToken);

    Task ReplaceAsync(ReplaceAttractionCommand command, CancellationToken cancellationToken);

    Task<AttractionResult> PatchAsync(PatchAttractionCommand command, CancellationToken cancellationToken);

    Task DeleteAsync(DeleteAttractionCommand command, CancellationToken cancellationToken);
}

public interface IAvailabilityService
{
    Task<AvailabilityResult> GetAsync(GetAttractionAvailabilityQuery query, CancellationToken cancellationToken);
}

public interface IReservationService
{
    Task<ReservationResult> CreateAsync(CreateReservationCommand command, CancellationToken cancellationToken);

    Task<ReservationResult> CancelAsync(CancelReservationCommand command, CancellationToken cancellationToken);

    Task<PaginationResult<ReservationResult>> ListAsync(GetReservationsQuery query, CancellationToken cancellationToken);

    Task<ReservationResult> GetAsync(GetReservationQuery query, CancellationToken cancellationToken);
}

public interface IUserProfileService
{
    Task<RegisteredUserResult> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken);

    Task<UserResult> GetCurrentAsync(AuthenticatedUser user, CancellationToken cancellationToken);
}

public interface ICustomerService
{
    Task<CustomerResult> GetCurrentAsync(AuthenticatedUser user, CancellationToken cancellationToken);

    Task<CustomerResult> UpdateCurrentAsync(UpdateCustomerCommand command, CancellationToken cancellationToken);
}

public interface IPurchaseService
{
    Task<PurchaseResult> CreateAsync(CreatePurchaseCommand command, CancellationToken cancellationToken);
}

public interface IOrderService
{
    Task<OrderResult> CreateAsync(CreateOrderCommand command, CancellationToken cancellationToken);

    Task<OrderResult> GetAsync(GetOrderQuery query, CancellationToken cancellationToken);

    Task<OrderEventsResult> GetEventsAsync(GetOrderEventsQuery query, CancellationToken cancellationToken);

    Task<OrderResult> CancelAsync(CancelOrderCommand command, CancellationToken cancellationToken);
}

public interface IPaymentSimulationService
{
    Task<PaymentSimulationResult> SimulateAsync(SimulatePaymentCommand command, CancellationToken cancellationToken);
}
