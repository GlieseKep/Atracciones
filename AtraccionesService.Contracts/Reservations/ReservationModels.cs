using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AtraccionesService.Contracts.Reservations;

/// <summary>
/// Estados de reserva (CORRECCIONES_CONTRATO.md §4.7). Transiciones: PENDING → CONFIRMED | CANCELLED; CONFIRMED → CANCELLED.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReservationStatus>))]
public enum ReservationStatus
{
    PENDING,
    CONFIRMED,
    CANCELLED
}

/// <summary>Cuerpo de <c>POST /atracciones/{id}/reservations</c> (CORRECCIONES_CONTRATO.md §3.10).</summary>
public sealed class ReservationRequest
{
    [Required]
    public DateOnly? Date { get; set; }

    /// <summary>Hora local de la franja <c>HH:mm</c>.</summary>
    [Required]
    [RegularExpression(Common.ValidationPatterns.LocalTime, ErrorMessage = "time debe tener formato HH:mm.")]
    public string Time { get; set; } = string.Empty;

    [Range(1, 100)]
    public int TicketCount { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string CustomerEmail { get; set; } = string.Empty;
}

public sealed class CancelReservationRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ReservationResponse
{
    public Guid ReservationId { get; init; }

    public Guid AttractionId { get; init; }

    public ReservationStatus Status { get; init; }

    public DateOnly Date { get; init; }

    public string Time { get; init; } = string.Empty;

    public int TicketCount { get; init; }

    public Common.PriceDto TotalPrice { get; init; } = new();
}

/// <summary>
/// Parámetros de historial de reservas (CORRECCIONES_CONTRATO.md §3.5 y §5.21).
/// La propiedad siempre se resuelve con el usuario autenticado; no se acepta un identificador de usuario.
/// </summary>
public sealed class ReservationListParameters : IValidatableObject
{
    [Range(1, 100)]
    public int Limit { get; set; } = 20;

    [Range(0, int.MaxValue)]
    public int Offset { get; set; }

    public ReservationStatus? Status { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    /// <summary>Campo de orden: <c>date</c> o <c>-date</c> (descendente, por defecto).</summary>
    [RegularExpression("^-?date$", ErrorMessage = "sort admite 'date' o '-date'.")]
    public string Sort { get; set; } = "-date";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromDate is { } from && ToDate is { } to && to < from)
        {
            yield return new ValidationResult("toDate debe ser igual o posterior a fromDate.", [nameof(ToDate)]);
        }
    }
}
