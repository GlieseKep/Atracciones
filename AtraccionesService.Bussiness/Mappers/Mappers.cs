using AtraccionesService.Application.Commands.Attractions;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Reservations;
using DomainMoney = AtraccionesService.Domain.Common.Money;

namespace AtraccionesService.Application.Mappers;

/// <summary>Mapeo entre comandos/resultados de Application y el dominio. No contiene mapeo HTTP.</summary>
public static class AttractionMapper
{
    public static AttractionDetails ToDomain(this AttractionData data) => new(
        data.Name.Trim(),
        data.LongDescription.Trim(),
        data.Duration,
        new DomainMoney(data.Price.Currency, data.Price.Total),
        data.Categories.Select(c => c.Trim()).Distinct().ToList(),
        data.Badges.Distinct().ToList(),
        data.Locations.Select(l => new Location(l.Address.Trim(), l.City.Trim(), l.Country, l.Latitude, l.Longitude, l.Type)).ToList(),
        data.PhotoUrls.ToList(),
        data.Operator is null ? null : new OperatorInfo(data.Operator.Id, data.Operator.Name.Trim()),
        Enum.Parse<ProductType>(data.ProductType),
        data.Includes.ToList(),
        data.SupportedLanguages.Distinct().ToList(),
        data.FreeCancellation);

    public static AttractionData ToData(this AttractionDetails details) => new(
        details.Name,
        details.LongDescription,
        details.Duration,
        details.Price.ToResult(),
        details.Categories,
        details.Badges,
        details.Locations.Select(l => new LocationData(l.Address, l.City, l.Country, l.Latitude, l.Longitude, l.Type)).ToList(),
        details.PhotoUrls,
        details.Operator is null ? null : new OperatorData(details.Operator.Id, details.Operator.Name),
        details.ProductType.ToString(),
        details.Includes,
        details.SupportedLanguages,
        details.FreeCancellation);

    /// <summary>Aplica un cambio parcial: las propiedades <c>null</c> conservan el valor actual.</summary>
    public static AttractionData Merge(this AttractionData current, AttractionPatch patch) => new(
        patch.Name ?? current.Name,
        patch.LongDescription ?? current.LongDescription,
        patch.Duration ?? current.Duration,
        patch.Price ?? current.Price,
        patch.Categories ?? current.Categories,
        patch.Badges ?? current.Badges,
        patch.Locations ?? current.Locations,
        patch.PhotoUrls ?? current.PhotoUrls,
        patch.Operator ?? current.Operator,
        patch.ProductType ?? current.ProductType,
        patch.Includes ?? current.Includes,
        patch.SupportedLanguages ?? current.SupportedLanguages,
        patch.FreeCancellation ?? current.FreeCancellation);

    public static AttractionResult ToResult(this Attraction attraction) => new(
        attraction.Id,
        attraction.Details.ToData(),
        attraction.Rating is null ? null : new RatingResult(attraction.Rating.NumberOfReviews, attraction.Rating.Score),
        attraction.Urls is null ? null : new UrlResult(attraction.Urls.Web, attraction.Urls.App));

    public static Money ToResult(this DomainMoney money) => new(money.Currency, money.Amount);
}

public static class ReservationMapper
{
    public static ReservationResult ToResult(this Reservation reservation) => new(
        reservation.Id,
        reservation.AttractionId,
        reservation.Status.ToString(),
        reservation.Date,
        TimeFormats.Format(reservation.Time),
        reservation.TicketCount,
        reservation.TotalPrice.ToResult());
}

public static class OrderMapper
{
    public static OrderResult ToResult(this Order order, PaymentSimulation? latestPayment) => new(
        order.Id,
        order.CustomerId,
        order.PurchaseId,
        order.ReservationId,
        order.Status.ToString(),
        order.Total.Currency,
        order.Total.Amount,
        order.CreatedAt,
        order.UpdatedAt,
        order.Items.Select(i => new OrderItemResult(
            i.Id, i.AttractionId, i.ServiceDate, TimeFormats.Format(i.ServiceTime), i.Quantity, i.UnitPrice.ToResult(), order.Status.ToString())).ToList(),
        latestPayment?.ToSummary());

    public static OrderEventResult ToResult(this OrderEvent orderEvent) => new(
        orderEvent.EventType,
        orderEvent.PreviousStatus?.ToString(),
        orderEvent.NewStatus.ToString(),
        orderEvent.CreatedAt);

    public static PurchaseResult ToPurchaseResult(this Order order, Purchase purchase, PaymentSimulation? payment) => new(
        purchase.Id,
        order.Id,
        purchase.AttractionId,
        purchase.ServiceDate,
        TimeFormats.Format(purchase.ServiceTime),
        purchase.Quantity,
        purchase.UnitPrice.ToResult(),
        order.Total.Amount,
        order.Total.Currency,
        order.Status.ToString(),
        order.ReservationId,
        payment?.ToSummary(),
        order.HoldExpiresAt,
        purchase.CreatedAt);
}

public static class PaymentMapper
{
    public static PaymentSummaryResult ToSummary(this PaymentSimulation payment) =>
        new(payment.Id, payment.Method.ToString(), payment.Status.ToString(), payment.GatewayReference);

    public static PaymentSimulationResult ToResult(this PaymentSimulation payment) => new(
        payment.Id,
        payment.OrderId,
        payment.Method.ToString(),
        payment.Status.ToString(),
        payment.Amount.Amount,
        payment.Amount.Currency,
        payment.GatewayReference,
        payment.CreatedAt);
}
