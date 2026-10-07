import {
  Attraction,
  AvailabilitySlot,
  Customer,
  Money,
  Order,
  PaymentSimulation,
  Reservation,
  User,
  type OrderStatus,
  type PaymentMethod,
  type PaymentStatus,
  type ProductType,
  type ReservationStatus,
  type UserStatus,
} from '@atracciones/domain';
import type { AttractionEntity, AvailabilityEntity, ReservationEntity } from './entities/catalog.entities';
import type { OrderEntity, PaymentSimulationEntity } from './entities/ecommerce.entities';
import type { CustomerEntity, UserEntity } from './entities/identity.entities';

const byName = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);

export function toAttraction(e: AttractionEntity): Attraction {
  return Attraction.restore(
    e.id,
    {
      name: e.name,
      longDescription: e.longDescription,
      duration: e.duration,
      price: new Money(e.priceCurrency, e.priceAmount),
      categories: (e.categories ?? []).map((c) => c.name).sort(byName),
      badges: (e.badges ?? []).map((b) => b.name).sort(byName),
      locations: [...(e.locations ?? [])]
        .sort((a, b) => a.position - b.position)
        .map((l) => ({ address: l.address, city: l.city, country: l.country.trim(), latitude: l.latitude, longitude: l.longitude, type: l.type })),
      photoUrls: [...(e.photos ?? [])].sort((a, b) => a.position - b.position).map((p) => p.url),
      operator: e.operator ? { id: e.operator.id, name: e.operator.name } : null,
      productType: e.productType as ProductType,
      includes: (e.inclusions ?? []).map((i) => i.name).sort(byName),
      supportedLanguages: (e.languages ?? []).map((l) => l.name).sort(byName),
      freeCancellation: e.freeCancellation,
    },
    e.ratingReviewCount !== null && e.ratingScore !== null ? { numberOfReviews: e.ratingReviewCount, score: e.ratingScore } : null,
    e.urlWeb === null && e.urlApp === null ? null : { web: e.urlWeb, app: e.urlApp },
  );
}

export const toSlot = (e: AvailabilityEntity): AvailabilitySlot =>
  new AvailabilitySlot(e.id, e.attractionId, e.date, e.time, e.capacity, e.reservedQuantity, e.version);

export const toReservation = (e: ReservationEntity): Reservation =>
  Reservation.restore({
    id: e.id,
    attractionId: e.attractionId,
    customerId: e.customerId,
    date: e.date,
    time: e.time,
    ticketCount: e.ticketCount,
    totalPrice: new Money(e.totalCurrency, e.totalAmount),
    customerName: e.customerName,
    customerEmail: e.customerEmail,
    status: e.status as ReservationStatus,
    cancellationReason: e.cancellationReason,
    createdAt: e.createdAt,
  });

export const fromReservation = (r: Reservation): Partial<ReservationEntity> => ({
  id: r.id,
  attractionId: r.attractionId,
  customerId: r.customerId,
  date: r.date,
  time: r.time,
  ticketCount: r.ticketCount,
  totalCurrency: r.totalPrice.currency,
  totalAmount: r.totalPrice.amount,
  customerName: r.customerName,
  customerEmail: r.customerEmail,
  status: r.status,
  cancellationReason: r.cancellationReason,
  createdAt: r.createdAt,
});

export const toUser = (e: UserEntity): User =>
  User.restore(e.id, e.oauthIssuer, e.oauthSubject, e.email, e.status as UserStatus, e.createdAt, e.updatedAt);

export const toCustomer = (e: CustomerEntity): Customer =>
  Customer.restore(
    e.id,
    e.userId,
    { billingName: e.billingName, billingEmail: e.billingEmail, billingAddress: e.billingAddress, taxId: e.taxId, paymentMethodReference: e.paymentMethodReference },
    e.createdAt,
    e.updatedAt,
  );

export const fromCustomer = (c: Customer): Partial<CustomerEntity> => ({
  id: c.id,
  userId: c.userId,
  billingName: c.billingName,
  billingEmail: c.billingEmail,
  billingAddress: c.billingAddress,
  taxId: c.taxId,
  paymentMethodReference: c.paymentMethodReference,
  createdAt: c.createdAt,
  updatedAt: c.updatedAt,
});

export const toOrder = (e: OrderEntity): Order =>
  Order.restore({
    id: e.id,
    customerId: e.customerId,
    purchaseId: e.purchaseId,
    reservationId: e.reservationId,
    status: e.status as OrderStatus,
    total: new Money(e.currency, e.totalAmount),
    items: (e.items ?? []).map((i) => ({
      id: i.id,
      attractionId: i.attractionId,
      serviceDate: i.serviceDate,
      serviceTime: i.serviceTime,
      quantity: i.quantity,
      unitPrice: new Money(i.unitPriceCurrency, i.unitPriceAmount),
      availabilitySlotId: i.availabilityId,
    })),
    holdExpiresAt: e.holdExpiresAt,
    cancellationReason: e.cancellationReason,
    createdAt: e.createdAt,
    updatedAt: e.updatedAt,
  });

export const toPayment = (e: PaymentSimulationEntity): PaymentSimulation =>
  PaymentSimulation.restore({
    id: e.id,
    orderId: e.orderId,
    method: e.paymentMethod as PaymentMethod,
    status: e.status as PaymentStatus,
    amount: new Money(e.currency, e.amount),
    gatewayReference: e.gatewayReference,
    createdAt: e.createdAt,
    processedAt: e.processedAt,
    failureReason: e.failureReason,
  });

export const fromPayment = (p: PaymentSimulation): Partial<PaymentSimulationEntity> => ({
  id: p.id,
  orderId: p.orderId,
  paymentMethod: p.method,
  status: p.status,
  amount: p.amount.amount,
  currency: p.amount.currency,
  gatewayReference: p.gatewayReference,
  createdAt: p.createdAt,
  processedAt: p.processedAt,
  failureReason: p.failureReason,
});
