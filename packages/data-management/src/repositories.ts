import type {
  Attraction,
  AuditEvent,
  AvailabilitySlot,
  Customer,
  IdempotencyRecord,
  InventoryMovement,
  IsoDate,
  LocalTime,
  Order,
  OrderEvent,
  OrderStatus,
  PaymentAttempt,
  PaymentEvent,
  PaymentSimulation,
  Purchase,
  Reservation,
  ReservationStatus,
  User,
} from '@atracciones/domain';
import type { PagedResult, PaginationRequest } from './pagination';

export type AttractionSort = 'MOST_POPULAR' | 'PRICE_ASC' | 'PRICE_DESC' | 'RATING_DESC';

/**
 * Especificación de búsqueda del catálogo (la paginación llega en `PaginationRequest`).
 * Con `startDate`/`endDate` solo se devuelven atracciones con alguna franja con cupos en el rango.
 */
export interface AttractionSearchCriteria {
  currency: string | null;
  cities: string[];
  countries: string[];
  startDate: IsoDate | null;
  endDate: IsoDate | null;
  minimumReviewScore: number | null;
  minimumReviewCount: number | null;
  sort: AttractionSort;
}

export interface AttractionRepository {
  /** Agregado completo con ubicaciones, fotos, operador, categorías, insignias, incluidos e idiomas. */
  getById(id: string): Promise<Attraction | null>;
  getByIds(ids: string[]): Promise<Attraction[]>;
  list(page: PaginationRequest): Promise<PagedResult<Attraction>>;
  search(criteria: AttractionSearchCriteria, page: PaginationRequest): Promise<PagedResult<Attraction>>;
  exists(id: string): Promise<boolean>;
  add(attraction: Attraction): Promise<void>;
  /** Actualiza datos y reemplaza las colecciones asociadas. */
  update(attraction: Attraction): Promise<void>;
  delete(id: string): Promise<void>;
}

export interface AvailabilityRepository {
  getSlots(attractionId: string, date: IsoDate): Promise<AvailabilitySlot[]>;
  getSlot(attractionId: string, date: IsoDate, time: LocalTime): Promise<AvailabilitySlot | null>;
  add(slot: AvailabilitySlot): Promise<void>;
  /**
   * Incrementa atómicamente los cupos reservados si quedan suficientes (actualización condicional, sin bloqueo
   * explícito). Devuelve `false` sin cambios cuando no alcanzan.
   */
  tryReserveQuantity(slotId: string, quantity: number): Promise<boolean>;
  /** Libera cupos reservados previamente. */
  releaseQuantity(slotId: string, quantity: number): Promise<void>;
}

/** Filtros y orden de reservas. La regla de acceso (cliente propietario) la fija el método del repositorio. */
export interface ReservationFilter {
  status: ReservationStatus | null;
  fromDate: IsoDate | null;
  toDate: IsoDate | null;
  sortDescending: boolean;
}

export interface ReservationRepository {
  getById(reservationId: string): Promise<Reservation | null>;
  getByIdForCustomer(reservationId: string, customerId: string): Promise<Reservation | null>;
  getByCustomer(customerId: string, filter: ReservationFilter, page: PaginationRequest): Promise<PagedResult<Reservation>>;
  /** Indica si la atracción tiene reservas no canceladas con fecha igual o posterior a `fromDate`. */
  hasActiveByAttraction(attractionId: string, fromDate: IsoDate): Promise<boolean>;
  add(reservation: Reservation): Promise<void>;
  update(reservation: Reservation): Promise<void>;
}

export interface UserRepository {
  /** Busca por la identidad única `issuer + subject`. */
  getByIdentity(issuer: string, subject: string): Promise<User | null>;
  add(user: User): Promise<void>;
}

/** Cliente de facturación (1:1 con el usuario). No gestiona credenciales. */
export interface CustomerRepository {
  getByUserId(userId: string): Promise<Customer | null>;
  add(customer: Customer): Promise<void>;
  update(customer: Customer): Promise<void>;
}

/** Asignaciones de roles locales. Las revocaciones conservan el registro. */
export interface UserRoleRepository {
  /** Permisos efectivos: unión de los permisos de los roles asignados y no revocados. */
  getEffectivePermissions(userId: string): Promise<Set<string>>;
}

export interface PurchaseRepository {
  add(purchase: Purchase): Promise<void>;
}

export interface OrderRepository {
  getByIdForCustomer(orderId: string, customerId: string): Promise<Order | null>;
  getByReservationId(reservationId: string): Promise<Order | null>;
  add(order: Order): Promise<void>;
  update(order: Order): Promise<void>;
}

export interface PaymentSimulationRepository {
  getByOrder(orderId: string): Promise<PaymentSimulation[]>;
  add(simulation: PaymentSimulation): Promise<void>;
  update(simulation: PaymentSimulation): Promise<void>;
  /** Registra un intento; los intentos son inmutables y únicos por simulación y número. */
  addAttempt(attempt: PaymentAttempt): Promise<void>;
}

/** Idempotencia con identidad única `issuer + subject + operation + key`. */
export interface IdempotencyRepository {
  getByIdentity(issuer: string, subject: string, operation: string, key: string): Promise<IdempotencyRecord | null>;
  /** Inserta el registro `IN_PROGRESS` respetando la unicidad. Devuelve `false` si otra solicitud ya lo creó. */
  tryCreateInProgress(record: IdempotencyRecord): Promise<boolean>;
  /** Marca el registro como `COMPLETED` con la respuesta reproducible, dentro de la unidad de trabajo. */
  complete(record: IdempotencyRecord): Promise<void>;
  removeExpired(now: Date): Promise<number>;
}

export interface InventoryMovementRepository {
  add(movement: InventoryMovement): Promise<void>;
}

// Repositorios de eventos de solo inserción: se persisten en la misma unidad de trabajo que el cambio que registran.
export interface OrderEventRepository {
  add(event: OrderEvent): Promise<void>;
  getByOrder(orderId: string): Promise<OrderEvent[]>;
}

export interface PaymentEventRepository {
  add(event: PaymentEvent): Promise<void>;
}

export interface AuditEventRepository {
  add(event: AuditEvent): Promise<void>;
}

export type { OrderStatus };
