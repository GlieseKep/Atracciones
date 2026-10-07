using System.Text.RegularExpressions;
using AtraccionesService.Application.Commands.Attractions;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.DependencyInjection;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Common;
using AtraccionesService.Domain.Ecommerce;
using Microsoft.Extensions.Options;

namespace AtraccionesService.Application.Validators;

/// <summary>Reglas de catálogo: nombre, descripción, duración, precio, categorías, ubicaciones, idiomas y tipo de producto.</summary>
public static partial class AttractionValidator
{
    public static void Validate(AttractionData data)
    {
        var errors = new ValidationErrors()
            .RequiredText(data.Name, "name", 200, minLength: 3)
            .RequiredText(data.LongDescription, "longDescription", 5000)
            .When(data.Duration is null || !IsoDuration().IsMatch(data.Duration), "duration", "duration debe ser una duración ISO 8601, por ejemplo PT3H.")
            .When(!Money.IsValidCurrency(data.Price.Currency), "price.currency", "currency debe ser un código ISO 4217.")
            .When(data.Price.Total <= 0 || decimal.Round(data.Price.Total, 2) != data.Price.Total, "price.total", "total debe ser positivo con un máximo de dos decimales.")
            .When(data.Categories.Count == 0, "categories", "Se requiere al menos una categoría.")
            .When(data.Categories.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 60), "categories", "Las categorías no pueden estar vacías ni superar 60 caracteres.")
            .When(data.Locations.Count == 0, "locations", "Se requiere al menos una ubicación.")
            .When(!Enum.TryParse<ProductType>(data.ProductType, out _) || !Enum.IsDefined(Enum.Parse<ProductType>(data.ProductType)), "productType", "productType no es un tipo de producto soportado.")
            .When(data.SupportedLanguages.Any(l => !LanguageTag().IsMatch(l)), "supportedLanguages", "Los idiomas deben ser códigos ISO 639-1, por ejemplo 'es' o 'es-EC'.")
            .When(data.PhotoUrls.Any(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")), "photos", "Las fotos deben ser URLs http(s) absolutas.");

        if (data.Operator is { } op)
        {
            errors.When(op.Id <= 0, "operator.id", "operator.id debe ser positivo.").RequiredText(op.Name, "operator.name", 200);
        }

        for (var i = 0; i < data.Locations.Count; i++)
        {
            var location = data.Locations[i];
            var prefix = $"locations[{i}]";
            errors.RequiredText(location.Address, $"{prefix}.address", 300)
                .RequiredText(location.City, $"{prefix}.city", 120)
                .When(location.Country is null || !CountryCode().IsMatch(location.Country), $"{prefix}.country", "country debe ser ISO 3166-1 alfa-2.")
                .When(location.Latitude.HasValue != location.Longitude.HasValue, $"{prefix}.coordinates", "Latitud y longitud deben enviarse juntas.")
                .When(location.Latitude is < -90 or > 90, $"{prefix}.coordinates.latitude", "Latitud fuera de rango.")
                .When(location.Longitude is < -180 or > 180, $"{prefix}.coordinates.longitude", "Longitud fuera de rango.");
        }

        errors.ThrowIfAny();
    }

    public static bool IsLanguage(string value) => LanguageTag().IsMatch(value);

    public static bool IsCountry(string value) => CountryCode().IsMatch(value);

    [GeneratedRegex(@"^P(?!$)(\d+D)?(T(?=\d)(\d+H)?(\d+M)?)?$")]
    private static partial Regex IsoDuration();

    [GeneratedRegex("^[a-z]{2}(-[A-Z]{2})?$")]
    private static partial Regex LanguageTag();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryCode();
}

/// <summary>Validación de una franja solicitada (reservas, compras y pedidos).</summary>
public sealed class SlotRequestValidator(BusinessClock clock, IOptions<ApplicationOptions> options)
{
    /// <summary>Valida fecha futura, hora <c>HH:mm</c> y cantidad; devuelve la hora interpretada.</summary>
    public TimeOnly Validate(DateOnly date, string time, int quantity, string quantityField = "quantity", Action<ValidationErrorsProxy>? extra = null)
    {
        var errors = new ValidationErrors();
        var max = options.Value.MaxTicketsPerOperation;
        errors.When(quantity < 1 || quantity > max, quantityField, $"{quantityField} debe estar entre 1 y {max}.");

        if (!ValidationErrors.TryParseSlotTime(time, out var slotTime))
        {
            errors.Add("time", "time debe tener formato HH:mm.");
        }
        else if (!clock.IsFuture(date, slotTime))
        {
            errors.Add("date", "La fecha y hora seleccionadas deben ser futuras.");
        }

        extra?.Invoke(new ValidationErrorsProxy(errors));
        errors.ThrowIfAny();
        return slotTime;
    }
}

/// <summary>Permite añadir reglas adicionales al validador de franjas sin exponer el acumulador interno.</summary>
public sealed class ValidationErrorsProxy
{
    private readonly ValidationErrors _errors;

    internal ValidationErrorsProxy(ValidationErrors errors) => _errors = errors;

    public void RequiredText(string? value, string field, int maxLength) => _errors.RequiredText(value, field, maxLength);

    public void Email(string? value, string field) => _errors.Email(value, field, required: true);
}

/// <summary>Datos de facturación: formatos y referencia de pago que nunca puede ser un número de tarjeta.</summary>
public static partial class BillingValidator
{
    public static void Validate(BillingData billing, bool requireCoreFields)
    {
        var errors = new ValidationErrors();
        if (requireCoreFields)
        {
            errors.RequiredText(billing.BillingName, "billingName", 200)
                .Email(billing.BillingEmail, "billingEmail", required: true)
                .RequiredText(billing.BillingAddress, "billingAddress", 300);
        }
        else
        {
            errors.OptionalText(billing.BillingName, "billingName", 200)
                .Email(billing.BillingEmail, "billingEmail", required: false)
                .OptionalText(billing.BillingAddress, "billingAddress", 300);
        }

        errors.OptionalText(billing.TaxId, "taxId", 30)
            .When(billing.PaymentMethodReference is not null && !PaymentReference().IsMatch(billing.PaymentMethodReference),
                "paymentMethodReference", "paymentMethodReference debe ser una referencia local alfanumérica, no un dato financiero.")
            .ThrowIfAny();
    }

    [GeneratedRegex(@"^(?!\d+$)[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex PaymentReference();
}

public static class PaymentSimulationValidator
{
    public static PaymentMethod Validate(string paymentMethod, decimal amount, string currency)
    {
        var errors = new ValidationErrors()
            .When(!Enum.TryParse<PaymentMethod>(paymentMethod, out _), "paymentMethod", "paymentMethod no es un método soportado por la simulación.")
            .When(amount <= 0 || decimal.Round(amount, 2) != amount, "amount", "amount debe ser positivo con un máximo de dos decimales.")
            .When(!Money.IsValidCurrency(currency), "currency", "currency debe ser un código ISO 4217.");
        errors.ThrowIfAny();
        return Enum.Parse<PaymentMethod>(paymentMethod);
    }
}

public static class PaginationValidator
{
    public const int MaxLimit = 100;

    public static void Validate(int limit, int offset)
    {
        new ValidationErrors()
            .When(limit < 1 || limit > MaxLimit, "limit", $"limit debe estar entre 1 y {MaxLimit}.")
            .When(offset < 0, "offset", "offset no puede ser negativo.")
            .ThrowIfAny();
    }
}
