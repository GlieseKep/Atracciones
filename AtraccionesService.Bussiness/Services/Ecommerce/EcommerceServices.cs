using AtraccionesService.Application.Abstractions.Idempotency;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Ecommerce;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Mappers;
using AtraccionesService.Application.Queries.Ecommerce;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.Application.Validators;
using AtraccionesService.DataManagment.UnitOfWork;
using AtraccionesService.Domain.Ecommerce;

namespace AtraccionesService.Application.Services.Ecommerce;

/// <summary>
/// Compra directa: una sola transacción valida disponibilidad, fija el precio en el servidor, crea reserva/compra/pedido y,
/// si se indica método de pago, procesa el pago simulado. Sin carrito ni sesión de checkout.
/// </summary>
public sealed class PurchaseService(
    IIdempotencyService idempotency,
    CustomerResolver customers,
    SlotRequestValidator slotValidator,
    OrderWorkflow workflow) : IPurchaseService
{
    public async Task<PurchaseResult> CreateAsync(CreatePurchaseCommand command, CancellationToken cancellationToken)
    {
        PaymentMethod? method = null;
        var time = slotValidator.Validate(command.Date, command.Time, command.Quantity);
        if (command.PaymentMethod is not null)
        {
            method = Enum.TryParse<PaymentMethod>(command.PaymentMethod, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new ValidationException("paymentMethod no es un método soportado por la simulación.");
        }

        var payload = new { command.AttractionId, command.Date, command.Time, command.Quantity, command.PaymentMethod };
        return await idempotency.ExecuteAsync(command.User, IdempotentOperations.CreatePurchase, command.IdempotencyKey, payload, async ct =>
        {
            var customer = await customers.RequireAsync(command.User, ct);
            var placed = await workflow.PlacePendingOrderAsync(customer, command.AttractionId, command.Date, time, command.Quantity, command.IdempotencyKey, ct);
            var payment = method is { } m ? await workflow.ProcessPaymentAsync(placed.Order, m, ct) : null;
            return placed.Order.ToPurchaseResult(placed.Purchase, payment);
        }, cancellationToken);
    }
}

public sealed class OrderService(
    IUnitOfWork unitOfWork,
    IIdempotencyService idempotency,
    CustomerResolver customers,
    SlotRequestValidator slotValidator,
    OrderWorkflow workflow) : IOrderService
{
    /// <summary>Crea un pedido PENDING_PAYMENT con snapshot de precio y retención temporal de cupos.</summary>
    public async Task<OrderResult> CreateAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var time = slotValidator.Validate(command.Date, command.Time, command.Quantity);

        var payload = new { command.AttractionId, command.Date, command.Time, command.Quantity };
        return await idempotency.ExecuteAsync(command.User, IdempotentOperations.CreateOrder, command.IdempotencyKey, payload, async ct =>
        {
            var customer = await customers.RequireAsync(command.User, ct);
            var placed = await workflow.PlacePendingOrderAsync(customer, command.AttractionId, command.Date, time, command.Quantity, command.IdempotencyKey, ct);
            return placed.Order.ToResult(null);
        }, cancellationToken);
    }

    public async Task<OrderResult> GetAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await GetOwnedAsync(query.OrderId, query.User, cancellationToken);
        var payments = await unitOfWork.Payments.GetByOrderAsync(order.Id, cancellationToken);
        return order.ToResult(payments.OrderBy(p => p.CreatedAt).LastOrDefault());
    }

    public async Task<OrderEventsResult> GetEventsAsync(GetOrderEventsQuery query, CancellationToken cancellationToken)
    {
        var order = await GetOwnedAsync(query.OrderId, query.User, cancellationToken);
        var events = await unitOfWork.OrderEvents.GetByOrderAsync(order.Id, cancellationToken);
        return new OrderEventsResult(order.Id, events.OrderBy(e => e.CreatedAt).Select(e => e.ToResult()).ToList());
    }

    /// <summary>
    /// El cliente solo cancela pedidos PENDING_PAYMENT; un pedido pagado requiere el caso de uso de reembolso.
    /// </summary>
    public async Task<OrderResult> CancelAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        new ValidationErrors().RequiredText(command.Reason, "reason", 500).ThrowIfAny();

        return await idempotency.ExecuteAsync(command.User, IdempotentOperations.CancelOrder, command.IdempotencyKey,
            new { command.OrderId, command.Reason }, async ct =>
            {
                var order = await GetOwnedAsync(command.OrderId, command.User, ct);
                if (order.Status != OrderStatus.PENDING_PAYMENT)
                {
                    throw new ConflictException(ConflictException.InvalidStateTransition,
                        $"No se puede cancelar un pedido {order.Status}; los pedidos pagados requieren reembolso.");
                }

                await workflow.CancelPendingOrderAsync(order, command.Reason.Trim(), ct);
                var payments = await unitOfWork.Payments.GetByOrderAsync(order.Id, ct);
                return order.ToResult(payments.OrderBy(p => p.CreatedAt).LastOrDefault());
            }, cancellationToken);
    }

    private async Task<Order> GetOwnedAsync(Guid orderId, Application.Abstractions.Authorization.AuthenticatedUser user, CancellationToken cancellationToken)
    {
        var customer = await customers.FindAsync(user, cancellationToken);
        var order = customer is null ? null : await unitOfWork.Orders.GetByIdForCustomerAsync(orderId, customer.Customer.Id, cancellationToken);
        return order ?? throw new NotFoundException("El pedido no existe.");
    }
}

/// <summary>
/// Pago simulado de un pedido pendiente. Application decide el resultado y registra intento y eventos; el cliente no puede
/// fijar el estado. Un rechazo deja el pedido pendiente y se devuelve con estado REJECTED/FAILED.
/// </summary>
public sealed class PaymentSimulationService(
    IUnitOfWork unitOfWork,
    IIdempotencyService idempotency,
    CustomerResolver customers,
    OrderWorkflow workflow,
    BusinessClock clock) : IPaymentSimulationService
{
    public async Task<PaymentSimulationResult> SimulateAsync(SimulatePaymentCommand command, CancellationToken cancellationToken)
    {
        var method = PaymentSimulationValidator.Validate(command.PaymentMethod, command.Amount, command.Currency);

        var payload = new { command.OrderId, command.PaymentMethod, command.Amount, command.Currency };
        return await idempotency.ExecuteAsync(command.User, IdempotentOperations.SimulatePayment, command.IdempotencyKey, payload, async ct =>
        {
            var customer = await customers.FindAsync(command.User, ct);
            var order = (customer is null ? null : await unitOfWork.Orders.GetByIdForCustomerAsync(command.OrderId, customer.Customer.Id, ct))
                ?? throw new NotFoundException("El pedido no existe.");

            if (order.Status != OrderStatus.PENDING_PAYMENT)
            {
                throw new ConflictException("ORDER_NOT_PAYABLE", $"El pedido está {order.Status} y no admite pagos.");
            }

            if (order.IsHoldExpired(clock.UtcNow))
            {
                throw new ConflictException("ORDER_HOLD_EXPIRED", "La retención de cupos del pedido expiró; cree un nuevo pedido.");
            }

            if (order.Total.Amount != command.Amount || order.Total.Currency != command.Currency)
            {
                throw new PaymentSimulationException("AMOUNT_MISMATCH", "El importe o la moneda no coinciden con el total del pedido.");
            }

            var payment = await workflow.ProcessPaymentAsync(order, method, ct);
            return payment.ToResult();
        }, cancellationToken);
    }
}
