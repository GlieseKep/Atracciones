import { Check, Column, Entity, Index, JoinColumn, ManyToOne, OneToMany, PrimaryColumn, Unique } from 'typeorm';
import { currency, instant, money, slotTime } from './columns';
import { CustomerEntity, UserEntity } from './identity.entities';
import { ReservationEntity } from './catalog.entities';

const ORDER_STATUSES = `'PENDING_PAYMENT', 'PAID', 'FULFILLED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'`;
const PAYMENT_STATUSES = `'PENDING', 'AUTHORIZED', 'SETTLED', 'REJECTED', 'FAILED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'`;

@Entity('purchases')
@Check('CK_purchases_quantity', `"quantity" > 0`)
@Index(['attractionId', 'serviceDate'])
export class PurchaseEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Index()
  @Column({ name: 'customer_id', type: 'uuid' })
  customerId!: string;

  @ManyToOne(() => CustomerEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'customer_id' })
  customer?: CustomerEntity;

  @Column({ name: 'attraction_id', type: 'uuid' })
  attractionId!: string;

  @Column({ name: 'service_date', type: 'date' })
  serviceDate!: string;

  @Column({ name: 'service_time', ...slotTime })
  serviceTime!: string;

  @Column({ type: 'int' })
  quantity!: number;

  @Column({ name: 'unit_price_currency', ...currency })
  unitPriceCurrency!: string;

  @Column({ name: 'unit_price_amount', ...money })
  unitPriceAmount!: number;

  @Column({ name: 'total_amount', ...money })
  totalAmount!: number;

  @Column({ name: 'request_idempotency_key', type: 'uuid' })
  requestIdempotencyKey!: string;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;
}

@Entity('orders')
@Check('CK_orders_status', `"status" IN (${ORDER_STATUSES})`)
export class OrderEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Index()
  @Column({ name: 'customer_id', type: 'uuid' })
  customerId!: string;

  @ManyToOne(() => CustomerEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'customer_id' })
  customer?: CustomerEntity;

  @Column({ name: 'purchase_id', type: 'uuid', nullable: true, unique: true })
  purchaseId!: string | null;

  @ManyToOne(() => PurchaseEntity, { onDelete: 'RESTRICT', nullable: true })
  @JoinColumn({ name: 'purchase_id' })
  purchase?: PurchaseEntity | null;

  @Index()
  @Column({ name: 'reservation_id', type: 'uuid', nullable: true })
  reservationId!: string | null;

  @ManyToOne(() => ReservationEntity, { onDelete: 'RESTRICT', nullable: true })
  @JoinColumn({ name: 'reservation_id' })
  reservation?: ReservationEntity | null;

  @Index()
  @Column({ type: 'varchar', length: 30 })
  status!: string;

  @Column({ ...currency })
  currency!: string;

  @Column({ name: 'total_amount', ...money })
  totalAmount!: number;

  @Column({ name: 'hold_expires_at', ...instant, nullable: true })
  holdExpiresAt!: Date | null;

  @Column({ name: 'cancellation_reason', type: 'varchar', length: 500, nullable: true })
  cancellationReason!: string | null;

  @Index()
  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;

  @Column({ name: 'updated_at', ...instant })
  updatedAt!: Date;

  @OneToMany(() => OrderItemEntity, (i) => i.order, { cascade: ['insert'] })
  items!: OrderItemEntity[];
}

@Entity('order_items')
@Check('CK_order_items_quantity', `"quantity" > 0`)
export class OrderItemEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'order_id', type: 'uuid' })
  orderId!: string;

  @ManyToOne(() => OrderEntity, (o) => o.items, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'order_id' })
  order?: OrderEntity;

  @Column({ name: 'attraction_id', type: 'uuid' })
  attractionId!: string;

  @Index()
  @Column({ name: 'availability_id', type: 'uuid' })
  availabilityId!: string;

  @Column({ name: 'service_date', type: 'date' })
  serviceDate!: string;

  @Column({ name: 'service_time', ...slotTime })
  serviceTime!: string;

  @Column({ type: 'int' })
  quantity!: number;

  @Column({ name: 'unit_price_currency', ...currency })
  unitPriceCurrency!: string;

  @Column({ name: 'unit_price_amount', ...money })
  unitPriceAmount!: number;
}

/** Historial inmutable de estados del pedido (solo inserción). */
@Entity('order_events')
@Index(['orderId', 'createdAt'])
export class OrderEventEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'order_id', type: 'uuid' })
  orderId!: string;

  @ManyToOne(() => OrderEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'order_id' })
  order?: OrderEntity;

  @Column({ name: 'event_type', type: 'varchar', length: 60 })
  eventType!: string;

  @Column({ name: 'previous_status', type: 'varchar', length: 30, nullable: true })
  previousStatus!: string | null;

  @Column({ name: 'new_status', type: 'varchar', length: 30 })
  newStatus!: string;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;
}

@Entity('payment_simulations')
@Check('CK_payment_simulations_status', `"status" IN (${PAYMENT_STATUSES})`)
@Check('CK_payment_simulations_method', `"payment_method" IN ('CARD', 'BANK_TRANSFER')`)
@Check('CK_payment_simulations_amount', `"amount" > 0`)
export class PaymentSimulationEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Index()
  @Column({ name: 'order_id', type: 'uuid' })
  orderId!: string;

  @ManyToOne(() => OrderEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'order_id' })
  order?: OrderEntity;

  @Column({ name: 'payment_method', type: 'varchar', length: 20 })
  paymentMethod!: string;

  @Index()
  @Column({ type: 'varchar', length: 30 })
  status!: string;

  @Column({ ...money })
  amount!: number;

  @Column({ ...currency })
  currency!: string;

  @Column({ name: 'gateway_reference', type: 'varchar', length: 64, unique: true })
  gatewayReference!: string;

  @Index()
  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;

  @Column({ name: 'processed_at', ...instant, nullable: true })
  processedAt!: Date | null;

  @Column({ name: 'failure_reason', type: 'varchar', length: 300, nullable: true })
  failureReason!: string | null;
}

/** Intento de pago simulado: inmutable y único por simulación y número. */
@Entity('payment_attempts')
@Unique('UQ_payment_attempts_number', ['paymentSimulationId', 'attemptNumber'])
export class PaymentAttemptEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'payment_simulation_id', type: 'uuid' })
  paymentSimulationId!: string;

  @ManyToOne(() => PaymentSimulationEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'payment_simulation_id' })
  paymentSimulation?: PaymentSimulationEntity;

  @Column({ name: 'attempt_number', type: 'int' })
  attemptNumber!: number;

  @Column({ type: 'varchar', length: 30 })
  status!: string;

  @Column({ name: 'response_code', type: 'varchar', length: 10 })
  responseCode!: string;

  @Column({ name: 'response_message', type: 'varchar', length: 300 })
  responseMessage!: string;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;
}

/** Eventos de la pasarela simulada (solo inserción). */
@Entity('payment_events')
@Index(['paymentSimulationId', 'createdAt'])
export class PaymentEventEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'payment_simulation_id', type: 'uuid' })
  paymentSimulationId!: string;

  @ManyToOne(() => PaymentSimulationEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'payment_simulation_id' })
  paymentSimulation?: PaymentSimulationEntity;

  @Column({ name: 'event_type', type: 'varchar', length: 60 })
  eventType!: string;

  /** JSON serializado; PostgreSQL lo valida y almacena como `jsonb`. */
  @Column({ type: 'jsonb' })
  payload!: string;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;
}

/** Cambio de cupos de una franja (solo inserción). */
@Entity('inventory_movements')
@Check('CK_inventory_movements_type', `"movement_type" IN ('CONFIRMED', 'RELEASED', 'CANCELLED')`)
export class InventoryMovementEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Index()
  @Column({ name: 'attraction_id', type: 'uuid' })
  attractionId!: string;

  @Index()
  @Column({ name: 'availability_id', type: 'uuid' })
  availabilityId!: string;

  @Column({ type: 'int' })
  quantity!: number;

  @Column({ name: 'movement_type', type: 'varchar', length: 20 })
  movementType!: string;

  @Index()
  @Column({ name: 'purchase_id', type: 'uuid', nullable: true })
  purchaseId!: string | null;

  @Index()
  @Column({ name: 'reservation_id', type: 'uuid', nullable: true })
  reservationId!: string | null;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;
}

/** Auditoría inmutable de mutaciones administrativas (solo inserción). */
@Entity('audit_events')
@Index(['resourceType', 'resourceId'])
export class AuditEventEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'actor_user_id', type: 'uuid', nullable: true })
  actorUserId!: string | null;

  @ManyToOne(() => UserEntity, { onDelete: 'RESTRICT', nullable: true })
  @JoinColumn({ name: 'actor_user_id' })
  actorUser?: UserEntity | null;

  @Column({ name: 'actor_subject', type: 'varchar', length: 300 })
  actorSubject!: string;

  @Column({ type: 'varchar', length: 100 })
  action!: string;

  @Column({ name: 'resource_type', type: 'varchar', length: 60 })
  resourceType!: string;

  @Column({ name: 'resource_id', type: 'varchar', length: 100 })
  resourceId!: string;

  @Column({ type: 'varchar', length: 500, nullable: true })
  reason!: string | null;

  @Index()
  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;
}

/** Registro de idempotencia: identidad única `issuer + subject + operation + key`. */
@Entity('idempotency_keys')
@Unique('UQ_idempotency_identity', ['issuer', 'subject', 'operation', 'key'])
@Check('CK_idempotency_keys_status', `"status" IN ('IN_PROGRESS', 'COMPLETED')`)
export class IdempotencyKeyEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ type: 'varchar', length: 300 })
  issuer!: string;

  @Column({ type: 'varchar', length: 300 })
  subject!: string;

  @Column({ type: 'varchar', length: 60 })
  operation!: string;

  @Column({ type: 'uuid' })
  key!: string;

  @Column({ name: 'request_hash', type: 'varchar', length: 64 })
  requestHash!: string;

  @Column({ type: 'varchar', length: 20 })
  status!: string;

  @Column({ name: 'resource_id', type: 'uuid', nullable: true })
  resourceId!: string | null;

  @Column({ name: 'response_body', type: 'text', nullable: true })
  responseBody!: string | null;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;

  @Index()
  @Column({ name: 'expires_at', ...instant })
  expiresAt!: Date;
}
