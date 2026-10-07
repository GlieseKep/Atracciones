import type { AdminRepository } from './admin';
import type {
  AttractionRepository,
  AuditEventRepository,
  AvailabilityRepository,
  CustomerRepository,
  IdempotencyRepository,
  InventoryMovementRepository,
  OrderEventRepository,
  OrderRepository,
  PaymentEventRepository,
  PaymentSimulationRepository,
  PurchaseRepository,
  ReservationRepository,
  UserRepository,
  UserRoleRepository,
} from './repositories';

/** Repositorios que comparten una misma conexión (y, si existe, una misma transacción). */
export interface UnitOfWork {
  readonly attractions: AttractionRepository;
  readonly availability: AvailabilityRepository;
  readonly reservations: ReservationRepository;
  readonly users: UserRepository;
  readonly customers: CustomerRepository;
  readonly userRoles: UserRoleRepository;
  readonly purchases: PurchaseRepository;
  readonly orders: OrderRepository;
  readonly payments: PaymentSimulationRepository;
  readonly idempotency: IdempotencyRepository;
  readonly inventory: InventoryMovementRepository;
  readonly orderEvents: OrderEventRepository;
  readonly paymentEvents: PaymentEventRepository;
  readonly auditEvents: AuditEventRepository;
  readonly admin: AdminRepository;
}

/**
 * Crea unidades de trabajo. Es el único mecanismo transaccional: `transaction` confirma al terminar
 * y revierte ante cualquier error; las lecturas usan `create()` sin transacción.
 */
export interface UnitOfWorkFactory {
  create(): UnitOfWork;
  transaction<T>(work: (uow: UnitOfWork) => Promise<T>): Promise<T>;
}
