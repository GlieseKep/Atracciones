using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Ecommerce;
using AtraccionesService.Application.Abstractions.Idempotency;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Services.Catalog;
using AtraccionesService.Application.Services.Ecommerce;
using AtraccionesService.Application.Services.Identity;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.Application.Validators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtraccionesService.Application.DependencyInjection;

/// <summary>
/// Registra los casos de uso de Application. No registra DataAccess: el composition root de la API debe registrar
/// también la implementación de <c>IUnitOfWork</c>.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApplicationOptions>()
            .Bind(configuration.GetSection(ApplicationOptions.SectionName))
            .Validate(o => o.MaxTicketsPerOperation > 0 && o.HoldMinutes > 0 && o.MaxPaymentAttemptsPerOrder > 0
                           && o.IdempotencyRetentionHours > 0 && o.PageTokenLifetimeMinutes > 0,
                "Los parámetros de Application deben ser positivos.")
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Application:TimeZone no es una zona horaria válida.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<BusinessClock>();
        services.AddSingleton<PageTokenService>();
        services.AddSingleton<IPriceCalculator, PriceCalculator>();
        services.AddSingleton<IPaymentSimulationPolicy, DeterministicPaymentSimulationPolicy>();
        services.AddSingleton<SlotRequestValidator>();

        services.AddScoped<TransactionRunner>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<CustomerResolver>();
        services.AddScoped<PermissionGuard>();
        services.AddScoped<OrderWorkflow>();

        services.AddScoped<ILocalPermissionService, LocalPermissionService>();
        services.AddScoped<IAttractionService, AttractionService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentSimulationService, PaymentSimulationService>();

        return services;
    }
}
