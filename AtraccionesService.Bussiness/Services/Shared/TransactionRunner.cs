using AtraccionesService.Application.Exceptions;
using AtraccionesService.DataManagment.Exceptions;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Common;

namespace AtraccionesService.Application.Services.Shared;

/// <summary>
/// Ejecuta una mutación dentro de la transacción de <see cref="IUnitOfWork"/>: guarda y confirma al terminar o revierte
/// ante cualquier error. Si ya hay una transacción activa, se une a ella. Traduce errores de dominio y de concurrencia
/// a excepciones de Application.
/// </summary>
public sealed class TransactionRunner(IUnitOfWork unitOfWork)
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var ownsTransaction = !unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var result = await action();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (ownsTransaction)
            {
                await unitOfWork.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch (Exception exception)
        {
            if (ownsTransaction)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }

            var translated = Translate(exception);
            if (translated is null)
            {
                throw;
            }

            throw translated;
        }
    }

    private static BusinessException? Translate(Exception exception) => exception switch
    {
        DomainException { Code: "INVALID_STATE_TRANSITION" } domain => new ConflictException(ConflictException.InvalidStateTransition, domain.Message),
        DomainException { Code: "INSUFFICIENT_AVAILABILITY" } domain => new ConflictException(ConflictException.InsufficientAvailability, domain.Message),
        DomainException domain => new ValidationException(domain.Message),
        ConcurrencyException => new ConflictException(ConflictException.ConcurrencyConflict, "El recurso fue modificado por otra operación. Reintente."),
        _ => null,
    };
}
