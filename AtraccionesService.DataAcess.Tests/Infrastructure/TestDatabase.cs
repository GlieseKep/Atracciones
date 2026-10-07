using AtraccionesService.Application.DependencyInjection;
using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Extensions;
using AtraccionesService.DataManagment.UnitOfWork;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AtraccionesService.DataAcess.Tests.Infrastructure;

public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// Base de datos real y aislada por prueba, creada con las migraciones del proveedor:
/// SQLite en memoria (siempre) y SQL Server LocalDB (si está disponible). Compone DataAccess y Application como la API.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private const string LocalDbServer = @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly Lazy<bool> SqlServerAvailable = new(() =>
    {
        if (Environment.GetEnvironmentVariable("ATRACCIONES_SKIP_SQLSERVER_TESTS") == "1")
        {
            return false;
        }

        try
        {
            using var connection = new SqlConnection(LocalDbServer + ";Connect Timeout=15");
            connection.Open();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    });

    private readonly SqliteConnection? _keepAlive;
    private readonly ServiceProvider _services;

    private TestDatabase(DatabaseProvider provider, string connectionString, SqliteConnection? keepAlive)
    {
        Provider = provider;
        _keepAlive = keepAlive;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider.ToString(),
            ["ConnectionStrings:Atracciones"] = connectionString,
        }).Build();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddDataAccess(configuration);
        services.AddApplication(configuration);
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>2026-10-06 07:00 en America/Guayaquil.</summary>
    public static readonly DateTimeOffset StartTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> Providers()
    {
        yield return [DatabaseProvider.Sqlite];
        if (SqlServerAvailable.Value)
        {
            yield return [DatabaseProvider.SqlServer];
        }
    }

    public DatabaseProvider Provider { get; }

    public ManualTimeProvider Clock { get; } = new(StartTime);

    public static async Task<TestDatabase> CreateAsync(DatabaseProvider provider)
    {
        TestDatabase database;
        if (provider == DatabaseProvider.Sqlite)
        {
            var connectionString = $"Data Source=file:atr-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
            database = new TestDatabase(provider, connectionString, keepAlive);
        }
        else
        {
            database = new TestDatabase(provider, $"{LocalDbServer};Database=AtrTests_{Guid.NewGuid():N}", null);
        }

        await using var scope = database.Scope();
        await scope.ServiceProvider.GetRequiredService<AtraccionesDbContext>().Database.MigrateAsync();
        return database;
    }

    public AsyncServiceScope Scope() => _services.CreateAsyncScope();

    public async Task<T> Run<TService, T>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        await using var scope = Scope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public Task<T> WithUnitOfWork<T>(Func<IUnitOfWork, Task<T>> action) => Run(action);

    public Task<T> WithContext<T>(Func<AtraccionesDbContext, Task<T>> action) => Run(action);

    public async ValueTask DisposeAsync()
    {
        if (Provider == DatabaseProvider.SqlServer)
        {
            await using var scope = Scope();
            await scope.ServiceProvider.GetRequiredService<AtraccionesDbContext>().Database.EnsureDeletedAsync();
        }

        await _services.DisposeAsync();
        if (_keepAlive is not null)
        {
            await _keepAlive.DisposeAsync();
        }
    }
}
