using System.ComponentModel.DataAnnotations;

namespace AtraccionesService.Contracts.Identity;

/// <summary>
/// Cuerpo de <c>POST /auth/register</c> (CORRECCIONES_CONTRATO.md §5.2).
/// La identidad (<c>sub</c>, <c>email</c>) se toma exclusivamente de los claims verificados del token.
/// </summary>
public sealed class RegisterProfileRequest
{
    [StringLength(200, MinimumLength = 1)]
    public string? BillingName { get; set; }

    [EmailAddress]
    [StringLength(254)]
    public string? BillingEmail { get; set; }

    [StringLength(300, MinimumLength = 1)]
    public string? BillingAddress { get; set; }

    [StringLength(30, MinimumLength = 1)]
    public string? TaxId { get; set; }
}

public sealed class RegisterProfileResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid CustomerId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Respuesta de <c>GET /users/me</c> (CORRECCIONES_CONTRATO.md §5.5).</summary>
public sealed class UserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Respuesta de <c>GET /customers/me</c> (CORRECCIONES_CONTRATO.md §5.7).</summary>
public sealed class CustomerResponse
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string? BillingName { get; init; }

    public string? BillingEmail { get; init; }

    public string? BillingAddress { get; init; }

    public string? TaxId { get; init; }

    public string? PaymentMethodReference { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Cuerpo de <c>PUT /customers/me</c> (CORRECCIONES_CONTRATO.md §5.8). Reemplaza todos los datos de facturación;
/// no modifica <c>userId</c>, <c>email</c> ni el <c>sub</c> OAuth2.
/// </summary>
public sealed class UpdateCustomerRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string BillingName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string BillingEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string BillingAddress { get; set; } = string.Empty;

    [StringLength(30, MinimumLength = 1)]
    public string? TaxId { get; set; }

    /// <summary>Referencia local para la simulación de pago; nunca un número de tarjeta ni credencial.</summary>
    [RegularExpression(Common.ValidationPatterns.PaymentMethodReference, ErrorMessage = "paymentMethodReference debe ser una referencia alfanumérica, no un dato financiero.")]
    public string? PaymentMethodReference { get; set; }
}
