using AtraccionesService.Domain.Identity;

namespace AtraccionesService.DataManagment.Contracts.Identity;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Busca por la identidad OAuth2 única <c>issuer + subject</c>.</summary>
    Task<User?> GetByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken);

    /// <summary>Usuarios con el correo indicado (puede repetirse entre issuers distintos).</summary>
    Task<IReadOnlyList<User>> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    /// <summary>Actualiza estado, email y fechas; nunca datos de acceso del issuer.</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken);
}

/// <summary>
/// Cliente de facturación (1:1 con el usuario). No gestiona credenciales; los listados administrativos usan
/// <see cref="Administration.IAdminReportRepository"/> en lugar de ampliar este repositorio.
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Confirma que el cliente pertenece al usuario.</summary>
    Task<bool> BelongsToUserAsync(Guid customerId, Guid userId, CancellationToken cancellationToken);

    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task UpdateAsync(Customer customer, CancellationToken cancellationToken);
}

/// <summary>Roles locales y sus permisos. No emite ni modifica scopes OAuth2.</summary>
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken);

    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken);

    Task AddAsync(Role role, CancellationToken cancellationToken);
}

/// <summary>Asignaciones de roles locales. Las revocaciones conservan el registro con actor, motivo y fecha.</summary>
public interface IUserRoleRepository
{
    /// <summary>Permisos efectivos: unión de los permisos de los roles asignados y no revocados.</summary>
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserRoleAssignment>> GetActiveAssignmentsAsync(Guid userId, CancellationToken cancellationToken);

    Task AssignAsync(UserRoleAssignment assignment, CancellationToken cancellationToken);

    /// <summary>Revoca la asignación activa del rol. Devuelve <c>false</c> si no había ninguna.</summary>
    Task<bool> RevokeAsync(Guid userId, Guid roleId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
