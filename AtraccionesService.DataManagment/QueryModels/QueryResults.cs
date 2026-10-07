namespace AtraccionesService.DataManagment.QueryModels;

// DTOs de lectura de persistencia (no HTTP). Exponen solo los datos necesarios para listados y reportes:
// sin PII de clientes, credenciales ni datos financieros sensibles.

/// <summary>Resumen de una atracción para listados.</summary>
public sealed record AttractionQueryResult(
    Guid Id,
    string Name,
    string ProductType,
    string Currency,
    decimal Price,
    double? RatingScore,
    int? RatingReviewCount);

/// <summary>Elemento de listado de reservas, sin nombre ni correo del cliente.</summary>
public sealed record ReservationQueryResult(
    Guid Id,
    Guid AttractionId,
    DateOnly Date,
    TimeOnly Time,
    int TicketCount,
    string Status,
    string Currency,
    decimal Total);

/// <summary>Compra directa resumida con el estado de su pedido.</summary>
public sealed record PurchaseQueryResult(
    Guid Id,
    Guid AttractionId,
    DateOnly ServiceDate,
    TimeOnly ServiceTime,
    int Quantity,
    string Currency,
    decimal TotalAmount,
    Guid? OrderId,
    string? OrderStatus,
    DateTimeOffset CreatedAt);

/// <summary>Pedido resumido para listados y reportes.</summary>
public sealed record OrderQueryResult(
    Guid Id,
    Guid CustomerId,
    string Status,
    string Currency,
    decimal TotalAmount,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Pago simulado resumido; no incluye referencia de pasarela ni motivos internos.</summary>
public sealed record PaymentSimulationQueryResult(
    Guid Id,
    Guid OrderId,
    string PaymentMethod,
    string Status,
    string Currency,
    decimal Amount,
    int AttemptCount,
    DateTimeOffset CreatedAt);

/// <summary>Cantidad por estado.</summary>
public sealed record StatusCount(string Status, int Count);

/// <summary>Importe liquidado por moneda.</summary>
public sealed record CurrencyTotal(string Currency, decimal Amount);

/// <summary>Métricas agregadas del periodo para el tablero administrativo.</summary>
public sealed record AdminDashboardSummary(
    IReadOnlyList<StatusCount> OrdersByStatus,
    IReadOnlyList<StatusCount> PaymentsByStatus,
    IReadOnlyList<StatusCount> ReservationsByStatus,
    IReadOnlyList<CurrencyTotal> SettledRevenue);
