using AtraccionesService.DataAcess.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtraccionesService.DataAcess.Configurations.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users", t => t.HasCheckConstraint("CK_users_status", "Status IN ('ACTIVE', 'LOCKED', 'DISABLED')"));
        builder.HasKey(u => u.Id);
        builder.Property(u => u.OauthIssuer).HasMaxLength(300).IsRequired();
        builder.Property(u => u.OauthSubject).HasMaxLength(300).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(254).IsRequired();
        builder.Property(u => u.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(u => new { u.OauthIssuer, u.OauthSubject }).IsUnique();
        builder.HasIndex(u => u.Email);
        builder.HasOne(u => u.Customer).WithOne().HasForeignKey<CustomerEntity>(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.Property(c => c.BillingName).HasMaxLength(200);
        builder.Property(c => c.BillingEmail).HasMaxLength(254);
        builder.Property(c => c.BillingAddress).HasMaxLength(300);
        builder.Property(c => c.TaxId).HasMaxLength(30);
        builder.Property(c => c.PaymentMethodReference).HasMaxLength(64);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(300);
        builder.HasIndex(r => r.Name).IsUnique();
        builder.HasMany(r => r.Permissions).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermissionEntity>
{
    public void Configure(EntityTypeBuilder<RolePermissionEntity> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(p => new { p.RoleId, p.Permission });
        builder.Property(p => p.Permission).HasMaxLength(100);
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
    public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(ur => new { ur.UserId, ur.RoleId });
        builder.HasOne(ur => ur.Role).WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserEntity>().WithMany().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserEntity>().WithMany().HasForeignKey(ur => ur.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
