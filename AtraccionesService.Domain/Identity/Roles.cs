using AtraccionesService.Domain.Common;

namespace AtraccionesService.Domain.Identity;

/// <summary>Rol local con sus permisos. No representa scopes del issuer OAuth2.</summary>
public sealed record Role(Guid Id, string Name, string? Description, IReadOnlyList<string> Permissions, DateTimeOffset CreatedAt)
{
    public static Role Create(string name, string? description, IEnumerable<string> permissions, DateTimeOffset now) =>
        new(Guid.NewGuid(), Guard.NotBlank(name, "name", 60), description, permissions.Distinct(StringComparer.Ordinal).ToList(), now);
}

/// <summary>Asignación de un rol local a un usuario. Registra actor y motivo; la revocación no borra el registro.</summary>
public sealed record UserRoleAssignment(
    Guid Id,
    Guid UserId,
    Guid RoleId,
    Guid? AssignedByUserId,
    string Reason,
    DateTimeOffset AssignedAt,
    DateTimeOffset? RevokedAt)
{
    public bool IsActive => RevokedAt is null;

    public static UserRoleAssignment Assign(Guid userId, Guid roleId, Guid? assignedByUserId, string reason, DateTimeOffset now) =>
        new(Guid.NewGuid(), userId, roleId, assignedByUserId, Guard.NotBlank(reason, "reason", 500), now, null);
}
