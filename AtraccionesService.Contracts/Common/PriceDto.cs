using System.ComponentModel.DataAnnotations;

namespace AtraccionesService.Contracts.Common;

/// <summary>
/// Representación monetaria (CORRECCIONES_CONTRATO.md §3.9): moneda ISO 4217 obligatoria,
/// importe positivo con un máximo de dos decimales.
/// </summary>
public sealed class PriceDto
{
    [Required]
    [RegularExpression(ValidationPatterns.CurrencyCode, ErrorMessage = "currency debe ser un código ISO 4217 de tres letras mayúsculas.")]
    public string Currency { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "total debe ser mayor que cero.")]
    [TwoDecimalPlaces]
    public decimal Total { get; set; }
}

/// <summary>Rechaza importes con más de dos decimales.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TwoDecimalPlacesAttribute : ValidationAttribute
{
    public TwoDecimalPlacesAttribute()
        : base("{0} admite como máximo dos decimales.")
    {
    }

    public override bool IsValid(object? value) =>
        value is not decimal amount || decimal.Round(amount, 2) == amount;
}
