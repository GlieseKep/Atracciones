using AtraccionesService.Application.Abstractions.Authorization;

namespace AtraccionesService.Application.Commands.Customers;

public sealed record BillingData(
    string? BillingName,
    string? BillingEmail,
    string? BillingAddress,
    string? TaxId,
    string? PaymentMethodReference);

/// <summary>
/// Aprovisiona el usuario local y su cliente a partir de claims verificados. Repetirlo para el mismo
/// issuer + subject devuelve el perfil existente sin duplicarlo.
/// </summary>
public sealed record RegisterUserCommand(AuthenticatedUser User, string Email, BillingData Billing);

/// <summary>Reemplaza los datos de facturación del cliente del usuario autenticado.</summary>
public sealed record UpdateCustomerCommand(AuthenticatedUser User, BillingData Billing);
