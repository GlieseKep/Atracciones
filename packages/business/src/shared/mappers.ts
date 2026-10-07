import {
  Money,
  type Attraction,
  type AttractionDetails,
  type Order,
  type OrderEvent,
  type PaymentSimulation,
  type ProductType,
  type Purchase,
  type Reservation,
} from '@atracciones/domain';
import type {
  AttractionData,
  AttractionPatch,
  AttractionResult,
  MoneyData,
  OrderResult,
  PaymentSimulationResult,
  PaymentSummaryResult,
  PurchaseResult,
  ReservationResult,
} from '../models';

const unique = (values: string[]): string[] => [...new Set(values)];

export const toMoneyData = (money: Money): MoneyData => ({ currency: money.currency, total: money.amount });

export function toDomainDetails(data: AttractionData): AttractionDetails {
  return {
    name: data.name.trim(),
    longDescription: data.longDescription.trim(),
    duration: data.duration,
    price: new Money(data.price.currency, data.price.total),
    categories: unique(data.categories.map((c) => c.trim())),
    badges: unique(data.badges),
    locations: data.locations.map((l) => ({
      address: l.address.trim(),
      city: l.city.trim(),
      country: l.country,
      latitude: l.latitude,
      longitude: l.longitude,
      type: l.type,
    })),
    photoUrls: [...data.photoUrls],
    operator: data.operator ? { id: data.operator.id, name: data.operator.name.trim() } : null,
    productType: data.productType as ProductType,
    includes: [...data.includes],
    supportedLanguages: unique(data.supportedLanguages),
    freeCancellation: data.freeCancellation,
  };
}

export function toAttractionData(details: AttractionDetails): AttractionData {
  return {
    name: details.name,
    longDescription: details.longDescription,
    duration: details.duration,
    price: toMoneyData(details.price),
    categories: details.categories,
    badges: details.badges,
    locations: details.locations.map((l) => ({ ...l })),
    photoUrls: details.photoUrls,
    operator: details.operator ? { ...details.operator } : null,
    productType: details.productType,
    includes: details.includes,
    supportedLanguages: details.supportedLanguages,
    freeCancellation: details.freeCancellation,
  };
}

/** Aplica un cambio parcial: las propiedades ausentes o `null` conservan el valor actual. */
export function mergePatch(current: AttractionData, patch: AttractionPatch): AttractionData {
  const merged = { ...current } as Record<string, unknown>;
  for (const [key, value] of Object.entries(patch)) {
    if (value !== undefined && value !== null) merged[key] = value;
  }
  return merged as unknown as AttractionData;
}

export function toAttractionResult(attraction: Attraction): AttractionResult {
  return {
    id: attraction.id,
    data: toAttractionData(attraction.details),
    ratings: attraction.rating ? { ...attraction.rating } : null,
    url: attraction.urls ? { ...attraction.urls } : null,
  };
}

export function toReservationResult(reservation: Reservation): ReservationResult {
  return {
    reservationId: reservation.id,
    attractionId: reservation.attractionId,
    status: reservation.status,
    date: reservation.date,
    time: reservation.time,
    ticketCount: reservation.ticketCount,
    totalPrice: toMoneyData(reservation.totalPrice),
  };
}

export function toPaymentSummary(payment: PaymentSimulation): PaymentSummaryResult {
  return { id: payment.id, paymentMethod: payment.method, status: payment.status, gatewayReference: payment.gatewayReference };
}

export function toPaymentResult(payment: PaymentSimulation): PaymentSimulationResult {
  return {
    id: payment.id,
    orderId: payment.orderId,
    paymentMethod: payment.method,
    status: payment.status,
    amount: payment.amount.amount,
    currency: payment.amount.currency,
    gatewayReference: payment.gatewayReference,
    createdAt: payment.createdAt,
  };
}

export function toOrderResult(order: Order, latestPayment: PaymentSimulation | null): OrderResult {
  return {
    id: order.id,
    customerId: order.customerId,
    purchaseId: order.purchaseId,
    reservationId: order.reservationId,
    status: order.status,
    currency: order.total.currency,
    totalAmount: order.total.amount,
    createdAt: order.createdAt,
    updatedAt: order.updatedAt,
    items: order.items.map((i) => ({
      id: i.id,
      attractionId: i.attractionId,
      date: i.serviceDate,
      time: i.serviceTime,
      quantity: i.quantity,
      unitPrice: toMoneyData(i.unitPrice),
      status: order.status,
    })),
    paymentSimulation: latestPayment ? toPaymentSummary(latestPayment) : null,
  };
}

export function toOrderEventResult(event: OrderEvent) {
  return { eventType: event.eventType, previousStatus: event.previousStatus, newStatus: event.newStatus, createdAt: event.createdAt };
}

export function toPurchaseResult(order: Order, purchase: Purchase, payment: PaymentSimulation | null): PurchaseResult {
  return {
    purchaseId: purchase.id,
    orderId: order.id,
    attractionId: purchase.attractionId,
    date: purchase.serviceDate,
    time: purchase.serviceTime,
    quantity: purchase.quantity,
    unitPrice: toMoneyData(purchase.unitPrice),
    totalAmount: order.total.amount,
    currency: order.total.currency,
    status: order.status,
    reservationId: order.reservationId,
    payment: payment ? toPaymentSummary(payment) : null,
    holdExpiresAt: order.holdExpiresAt,
    createdAt: purchase.createdAt,
  };
}

/** Último pago por fecha de creación. */
export const latestPayment = (payments: PaymentSimulation[]): PaymentSimulation | null =>
  [...payments].sort((a, b) => a.createdAt.getTime() - b.createdAt.getTime()).at(-1) ?? null;
