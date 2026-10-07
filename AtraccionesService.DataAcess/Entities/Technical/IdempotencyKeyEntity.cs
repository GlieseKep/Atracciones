namespace AtraccionesService.DataAcess.Entities.Technical;

/// <summary>Clave de idempotencia; única por <c>Issuer + Subject + Operation + Key</c>. Nunca guarda tokens.</summary>
public class IdempotencyKeyEntity
{
    public Guid Id { get; set; }

    public string Issuer { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public Guid Key { get; set; }

    public string RequestHash { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? ResourceId { get; set; }

    public string? ResponseBody { get; set; }

    public string? ResponseHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
