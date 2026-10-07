using AtraccionesService.DataManagment.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AtraccionesService.DataAcess.Exceptions;

/// <summary>Error de persistencia de DataAccess que no corresponde a concurrencia ni unicidad.</summary>
public sealed class DataAccessException(string message, Exception? innerException = null) : DataManagementException(message, innerException);

internal static class DbContextSaveExtensions
{
    /// <summary>
    /// Guarda y traduce errores del proveedor a contratos de DataManagement: conflictos de versión y violaciones de
    /// unicidad se exponen como <see cref="ConcurrencyException"/> sin detalles del motor.
    /// </summary>
    public static async Task<int> SaveTranslatedAsync(this DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyException("El registro fue modificado por otra operación.", exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConcurrencyException("Se violó una restricción de unicidad.", exception);
        }
        catch (DbUpdateException exception)
        {
            throw new DataAccessException("No se pudieron guardar los cambios.", exception);
        }
    }

    public static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException switch
    {
        SqlException sql => sql.Number is 2601 or 2627,
        SqliteException sqlite => sqlite.SqliteErrorCode == 19 && sqlite.SqliteExtendedErrorCode is 2067 or 1555,
        _ => false,
    };
}
