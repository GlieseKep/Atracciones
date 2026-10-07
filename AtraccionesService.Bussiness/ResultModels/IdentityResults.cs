namespace AtraccionesService.Application.ResultModels;

/// <param name="Created"><c>true</c> si la operación creó el perfil; <c>false</c> si ya existía.</param>
public sealed record RegisteredUserResult(
    Guid Id,
    string Email,
    string Status,
    Guid CustomerId,
    DateTimeOffset CreatedAt,
    bool Created);

public sealed record UserResult(
    Guid Id,
    string Email,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerResult(
    Guid Id,
    Guid UserId,
    string? BillingName,
    string? BillingEmail,
    string? BillingAddress,
    string? TaxId,
    string? PaymentMethodReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
