import { ConcurrencyError, DataManagementError, type UnitOfWork, type UnitOfWorkFactory } from '@atracciones/data-management';
import { QueryFailedError, type DataSource, type EntityManager } from 'typeorm';
import { TypeOrmAdminRepository } from './repositories/admin.repository';
import { TypeOrmAttractionRepository, TypeOrmAvailabilityRepository, TypeOrmReservationRepository } from './repositories/catalog.repositories';
import {
  TypeOrmAuditEventRepository,
  TypeOrmCustomerRepository,
  TypeOrmIdempotencyRepository,
  TypeOrmInventoryRepository,
  TypeOrmOrderEventRepository,
  TypeOrmOrderRepository,
  TypeOrmPaymentEventRepository,
  TypeOrmPaymentRepository,
  TypeOrmPurchaseRepository,
  TypeOrmUserRepository,
  TypeOrmUserRoleRepository,
} from './repositories/other.repositories';

/** Repositorios ligados a un `EntityManager` (el de la transacción en curso o el global de lectura). */
export class TypeOrmUnitOfWork implements UnitOfWork {
  readonly attractions;
  readonly availability;
  readonly reservations;
  readonly users;
  readonly customers;
  readonly userRoles;
  readonly purchases;
  readonly orders;
  readonly payments;
  readonly idempotency;
  readonly inventory;
  readonly orderEvents;
  readonly paymentEvents;
  readonly auditEvents;
  readonly admin;

  constructor(manager: EntityManager) {
    this.attractions = new TypeOrmAttractionRepository(manager);
    this.availability = new TypeOrmAvailabilityRepository(manager);
    this.reservations = new TypeOrmReservationRepository(manager);
    this.users = new TypeOrmUserRepository(manager);
    this.customers = new TypeOrmCustomerRepository(manager);
    this.userRoles = new TypeOrmUserRoleRepository(manager);
    this.purchases = new TypeOrmPurchaseRepository(manager);
    this.orders = new TypeOrmOrderRepository(manager);
    this.payments = new TypeOrmPaymentRepository(manager);
    this.idempotency = new TypeOrmIdempotencyRepository(manager);
    this.inventory = new TypeOrmInventoryRepository(manager);
    this.orderEvents = new TypeOrmOrderEventRepository(manager);
    this.paymentEvents = new TypeOrmPaymentEventRepository(manager);
    this.auditEvents = new TypeOrmAuditEventRepository(manager);
    this.admin = new TypeOrmAdminRepository(manager);
  }
}

const UNIQUE_VIOLATION = '23505';
const SERIALIZATION_FAILURE = '40001';

/** Traduce errores del proveedor a errores de persistencia sin detalles de PostgreSQL. */
export function translatePersistenceError(error: unknown): unknown {
  if (error instanceof QueryFailedError) {
    const code = (error.driverError as { code?: string } | undefined)?.code;
    if (code === UNIQUE_VIOLATION) return new ConcurrencyError('Se violó una restricción de unicidad.', { cause: error });
    if (code === SERIALIZATION_FAILURE) return new ConcurrencyError('El registro fue modificado por otra operación.', { cause: error });
    return new DataManagementError('No se pudieron guardar los cambios.', { cause: error });
  }
  return error;
}

export class TypeOrmUnitOfWorkFactory implements UnitOfWorkFactory {
  constructor(private readonly dataSource: DataSource) {}

  create(): UnitOfWork {
    return new TypeOrmUnitOfWork(this.dataSource.manager);
  }

  async transaction<T>(work: (uow: UnitOfWork) => Promise<T>): Promise<T> {
    try {
      return await this.dataSource.transaction((manager) => work(new TypeOrmUnitOfWork(manager)));
    } catch (error) {
      throw translatePersistenceError(error);
    }
  }
}
