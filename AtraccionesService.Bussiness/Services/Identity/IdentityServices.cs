using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.Application.Validators;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Identity;

namespace AtraccionesService.Application.Services.Identity;

/// <summary>
/// Aprovisiona el perfil local a partir de claims verificados. No almacena contraseñas, tokens ni credenciales OAuth2.
/// </summary>
public sealed class UserProfileService(
    IUnitOfWork unitOfWork,
    TransactionRunner transactions,
    BusinessClock clock) : IUserProfileService
{
    public async Task<RegisteredUserResult> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        new ValidationErrors()
            .When(!ValidationErrors.IsEmail(command.Email), "email", "El claim email del token no es un correo válido.")
            .When(command.User.EmailVerified == false, "email", "El claim email del token no está verificado.")
            .ThrowIfAny();
        BillingValidator.Validate(command.Billing, requireCoreFields: false);

        try
        {
            return await transactions.ExecuteAsync(async () =>
            {
                var existing = await FindExistingAsync(command.User, cancellationToken);
                if (existing is not null)
                {
                    return existing;
                }

                var now = clock.UtcNow;
                var user = User.Register(command.User.Issuer, command.User.Subject, command.Email, now);
                var customer = Customer.CreateFor(user, now);
                var billing = command.Billing;
                customer.UpdateBilling(billing.BillingName, billing.BillingEmail, billing.BillingAddress, billing.TaxId, billing.PaymentMethodReference, now);

                // Usuario y cliente se crean en la misma transacción: no quedan registros parciales.
                await unitOfWork.Users.AddAsync(user, cancellationToken);
                await unitOfWork.Customers.AddAsync(customer, cancellationToken);
                return new RegisteredUserResult(user.Id, user.Email, user.Status.ToString(), customer.Id, user.CreatedAt, Created: true);
            }, cancellationToken);
        }
        catch (ConflictException conflict) when (conflict.Code == ConflictException.ConcurrencyConflict)
        {
            // Registro concurrente del mismo sub: la unicidad issuer + subject garantiza un único perfil.
            var existing = await FindExistingAsync(command.User, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return existing;
        }
    }

    public async Task<UserResult> GetCurrentAsync(AuthenticatedUser user, CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.Users.GetByIdentityAsync(user.Issuer, user.Subject, cancellationToken)
            ?? throw new NotFoundException("El perfil local no existe.");
        return new UserResult(existing.Id, existing.Email, existing.Status.ToString(), existing.CreatedAt, existing.UpdatedAt);
    }

    private async Task<RegisteredUserResult?> FindExistingAsync(AuthenticatedUser identity, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Users.GetByIdentityAsync(identity.Issuer, identity.Subject, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var customer = await unitOfWork.Customers.GetByUserIdAsync(user.Id, cancellationToken)
            ?? throw new ConflictException("PROFILE_INCOMPLETE", "El perfil existe sin cliente asociado.");
        return new RegisteredUserResult(user.Id, user.Email, user.Status.ToString(), customer.Id, user.CreatedAt, Created: false);
    }
}

/// <summary>Cliente y facturación del usuario autenticado.</summary>
public sealed class CustomerService(
    IUnitOfWork unitOfWork,
    TransactionRunner transactions,
    CustomerResolver customers,
    BusinessClock clock) : ICustomerService
{
    public async Task<CustomerResult> GetCurrentAsync(AuthenticatedUser user, CancellationToken cancellationToken) =>
        ToResult((await customers.FindAsync(user, cancellationToken) ?? throw NotFound()).Customer);

    /// <summary>Reemplaza los datos de facturación; no modifica usuario, email ni identidad OAuth2.</summary>
    public async Task<CustomerResult> UpdateCurrentAsync(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        BillingValidator.Validate(command.Billing, requireCoreFields: true);

        return await transactions.ExecuteAsync(async () =>
        {
            var customer = (await customers.FindAsync(command.User, cancellationToken) ?? throw NotFound()).Customer;
            var billing = command.Billing;
            customer.UpdateBilling(billing.BillingName, billing.BillingEmail, billing.BillingAddress, billing.TaxId, billing.PaymentMethodReference, clock.UtcNow);
            await unitOfWork.Customers.UpdateAsync(customer, cancellationToken);
            return ToResult(customer);
        }, cancellationToken);
    }

    private static CustomerResult ToResult(Customer c) => new(
        c.Id, c.UserId, c.BillingName, c.BillingEmail, c.BillingAddress, c.TaxId, c.PaymentMethodReference, c.CreatedAt, c.UpdatedAt);

    private static NotFoundException NotFound() => new("El cliente no existe. Debe aprovisionar su perfil.");
}

/// <summary>Permisos locales efectivos; un usuario inactivo o sin perfil no tiene permisos.</summary>
public sealed class LocalPermissionService(IUnitOfWork unitOfWork) : ILocalPermissionService
{
    public async Task<bool> HasPermissionAsync(AuthenticatedUser user, string permission, CancellationToken cancellationToken)
    {
        var local = await unitOfWork.Users.GetByIdentityAsync(user.Issuer, user.Subject, cancellationToken);
        if (local is not { IsActive: true })
        {
            return false;
        }

        var permissions = await unitOfWork.UserRoles.GetEffectivePermissionsAsync(local.Id, cancellationToken);
        return permissions.Contains(permission);
    }
}
