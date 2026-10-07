using System.Text.Json.Serialization;

namespace AtraccionesService.Contracts.Catalog;

[JsonConverter(typeof(JsonStringEnumConverter<SlotStatus>))]
public enum SlotStatus
{
    AVAILABLE,
    SOLD_OUT
}

/// <summary>
/// Respuesta de <c>GET /atracciones/{id}/availability</c> (CORRECCIONES_CONTRATO.md §3.7).
/// La fecha y las horas son locales de la atracción; <see cref="TimeZone"/> es un identificador IANA.
/// </summary>
public sealed class AvailabilityResponse
{
    public DateOnly Date { get; init; }

    public string TimeZone { get; init; } = string.Empty;

    /// <summary>Suma de cupos disponibles de todas las franjas de la fecha.</summary>
    public int AvailableSpots { get; init; }

    public IReadOnlyList<AvailabilitySlotDto> Times { get; init; } = [];
}

public sealed class AvailabilitySlotDto
{
    /// <summary>Hora local <c>HH:mm</c>; identifica la franja dentro de la fecha.</summary>
    public string Time { get; init; } = string.Empty;

    public int AvailableSpots { get; init; }

    public SlotStatus Status { get; init; }
}
