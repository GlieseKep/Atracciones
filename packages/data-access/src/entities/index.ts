import { AttractionEntity, AvailabilityEntity, BadgeEntity, CategoryEntity, InclusionEntity, LanguageEntity, LocationEntity, OperatorEntity, PhotoEntity, ReservationEntity } from './catalog.entities';
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
} from './ecommerce.entities';
import { CustomerEntity, RoleEntity, RolePermissionEntity, UserEntity, UserRoleEntity } from './identity.entities';

export * from './catalog.entities';
export * from './ecommerce.entities';
export * from './identity.entities';

export const ENTITIES = [
  OperatorEntity, CategoryEntity, BadgeEntity, InclusionEntity, LanguageEntity, AttractionEntity, LocationEntity, PhotoEntity,
  AvailabilityEntity, ReservationEntity, UserEntity, CustomerEntity, RoleEntity, RolePermissionEntity, UserRoleEntity,
  PurchaseEntity, OrderEntity, OrderItemEntity, OrderEventEntity, PaymentSimulationEntity, PaymentAttemptEntity,
  PaymentEventEntity, InventoryMovementEntity, AuditEventEntity, IdempotencyKeyEntity,
];
