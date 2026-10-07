using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Entities.Identity;
using AtraccionesService.DataAcess.Mapping;
using AtraccionesService.DataAcess.Repositories.Generic;
using AtraccionesService.DataManagment.Contracts.Identity;
using AtraccionesService.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace AtraccionesService.DataAcess.Repositories.Identity;

public sealed class UserRepository(AtraccionesDbContext context) : GenericRepository<UserEntity>(context), IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken))?.ToDomain();

    public async Task<User?> GetByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(u => u.OauthIssuer == issuer && u.OauthSubject == subject, cancellationToken))?.ToDomain();

    public async Task<IReadOnlyList<User>> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().Where(u => u.Email == email).ToListAsync(cancellationToken)).Select(u => u.ToDomain()).ToList();

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        var entity = new UserEntity();
        entity.Apply(user);
        return AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstAsync(u => u.Id == user.Id, cancellationToken);
        entity.Apply(user);
        await SaveChangesAsync(cancellationToken);
    }
}

public sealed class CustomerRepository(AtraccionesDbContext context) : GenericRepository<CustomerEntity>(context), ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken))?.ToDomain();

    public async Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken))?.ToDomain();

    public Task<bool> BelongsToUserAsync(Guid customerId, Guid userId, CancellationToken cancellationToken) =>
        Set.AnyAsync(c => c.Id == customerId && c.UserId == userId, cancellationToken);

    public Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        var entity = new CustomerEntity();
        entity.Apply(customer);
        return AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstAsync(c => c.Id == customer.Id, cancellationToken);
        entity.Apply(customer);
        await SaveChangesAsync(cancellationToken);
    }
}

public sealed class RoleRepository(AtraccionesDbContext context) : GenericRepository<RoleEntity>(context), IRoleRepository
{
    private IQueryable<RoleEntity> Roles => Set.AsNoTracking().Include(r => r.Permissions);

    public async Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken) =>
        ToDomain(await Roles.FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken));

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) =>
        ToDomain(await Roles.FirstOrDefaultAsync(r => r.Name == name, cancellationToken));

    /// <summary>Roles en dominio; oculta la variante genérica que devuelve entidades de persistencia.</summary>
    public new async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken) =>
        (await Roles.OrderBy(r => r.Name).ToListAsync(cancellationToken)).Select(r => ToDomain(r)!).ToList();

    public Task AddAsync(Role role, CancellationToken cancellationToken) => AddAsync(new RoleEntity
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        CreatedAt = role.CreatedAt,
        Permissions = role.Permissions.Select(p => new RolePermissionEntity { RoleId = role.Id, Permission = p }).ToList(),
    }, cancellationToken);

    private static Role? ToDomain(RoleEntity? e) =>
        e is null ? null : new Role(e.Id, e.Name, e.Description, e.Permissions.Select(p => p.Permission).Order().ToList(), e.CreatedAt);
}

/// <summary>Asignaciones de roles; los permisos efectivos son la unión de los roles no revocados.</summary>
public sealed class UserRoleRepository(AtraccionesDbContext context) : GenericRepository<UserRoleEntity>(context), IUserRoleRepository
{
    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var permissions = await Set.AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.RevokedAt == null)
            .SelectMany(ur => ur.Role!.Permissions.Select(p => p.Permission))
            .Distinct()
            .ToListAsync(cancellationToken);
        return permissions.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<UserRoleAssignment>> GetActiveAssignmentsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().Where(ur => ur.UserId == userId && ur.RevokedAt == null).ToListAsync(cancellationToken))
        .Select(ur => new UserRoleAssignment(ur.Id, ur.UserId, ur.RoleId, ur.AssignedByUserId, ur.Reason, ur.AssignedAt, ur.RevokedAt))
        .ToList();

    public Task AssignAsync(UserRoleAssignment assignment, CancellationToken cancellationToken) => AddAsync(new UserRoleEntity
    {
        Id = assignment.Id,
        UserId = assignment.UserId,
        RoleId = assignment.RoleId,
        AssignedByUserId = assignment.AssignedByUserId,
        Reason = assignment.Reason,
        AssignedAt = assignment.AssignedAt,
        RevokedAt = assignment.RevokedAt,
    }, cancellationToken);

    public async Task<bool> RevokeAsync(Guid userId, Guid roleId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var updated = await Set
            .Where(ur => ur.UserId == userId && ur.RoleId == roleId && ur.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(ur => ur.RevokedAt, revokedAt), cancellationToken);
        return updated > 0;
    }
}
