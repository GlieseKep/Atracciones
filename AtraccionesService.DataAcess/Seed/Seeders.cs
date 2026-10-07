using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Entities.Catalog;
using AtraccionesService.DataAcess.Entities.Ecommerce;
using AtraccionesService.DataAcess.Entities.Identity;
using AtraccionesService.DataAcess.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AtraccionesService.DataAcess.Seed;

/// <summary>Orquesta los seeders. Son idempotentes: no duplican datos si ya existen.</summary>
public sealed class DatabaseSeeder(CatalogSeeder catalog, EcommerceSeeder ecommerce)
{
    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken)
    {
        if (options.Catalog || options.Ecommerce)
        {
            await catalog.SeedAsync(options.AvailabilityDays, cancellationToken);
        }

        if (options.Ecommerce)
        {
            await ecommerce.SeedAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Catálogo de ejemplo para desarrollo: operadores, valores de lista, ubicaciones, atracciones y disponibilidad.
/// Sin datos de producción ni credenciales.
/// </summary>
public sealed class CatalogSeeder(AtraccionesDbContext context, TimeProvider? clock = null)
{
    public static readonly Guid TelefericoId = Guid.Parse("a1b2c3d4-0001-4000-8000-000000000001");
    public static readonly Guid MitadDelMundoId = Guid.Parse("a1b2c3d4-0002-4000-8000-000000000002");
    public static readonly Guid CotopaxiId = Guid.Parse("a1b2c3d4-0003-4000-8000-000000000003");

    public static readonly TimeOnly[] SlotTimes = [new(9, 0), new(14, 0)];

    public async Task SeedAsync(int availabilityDays, CancellationToken cancellationToken)
    {
        if (await context.Attractions.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = Values<CategoryEntity>("Aventura", "Cultura", "Naturaleza");
        var badges = Values<BadgeEntity>("Más vendido", "Nuevo");
        var includes = Values<InclusionEntity>("Guía", "Transporte", "Entrada");
        var languages = Values<LanguageEntity>("es", "en");
        var quitoTours = new OperatorEntity { Id = 1001, Name = "Quito Tours (ejemplo)" };
        var andesTrips = new OperatorEntity { Id = 1002, Name = "Andes Trips (ejemplo)" };

        context.AddRange(categories.Values);
        context.AddRange(badges.Values);
        context.AddRange(includes.Values);
        context.AddRange(languages.Values);
        context.AddRange(quitoTours, andesTrips);

        var attractions = new[]
        {
            Attraction(TelefericoId, "TeleférQuito", "Ascenso en teleférico al volcán Pichincha con vistas de la ciudad.", "PT2H", 9.50m,
                "SINGLE_TICKET", quitoTours, 1250, 4.6, "Av. Occidental y La Gasca", "Quito", -0.1925, -78.5195,
                [categories["Aventura"], categories["Naturaleza"]], [badges["Más vendido"]], [includes["Entrada"]], [languages["es"], languages["en"]]),
            Attraction(MitadDelMundoId, "Ciudad Mitad del Mundo", "Visita guiada al monumento de la línea ecuatorial.", "PT3H", 25.00m,
                "GUIDED_TOUR", quitoTours, 840, 4.4, "Av. Manuel Córdova Galarza", "San Antonio de Pichincha", -0.0022, -78.4558,
                [categories["Cultura"]], [], [includes["Guía"], includes["Transporte"], includes["Entrada"]], [languages["es"], languages["en"]]),
            Attraction(CotopaxiId, "Parque Nacional Cotopaxi", "Excursión de día completo al Parque Nacional Cotopaxi.", "P1D", 75.00m,
                "PACKAGE", andesTrips, 310, 4.8, "Panamericana Sur km 45", "Latacunga", -0.6844, -78.4378,
                [categories["Aventura"], categories["Naturaleza"]], [badges["Nuevo"]], [includes["Guía"], includes["Transporte"]], [languages["es"]]),
        };
        context.Attractions.AddRange(attractions);

        var today = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        foreach (var attraction in attractions)
        {
            for (var day = 0; day < availabilityDays; day++)
            {
                foreach (var time in SlotTimes)
                {
                    context.Availability.Add(new AttractionAvailabilityEntity
                    {
                        Id = Guid.NewGuid(), AttractionId = attraction.Id, Date = today.AddDays(day), Time = time, Capacity = 20,
                    });
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, T> Values<T>(params string[] names)
        where T : class, ICatalogValue, new() => names.ToDictionary(n => n, n => new T { Name = n });

    private static AttractionEntity Attraction(
        Guid id, string name, string description, string duration, decimal price, string productType, OperatorEntity op,
        int reviews, double score, string address, string city, double lat, double lon,
        List<CategoryEntity> categories, List<BadgeEntity> badges, List<InclusionEntity> includes, List<LanguageEntity> languages) => new()
    {
        Id = id,
        Name = name,
        LongDescription = description,
        Duration = duration,
        Price = new PriceValue { Currency = "USD", Amount = price },
        ProductType = productType,
        Operator = op,
        RatingReviewCount = reviews,
        RatingScore = score,
        FreeCancellation = true,
        Categories = categories,
        Badges = badges,
        Inclusions = includes,
        Languages = languages,
        Locations = [new LocationEntity { Id = Guid.NewGuid(), AttractionId = id, Address = address, City = city, Country = "EC", Latitude = lat, Longitude = lon, Type = "MEETING_POINT" }],
    };
}

/// <summary>
/// Datos de ecommerce para desarrollo local: un cliente de prueba con una compra pagada y su historial.
/// No crea roles ni administradores; el primer <c>admin</c> se provisiona por un procedimiento operativo controlado.
/// </summary>
public sealed class EcommerceSeeder(AtraccionesDbContext context, TimeProvider? clock = null)
{
    public const string DevIssuer = "https://localhost/dev-issuer";
    public const string DevSubject = "dev-customer-1";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.OauthIssuer == DevIssuer && u.OauthSubject == DevSubject, cancellationToken))
        {
            return;
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var slot = await context.Availability
            .Where(s => s.AttractionId == CatalogSeeder.TelefericoId && s.Capacity - s.ReservedQuantity >= 2)
            .OrderBy(s => s.Date).ThenBy(s => s.Time)
            .FirstOrDefaultAsync(cancellationToken);
        if (slot is null)
        {
            return;
        }

        var user = new UserEntity { Id = Guid.NewGuid(), OauthIssuer = DevIssuer, OauthSubject = DevSubject, Email = "cliente.dev@example.test", Status = "ACTIVE", CreatedAt = now, UpdatedAt = now };
        var customer = new CustomerEntity { Id = Guid.NewGuid(), UserId = user.Id, BillingName = "Cliente de desarrollo", BillingEmail = "cliente.dev@example.test", BillingAddress = "Quito", PaymentMethodReference = "sim-card-dev", CreatedAt = now, UpdatedAt = now };
        var unit = new PriceValue { Currency = "USD", Amount = 9.50m };
        var reservation = new ReservationEntity
        {
            Id = Guid.NewGuid(), AttractionId = slot.AttractionId, CustomerId = customer.Id, Date = slot.Date, Time = slot.Time, TicketCount = 2,
            TotalPrice = new PriceValue { Currency = "USD", Amount = 19.00m }, CustomerName = "Cliente de desarrollo",
            CustomerEmail = "cliente.dev@example.test", Status = "CONFIRMED", CreatedAt = now,
        };
        var purchase = new PurchaseEntity
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, AttractionId = slot.AttractionId, ServiceDate = slot.Date, ServiceTime = slot.Time,
            Quantity = 2, UnitPrice = unit, TotalAmount = 19.00m, RequestIdempotencyKey = Guid.NewGuid(), CreatedAt = now,
        };
        var order = new OrderEntity
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, PurchaseId = purchase.Id, ReservationId = reservation.Id, Status = "PAID",
            Currency = "USD", TotalAmount = 19.00m, CreatedAt = now, UpdatedAt = now,
            Items = [new OrderItemEntity { Id = Guid.NewGuid(), AttractionId = slot.AttractionId, AvailabilityId = slot.Id, ServiceDate = slot.Date, ServiceTime = slot.Time, Quantity = 2, UnitPrice = new PriceValue { Currency = "USD", Amount = 9.50m } }],
        };
        var paymentId = Guid.NewGuid();
        var payment = new PaymentSimulationEntity
        {
            Id = paymentId, OrderId = order.Id, PaymentMethod = "CARD", Status = "SETTLED", Amount = 19.00m, Currency = "USD",
            GatewayReference = $"SIM-DEV-{paymentId:N}"[..20].ToUpperInvariant(), CreatedAt = now, ProcessedAt = now,
            Attempts = [new PaymentAttemptEntity { Id = Guid.NewGuid(), PaymentSimulationId = paymentId, AttemptNumber = 1, Status = "AUTHORIZED", ResponseCode = "00", ResponseMessage = "Aprobado (simulado).", CreatedAt = now }],
            Events =
            [
                new PaymentEventEntity { Id = Guid.NewGuid(), PaymentSimulationId = paymentId, EventType = "PAYMENT_AUTHORIZED", Payload = "{\"status\":\"AUTHORIZED\"}", CreatedAt = now },
                new PaymentEventEntity { Id = Guid.NewGuid(), PaymentSimulationId = paymentId, EventType = "PAYMENT_SETTLED", Payload = "{\"status\":\"SETTLED\"}", CreatedAt = now },
            ],
        };

        slot.ReservedQuantity += 2;
        slot.Version++;
        context.AddRange(user, customer, reservation, purchase, order, payment);
        context.OrderEvents.AddRange(
            new OrderEventEntity { Id = Guid.NewGuid(), OrderId = order.Id, EventType = "ORDER_CREATED", NewStatus = "PENDING_PAYMENT", CreatedAt = now },
            new OrderEventEntity { Id = Guid.NewGuid(), OrderId = order.Id, EventType = "PAYMENT_SETTLED", PreviousStatus = "PENDING_PAYMENT", NewStatus = "PAID", CreatedAt = now });
        context.InventoryMovements.Add(new InventoryMovementEntity
        {
            Id = Guid.NewGuid(), AttractionId = slot.AttractionId, AvailabilityId = slot.Id, Quantity = 2, MovementType = "CONFIRMED",
            PurchaseId = purchase.Id, ReservationId = reservation.Id, CreatedAt = now,
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
