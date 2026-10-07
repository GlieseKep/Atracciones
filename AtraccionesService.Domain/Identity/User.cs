using AtraccionesService.Domain.Common;

namespace AtraccionesService.Domain.Identity;

public enum UserStatus
{
    ACTIVE,
    LOCKED,
    DISABLED
}

/// <summary>
/// Perfil local vinculado a una identidad OAuth2 (<see cref="OAuthIssuer"/> + <see cref="OAuthSubject"/>, únicos).
/// No almacena contraseñas ni tokens.
/// </summary>
public sealed class User
{
    private User(Guid id, string issuer, string subject, string email, UserStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        OAuthIssuer = Guard.NotBlank(issuer, "issuer", 300);
        OAuthSubject = Guard.NotBlank(subject, "subject", 300);
        Email = Guard.NotBlank(email, "email", 254);
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public string OAuthIssuer { get; }

    public string OAuthSubject { get; }

    public string Email { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status == UserStatus.ACTIVE;

    public static User Register(string issuer, string subject, string email, DateTimeOffset now) =>
        new(Guid.NewGuid(), issuer, subject, email, UserStatus.ACTIVE, now, now);

    public static User Restore(Guid id, string issuer, string subject, string email, UserStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, issuer, subject, email, status, createdAt, updatedAt);
}

/// <summary>Datos de facturación del cliente asociado a un usuario (relación 1:1).</summary>
public sealed class Customer
{
    private Customer(Guid id, Guid userId, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string? BillingName { get; private set; }

    public string? BillingEmail { get; private set; }

    public string? BillingAddress { get; private set; }

    public string? TaxId { get; private set; }

    /// <summary>Referencia local de la simulación de pago; nunca un dato financiero real.</summary>
    public string? PaymentMethodReference { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Customer CreateFor(User user, DateTimeOffset now) => new(Guid.NewGuid(), user.Id, now);

    public static Customer Restore(
        Guid id, Guid userId, string? billingName, string? billingEmail, string? billingAddress, string? taxId,
        string? paymentMethodReference, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var customer = new Customer(id, userId, createdAt);
        customer.Apply(billingName, billingEmail, billingAddress, taxId, paymentMethodReference);
        customer.UpdatedAt = updatedAt;
        return customer;
    }

    public void UpdateBilling(string? billingName, string? billingEmail, string? billingAddress, string? taxId, string? paymentMethodReference, DateTimeOffset now)
    {
        Apply(billingName, billingEmail, billingAddress, taxId, paymentMethodReference);
        UpdatedAt = now;
    }

    private void Apply(string? billingName, string? billingEmail, string? billingAddress, string? taxId, string? paymentMethodReference)
    {
        BillingName = Guard.Optional(billingName, "billingName", 200);
        BillingEmail = Guard.Optional(billingEmail, "billingEmail", 254);
        BillingAddress = Guard.Optional(billingAddress, "billingAddress", 300);
        TaxId = Guard.Optional(taxId, "taxId", 30);
        PaymentMethodReference = Guard.Optional(paymentMethodReference, "paymentMethodReference", 64);
    }
}
