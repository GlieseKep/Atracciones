using System.Text.RegularExpressions;

namespace AtraccionesService.Domain.Common;

/// <summary>Importe positivo con moneda ISO 4217 y máximo dos decimales.</summary>
public sealed partial record Money
{
    public Money(string currency, decimal amount)
    {
        if (currency is null || !CurrencyPattern().IsMatch(currency))
        {
            throw new DomainException("INVALID_CURRENCY", "La moneda debe ser un código ISO 4217.");
        }

        if (amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("INVALID_AMOUNT", "El importe debe ser positivo y tener como máximo dos decimales.");
        }

        Currency = currency;
        Amount = amount;
    }

    public string Currency { get; }

    public decimal Amount { get; }

    public Money Multiply(int quantity) => new(Currency, Amount * Guard.Positive(quantity, "quantity"));

    public static bool IsValidCurrency(string? currency) => currency is not null && CurrencyPattern().IsMatch(currency);

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
