using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.DependencyInjection;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AtraccionesService.Application.Tests.Infrastructure;

public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan delta) => Now += delta;
}

/// <summary>
/// Compone Application tal como lo hará la API (<c>AddApplication</c>) sobre el almacenamiento en memoria.
/// Cada llamada usa un scope nuevo, igual que una solicitud HTTP.
/// </summary>
public sealed class ApplicationTestHost : IDisposable
{
    public const string Issuer = "https://issuer.test";

    /// <summary>2026-10-06 07:00 en America/Guayaquil.</summary>
    public static readonly DateTimeOffset StartTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly ServiceDate = new(2026, 12, 15);
    public static readonly TimeOnly ServiceTime = new(9, 30);

    private readonly ServiceProvider _provider;

    public ApplicationTestHost(IDictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build();
        var services = new ServiceCollection();
        services.AddSingleton(Database);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
        services.AddApplication(configuration);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public InMemoryDatabase Database { get; } = new();

    public ManualTimeProvider Clock { get; } = new(StartTime);

    public static AuthenticatedUser User(string subject = "user-1") => new(Issuer, subject, $"{subject}@ejemplo.com", true);

    public async Task<T> Run<TService, T>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task Run<TService>(Func<TService, Task> action)
        where TService : notnull
    {
        await using var scope = _provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public T Resolve<T>()
        where T : notnull => _provider.CreateScope().ServiceProvider.GetRequiredService<T>();

    /// <summary>Registra el perfil local del usuario (como haría <c>POST /auth/register</c>).</summary>
    public async Task<AuthenticatedUser> RegisterAsync(string subject = "user-1", params string[] permissions)
    {
        var user = User(subject);
        var result = await Run<IUserProfileService, Application.ResultModels.RegisteredUserResult>(s =>
            s.RegisterAsync(new RegisterUserCommand(user, user.Email!, new BillingData("Cliente " + subject, null, null, null, null)), default));
        if (permissions.Length > 0)
        {
            Database.Permissions[result.Id] = [.. permissions];
        }

        return user;
    }

    /// <summary>Crea una atracción con una franja (<see cref="ServiceDate"/> <see cref="ServiceTime"/>) de la capacidad indicada.</summary>
    public Guid SeedAttraction(decimal price = 45m, int capacity = 10, string name = "Teleférico de Quito", string city = "Quito",
        double score = 4.5, int reviews = 100, DateOnly? date = null, TimeOnly? time = null)
    {
        var details = new AttractionDetails(
            name, "Descripción", "PT2H", new Money("USD", price), ["Aventura"], [],
            [new Location("Av. Occidental", city, "EC", null, null, null)], [], null, ProductType.SINGLE_TICKET, [], ["es"], false);
        var attraction = Attraction.Restore(Guid.NewGuid(), details, new Rating(reviews, score), null);
        Database.Attractions[attraction.Id] = attraction;
        var slot = new AvailabilitySlot(Guid.NewGuid(), attraction.Id, date ?? ServiceDate, time ?? ServiceTime, capacity, 0, 0);
        Database.Slots[slot.Id] = slot;
        return attraction.Id;
    }

    public AvailabilitySlot Slot(Guid attractionId) => Database.Slots.Values.Single(s => s.AttractionId == attractionId && s.Time == ServiceTime);

    public void Dispose() => _provider.Dispose();
}
