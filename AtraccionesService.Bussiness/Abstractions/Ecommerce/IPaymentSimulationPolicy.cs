using AtraccionesService.Domain.Common;
using AtraccionesService.Domain.Ecommerce;

namespace AtraccionesService.Application.Abstractions.Ecommerce;

/// <summary>Resultado determinista de un intento de pago simulado.</summary>
public sealed record PaymentSimulationOutcome(PaymentStatus Status, string ResponseCode, string ResponseMessage);

/// <summary>Decide el resultado de la pasarela simulada. No llama a proveedores externos.</summary>
public interface IPaymentSimulationPolicy
{
    PaymentSimulationOutcome Evaluate(Money amount, PaymentMethod method, int attemptNumber);
}

/// <summary>
/// Política determinista basada en los céntimos del importe (al estilo de las tarjetas de prueba):
/// <c>.51</c> → REJECTED (fondos insuficientes), <c>.52</c> → FAILED (error de pasarela), cualquier otro → AUTHORIZED.
/// </summary>
public sealed class DeterministicPaymentSimulationPolicy : IPaymentSimulationPolicy
{
    public PaymentSimulationOutcome Evaluate(Money amount, PaymentMethod method, int attemptNumber)
    {
        var cents = (int)(amount.Amount * 100 % 100);
        return cents switch
        {
            51 => new PaymentSimulationOutcome(PaymentStatus.REJECTED, "51", "Fondos insuficientes (simulado)."),
            52 => new PaymentSimulationOutcome(PaymentStatus.FAILED, "96", "Error de la pasarela simulada."),
            _ => new PaymentSimulationOutcome(PaymentStatus.AUTHORIZED, "00", "Aprobado (simulado)."),
        };
    }
}
