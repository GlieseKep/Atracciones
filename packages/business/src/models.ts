import type { IsoDate, LocalTime } from '@atracciones/domain';
import type { AuthenticatedUser } from './shared/auth';

// ---------------------------------------------------------------- Comunes

/** Importe con moneda ISO 4217. */
export interface MoneyData {
  currency: string;
  total: number;
}

/** Página de resultados independiente de HTTP. */
export class PaginationResult<T> {
  constructor(
    readonly items: T[],
    readonly totalItems: number,
    readonly limit: number,
    readonly offset: number,
  ) {}

  get totalPages(): number {
    return this.limit <= 0 ? 0 : Math.ceil(this.totalItems / this.limit);
  }

  get currentPage(): number {
    return this.limit <= 0 ? 1 : Math.floor(this.offset / this.limit) + 1;
  }
}

// ---------------------------------------------------------------- Catálogo

export interface LocationData {
  address: string;
  city: string;
  country: string;
  latitude: number | null;
  longitude: number | null;
  type: string | null;
}

export interface OperatorData {
  id: number;
  name: string;
}

/** Datos completos y editables de una atracción. */
export interface AttractionData {
  name: string;
  longDescription: string;
  duration: string;
  price: MoneyData;
  categories: string[];
  badges: string[];
  locations: LocationData[];
  photoUrls: string[];
  operator: OperatorData | null;
  productType: string;
  includes: string[];
  supportedLanguages: string[];
  freeCancellation: boolean;
}

/** Cambios parciales: `undefined`/`null` significa "sin cambios". */
export type AttractionPatch = { [K in keyof AttractionData]?: AttractionData[K] | null };

export interface AttractionResult {
  id: string;
  data: AttractionData;
  ratings: { numberOfReviews: number; score: number } | null;
  url: { web: string | null; app: string | null } | null;
}

export interface SearchAttractionsQuery {
  currency: string | null;
  cities: string[];
  countries: string[];
  startDate: IsoDate | null;
  endDate: IsoDate | null;
  minimumReviewScore: number | null;
  minimumReviewCount: number | null;
  nextPage: string | null;
  rows: number;
  sortBy: string;
}

export interface SearchAttractionsResult {
  items: AttractionResult[];
  totalResults: number;
  nextPage: string | null;
}

/** Disponibilidad de una fecha local de la atracción (zona horaria IANA). */
export interface AvailabilityResult {
  date: IsoDate;
  timeZone: string;
  availableSpots: number;
  slots: { time: LocalTime; availableSpots: number }[];
}

export interface CreateAttractionCommand {
  data: AttractionData;
  idempotencyKey: string;
  actor: AuthenticatedUser;
}

export interface ReplaceAttractionCommand extends CreateAttractionCommand {
  attractionId: string;
}

export interface PatchAttractionCommand {
  attractionId: string;
  patch: AttractionPatch;
  idempotencyKey: string;
  actor: AuthenticatedUser;
}

export interface DeleteAttractionCommand {
  attractionId: string;
  idempotencyKey: string;
  actor: AuthenticatedUser;
}

// ---------------------------------------------------------------- Reservas

export interface ReservationResult {
  reservationId: string;
  attractionId: string;
  status: string;
  date: IsoDate;
  time: LocalTime;
  ticketCount: number;
  totalPrice: MoneyData;
}

export interface CreateReservationCommand {
  attractionId: string;
  date: IsoDate;
  time: LocalTime;
  ticketCount: number;
  customerName: string;
  customerEmail: string;
  idempotencyKey: string;
  user: AuthenticatedUser;
}

export interface CancelReservationCommand {
  reservationId: string;
  reason: string;
  idempotencyKey: string;
  user: AuthenticatedUser;
}

/** Historial de reservas del usuario autenticado; el cliente se resuelve a partir de `user`. */
export interface GetReservationsQuery {
  user: AuthenticatedUser;
  limit: number;
  offset: number;
  status: string | null;
  fromDate: IsoDate | null;
  toDate: IsoDate | null;
  sortDescending: boolean;
}

// ---------------------------------------------------------------- Identidad y clientes

export interface BillingData {
  billingName?: string | null;
  billingEmail?: string | null;
  billingAddress?: string | null;
  taxId?: string | null;
  paymentMethodReference?: string | null;
}

/** Aprovisiona el usuario local y su cliente a partir de claims verificados; repetirlo no duplica el perfil. */
export interface RegisterUserCommand {
  user: AuthenticatedUser;
  email: string;
  billing: BillingData;
}

export interface RegisteredUserResult {
  id: string;
  email: string;
  status: string;
  customerId: string;
  createdAt: Date;
  /** `true` si la operación creó el perfil; `false` si ya existía. */
  created: boolean;
}

export interface UserResult {
  id: string;
  email: string;
  status: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface CustomerResult {
  id: string;
  userId: string;
  billingName: string | null;
  billingEmail: string | null;
  billingAddress: string | null;
  taxId: string | null;
  paymentMethodReference: string | null;
  createdAt: Date;
  updatedAt: Date;
}

// ---------------------------------------------------------------- Ecommerce

/** Estado y referencia segura de la simulación de pago; nunca incluye datos financieros. */
export interface PaymentSummaryResult {
  id: string;
  paymentMethod: string;
  status: string;
  gatewayReference: string | null;
}

export interface PurchaseResult {
  purchaseId: string;
  orderId: string;
  attractionId: string;
  date: IsoDate;
  time: LocalTime;
  quantity: number;
  unitPrice: MoneyData;
  totalAmount: number;
  currency: string;
  status: string;
  reservationId: string | null;
  payment: PaymentSummaryResult | null;
  holdExpiresAt: Date | null;
  createdAt: Date;
}

export interface OrderItemResult {
  id: string;
  attractionId: string;
  date: IsoDate;
  time: LocalTime;
  quantity: number;
  unitPrice: MoneyData;
  status: string;
}

export interface OrderResult {
  id: string;
  customerId: string;
  purchaseId: string | null;
  reservationId: string | null;
  status: string;
  currency: string;
  totalAmount: number;
  createdAt: Date;
  updatedAt: Date;
  items: OrderItemResult[];
  paymentSimulation: PaymentSummaryResult | null;
}

export interface OrderEventsResult {
  orderId: string;
  events: { eventType: string; previousStatus: string | null; newStatus: string; createdAt: Date }[];
}

export interface PaymentSimulationResult {
  id: string;
  orderId: string;
  paymentMethod: string;
  status: string;
  amount: number;
  currency: string;
  gatewayReference: string | null;
  createdAt: Date;
}

/** Compra directa de una franja. Si `paymentMethod` tiene valor, el pago simulado se procesa en la misma operación. */
export interface CreatePurchaseCommand {
  attractionId: string;
  date: IsoDate;
  time: LocalTime;
  quantity: number;
  paymentMethod: string | null;
  idempotencyKey: string;
  user: AuthenticatedUser;
}

export interface CreateOrderCommand {
  attractionId: string;
  date: IsoDate;
  time: LocalTime;
  quantity: number;
  idempotencyKey: string;
  user: AuthenticatedUser;
}

export interface CancelOrderCommand {
  orderId: string;
  reason: string;
  idempotencyKey: string;
  user: AuthenticatedUser;
}

/** Solicita una simulación de pago. Business decide el resultado y registra intentos y eventos internamente. */
export interface SimulatePaymentCommand {
  orderId: string;
  paymentMethod: string;
  amount: number;
  currency: string;
  idempotencyKey: string;
  user: AuthenticatedUser;
}
