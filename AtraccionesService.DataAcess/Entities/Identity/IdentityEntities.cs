namespace AtraccionesService.DataAcess.Entities.Identity;

/// <summary>Perfil local; <c>OauthIssuer + OauthSubject</c> es único. Sin contraseñas ni tokens.</summary>
public class UserEntity
{
    public Guid Id { get; set; }

    public string OauthIssuer { get; set; } = string.Empty;

    public string OauthSubject { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public CustomerEntity? Customer { get; set; }
}

public class CustomerEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? BillingName { get; set; }

    public string? BillingEmail { get; set; }

    public string? BillingAddress { get; set; }

    public string? TaxId { get; set; }

    public string? PaymentMethodReference { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Rol local. No representa scopes del issuer OAuth2.</summary>
public class RoleEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<RolePermissionEntity> Permissions { get; set; } = [];
}

public class RolePermissionEntity
{
    public Guid RoleId { get; set; }

    public string Permission { get; set; } = string.Empty;
}

/// <summary>Asignación de rol con actor, motivo y fecha; <see cref="RevokedAt"/> la desactiva sin borrarla.</summary>
public class UserRoleEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public Guid? AssignedByUserId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public RoleEntity? Role { get; set; }
}
