import { randomUUID } from 'node:crypto';
import type {
  AuditEvent,
  Customer,
  IdempotencyRecord,
  InventoryMovement,
  Order,
  OrderEvent,
  OrderStatus,
  PaymentAttempt,
  PaymentEvent,
  PaymentSimulation,
  Purchase,
  User,
} from '@atracciones/domain';
import type {
  AuditEventRepository,
  CustomerRepository,
  IdempotencyRepository,
  InventoryMovementRepository,
  OrderEventRepository,
  OrderRepository,
  PaymentEventRepository,
  PaymentSimulationRepository,
  PurchaseRepository,
  UserRepository,
  UserRoleRepository,
} from '@atracciones/data-management';
import { LessThanOrEqual, type EntityManager } from 'typeorm';
import {
  AuditEventEntity,
  IdempotencyKeyEntity,
  InventoryMovementEntity,
  OrderEntity,
  OrderEventEntity,
  OrderItemEntity,
  PaymentAttemptEntity,
  PaymentEventEntity,
  PaymentSimulationEntity,
  PurchaseEntity,
} from '../entities/ecommerce.entities';
import { CustomerEntity, RolePermissionEntity, UserEntity, UserRoleEntity } from '../entities/identity.entities';
import { fromCustomer, fromPayment, toCustomer, toOrder, toPayment, toUser } from '../mapping';

export class TypeOrmUserRepository implements UserRepository {
  constructor(private readonly manager: EntityManager) {}

  async getByIdentity(issuer: string, subject: string): Promise<User | null> {
    const row = await this.manager.findOneBy(UserEntity, { oauthIssuer: issuer, oauthSubject: subject });
    return row ? toUser(row) : null;
  }

  async add(user: User): Promise<void> {
    await this.manager.insert(UserEntity, {
      id: user.id, oauthIssuer: user.oauthIssuer, oauthSubject: user.oauthSubject, email: user.email, status: user.status,
      createdAt: user.createdAt, updatedAt: user.updatedAt,
    });
  }
}

export class TypeOrmCustomerRepository implements CustomerRepository {
  constructor(private readonly manager: EntityManager) {}

  async getByUserId(userId: string): Promise<Customer | null> {
    const row = await this.manager.findOneBy(CustomerEntity, { userId });
    return row ? toCustomer(row) : null;
  }

  async add(customer: Customer): Promise<void> {
    await this.manager.insert(CustomerEntity, fromCustomer(customer));
  }

  async update(customer: Customer): Promise<void> {
    const { id, ...values } = fromCustomer(customer);
    await this.manager.update(CustomerEntity, { id }, values);
  }
}

export class TypeOrmUserRoleRepository implements UserRoleRepository {
  constructor(private readonly manager: EntityManager) {}

  async getEffectivePermissions(userId: string): Promise<Set<string>> {
    const rows = await this.manager
      .createQueryBuilder(RolePermissionEntity, 'p')
      .innerJoin(UserRoleEntity, 'ur', 'ur.role_id = p.role_id')
      .where('ur.user_id = :userId AND ur.revoked_at IS NULL', { userId })
      .select('DISTINCT p.permission', 'permission')
      .getRawMany<{ permission: string }>();
    return new Set(rows.map((r) => r.permission));
  }
}

export class TypeOrmPurchaseRepository implements PurchaseRepository {
  constructor(private readonly manager: EntityManager) {}

  async add(p: Purchase): Promise<void> {
    await this.manager.insert(PurchaseEntity, {
      id: p.id, customerId: p.customerId, attractionId: p.attractionId, serviceDate: p.serviceDate, serviceTime: p.serviceTime,
      quantity: p.quantity, unitPriceCurrency: p.unitPrice.currency, unitPriceAmount: p.unitPrice.amount, totalAmount: p.total.amount,
      requestIdempotencyKey: p.requestIdempotencyKey, createdAt: p.createdAt,
    });
  }
}

export class TypeOrmOrderRepository implements OrderRepository {
  constructor(private readonly manager: EntityManager) {}

  async getById(orderId: string): Promise<Order | null> {
    const row = await this.manager.findOne(OrderEntity, { where: { id: orderId }, relations: ['items'] });
    return row ? toOrder(row) : null;
  }

  async getByIdForCustomer(orderId: string, customerId: string): Promise<Order | null> {
    const row = await this.manager.findOne(OrderEntity, { where: { id: orderId, customerId }, relations: ['items'] });
    return row ? toOrder(row) : null;
  }

  async getByReservationId(reservationId: string): Promise<Order | null> {
    const row = await this.manager.findOne(OrderEntity, { where: { reservationId }, relations: ['items'] });
    return row ? toOrder(row) : null;
  }

  async add(o: Order): Promise<void> {
    await this.manager.insert(OrderEntity, {
      id: o.id, customerId: o.customerId, purchaseId: o.purchaseId, reservationId: o.reservationId, status: o.status,
      currency: o.total.currency, totalAmount: o.total.amount, holdExpiresAt: o.holdExpiresAt, cancellationReason: o.cancellationReason,
      createdAt: o.createdAt, updatedAt: o.updatedAt,
    });
    await this.manager.insert(
      OrderItemEntity,
      o.items.map((i) => ({
        id: i.id, orderId: o.id, attractionId: i.attractionId, availabilityId: i.availabilitySlotId, serviceDate: i.serviceDate,
        serviceTime: i.serviceTime, quantity: i.quantity, unitPriceCurrency: i.unitPrice.currency, unitPriceAmount: i.unitPrice.amount,
      })),
    );
  }

  async update(o: Order): Promise<void> {
    await this.manager.update(
      OrderEntity,
      { id: o.id },
      { status: o.status, holdExpiresAt: o.holdExpiresAt, cancellationReason: o.cancellationReason, updatedAt: o.updatedAt },
    );
  }
}

export class TypeOrmPaymentRepository implements PaymentSimulationRepository {
  constructor(private readonly manager: EntityManager) {}

  async getByOrder(orderId: string): Promise<PaymentSimulation[]> {
    return (await this.manager.find(PaymentSimulationEntity, { where: { orderId }, order: { createdAt: 'ASC' } })).map(toPayment);
  }

  async add(payment: PaymentSimulation): Promise<void> {
    await this.manager.insert(PaymentSimulationEntity, fromPayment(payment));
  }

  async update(payment: PaymentSimulation): Promise<void> {
    await this.manager.update(
      PaymentSimulationEntity,
      { id: payment.id },
      { status: payment.status, processedAt: payment.processedAt, failureReason: payment.failureReason },
    );
  }

  async addAttempt(a: PaymentAttempt): Promise<void> {
    await this.manager.insert(PaymentAttemptEntity, { ...a });
  }
}

export class TypeOrmIdempotencyRepository implements IdempotencyRepository {
  constructor(private readonly manager: EntityManager) {}

  async getByIdentity(issuer: string, subject: string, operation: string, key: string): Promise<IdempotencyRecord | null> {
    const row = await this.manager.findOneBy(IdempotencyKeyEntity, { issuer, subject, operation, key });
    return row
      ? {
          issuer: row.issuer, subject: row.subject, operation: row.operation, key: row.key, requestHash: row.requestHash,
          status: row.status as IdempotencyRecord['status'], resourceId: row.resourceId, responseBody: row.responseBody,
          createdAt: row.createdAt, expiresAt: row.expiresAt,
        }
      : null;
  }

  /** `ON CONFLICT DO NOTHING` no aborta la transacción: si otra solicitud ya creó la clave, no se inserta fila. */
  async tryCreateInProgress(record: IdempotencyRecord): Promise<boolean> {
    const rows: unknown[] = await this.manager.query(
      `INSERT INTO idempotency_keys
         (id, issuer, subject, operation, key, request_hash, status, resource_id, response_body, created_at, expires_at)
       VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
       ON CONFLICT (issuer, subject, operation, key) DO NOTHING
       RETURNING id`,
      [
        randomUUID(), record.issuer, record.subject, record.operation, record.key, record.requestHash, record.status,
        record.resourceId, record.responseBody, record.createdAt, record.expiresAt,
      ],
    );
    return rows.length === 1;
  }

  async complete(record: IdempotencyRecord): Promise<void> {
    await this.manager.update(
      IdempotencyKeyEntity,
      { issuer: record.issuer, subject: record.subject, operation: record.operation, key: record.key },
      { status: record.status, responseBody: record.responseBody, resourceId: record.resourceId },
    );
  }

  async removeExpired(now: Date): Promise<number> {
    return (await this.manager.delete(IdempotencyKeyEntity, { expiresAt: LessThanOrEqual(now) })).affected ?? 0;
  }
}

export class TypeOrmInventoryRepository implements InventoryMovementRepository {
  constructor(private readonly manager: EntityManager) {}

  async add(m: InventoryMovement): Promise<void> {
    await this.manager.insert(InventoryMovementEntity, {
      id: m.id, attractionId: m.attractionId, availabilityId: m.availabilitySlotId, quantity: m.quantity, movementType: m.movementType,
      purchaseId: m.purchaseId, reservationId: m.reservationId, createdAt: m.createdAt,
    });
  }
}

export class TypeOrmOrderEventRepository implements OrderEventRepository {
  constructor(private readonly manager: EntityManager) {}

  async add(e: OrderEvent): Promise<void> {
    await this.manager.insert(OrderEventEntity, { ...e });
  }

  async getByOrder(orderId: string): Promise<OrderEvent[]> {
    const rows = await this.manager.find(OrderEventEntity, { where: { orderId }, order: { createdAt: 'ASC' } });
    return rows.map((r) => ({
      id: r.id, orderId: r.orderId, eventType: r.eventType, previousStatus: r.previousStatus as OrderStatus | null,
      newStatus: r.newStatus as OrderStatus, createdAt: r.createdAt,
    }));
  }
}

export class TypeOrmPaymentEventRepository implements PaymentEventRepository {
  constructor(private readonly manager: EntityManager) {}

  async add(e: PaymentEvent): Promise<void> {
    await this.manager.insert(PaymentEventEntity, { ...e });
  }
}

export class TypeOrmAuditEventRepository implements AuditEventRepository {
  constructor(private readonly manager: EntityManager) {}

  async add(e: AuditEvent): Promise<void> {
    await this.manager.insert(AuditEventEntity, { ...e });
  }
}
