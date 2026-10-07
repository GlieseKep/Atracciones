using System.Globalization;
using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.DependencyInjection;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Common;
using AtraccionesService.Domain.Identity;
using Microsoft.Extensions.Options;

namespace AtraccionesService.Application.Services.Shared;

/// <summary>Usuario y cliente locales resueltos desde la identidad OAuth2 validada.</summary>
public sealed record CustomerContext(User User, Customer Customer);

/// <summary>
/// Resuelve el cliente propietario a partir de <c>issuer + subject</c>. Nunca usa identificadores enviados por el cliente.
/// </summary>
public sealed class CustomerResolver(IUnitOfWork unitOfWork)
{
    /// <summary>Devuelve <c>null</c> si el perfil no está aprovisionado; lanza 403 si el usuario está inactivo.</summary>
    public async Task<CustomerContext?> FindAsync(AuthenticatedUser identity, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Users.GetByIdentityAsync(identity.Issuer, identity.Subject, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("El perfil del usuario no está activo.", ForbiddenException.UserInactive);
        }

        var customer = await unitOfWork.Customers.GetByUserIdAsync(user.Id, cancellationToken);
        return customer is null ? null : new CustomerContext(user, customer);
    }

    /// <summary>Exige un perfil aprovisionado para operaciones transaccionales.</summary>
    public async Task<CustomerContext> RequireAsync(AuthenticatedUser identity, CancellationToken cancellationToken) =>
        await FindAsync(identity, cancellationToken)
        ?? throw new ForbiddenException(
            "El usuario autenticado no tiene un perfil de cliente aprovisionado. Debe registrarse antes de operar.",
            ForbiddenException.ProfileNotRegistered);
}

/// <summary>Fecha y hora actuales en la zona horaria de negocio configurada.</summary>
public sealed class BusinessClock(TimeProvider timeProvider, IOptions<ApplicationOptions> options)
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public string TimeZoneId => options.Value.TimeZone;

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateTime LocalNow => TimeZoneInfo.ConvertTime(UtcNow, _timeZone).DateTime;

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    /// <summary>Una franja es futura si su fecha y hora locales todavía no han llegado.</summary>
    public bool IsFuture(DateOnly date, TimeOnly time) => date.ToDateTime(time) > LocalNow;
}

/// <summary>Calcula precios en el servidor; los precios enviados por el cliente nunca se usan.</summary>
public interface IPriceCalculator
{
    Money UnitPrice(Domain.Catalog.Attraction attraction);

    Money Total(Domain.Catalog.Attraction attraction, int quantity);
}

public sealed class PriceCalculator : IPriceCalculator
{
    public Money UnitPrice(Domain.Catalog.Attraction attraction) => attraction.Details.Price;

    public Money Total(Domain.Catalog.Attraction attraction, int quantity) => UnitPrice(attraction).Multiply(quantity);
}

/// <summary>Verificación de permisos locales dentro de Application (defensa en profundidad tras la API).</summary>
public sealed class PermissionGuard(ILocalPermissionService permissions)
{
    public async Task EnsureAsync(AuthenticatedUser actor, string permission, CancellationToken cancellationToken)
    {
        if (!await permissions.HasPermissionAsync(actor, permission, cancellationToken))
        {
            throw new ForbiddenException($"El usuario no tiene el permiso local '{permission}'.");
        }
    }
}

internal static class TimeFormats
{
    public const string Slot = "HH:mm";

    public static string Format(TimeOnly time) => time.ToString(Slot, CultureInfo.InvariantCulture);
}
