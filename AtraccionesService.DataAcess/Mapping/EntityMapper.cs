using AtraccionesService.DataAcess.Entities.Catalog;
using AtraccionesService.DataAcess.Entities.Ecommerce;
using AtraccionesService.DataAcess.Entities.Identity;
using AtraccionesService.DataAcess.Entities.Technical;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Common;
using AtraccionesService.Domain.Ecommerce;
using AtraccionesService.Domain.Identity;
using AtraccionesService.Domain.Reservations;

namespace AtraccionesService.DataAcess.Mapping;

/// <summary>Conversión entre entidades EF Core y el dominio. Application nunca recibe entidades de persistencia.</summary>
internal static class EntityMapper
{
    public static Money ToMoney(this PriceValue price) => new(price.Currency, price.Amount);

    public static PriceValue ToPrice(this Money money) => new() { Currency = money.Currency, Amount = money.Amount };

    public static Attraction ToDomain(this AttractionEntity e) => Attraction.Restore(
        e.Id,
        new AttractionDetails(
            e.Name,
            e.LongDescription,
            e.Duration,
            e.Price.ToMoney(),
            e.Categories.Select(c => c.Name).Order().ToList(),
            e.Badges.Select(b => b.Name).Order().ToList(),
            e.Locations.OrderBy(l => l.Position).Select(l => new Location(l.Address, l.City, l.Country, l.Latitude, l.Longitude, l.Type)).ToList(),
            e.Photos.OrderBy(p => p.Position).Select(p => p.Url).ToList(),
            e.Operator is null ? null : new OperatorInfo(e.Operator.Id, e.Operator.Name),
            Enum.Parse<ProductType>(e.ProductType),
            e.Inclusions.Select(i => i.Name).Order().ToList(),
            e.Languages.Select(l => l.Name).Order().ToList(),
            e.FreeCancellation),
        e.RatingReviewCount is { } reviews && e.RatingScore is { } score ? new Rating(reviews, score) : null,
        e.UrlWeb is null && e.UrlApp is null ? null : new AttractionUrls(e.UrlWeb, e.UrlApp));

    public static AvailabilitySlot ToDomain(this AttractionAvailabilityEntity e) =>
        new(e.Id, e.AttractionId, e.Date, e.Time, e.Capacity, e.ReservedQuantity, e.Version);

    public static AttractionAvailabilityEntity ToEntity(this AvailabilitySlot s) => new()
    {
        Id = s.Id,
        AttractionId = s.AttractionId,
        Date = s.Date,
        Time = s.Time,
        Capacity = s.Capacity,
        ReservedQuantity = s.ReservedQuantity,
        Version = s.Version,
    };

    public static Reservation ToDomain(this ReservationEntity e) => Reservation.Restore(
        e.Id, e.AttractionId, e.CustomerId, e.Date, e.Time, e.TicketCount, e.TotalPrice.ToMoney(), e.CustomerName, e.CustomerEmail,
        Enum.Parse<ReservationStatus>(e.Status), e.CancellationReason, e.CreatedAt);

    public static void Apply(this ReservationEntity e, Reservation r)
    {
        e.Id = r.Id;
        e.AttractionId = r.AttractionId;
        e.CustomerId = r.CustomerId;
        e.Date = r.Date;
        e.Time = r.Time;
        e.TicketCount = r.TicketCount;
        e.TotalPrice = r.TotalPrice.ToPrice();
        e.CustomerName = r.CustomerName;
        e.CustomerEmail = r.CustomerEmail;
        e.Status = r.Status.ToString();
        e.CancellationReason = r.CancellationReason;
        e.CreatedAt = r.CreatedAt;
    }

    public static User ToDomain(this UserEntity e) =>
        User.Restore(e.Id, e.OauthIssuer, e.OauthSubject, e.Email, Enum.Parse<UserStatus>(e.Status), e.CreatedAt, e.UpdatedAt);

    public static void Apply(this UserEntity e, User u)
    {
        e.Id = u.Id;
        e.OauthIssuer = u.OAuthIssuer;
        e.OauthSubject = u.OAuthSubject;
        e.Email = u.Email;
        e.Status = u.Status.ToString();
        e.CreatedAt = u.CreatedAt;
        e.UpdatedAt = u.UpdatedAt;
    }

    public static Customer ToDomain(this CustomerEntity e) => Customer.Restore(
        e.Id, e.UserId, e.BillingName, e.BillingEmail, e.BillingAddress, e.TaxId, e.PaymentMethodReference, e.CreatedAt, e.UpdatedAt);

    public static void Apply(this CustomerEntity e, Customer c)
    {
        e.Id = c.Id;
        e.UserId = c.UserId;
        e.BillingName = c.BillingName;
        e.BillingEmail = c.BillingEmail;
        e.BillingAddress = c.BillingAddress;
        e.TaxId = c.TaxId;
        e.PaymentMethodReference = c.PaymentMethodReference;
        e.CreatedAt = c.CreatedAt;
        e.UpdatedAt = c.UpdatedAt;
    }

    public static Purchase ToDomain(this PurchaseEntity e) => new(
        e.Id, e.CustomerId, e.AttractionId, e.ServiceDate, e.ServiceTime, e.Quantity, e.UnitPrice.ToMoney(),
        new Money(e.UnitPrice.Currency, e.TotalAmount), e.RequestIdempotencyKey, e.CreatedAt);

    public static PurchaseEntity ToEntity(this Purchase p) => new()
    {
        Id = p.Id,
        CustomerId = p.CustomerId,
        AttractionId = p.AttractionId,
        ServiceDate = p.ServiceDate,
        ServiceTime = p.ServiceTime,
        Quantity = p.Quantity,
        UnitPrice = p.UnitPrice.ToPrice(),
        TotalAmount = p.Total.Amount,
        RequestIdempotencyKey = p.RequestIdempotencyKey,
        CreatedAt = p.CreatedAt,
    };

    public static Order ToDomain(this OrderEntity e) => Order.Restore(
        e.Id, e.CustomerId, e.PurchaseId, e.ReservationId, Enum.Parse<OrderStatus>(e.Status), new Money(e.Currency, e.TotalAmount),
        e.Items.Select(i => new OrderItem(i.Id, i.AttractionId, i.ServiceDate, i.ServiceTime, i.Quantity, i.UnitPrice.ToMoney(), i.AvailabilityId)),
        e.HoldExpiresAt, e.CancellationReason, e.CreatedAt, e.UpdatedAt);

    /// <summary>Copia el estado mutable del pedido; los elementos solo se crean con el pedido.</summary>
    public static void Apply(this OrderEntity e, Order o)
    {
        e.Status = o.Status.ToString();
        e.HoldExpiresAt = o.HoldExpiresAt;
        e.CancellationReason = o.CancellationReason;
        e.UpdatedAt = o.UpdatedAt;
    }

    public static OrderEntity ToEntity(this Order o)
    {
        var entity = new OrderEntity
        {
            Id = o.Id,
            CustomerId = o.CustomerId,
            PurchaseId = o.PurchaseId,
            ReservationId = o.ReservationId,
            Currency = o.Total.Currency,
            TotalAmount = o.Total.Amount,
            CreatedAt = o.CreatedAt,
            Items = o.Items.Select(i => new OrderItemEntity
            {
                Id = i.Id,
                OrderId = o.Id,
                AttractionId = i.AttractionId,
                AvailabilityId = i.AvailabilitySlotId,
                ServiceDate = i.ServiceDate,
                ServiceTime = i.ServiceTime,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice.ToPrice(),
            }).ToList(),
        };
        entity.Apply(o);
        return entity;
    }

    public static PaymentSimulation ToDomain(this PaymentSimulationEntity e) => PaymentSimulation.Restore(
        e.Id, e.OrderId, Enum.Parse<PaymentMethod>(e.PaymentMethod), Enum.Parse<PaymentStatus>(e.Status), new Money(e.Currency, e.Amount),
        e.GatewayReference, e.CreatedAt, e.ProcessedAt, e.FailureReason);

    public static void Apply(this PaymentSimulationEntity e, PaymentSimulation p)
    {
        e.Id = p.Id;
        e.OrderId = p.OrderId;
        e.PaymentMethod = p.Method.ToString();
        e.Status = p.Status.ToString();
        e.Amount = p.Amount.Amount;
        e.Currency = p.Amount.Currency;
        e.GatewayReference = p.GatewayReference;
        e.CreatedAt = p.CreatedAt;
        e.ProcessedAt = p.ProcessedAt;
        e.FailureReason = p.FailureReason;
    }

    public static OrderEvent ToDomain(this OrderEventEntity e) => new(
        e.Id, e.OrderId, e.EventType, e.PreviousStatus is null ? null : Enum.Parse<OrderStatus>(e.PreviousStatus),
        Enum.Parse<OrderStatus>(e.NewStatus), e.CreatedAt);

    public static InventoryMovement ToDomain(this InventoryMovementEntity e) => new(
        e.Id, e.AttractionId, e.AvailabilityId, e.Quantity, Enum.Parse<InventoryMovementType>(e.MovementType), e.PurchaseId, e.ReservationId, e.CreatedAt);

    public static IdempotencyRecord ToDomain(this IdempotencyKeyEntity e) => new(
        e.Issuer, e.Subject, e.Operation, e.Key, e.RequestHash, Enum.Parse<IdempotencyStatus>(e.Status), e.ResourceId, e.ResponseBody, e.CreatedAt, e.ExpiresAt);
}
