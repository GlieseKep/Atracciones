using AtraccionesService.Application.Commands.Attractions;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Contracts.Catalog;
using AtraccionesService.Contracts.Common;
using AtraccionesService.Contracts.Ecommerce;
using AtraccionesService.Contracts.Identity;
using AtraccionesService.Contracts.Reservations;

namespace AtraccionesService.API.Mapping;

/// <summary>Mapeo entre DTOs HTTP de Contracts y modelos de Application. No contiene reglas de negocio.</summary>
public static class ContractMappings
{
    // ---- Comunes ----

    public static Money ToMoney(this PriceDto price) => new(price.Currency, price.Total);

    public static PriceDto ToDto(this Money money) => new() { Currency = money.Currency, Total = money.Total };

    public static PagedResponse<TOut> ToPagedResponse<TIn, TOut>(this PaginationResult<TIn> page, Func<TIn, TOut> map) => new()
    {
        TotalItems = page.TotalItems,
        ItemsPerPage = page.Limit,
        CurrentPage = page.CurrentPage,
        TotalPages = page.TotalPages,
        Data = page.Items.Select(map).ToList(),
    };

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum => Enum.Parse<TEnum>(value, ignoreCase: true);

    // ---- Catálogo ----

    public static AttractionData ToData(this CreateAttractionRequest request) => new(
        request.Name,
        request.LongDescription,
        request.Duration,
        request.Price.ToMoney(),
        request.Categories,
        request.Badges,
        request.Locations.Select(ToData).ToList(),
        request.Photos.Select(p => p.Url).ToList(),
        request.Operator is null ? null : new OperatorData(request.Operator.Id, request.Operator.Name),
        request.ProductType!.Value.ToString(),
        request.Includes,
        request.SupportedLanguages,
        request.FreeCancellation);

    public static AttractionPatch ToPatch(this UpdateAttractionRequest request) => new(
        request.Name,
        request.LongDescription,
        request.Duration,
        request.Price?.ToMoney(),
        request.Categories,
        request.Badges,
        request.Locations?.Select(ToData).ToList(),
        request.Photos?.Select(p => p.Url).ToList(),
        request.Operator is null ? null : new OperatorData(request.Operator.Id, request.Operator.Name),
        request.ProductType?.ToString(),
        request.Includes,
        request.SupportedLanguages,
        request.FreeCancellation);

    private static LocationData ToData(LocationDto location) => new(
        location.Address,
        location.City,
        location.Country,
        location.Coordinates?.Latitude,
        location.Coordinates?.Longitude,
        location.Type);

    public static AttractionResponse ToResponse(this AttractionResult result, string selfLink)
    {
        var data = result.Data;
        return new AttractionResponse
        {
            Id = result.Id,
            Name = data.Name,
            LongDescription = data.LongDescription,
            Duration = data.Duration,
            Price = data.Price.ToDto(),
            Categories = data.Categories,
            Badges = data.Badges,
            Locations = data.Locations.Select(l => new LocationDto
            {
                Address = l.Address,
                City = l.City,
                Country = l.Country,
                Type = l.Type,
                Coordinates = l.Latitude is { } lat && l.Longitude is { } lon
                    ? new CoordinatesDto { Latitude = lat, Longitude = lon }
                    : null,
            }).ToList(),
            Photos = data.PhotoUrls.Select(url => new PhotoDto { Url = url }).ToList(),
            Operator = data.Operator is null ? null : new OperatorDto { Id = data.Operator.Id, Name = data.Operator.Name },
            ProductType = ParseEnum<ProductType>(data.ProductType),
            Includes = data.Includes,
            SupportedLanguages = data.SupportedLanguages,
            FreeCancellation = data.FreeCancellation,
            Ratings = result.Ratings is null ? null : new RatingDto { NumberOfReviews = result.Ratings.NumberOfReviews, Score = result.Ratings.Score },
            Url = result.Url is null ? null : new UrlDto { Web = result.Url.Web, App = result.Url.App },
            Links = new Dictionary<string, string>
            {
                ["self"] = selfLink,
                ["availability"] = selfLink + "/availability",
            },
        };
    }

    public static AvailabilityResponse ToResponse(this AvailabilityResult result) => new()
    {
        Date = result.Date,
        TimeZone = result.TimeZone,
        AvailableSpots = result.AvailableSpots,
        Times = result.Slots.Select(s => new AvailabilitySlotDto
        {
            Time = s.Time,
            AvailableSpots = s.AvailableSpots,
            Status = s.AvailableSpots > 0 ? SlotStatus.AVAILABLE : SlotStatus.SOLD_OUT,
        }).ToList(),
    };

    // ---- Reservas ----

    public static ReservationResponse ToResponse(this ReservationResult result) => new()
    {
        ReservationId = result.ReservationId,
        AttractionId = result.AttractionId,
        Status = ParseEnum<ReservationStatus>(result.Status),
        Date = result.Date,
        Time = result.Time,
        TicketCount = result.TicketCount,
        TotalPrice = result.TotalPrice.ToDto(),
    };

    // ---- Identidad y clientes ----

    public static BillingData ToBilling(this RegisterProfileRequest request) =>
        new(request.BillingName, request.BillingEmail, request.BillingAddress, request.TaxId, null);

    public static BillingData ToBilling(this UpdateCustomerRequest request) =>
        new(request.BillingName, request.BillingEmail, request.BillingAddress, request.TaxId, request.PaymentMethodReference);

    public static RegisterProfileResponse ToResponse(this RegisteredUserResult result) => new()
    {
        Id = result.Id,
        Email = result.Email,
        Status = result.Status,
        CustomerId = result.CustomerId,
        CreatedAt = result.CreatedAt,
    };

    public static UserResponse ToResponse(this UserResult result) => new()
    {
        Id = result.Id,
        Email = result.Email,
        Status = result.Status,
        CreatedAt = result.CreatedAt,
        UpdatedAt = result.UpdatedAt,
    };

    public static CustomerResponse ToResponse(this CustomerResult result) => new()
    {
        Id = result.Id,
        UserId = result.UserId,
        BillingName = result.BillingName,
        BillingEmail = result.BillingEmail,
        BillingAddress = result.BillingAddress,
        TaxId = result.TaxId,
        PaymentMethodReference = result.PaymentMethodReference,
        CreatedAt = result.CreatedAt,
        UpdatedAt = result.UpdatedAt,
    };

    // ---- Ecommerce ----

    private static PaymentSummaryDto? ToDto(this PaymentSummaryResult? payment) => payment is null ? null : new PaymentSummaryDto
    {
        Id = payment.Id,
        PaymentMethod = ParseEnum<PaymentMethod>(payment.PaymentMethod),
        Status = ParseEnum<PaymentSimulationStatus>(payment.Status),
        GatewayReference = payment.GatewayReference,
    };

    public static PurchaseResponse ToResponse(this PurchaseResult result) => new()
    {
        PurchaseId = result.PurchaseId,
        OrderId = result.OrderId,
        AttractionId = result.AttractionId,
        Date = result.Date,
        Time = result.Time,
        Quantity = result.Quantity,
        UnitPrice = result.UnitPrice.ToDto(),
        TotalAmount = result.TotalAmount,
        Currency = result.Currency,
        Status = ParseEnum<OrderStatus>(result.Status),
        ReservationId = result.ReservationId,
        Payment = result.Payment.ToDto(),
        HoldExpiresAt = result.HoldExpiresAt,
        CreatedAt = result.CreatedAt,
    };

    public static OrderResponse ToResponse(this OrderResult result) => new()
    {
        Id = result.Id,
        CustomerId = result.CustomerId,
        PurchaseId = result.PurchaseId,
        ReservationId = result.ReservationId,
        Status = ParseEnum<OrderStatus>(result.Status),
        Currency = result.Currency,
        TotalAmount = result.TotalAmount,
        CreatedAt = result.CreatedAt,
        UpdatedAt = result.UpdatedAt,
        Items = result.Items.Select(i => new OrderItemResponse
        {
            Id = i.Id,
            AttractionId = i.AttractionId,
            Date = i.Date,
            Time = i.Time,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice.ToDto(),
            Status = i.Status,
        }).ToList(),
        PaymentSimulation = result.PaymentSimulation.ToDto(),
    };

    public static OrderEventsResponse ToResponse(this OrderEventsResult result) => new()
    {
        OrderId = result.OrderId,
        Events = result.Events.Select(e => new OrderEventResponse
        {
            EventType = e.EventType,
            PreviousStatus = e.PreviousStatus,
            NewStatus = e.NewStatus,
            CreatedAt = e.CreatedAt,
        }).ToList(),
    };

    public static PaymentSimulationResponse ToResponse(this PaymentSimulationResult result) => new()
    {
        Id = result.Id,
        OrderId = result.OrderId,
        PaymentMethod = ParseEnum<PaymentMethod>(result.PaymentMethod),
        Status = ParseEnum<PaymentSimulationStatus>(result.Status),
        Amount = result.Amount,
        Currency = result.Currency,
        GatewayReference = result.GatewayReference,
        CreatedAt = result.CreatedAt,
    };
}
