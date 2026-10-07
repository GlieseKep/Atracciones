import { DomainError, Guard, Money, newId, roundCents, type IsoDate, type LocalTime } from './common';

export const ORDER_STATUSES = ['PENDING_PAYMENT', 'PAID', 'FULFILLED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'] as const;
export type OrderStatus = (typeof ORDER_STATUSES)[number];

export const PAYMENT_METHODS = ['CARD', 'BANK_TRANSFER'] as const;
export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

export const PAYMENT_STATUSES = [
  'PENDING', 'AUTHORIZED', 'SETTLED', 'REJECTED', 'FAILED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED',
] as const;
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number];

export type InventoryMovementType = 'CONFIRMED' | 'RELEASED' | 'CANCELLED';
export type IdempotencyStatus = 'IN_PROGRESS' | 'COMPLETED';

/** Compra directa de una franja; origina como máximo un pedido. */
export interface Purchase {
  id: string;
  customerId: string;
  attractionId: string;
  serviceDate: IsoDate;
  serviceTime: LocalTime;
  quantity: number;
  unitPrice: Money;
  total: Money;
  requestIdempotencyKey: string;
  createdAt: Date;
}

export interface OrderItem {
  id: string;
  attractionId: string;
  serviceDate: IsoDate;
  serviceTime: LocalTime;
  quantity: number;
  unitPrice: Money;
  availabilitySlotId: string;
}

const ORDER_TRANSITIONS: Partial<Record<OrderStatus, OrderStatus[]>> = {
  PENDING_PAYMENT: ['PAID', 'CANCELLED'],
  PAID: ['FULFILLED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'],
  PARTIALLY_REFUNDED: ['PARTIALLY_REFUNDED', 'REFUNDED'],
};

/**
 * Pedido con snapshot de precios. Transiciones: PENDING_PAYMENT → PAID | CANCELLED;
 * PAID → FULFILLED | CANCELLED | PARTIALLY_REFUNDED | REFUNDED; PARTIALLY_REFUNDED → PARTIALLY_REFUNDED | REFUNDED.
 */
export class Order {
  private _status: OrderStatus;
  private _holdExpiresAt: Date | null;
  private _cancellationReason: string | null;
  private _updatedAt: Date;

  private constructor(
    readonly id: string,
    readonly customerId: string,
    readonly purchaseId: string | null,
    readonly reservationId: string | null,
    status: OrderStatus,
    readonly total: Money,
    readonly items: readonly OrderItem[],
    holdExpiresAt: Date | null,
    cancellationReason: string | null,
    readonly createdAt: Date,
    updatedAt: Date,
  ) {
    if (items.length === 0) {
      throw new DomainError('EMPTY_ORDER', 'Un pedido requiere al menos un elemento.');
    }
    this._status = status;
    this._holdExpiresAt = holdExpiresAt;
    this._cancellationReason = cancellationReason;
    this._updatedAt = updatedAt;
  }

  get status(): OrderStatus {
    return this._status;
  }

  /** Fin de la retención temporal de cupos mientras el pedido espera el pago. */
  get holdExpiresAt(): Date | null {
    return this._holdExpiresAt;
  }

  get cancellationReason(): string | null {
    return this._cancellationReason;
  }

  get updatedAt(): Date {
    return this._updatedAt;
  }

  static placePending(args: {
    customerId: string;
    purchaseId: string | null;
    reservationId: string | null;
    items: OrderItem[];
    holdExpiresAt: Date;
    now: Date;
  }): Order {
    const currencies = [...new Set(args.items.map((i) => i.unitPrice.currency))];
    if (currencies.length !== 1) {
      throw new DomainError('MIXED_CURRENCY', 'Todos los elementos del pedido deben usar la misma moneda.');
    }
    const total = new Money(
      currencies[0],
      roundCents(args.items.reduce((sum, i) => sum + i.unitPrice.amount * i.quantity, 0)),
    );
    return new Order(
      newId(), args.customerId, args.purchaseId, args.reservationId, 'PENDING_PAYMENT', total, args.items,
      args.holdExpiresAt, null, args.now, args.now,
    );
  }

  static restore(args: {
    id: string;
    customerId: string;
    purchaseId: string | null;
    reservationId: string | null;
    status: OrderStatus;
    total: Money;
    items: OrderItem[];
    holdExpiresAt: Date | null;
    cancellationReason: string | null;
    createdAt: Date;
    updatedAt: Date;
  }): Order {
    return new Order(
      args.id, args.customerId, args.purchaseId, args.reservationId, args.status, args.total, args.items,
      args.holdExpiresAt, args.cancellationReason, args.createdAt, args.updatedAt,
    );
  }

  canTransitionTo(next: OrderStatus): boolean {
    return ORDER_TRANSITIONS[this._status]?.includes(next) ?? false;
  }

  isHoldExpired(now: Date): boolean {
    return this._status === 'PENDING_PAYMENT' && this._holdExpiresAt !== null && this._holdExpiresAt.getTime() <= now.getTime();
  }

  /** Aplica una transición permitida y devuelve el estado anterior. */
  transitionTo(next: OrderStatus, now: Date, reason?: string): OrderStatus {
    if (!this.canTransitionTo(next)) {
      throw new DomainError('INVALID_STATE_TRANSITION', `Transición de pedido no permitida: ${this._status} → ${next}.`);
    }
    const previous = this._status;
    this._status = next;
    this._updatedAt = now;
    if (next !== 'PENDING_PAYMENT') {
      this._holdExpiresAt = null;
    }
    if (next === 'CANCELLED') {
      this._cancellationReason = Guard.notBlank(reason, 'reason', 500);
    }
    return previous;
  }
}

/** Registro inmutable de un cambio de estado del pedido. */
export interface OrderEvent {
  id: string;
  orderId: string;
  eventType: string;
  previousStatus: OrderStatus | null;
  newStatus: OrderStatus;
  createdAt: Date;
}

const PAYMENT_TRANSITIONS: Partial<Record<PaymentStatus, PaymentStatus[]>> = {
  PENDING: ['AUTHORIZED', 'REJECTED', 'FAILED'],
  AUTHORIZED: ['SETTLED', 'CANCELLED', 'FAILED'],
  SETTLED: ['PARTIALLY_REFUNDED', 'REFUNDED'],
};

/**
 * Pago simulado. Transiciones: PENDING → AUTHORIZED | REJECTED | FAILED; AUTHORIZED → SETTLED | CANCELLED | FAILED;
 * SETTLED → PARTIALLY_REFUNDED | REFUNDED. No contiene datos de tarjeta ni credenciales.
 */
export class PaymentSimulation {
  private _status: PaymentStatus;
  private _processedAt: Date | null;
  private _failureReason: string | null;

  private constructor(
    readonly id: string,
    readonly orderId: string,
    readonly method: PaymentMethod,
    status: PaymentStatus,
    readonly amount: Money,
    readonly gatewayReference: string,
    readonly createdAt: Date,
    processedAt: Date | null,
    failureReason: string | null,
  ) {
    this._status = status;
    this._processedAt = processedAt;
    this._failureReason = failureReason;
  }

  get status(): PaymentStatus {
    return this._status;
  }

  get processedAt(): Date | null {
    return this._processedAt;
  }

  get failureReason(): string | null {
    return this._failureReason;
  }

  static start(orderId: string, method: PaymentMethod, amount: Money, now: Date): PaymentSimulation {
    const id = newId();
    const reference = `SIM-${id.replace(/-/g, '')}`.slice(0, 16).toUpperCase();
    return new PaymentSimulation(id, orderId, method, 'PENDING', amount, reference, now, null, null);
  }

  static restore(args: {
    id: string;
    orderId: string;
    method: PaymentMethod;
    status: PaymentStatus;
    amount: Money;
    gatewayReference: string;
    createdAt: Date;
    processedAt: Date | null;
    failureReason: string | null;
  }): PaymentSimulation {
    return new PaymentSimulation(
      args.id, args.orderId, args.method, args.status, args.amount, args.gatewayReference, args.createdAt,
      args.processedAt, args.failureReason,
    );
  }

  transitionTo(next: PaymentStatus, now: Date, failureReason?: string): PaymentStatus {
    if (!(PAYMENT_TRANSITIONS[this._status]?.includes(next) ?? false)) {
      throw new DomainError('INVALID_STATE_TRANSITION', `Transición de pago no permitida: ${this._status} → ${next}.`);
    }
    const previous = this._status;
    this._status = next;
    this._processedAt = now;
    this._failureReason = next === 'REJECTED' || next === 'FAILED' ? (failureReason ?? null) : null;
    return previous;
  }
}

/** Intento de pago simulado (interno, sin endpoint público). */
export interface PaymentAttempt {
  id: string;
  paymentSimulationId: string;
  attemptNumber: number;
  status: PaymentStatus;
  responseCode: string;
  responseMessage: string;
  createdAt: Date;
}

/** Evento inmutable de la pasarela simulada; el payload solo contiene datos de la simulación. */
export interface PaymentEvent {
  id: string;
  paymentSimulationId: string;
  eventType: string;
  payload: string;
  createdAt: Date;
}

/** Cambio de cupos de una franja. `quantity` es positivo al tomar cupos y negativo al liberarlos. */
export interface InventoryMovement {
  id: string;
  attractionId: string;
  availabilitySlotId: string;
  quantity: number;
  movementType: InventoryMovementType;
  purchaseId: string | null;
  reservationId: string | null;
  createdAt: Date;
}

/**
 * Registro de idempotencia; identidad única `issuer + subject + operation + key`.
 * Guarda el hash canónico del payload y la respuesta serializada para replay. Nunca almacena tokens ni secretos.
 */
export interface IdempotencyRecord {
  issuer: string;
  subject: string;
  operation: string;
  key: string;
  requestHash: string;
  status: IdempotencyStatus;
  resourceId: string | null;
  responseBody: string | null;
  createdAt: Date;
  expiresAt: Date;
}

/** Registro de auditoría inmutable de mutaciones administrativas (sin tokens ni datos financieros). */
export interface AuditEvent {
  id: string;
  actorUserId: string | null;
  actorSubject: string;
  action: string;
  resourceType: string;
  resourceId: string;
  reason: string | null;
  createdAt: Date;
}
