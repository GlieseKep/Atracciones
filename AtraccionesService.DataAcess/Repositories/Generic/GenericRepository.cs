using System.Linq.Expressions;
using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AtraccionesService.DataAcess.Repositories.Generic;

/// <summary>
/// Operaciones comunes sobre entidades de persistencia. No contiene reglas de negocio y no se expone a Application:
/// los repositorios especializados lo usan internamente.
/// </summary>
public class GenericRepository<T>(AtraccionesDbContext context)
    where T : class
{
    protected AtraccionesDbContext Context { get; } = context;

    protected DbSet<T> Set => Context.Set<T>();

    public ValueTask<T?> GetByIdAsync(object id, CancellationToken cancellationToken) => Set.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken) => await Set.AsNoTracking().ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) =>
        Set.AnyAsync(predicate, cancellationToken);

    /// <summary>
    /// Agrega y guarda inmediatamente dentro de la transacción activa de la unidad de trabajo, para que las lecturas
    /// posteriores de la misma operación vean el cambio. La atomicidad la garantiza la transacción.
    /// </summary>
    public async Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        await Set.AddAsync(entity, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        if (Context.Entry(entity).State == EntityState.Detached)
        {
            Set.Update(entity);
        }

        return SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken)
    {
        Set.Remove(entity);
        return SaveChangesAsync(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Context.SaveTranslatedAsync(cancellationToken);
}
