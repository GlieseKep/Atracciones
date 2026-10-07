using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Repositories.Generic;
using AtraccionesService.DataAcess.Seed;
using AtraccionesService.DataAcess.UnitOfWork;
using AtraccionesService.DataManagment.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AtraccionesService.DataAcess.Extensions;

public enum DatabaseProvider
{
    SqlServer,
    Sqlite
}

/// <summary>Sección <c>Database</c>. La cadena de conexión se lee de <c>ConnectionStrings</c> (variables de entorno o secretos en producción).</summary>
public sealed class DataAccessOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    public string ConnectionStringName { get; set; } = "Atracciones";

    /// <summary>Aplica migraciones al iniciar. Solo se respeta fuera de producción.</summary>
    public bool ApplyMigrations { get; set; }

    public SeedOptions Seed { get; set; } = new();
}

public sealed class SeedOptions
{
    /// <summary>Carga el catálogo de ejemplo y su disponibilidad. Solo fuera de producción.</summary>
    public bool Catalog { get; set; }

    /// <summary>Carga un cliente local con una compra, pedido y pago simulados de ejemplo.</summary>
    public bool Ecommerce { get; set; }

    /// <summary>Días de disponibilidad generados a partir de hoy.</summary>
    public int AvailabilityDays { get; set; } = 30;
}

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DataAccessOptions.SectionName).Get<DataAccessOptions>() ?? new DataAccessOptions();
        services.Configure<DataAccessOptions>(configuration.GetSection(DataAccessOptions.SectionName));

        var connectionString = configuration.GetConnectionString(options.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{options.ConnectionStringName}'. Configúrela mediante variables de entorno o secretos.");
        }

        switch (options.Provider)
        {
            case DatabaseProvider.SqlServer:
                services.AddDbContext<SqlServerAtraccionesDbContext>(o => o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(0)));
                services.AddScoped<AtraccionesDbContext>(sp => sp.GetRequiredService<SqlServerAtraccionesDbContext>());
                break;
            case DatabaseProvider.Sqlite:
                services.AddDbContext<SqliteAtraccionesDbContext>(o => o.UseSqlite(connectionString));
                services.AddScoped<AtraccionesDbContext>(sp => sp.GetRequiredService<SqliteAtraccionesDbContext>());
                break;
            default:
                throw new InvalidOperationException($"Proveedor de base de datos no soportado: {options.Provider}.");
        }

        services.AddScoped<IUnitOfWork>(sp => new EfUnitOfWork(sp.GetRequiredService<AtraccionesDbContext>()));
        services.AddSingleton<IUnitOfWorkFactory, EfUnitOfWorkFactory>();
        services.AddScoped(typeof(GenericRepository<>));
        services.AddScoped<CatalogSeeder>();
        services.AddScoped<EcommerceSeeder>();
        services.AddScoped<DatabaseSeeder>();
        return services;
    }

    /// <summary>
    /// Migra y siembra según configuración solo cuando <paramref name="allowAutomaticChanges"/> es verdadero
    /// (desarrollo/pruebas). En producción la migración es manual con scripts SQL generados.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, bool allowAutomaticChanges, CancellationToken cancellationToken = default)
    {
        if (!allowAutomaticChanges)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DataAccessOptions>>().Value;
        if (options.ApplyMigrations)
        {
            await scope.ServiceProvider.GetRequiredService<AtraccionesDbContext>().Database.MigrateAsync(cancellationToken);
        }

        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(options.Seed, cancellationToken);
    }
}
