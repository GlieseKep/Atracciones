import type { UnitOfWorkFactory } from '@atracciones/data-management';
import { AdminService } from './services/admin';
import { AttractionService, AvailabilityService } from './services/catalog';
import { OrderService, OrderWorkflow, PaymentSimulationService, PurchaseService } from './services/ecommerce';
import { CustomerService, UserProfileService } from './services/identity';
import { ReservationService } from './services/reservations';
import { BusinessClock } from './shared/clock';
import { CustomerResolver, deterministicPaymentPolicy, LocalPermissionService, type PaymentSimulationPolicy } from './shared/customers';
import { DEFAULT_BUSINESS_OPTIONS, validateBusinessOptions, type BusinessOptions } from './shared/options';
import { PageTokenService } from './shared/page-tokens';
import { IdempotencyService, TransactionRunner } from './shared/transactions';
import { SlotRequestValidator } from './shared/validation';

export * from './errors';
export * from './models';
export * from './services/admin';
export * from './services/catalog';
export * from './services/ecommerce';
export * from './services/identity';
export * from './services/reservations';
export * from './shared/auth';
export * from './shared/clock';
export * from './shared/customers';
export * from './shared/options';
export * from './shared/page-tokens';
export * from './shared/transactions';
export * from './shared/validation';

export interface BusinessServices {
  attractions: AttractionService;
  availability: AvailabilityService;
  reservations: ReservationService;
  profiles: UserProfileService;
  customers: CustomerService;
  purchases: PurchaseService;
  orders: OrderService;
  payments: PaymentSimulationService;
  permissions: LocalPermissionService;
  admin: AdminService;
  clock: BusinessClock;
}

/** Composition root de Business: construye los casos de uso sobre una implementación de `UnitOfWorkFactory`. */
export function createBusinessServices(
  units: UnitOfWorkFactory,
  options: Partial<BusinessOptions> = {},
  deps: { now?: () => Date; paymentPolicy?: PaymentSimulationPolicy } = {},
): BusinessServices {
  const settings = validateBusinessOptions({ ...DEFAULT_BUSINESS_OPTIONS, ...options });
  const now = deps.now ?? (() => new Date());
  const clock = new BusinessClock(settings.timeZone, now);
  const transactions = new TransactionRunner(units);
  const idempotency = new IdempotencyService(transactions, settings, now);
  const customers = new CustomerResolver();
  const permissions = new LocalPermissionService(units);
  const slots = new SlotRequestValidator(clock, settings);
  const workflow = new OrderWorkflow(clock, settings, deps.paymentPolicy ?? deterministicPaymentPolicy);

  return {
    attractions: new AttractionService(units, idempotency, permissions, new PageTokenService(settings, now), clock),
    availability: new AvailabilityService(units, clock),
    reservations: new ReservationService(units, idempotency, customers, slots, clock),
    profiles: new UserProfileService(units, transactions, clock),
    customers: new CustomerService(units, transactions, customers, clock),
    purchases: new PurchaseService(idempotency, customers, slots, workflow),
    orders: new OrderService(units, idempotency, customers, slots, workflow),
    payments: new PaymentSimulationService(idempotency, customers, workflow, clock),
    permissions,
    admin: new AdminService(units, transactions, permissions, clock),
    clock,
  };
}
