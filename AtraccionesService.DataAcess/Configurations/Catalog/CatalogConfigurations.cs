using AtraccionesService.DataAcess.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtraccionesService.DataAcess.Configurations.Catalog;

internal static class PriceMapping
{
    public static void Map<T>(ComplexPropertyBuilder<T> price, string prefix)
        where T : notnull
    {
        price.Property("Currency").HasColumnName($"{prefix}Currency").HasMaxLength(3).IsRequired();
        price.Property("Amount").HasColumnName($"{prefix}Amount").HasPrecision(18, 2);
    }
}

public sealed class AttractionConfiguration : IEntityTypeConfiguration<AttractionEntity>
{
    public void Configure(EntityTypeBuilder<AttractionEntity> builder)
    {
        builder.ToTable("attractions");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.LongDescription).HasMaxLength(5000).IsRequired();
        builder.Property(a => a.Duration).HasMaxLength(50).IsRequired();
        builder.Property(a => a.ProductType).HasMaxLength(20).IsRequired();
        builder.Property(a => a.UrlWeb).HasMaxLength(500);
        builder.Property(a => a.UrlApp).HasMaxLength(500);
        builder.ComplexProperty(a => a.Price, p => PriceMapping.Map(p, "Price"));
        builder.HasIndex(a => a.ProductType);
        builder.ToTable(t => t.HasCheckConstraint("CK_attractions_product_type", "ProductType IN ('SINGLE_TICKET', 'GUIDED_TOUR', 'PACKAGE')"));

        builder.HasOne(a => a.Operator).WithMany().HasForeignKey(a => a.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Locations).WithOne().HasForeignKey(l => l.AttractionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Photos).WithOne().HasForeignKey(p => p.AttractionId).OnDelete(DeleteBehavior.Cascade);

        Join<CategoryEntity>(builder.HasMany(a => a.Categories).WithMany(), "attraction_categories", "CategoryId");
        Join<BadgeEntity>(builder.HasMany(a => a.Badges).WithMany(), "attraction_badges", "BadgeId");
        Join<InclusionEntity>(builder.HasMany(a => a.Inclusions).WithMany(), "attraction_includes", "InclusionId");
        Join<LanguageEntity>(builder.HasMany(a => a.Languages).WithMany(), "attraction_languages", "LanguageId");
    }

    private static void Join<TValue>(CollectionCollectionBuilder<TValue, AttractionEntity> relation, string table, string valueKey)
        where TValue : class =>
        relation.UsingEntity(
            table,
            r => r.HasOne(typeof(TValue)).WithMany().HasForeignKey(valueKey).OnDelete(DeleteBehavior.Restrict),
            l => l.HasOne(typeof(AttractionEntity)).WithMany().HasForeignKey("AttractionId").OnDelete(DeleteBehavior.Cascade),
            j => j.HasKey("AttractionId", valueKey));
}

public sealed class OperatorConfiguration : IEntityTypeConfiguration<OperatorEntity>
{
    public void Configure(EntityTypeBuilder<OperatorEntity> builder)
    {
        builder.ToTable("operators");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
    }
}

public abstract class CatalogValueConfiguration<T>(string table, int maxLength) : IEntityTypeConfiguration<T>
    where T : class, ICatalogValue
{
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(table);
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).HasMaxLength(maxLength).IsRequired();
        builder.HasIndex(v => v.Name).IsUnique();
    }
}

public sealed class CategoryConfiguration() : CatalogValueConfiguration<CategoryEntity>("categories", 60);

public sealed class BadgeConfiguration() : CatalogValueConfiguration<BadgeEntity>("badges", 60);

public sealed class InclusionConfiguration() : CatalogValueConfiguration<InclusionEntity>("includes", 200);

public sealed class LanguageConfiguration() : CatalogValueConfiguration<LanguageEntity>("supported_languages", 10);

public sealed class LocationConfiguration : IEntityTypeConfiguration<LocationEntity>
{
    public void Configure(EntityTypeBuilder<LocationEntity> builder)
    {
        builder.ToTable("locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Address).HasMaxLength(300).IsRequired();
        builder.Property(l => l.City).HasMaxLength(120).IsRequired();
        builder.Property(l => l.Country).HasMaxLength(2).IsRequired();
        builder.Property(l => l.Type).HasMaxLength(60);
        builder.HasIndex(l => l.City);
        builder.HasIndex(l => l.Country);
    }
}

public sealed class PhotoConfiguration : IEntityTypeConfiguration<PhotoEntity>
{
    public void Configure(EntityTypeBuilder<PhotoEntity> builder)
    {
        builder.ToTable("photos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Url).HasMaxLength(1000).IsRequired();
    }
}

public sealed class AvailabilityConfiguration : IEntityTypeConfiguration<AttractionAvailabilityEntity>
{
    public void Configure(EntityTypeBuilder<AttractionAvailabilityEntity> builder)
    {
        builder.ToTable("attraction_availability", t =>
            t.HasCheckConstraint("CK_availability_capacity", "Capacity >= 0 AND ReservedQuantity >= 0 AND ReservedQuantity <= Capacity"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Version).IsConcurrencyToken();
        builder.HasIndex(s => new { s.AttractionId, s.Date, s.Time }).IsUnique();
        builder.HasOne<AttractionEntity>().WithMany().HasForeignKey(s => s.AttractionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReservationConfiguration : IEntityTypeConfiguration<ReservationEntity>
{
    public void Configure(EntityTypeBuilder<ReservationEntity> builder)
    {
        builder.ToTable("reservations", t =>
        {
            t.HasCheckConstraint("CK_reservations_status", "Status IN ('PENDING', 'CONFIRMED', 'CANCELLED')");
            t.HasCheckConstraint("CK_reservations_ticket_count", "TicketCount > 0");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.CustomerEmail).HasMaxLength(254).IsRequired();
        builder.Property(r => r.CancellationReason).HasMaxLength(500);
        builder.ComplexProperty(r => r.TotalPrice, p => PriceMapping.Map(p, "Total"));
        builder.HasIndex(r => r.AttractionId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.CustomerEmail);
        builder.HasIndex(r => new { r.CustomerId, r.Date });
        builder.HasIndex(r => r.CreatedAt);

        // AttractionId sin FK: la reserva es histórica y debe sobrevivir al borrado de una atracción sin reservas activas.
        builder.HasOne<Entities.Identity.CustomerEntity>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
