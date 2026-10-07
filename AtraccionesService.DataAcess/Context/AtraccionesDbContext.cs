using AtraccionesService.DataAcess.Entities.Catalog;
using AtraccionesService.DataAcess.Entities.Ecommerce;
using AtraccionesService.DataAcess.Entities.Identity;
using AtraccionesService.DataAcess.Entities.Technical;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AtraccionesService.DataAcess.Context;

/// <summary>
/// Contexto EF Core del servicio. Cada proveedor tiene una subclase con sus propias migraciones
/// (<see cref="SqlServerAtraccionesDbContext"/> y <see cref="SqliteAtraccionesDbContext"/>).
/// </summary>
public abstract class AtraccionesDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<AttractionEntity> Attractions => Set<AttractionEntity>();
    public DbSet<OperatorEntity> Operators => Set<OperatorEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<BadgeEntity> Badges => Set<BadgeEntity>();
    public DbSet<InclusionEntity> Inclusions => Set<InclusionEntity>();
    public DbSet<LanguageEntity> Languages => Set<LanguageEntity>();
    public DbSet<LocationEntity> Locations => Set<LocationEntity>();
    public DbSet<PhotoEntity> Photos => Set<PhotoEntity>();
    public DbSet<AttractionAvailabilityEntity> Availability => Set<AttractionAvailabilityEntity>();
    public DbSet<ReservationEntity> Reservations => Set<ReservationEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<PurchaseEntity> Purchases => Set<PurchaseEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();
    public DbSet<OrderEventEntity> OrderEvents => Set<OrderEventEntity>();
    public DbSet<PaymentSimulationEntity> PaymentSimulations => Set<PaymentSimulationEntity>();
    public DbSet<PaymentAttemptEntity> PaymentAttempts => Set<PaymentAttemptEntity>();
    public DbSet<PaymentEventEntity> PaymentEvents => Set<PaymentEventEntity>();
    public DbSet<RefundSimulationEntity> RefundSimulations => Set<RefundSimulationEntity>();
    public DbSet<InventoryMovementEntity> InventoryMovements => Set<InventoryMovementEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<IdempotencyKeyEntity> IdempotencyKeys => Set<IdempotencyKeyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AtraccionesDbContext).Assembly);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var mutated = ChangeTracker.Entries<IAppendOnlyEntity>()
            .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (mutated is not null)
        {
            throw new InvalidOperationException($"{mutated.Entity.GetType().Name} es un registro inmutable: no admite actualizaciones ni borrados.");
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}

public sealed class SqlServerAtraccionesDbContext(DbContextOptions<SqlServerAtraccionesDbContext> options) : AtraccionesDbContext(options);

/// <summary>
/// SQLite no admite <c>decimal</c> ni <c>DateTimeOffset</c> en comparaciones u ordenamientos: se almacenan como REAL
/// (importes de dos decimales) y como enteros binarios (instantes UTC) para poder filtrar y ordenar.
/// </summary>
public sealed class SqliteAtraccionesDbContext(DbContextOptions<SqliteAtraccionesDbContext> options) : AtraccionesDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HaveConversion<double>();
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}

/// <summary>Fábricas de diseño para <c>dotnet ef</c>. Usan bases locales sin credenciales.</summary>
public sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerAtraccionesDbContext>
{
    public SqlServerAtraccionesDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SqlServerAtraccionesDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("ATRACCIONES_DESIGN_SQLSERVER")
                          ?? @"Server=(localdb)\MSSQLLocalDB;Database=AtraccionesDesign;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}

public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAtraccionesDbContext>
{
    public SqliteAtraccionesDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SqliteAtraccionesDbContext>().UseSqlite("Data Source=atracciones-design.db").Options);
}
