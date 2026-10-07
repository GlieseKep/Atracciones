import {
  newId,
  Order,
  PaymentSimulation,
  PAYMENT_METHODS,
  Reservation,
  type IsoDate,
  type LocalTime,
  type PaymentMethod,
  type Purchase,
} from '@atracciones/domain';
import type { UnitOfWork, UnitOfWorkFactory } from '@atracciones/data-management';
import { ConflictError, NotFoundError, PaymentSimulationError, ValidationError } from '../errors';
import type {
  CancelOrderCommand,
  CreateOrderCommand,
  CreatePurchaseCommand,
  OrderEventsResult,
  OrderResult,
  PaymentSimulationResult,
  PurchaseResult,
  SimulatePaymentCommand,
} from '../models';
import type { AuthenticatedUser } from '../shared/auth';
import type { BusinessClock } from '../shared/clock';
import { PriceCalculator, type CustomerContext, type CustomerResolver, type PaymentSimulationPolicy } from '../shared/customers';
import { latestPayment, toOrderEventResult, toOrderResult, toPaymentResult, toPurchaseResult } from '../shared/mappers';
import type { BusinessOptions } from '../shared/options';
import { IdempotentOperations, type IdempotencyService } from '../shared/transactions';
import { validatePaymentRequest, ValidationErrors, type SlotRequestValidator } from '../shared/validation';

export interface PlacedOrder {
  purchase: Purchase;
  order: Order;
  reservation: Reservation;
}

const orderNotFound = () => new NotFoundError('El pedido no existe.');

/**
 * Pasos compartidos por compra directa, pedidos y pagos. Se ejecutan dentro de la transacción del caso de uso:
 * validar disponibilidad, tomar cupos, fijar el precio, crear reserva/compra/pedido y registrar eventos.
 */
export class OrderWorkflow {
  constructor(
    private readonly clock: BusinessClock,
    private readonly options: BusinessOptions,
    private readonly paymentPolicy: PaymentSimulationPolicy,
  ) {}

  async placePendingOrder(
    uow: UnitOfWork, customer: CustomerContext, attractionId: string, date: IsoDate, time: LocalTime, quantity: number, idempotencyKey: string,
  ): Promise<PlacedOrder> {
    const now = this.clock.utcNow;
    const attraction = await uow.attractions.getById(attractionId);
    if (!attraction) throw new NotFoundError('La atracción no existe.');
    const slot = await uow.availability.getSlot(attractionId, date, time);
    if (!slot) throw new ConflictError(ConflictError.SLOT_UNAVAILABLE, 'La atracción no opera en la fecha y hora seleccionadas.');
    if (!(await uow.availability.tryReserveQuantity(slot.id, quantity))) {
      throw new ConflictError(ConflictError.INSUFFICIENT_AVAILABILITY, 'No hay cupos suficientes para la franja solicitada.');
    }
    const unitPrice = PriceCalculator.unitPrice(attraction);
    const total = PriceCalculator.total(attraction, quantity);
    const reservation = Reservation.create({
      attractionId, customerId: customer.customer.id, date, time, ticketCount: quantity, totalPrice: total,
      customerName: customer.customer.billingName ?? customer.user.email,
      customerEmail: customer.customer.billingEmail ?? customer.user.email,
      initialStatus: 'PENDING', now,
    });
    const purchase: Purchase = {
      id: newId(), customerId: customer.customer.id, attractionId, serviceDate: date, serviceTime: time, quantity, unitPrice, total,
      requestIdempotencyKey: idempotencyKey, createdAt: now,
    };
    const order = Order.placePending({
      customerId: customer.customer.id,
      purchaseId: purchase.id,
      reservationId: reservation.id,
      items: [{ id: newId(), attractionId, serviceDate: date, serviceTime: time, quantity, unitPrice, availabilitySlotId: slot.id }],
      holdExpiresAt: new Date(now.getTime() + this.options.holdMinutes * 60_000),
      now,
    });
    await uow.reservations.add(reservation);
    await uow.purchases.add(purchase);
    await uow.orders.add(order);
    await uow.inventory.add({
      id: newId(), attractionId, availabilitySlotId: slot.id, quantity, movementType: 'CONFIRMED',
      purchaseId: purchase.id, reservationId: reservation.id, createdAt: now,
    });
    await uow.orderEvents.add({ id: newId(), orderId: order.id, eventType: 'ORDER_CREATED', previousStatus: null, newStatus: 'PENDING_PAYMENT', createdAt: now });
    return { purchase, order, reservation };
  }

  /**
   * Crea la simulación y procesa un intento determinista. Si se aprueba, liquida el pago, marca el pedido PAID y
   * confirma la reserva. Un rechazo o fallo deja el pedido PENDING_PAYMENT para reintentos dentro del límite.
   */
  async processPayment(uow: UnitOfWork, order: Order, method: PaymentMethod): Promise<PaymentSimulation> {
    const now = this.clock.utcNow;
    const previousAttempts = (await uow.payments.getByOrder(order.id)).length;
    if (previousAttempts >= this.options.maxPaymentAttemptsPerOrder) {
      throw new ConflictError('PAYMENT_ATTEMPTS_EXCEEDED', 'Se alcanzó el número máximo de intentos de pago para el pedido.');
    }
    const payment = PaymentSimulation.start(order.id, method, order.total, now);
    await uow.payments.add(payment);
    await this.addPaymentEvent(uow, payment, 'PAYMENT_REQUESTED', now);
    const outcome = this.paymentPolicy.evaluate(order.total, method, previousAttempts + 1);
    await uow.payments.addAttempt({
      id: newId(), paymentSimulationId: payment.id, attemptNumber: previousAttempts + 1, status: outcome.status,
      responseCode: outcome.responseCode, responseMessage: outcome.responseMessage, createdAt: now,
    });
    if (outcome.status === 'AUTHORIZED') {
      payment.transitionTo('AUTHORIZED', now);
      await this.addPaymentEvent(uow, payment, 'PAYMENT_AUTHORIZED', now);
      payment.transitionTo('SETTLED', now);
      await this.addPaymentEvent(uow, payment, 'PAYMENT_SETTLED', now);
      await this.markPaid(uow, order, now);
    } else {
      payment.transitionTo(outcome.status, now, outcome.responseMessage);
      await this.addPaymentEvent(uow, payment, `PAYMENT_${outcome.status}`, now);
    }
    await uow.payments.update(payment);
    return payment;
  }

  /** Cancela un pedido pendiente: libera cupos, cancela la reserva asociada y registra eventos. */
  async cancelPendingOrder(uow: UnitOfWork, order: Order, reason: string): Promise<void> {
    const now = this.clock.utcNow;
    const previous = order.transitionTo('CANCELLED', now, reason);
    await uow.orders.update(order);
    await uow.orderEvents.add({ id: newId(), orderId: order.id, eventType: 'ORDER_CANCELLED', previousStatus: previous, newStatus: 'CANCELLED', createdAt: now });
    for (const item of order.items) {
      await uow.availability.releaseQuantity(item.availabilitySlotId, item.quantity);
      await uow.inventory.add({
        id: newId(), attractionId: item.attractionId, availabilitySlotId: item.availabilitySlotId, quantity: -item.quantity,
        movementType: 'RELEASED', purchaseId: order.purchaseId, reservationId: order.reservationId, createdAt: now,
      });
    }
    if (order.reservationId) {
      const reservation = await uow.reservations.getById(order.reservationId);
      if (reservation?.canCancel) {
        reservation.cancel(reason);
        await uow.reservations.update(reservation);
      }
    }
  }

  private async markPaid(uow: UnitOfWork, order: Order, now: Date): Promise<void> {
    const previous = order.transitionTo('PAID', now);
    await uow.orders.update(order);
    await uow.orderEvents.add({ id: newId(), orderId: order.id, eventType: 'PAYMENT_SETTLED', previousStatus: previous, newStatus: 'PAID', createdAt: now });
    if (order.reservationId) {
      const reservation = await uow.reservations.getById(order.reservationId);
      if (reservation?.status === 'PENDING') {
        reservation.confirm();
        await uow.reservations.update(reservation);
      }
    }
  }

  private addPaymentEvent(uow: UnitOfWork, payment: PaymentSimulation, eventType: string, now: Date): Promise<void> {
    const payload = JSON.stringify({
      status: payment.status,
      amount: payment.amount.amount,
      currency: payment.amount.currency,
      gatewayReference: payment.gatewayReference,
      failureReason: payment.failureReason,
    });
    return uow.paymentEvents.add({ id: newId(), paymentSimulationId: payment.id, eventType, payload, createdAt: now });
  }
}

/**
 * Compra directa: una sola transacción valida disponibilidad, fija el precio en el servidor, crea reserva/compra/pedido
 * y, si se indica método de pago, procesa el pago simulado. Sin carrito ni sesión de checkout.
 */
export class PurchaseService {
  constructor(
    private readonly idempotency: IdempotencyService,
    private readonly customers: CustomerResolver,
    private readonly slots: SlotRequestValidator,
    private readonly workflow: OrderWorkflow,
  ) {}

  create(command: CreatePurchaseCommand): Promise<PurchaseResult> {
    this.slots.validate(command.date, command.time, command.quantity);
    let method: PaymentMethod | null = null;
    if (command.paymentMethod !== null) {
      if (!PAYMENT_METHODS.includes(command.paymentMethod as PaymentMethod)) {
        throw new ValidationError('paymentMethod no es un método soportado por la simulación.');
      }
      method = command.paymentMethod as PaymentMethod;
    }
    const payload = {
      attractionId: command.attractionId, date: command.date, time: command.time, quantity: command.quantity, paymentMethod: command.paymentMethod,
    };
    return this.idempotency.execute(command.user, IdempotentOperations.CreatePurchase, command.idempotencyKey, payload, async (uow) => {
      const customer = await this.customers.require(uow, command.user);
      const placed = await this.workflow.placePendingOrder(uow, customer, command.attractionId, command.date, command.time, command.quantity, command.idempotencyKey);
      const payment = method ? await this.workflow.processPayment(uow, placed.order, method) : null;
      return toPurchaseResult(placed.order, placed.purchase, payment);
    });
  }
}

export class OrderService {
  constructor(
    private readonly units: UnitOfWorkFactory,
    private readonly idempotency: IdempotencyService,
    private readonly customers: CustomerResolver,
    private readonly slots: SlotRequestValidator,
    private readonly workflow: OrderWorkflow,
  ) {}

  /** Crea un pedido PENDING_PAYMENT con snapshot de precio y retención temporal de cupos. */
  create(command: CreateOrderCommand): Promise<OrderResult> {
    this.slots.validate(command.date, command.time, command.quantity);
    const payload = { attractionId: command.attractionId, date: command.date, time: command.time, quantity: command.quantity };
    return this.idempotency.execute(command.user, IdempotentOperations.CreateOrder, command.idempotencyKey, payload, async (uow) => {
      const customer = await this.customers.require(uow, command.user);
      const placed = await this.workflow.placePendingOrder(uow, customer, command.attractionId, command.date, command.time, command.quantity, command.idempotencyKey);
      return toOrderResult(placed.order, null);
    });
  }

  async get(orderId: string, user: AuthenticatedUser): Promise<OrderResult> {
    const uow = this.units.create();
    const order = await this.getOwned(uow, orderId, user);
    return toOrderResult(order, latestPayment(await uow.payments.getByOrder(order.id)));
  }

  async getEvents(orderId: string, user: AuthenticatedUser): Promise<OrderEventsResult> {
    const uow = this.units.create();
    const order = await this.getOwned(uow, orderId, user);
    const events = await uow.orderEvents.getByOrder(order.id);
    return {
      orderId: order.id,
      events: [...events].sort((a, b) => a.createdAt.getTime() - b.createdAt.getTime()).map(toOrderEventResult),
    };
  }

  /** El cliente solo cancela pedidos PENDING_PAYMENT; un pedido pagado requiere el caso de uso de reembolso. */
  cancel(command: CancelOrderCommand): Promise<OrderResult> {
    new ValidationErrors().requiredText(command.reason, 'reason', 500).throwIfAny();
    return this.idempotency.execute(
      command.user, IdempotentOperations.CancelOrder, command.idempotencyKey, { orderId: command.orderId, reason: command.reason },
      async (uow) => {
        const order = await this.getOwned(uow, command.orderId, command.user);
        if (order.status !== 'PENDING_PAYMENT') {
          throw new ConflictError(ConflictError.INVALID_STATE_TRANSITION, `No se puede cancelar un pedido ${order.status}; los pedidos pagados requieren reembolso.`);
        }
        await this.workflow.cancelPendingOrder(uow, order, command.reason.trim());
        return toOrderResult(order, latestPayment(await uow.payments.getByOrder(order.id)));
      },
    );
  }

  private async getOwned(uow: UnitOfWork, orderId: string, user: AuthenticatedUser): Promise<Order> {
    const customer = await this.customers.find(uow, user);
    const order = customer ? await uow.orders.getByIdForCustomer(orderId, customer.customer.id) : null;
    if (!order) throw orderNotFound();
    return order;
  }
}

/**
 * Pago simulado de un pedido pendiente. Business decide el resultado y registra intento y eventos; el cliente no puede
 * fijar el estado. Un rechazo deja el pedido pendiente y se devuelve con estado REJECTED/FAILED.
 */
export class PaymentSimulationService {
  constructor(
    private readonly idempotency: IdempotencyService,
    private readonly customers: CustomerResolver,
    private readonly workflow: OrderWorkflow,
    private readonly clock: BusinessClock,
  ) {}

  simulate(command: SimulatePaymentCommand): Promise<PaymentSimulationResult> {
    const method = validatePaymentRequest(command.paymentMethod, command.amount, command.currency);
    const payload = { orderId: command.orderId, paymentMethod: command.paymentMethod, amount: command.amount, currency: command.currency };
    return this.idempotency.execute(command.user, IdempotentOperations.SimulatePayment, command.idempotencyKey, payload, async (uow) => {
      const customer = await this.customers.find(uow, command.user);
      const order = customer ? await uow.orders.getByIdForCustomer(command.orderId, customer.customer.id) : null;
      if (!order) throw orderNotFound();
      if (order.status !== 'PENDING_PAYMENT') {
        throw new ConflictError('ORDER_NOT_PAYABLE', `El pedido está ${order.status} y no admite pagos.`);
      }
      if (order.isHoldExpired(this.clock.utcNow)) {
        throw new ConflictError('ORDER_HOLD_EXPIRED', 'La retención de cupos del pedido expiró; cree un nuevo pedido.');
      }
      if (order.total.amount !== command.amount || order.total.currency !== command.currency) {
        throw new PaymentSimulationError('AMOUNT_MISMATCH', 'El importe o la moneda no coinciden con el total del pedido.');
      }
      return toPaymentResult(await this.workflow.processPayment(uow, order, method));
    });
  }
}
