using AtraccionesService.DataAcess.Configurations.Catalog;
using AtraccionesService.DataAcess.Entities.Catalog;
using AtraccionesService.DataAcess.Entities.Ecommerce;
using AtraccionesService.DataAcess.Entities.Identity;
using AtraccionesService.DataAcess.Entities.Technical;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtraccionesService.DataAcess.Configurations.Ecommerce;

internal static class StatusValues
{
    public const string Order = "'PENDING_PAYMENT', 'PAID', 'FULFILLED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'";
    public const string Payment = "'PENDING', 'AUTHORIZED', 'SETTLED', 'REJECTED', 'FAILED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'";
    public const string Method = "'CARD', 'BANK_TRANSFER'";
}

public sealed class PurchaseConfiguration : IEntityTypeConfiguration<PurchaseEntity>
{
    public void Configure(EntityTypeBuilder<PurchaseEntity> builder)
    {
        builder.ToTable("purchases", t => t.HasCheckConstraint("CK_purchases_quantity", "Quantity > 0"));
        builder.HasKey(p => p.Id);
        builder.ComplexProperty(p => p.UnitPrice, p => PriceMapping.Map(p, "UnitPrice"));
        builder.Property(p => p.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(p => p.CustomerId);
        builder.HasIndex(p => new { p.AttractionId, p.ServiceDate });
        builder.HasOne<CustomerEntity>().WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.ToTable("orders", t => t.HasCheckConstraint("CK_orders_status", $"Status IN ({StatusValues.Order})"));
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasMaxLength(30).IsRequired();
        builder.Property(o => o.Currency).HasMaxLength(3).IsRequired();
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.PurchaseId).IsUnique();
        builder.HasIndex(o => o.ReservationId);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasOne<CustomerEntity>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseEntity>().WithMany().HasForeignKey(o => o.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReservationEntity>().WithMany().HasForeignKey(o => o.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItemEntity>
{
    public void Configure(EntityTypeBuilder<OrderItemEntity> builder)
    {
        builder.ToTable("order_items", t => t.HasCheckConstraint("CK_order_items_quantity", "Quantity > 0"));
        builder.HasKey(i => i.Id);
        builder.ComplexProperty(i => i.UnitPrice, p => PriceMapping.Map(p, "UnitPrice"));
        builder.HasIndex(i => i.AvailabilityId);
    }
}

public sealed class OrderEventConfiguration : IEntityTypeConfiguration<OrderEventEntity>
{
    public void Configure(EntityTypeBuilder<OrderEventEntity> builder)
    {
        builder.ToTable("order_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.PreviousStatus).HasMaxLength(30);
        builder.Property(e => e.NewStatus).HasMaxLength(30).IsRequired();
        builder.HasIndex(e => new { e.OrderId, e.CreatedAt });
        builder.HasOne<OrderEntity>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentSimulationConfiguration : IEntityTypeConfiguration<PaymentSimulationEntity>
{
    public void Configure(EntityTypeBuilder<PaymentSimulationEntity> builder)
    {
        builder.ToTable("payment_simulations", t =>
        {
            t.HasCheckConstraint("CK_payment_simulations_status", $"Status IN ({StatusValues.Payment})");
            t.HasCheckConstraint("CK_payment_simulations_method", $"PaymentMethod IN ({StatusValues.Method})");
            t.HasCheckConstraint("CK_payment_simulations_amount", "Amount > 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PaymentMethod).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Status).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.GatewayReference).HasMaxLength(64).IsRequired();
        builder.Property(p => p.FailureReason).HasMaxLength(300);
        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.GatewayReference).IsUnique();
        builder.HasIndex(p => p.CreatedAt);
        builder.HasOne<OrderEntity>().WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Attempts).WithOne().HasForeignKey(a => a.PaymentSimulationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Events).WithOne().HasForeignKey(e => e.PaymentSimulationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttemptEntity>
{
    public void Configure(EntityTypeBuilder<PaymentAttemptEntity> builder)
    {
        builder.ToTable("payment_attempts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasMaxLength(30).IsRequired();
        builder.Property(a => a.ResponseCode).HasMaxLength(10).IsRequired();
        builder.Property(a => a.ResponseMessage).HasMaxLength(300).IsRequired();
        builder.HasIndex(a => new { a.PaymentSimulationId, a.AttemptNumber }).IsUnique();
    }
}

public sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEventEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEventEntity> builder)
    {
        builder.ToTable("payment_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Payload).IsRequired();
        builder.HasIndex(e => new { e.PaymentSimulationId, e.CreatedAt });
    }
}

public sealed class RefundSimulationConfiguration : IEntityTypeConfiguration<RefundSimulationEntity>
{
    public void Configure(EntityTypeBuilder<RefundSimulationEntity> builder)
    {
        builder.ToTable("refund_simulations", t =>
        {
            t.HasCheckConstraint("CK_refund_simulations_status", "Status IN ('PENDING', 'SETTLED', 'FAILED', 'CANCELLED')");
            t.HasCheckConstraint("CK_refund_simulations_amount", "Amount > 0");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Amount).HasPrecision(18, 2);
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.HasOne<PaymentSimulationEntity>().WithMany().HasForeignKey(r => r.PaymentSimulationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserEntity>().WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovementEntity>
{
    public void Configure(EntityTypeBuilder<InventoryMovementEntity> builder)
    {
        builder.ToTable("inventory_movements", t =>
            t.HasCheckConstraint("CK_inventory_movements_type", "MovementType IN ('HELD', 'CONFIRMED', 'RELEASED', 'CANCELLED')"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MovementType).HasMaxLength(20).IsRequired();
        builder.HasIndex(m => m.AttractionId);
        builder.HasIndex(m => m.ReservationId);
        builder.HasIndex(m => m.PurchaseId);
        builder.HasIndex(m => m.AvailabilityId);
    }
}

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventEntity> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ActorSubject).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ResourceType).HasMaxLength(60).IsRequired();
        builder.Property(a => a.ResourceId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(500);
        builder.HasIndex(a => new { a.ResourceType, a.ResourceId });
        builder.HasIndex(a => a.CreatedAt);
    }
}

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntity> builder)
    {
        builder.ToTable("idempotency_keys", t => t.HasCheckConstraint("CK_idempotency_keys_status", "Status IN ('IN_PROGRESS', 'COMPLETED')"));
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Issuer).HasMaxLength(300).IsRequired();
        builder.Property(k => k.Subject).HasMaxLength(300).IsRequired();
        builder.Property(k => k.Operation).HasMaxLength(60).IsRequired();
        builder.Property(k => k.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.ResponseHash).HasMaxLength(64);
        builder.Property(k => k.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(k => new { k.Issuer, k.Subject, k.Operation, k.Key }).IsUnique();
        builder.HasIndex(k => new { k.Key, k.Operation });
        builder.HasIndex(k => k.ExpiresAt);
    }
}
