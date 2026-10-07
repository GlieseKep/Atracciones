import { newId, ORDER_STATUSES, roundCents, PAYMENT_STATUSES, RESERVATION_STATUSES, USER_STATUSES, type UserStatus } from '@atracciones/domain';
import type {
  AdminAuditRow,
  AdminListFilter,
  AdminOrderDetail,
  AdminOrderRow,
  AdminPaymentRow,
  AdminReservationRow,
  AdminRoleRow,
  AdminSalesReport,
  AdminSlotRow,
  AdminSummary,
  AdminUserRow,
  PagedResult,
  UnitOfWork,
  UnitOfWorkFactory,
} from '@atracciones/data-management';
import { ConflictError, NotFoundError } from '../errors';
import { PaginationResult } from '../models';
import { LocalPermissions, type AuthenticatedUser } from '../shared/auth';
import type { BusinessClock } from '../shared/clock';
import type { LocalPermissionService } from '../shared/customers';
import { IdempotentOperations, type IdempotencyService, type TransactionRunner } from '../shared/transactions';
import type { OrderWorkflow } from './ecommerce';
import { isIsoDate, isSlotTime, validatePagination, ValidationErrors } from '../shared/validation';

/** Consulta común de los listados administrativos. */
export interface AdminListQuery {
  limit: number;
  offset: number;
  search: string | null;
  status: string | null;
  fromDate: string | null;
  toDate: string | null;
  attractionId: string | null;
}

export const SLOT_FILTERS = ['AVAILABLE', 'FULL'] as const;
export const AUDIT_RESOURCE_TYPES = ['attraction', 'availability', 'order', 'user'] as const;
export const MAX_SLOT_CAPACITY = 10_000;
const MAX_REPORT_DAYS = 366;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const toPage = <T>(page: PagedResult<T>, query: AdminListQuery) => new PaginationResult(page.items, page.totalItems, query.limit, query.offset);

const addDays = (date: string, days: number): string => {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

/**
 * Casos de uso del panel de administración: consultas de clientes, reservas, pedidos, pagos y disponibilidad, cambios
 * de capacidad, estado de usuarios, roles y reportes de ventas. Todos exigen el permiso local `admin:manage`; los
 * cambios quedan en `audit_events`.
 */
export class AdminService {
  constructor(
    private readonly units: UnitOfWorkFactory,
    private readonly transactions: TransactionRunner,
    private readonly permissions: LocalPermissionService,
    private readonly clock: BusinessClock,
    private readonly idempotency: IdempotencyService,
    private readonly workflow: OrderWorkflow,
  ) {}

  async summary(actor: AuthenticatedUser, days = 30): Promise<AdminSummary> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    return this.units.create().admin.getSummary(this.clock.today, days);
  }

  async users(actor: AuthenticatedUser, query: AdminListQuery): Promise<PaginationResult<AdminUserRow>> {
    const filter = await this.prepare(actor, query, USER_STATUSES);
    return toPage(await this.units.create().admin.listUsers(filter, query), query);
  }

  async user(actor: AuthenticatedUser, userId: string): Promise<AdminUserRow> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    const user = await this.units.create().admin.getUser(userId);
    if (!user) throw new NotFoundError('El usuario no existe.');
    return user;
  }

  async reservations(actor: AuthenticatedUser, query: AdminListQuery): Promise<PaginationResult<AdminReservationRow>> {
    const filter = await this.prepare(actor, query, RESERVATION_STATUSES);
    return toPage(await this.units.create().admin.listReservations(filter, query), query);
  }

  async orders(actor: AuthenticatedUser, query: AdminListQuery): Promise<PaginationResult<AdminOrderRow>> {
    const filter = await this.prepare(actor, query, ORDER_STATUSES);
    return toPage(await this.units.create().admin.listOrders(filter, query), query);
  }

  async order(actor: AuthenticatedUser, orderId: string): Promise<AdminOrderDetail> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    const order = await this.units.create().admin.getOrderDetail(orderId);
    if (!order) throw new NotFoundError('El pedido no existe.');
    return order;
  }

  /** Cancela un pedido pendiente de pago de cualquier cliente: libera cupos y cancela su reserva. */
  async cancelOrder(actor: AuthenticatedUser, orderId: string, reason: string, idempotencyKey: string): Promise<AdminOrderDetail> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    new ValidationErrors().requiredText(reason, 'reason', 500).throwIfAny();
    const text = reason.trim();
    await this.idempotency.execute(actor, IdempotentOperations.AdminCancelOrder, idempotencyKey, { orderId, reason: text }, async (uow) => {
      const order = await uow.orders.getById(orderId);
      if (!order) throw new NotFoundError('El pedido no existe.');
      if (order.status !== 'PENDING_PAYMENT') {
        throw new ConflictError(
          ConflictError.INVALID_STATE_TRANSITION,
          `Solo se cancelan pedidos pendientes de pago; este está ${order.status}. Un pedido pagado se reembolsa.`,
        );
      }
      await this.workflow.cancelPendingOrder(uow, order, text);
      await this.audit(uow, actor, 'order.cancelled', 'order', orderId, text);
      return { orderId };
    });
    return (await this.units.create().admin.getOrderDetail(orderId))!;
  }

  /**
   * Reembolso simulado (total si se omite `amount`). Solo para pedidos pagados o parcialmente reembolsados con un pago
   * liquidado; nunca supera lo cobrado. El reembolso total cancela la reserva y libera los cupos.
   */
  async refundOrder(
    actor: AuthenticatedUser, orderId: string, amount: number | null, reason: string, idempotencyKey: string,
  ): Promise<AdminOrderDetail> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    new ValidationErrors()
      .requiredText(reason, 'reason', 500)
      .when(amount !== null && (!Number.isFinite(amount) || amount <= 0 || roundCents(amount) !== amount), 'amount', 'amount debe ser mayor que cero y tener como máximo dos decimales.')
      .throwIfAny();
    const text = reason.trim();
    await this.idempotency.execute(actor, IdempotentOperations.AdminRefundOrder, idempotencyKey, { orderId, amount, reason: text }, async (uow) => {
      const order = await uow.orders.getById(orderId);
      if (!order) throw new NotFoundError('El pedido no existe.');
      if (order.status !== 'PAID' && order.status !== 'PARTIALLY_REFUNDED') {
        throw new ConflictError(ConflictError.INVALID_STATE_TRANSITION, `Solo se reembolsan pedidos pagados; este está ${order.status}.`);
      }
      const payment = (await uow.payments.getByOrder(order.id)).find((p) => p.status === 'SETTLED' || p.status === 'PARTIALLY_REFUNDED');
      if (!payment) throw new ConflictError('NO_SETTLED_PAYMENT', 'El pedido no tiene un pago liquidado que reembolsar.');
      const already = await uow.admin.getRefundedAmount(payment.id);
      const remaining = roundCents(payment.amount.amount - already);
      const value = amount ?? remaining;
      if (value > remaining) {
        throw new ConflictError('REFUND_EXCEEDS_PAYMENT', `El reembolso no puede superar lo pendiente de devolver (${remaining.toFixed(2)} ${payment.amount.currency}).`);
      }
      const result = await this.workflow.refundOrder(uow, order, payment, value, already, text);
      await this.audit(uow, actor, result.full ? 'order.refunded' : 'order.partially_refunded', 'order', orderId, `${value.toFixed(2)} ${payment.amount.currency}: ${text}`);
      return { orderId, refunded: result.refunded };
    });
    return (await this.units.create().admin.getOrderDetail(orderId))!;
  }

  async payments(actor: AuthenticatedUser, query: AdminListQuery): Promise<PaginationResult<AdminPaymentRow>> {
    const filter = await this.prepare(actor, query, PAYMENT_STATUSES);
    return toPage(await this.units.create().admin.listPayments(filter, query), query);
  }

  async slots(actor: AuthenticatedUser, query: AdminListQuery): Promise<PaginationResult<AdminSlotRow>> {
    const filter = await this.prepare(actor, query, SLOT_FILTERS);
    return toPage(await this.units.create().admin.listSlots(filter, query), query);
  }

  async updateSlotCapacity(actor: AuthenticatedUser, slotId: string, capacity: number): Promise<AdminSlotRow> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    validateCapacity(capacity);
    return this.transactions.execute(async (uow) => {
      const slot = await uow.admin.getSlot(slotId);
      if (!slot) throw new NotFoundError('La franja no existe.');
      if (!(await uow.admin.updateSlotCapacity(slotId, capacity))) {
        throw new ConflictError(
          ConflictError.INSUFFICIENT_AVAILABILITY,
          `La capacidad no puede ser menor que los ${slot.reserved} cupos ya reservados.`,
        );
      }
      await this.audit(uow, actor, 'availability.capacity_changed', 'availability', slotId, `${slot.capacity} -> ${capacity}`);
      return { ...slot, capacity };
    });
  }

  async addSlot(actor: AuthenticatedUser, attractionId: string, date: string, time: string, capacity: number): Promise<AdminSlotRow> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    new ValidationErrors()
      .when(!isIsoDate(date), 'date', 'date debe ser una fecha YYYY-MM-DD válida.')
      .when(isIsoDate(date) && date < this.clock.today, 'date', 'No se pueden crear franjas en fechas pasadas.')
      .when(!isSlotTime(time), 'time', 'time debe tener formato HH:mm.')
      .throwIfAny();
    validateCapacity(capacity);
    return this.transactions.execute(async (uow) => {
      if (!(await uow.attractions.exists(attractionId))) throw new NotFoundError('La atracción no existe.');
      const id = await uow.admin.addSlot(attractionId, date, time, capacity);
      if (!id) throw new ConflictError(ConflictError.SLOT_UNAVAILABLE, `Ya existe una franja el ${date} a las ${time}.`);
      await this.audit(uow, actor, 'availability.slot_created', 'availability', id, `${date} ${time}, capacidad ${capacity}`);
      return (await uow.admin.getSlot(id))!;
    });
  }

  async roles(actor: AuthenticatedUser): Promise<AdminRoleRow[]> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    return this.units.create().admin.listRoles();
  }

  async setUserStatus(actor: AuthenticatedUser, userId: string, status: string, reason: string | null): Promise<AdminUserRow> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    new ValidationErrors()
      .when(!USER_STATUSES.includes(status as UserStatus), 'status', `status debe ser uno de: ${USER_STATUSES.join(', ')}.`)
      .throwIfAny();
    return this.transactions.execute(async (uow) => {
      const target = await this.requireUser(uow, userId);
      const self = await uow.users.getByIdentity(actor.issuer, actor.subject);
      if (self?.id === userId && status !== 'ACTIVE') {
        throw new ConflictError(ConflictError.INVALID_STATE_TRANSITION, 'No puedes desactivar tu propia cuenta.');
      }
      if (target.status !== status) {
        await uow.admin.setUserStatus(userId, status as UserStatus, this.clock.utcNow);
        await this.audit(uow, actor, 'user.status_changed', 'user', userId, reason ?? `${target.status} -> ${status}`);
      }
      return (await uow.admin.getUser(userId))!;
    });
  }

  async assignRole(actor: AuthenticatedUser, userId: string, roleId: string, reason: string | null): Promise<AdminUserRow> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    return this.transactions.execute(async (uow) => {
      await this.requireUser(uow, userId);
      const role = await this.requireRole(uow, roleId);
      const self = await uow.users.getByIdentity(actor.issuer, actor.subject);
      const assigned = await uow.admin.assignRole(userId, role.id, self?.id ?? null, reason ?? 'Asignado desde el panel de administración', this.clock.utcNow);
      if (assigned) await this.audit(uow, actor, 'user.role_assigned', 'user', userId, `${role.name}${reason ? `: ${reason}` : ''}`);
      return (await uow.admin.getUser(userId))!;
    });
  }

  async revokeRole(actor: AuthenticatedUser, userId: string, roleId: string, reason: string | null): Promise<AdminUserRow> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    return this.transactions.execute(async (uow) => {
      await this.requireUser(uow, userId);
      const role = await this.requireRole(uow, roleId);
      const self = await uow.users.getByIdentity(actor.issuer, actor.subject);
      if (self?.id === userId && role.permissions.includes(LocalPermissions.AdminManage)) {
        throw new ConflictError(ConflictError.INVALID_STATE_TRANSITION, 'No puedes quitarte tu propio rol de administración.');
      }
      if (await uow.admin.revokeRole(userId, role.id, this.clock.utcNow)) {
        await this.audit(uow, actor, 'user.role_revoked', 'user', userId, `${role.name}${reason ? `: ${reason}` : ''}`);
      }
      return (await uow.admin.getUser(userId))!;
    });
  }

  /** Ventas (pedidos cobrados) y reservas confirmadas en un rango; por defecto, los últimos 30 días. */
  async salesReport(actor: AuthenticatedUser, fromDate: string | null, toDate: string | null): Promise<AdminSalesReport & { fromDate: string; toDate: string }> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    const to = toDate ?? this.clock.today;
    const from = fromDate ?? addDays(to, -29);
    new ValidationErrors()
      .when(!isIsoDate(from), 'fromDate', 'fromDate debe ser una fecha YYYY-MM-DD válida.')
      .when(!isIsoDate(to), 'toDate', 'toDate debe ser una fecha YYYY-MM-DD válida.')
      .when(isIsoDate(from) && isIsoDate(to) && to < from, 'toDate', 'toDate debe ser igual o posterior a fromDate.')
      .when(isIsoDate(from) && isIsoDate(to) && addDays(from, MAX_REPORT_DAYS) < to, 'toDate', `El rango no puede superar ${MAX_REPORT_DAYS} días.`)
      .throwIfAny();
    return { fromDate: from, toDate: to, ...(await this.units.create().admin.salesReport(from, to)) };
  }

  /** Registro de auditoría de los cambios administrativos y de catálogo. `status` = tipo de recurso. */
  async auditLog(actor: AuthenticatedUser, query: AdminListQuery): Promise<PaginationResult<AdminAuditRow>> {
    const filter = await this.prepare(actor, query, AUDIT_RESOURCE_TYPES);
    return toPage(await this.units.create().admin.listAudit(filter, query), query);
  }

  private async prepare(actor: AuthenticatedUser, query: AdminListQuery, statuses: readonly string[]): Promise<AdminListFilter> {
    await this.permissions.ensure(actor, LocalPermissions.AdminManage);
    validatePagination(query.limit, query.offset);
    const search = query.search?.trim() || null;
    new ValidationErrors()
      .when(search !== null && search.length > 100, 'search', 'search admite como máximo 100 caracteres.')
      .when(query.status !== null && !statuses.includes(query.status), 'status', `status debe ser uno de: ${statuses.join(', ')}.`)
      .when(query.fromDate !== null && !isIsoDate(query.fromDate), 'fromDate', 'fromDate debe ser una fecha YYYY-MM-DD válida.')
      .when(query.toDate !== null && !isIsoDate(query.toDate), 'toDate', 'toDate debe ser una fecha YYYY-MM-DD válida.')
      .when(query.fromDate !== null && query.toDate !== null && query.toDate < query.fromDate, 'toDate', 'toDate debe ser igual o posterior a fromDate.')
      .when(query.attractionId !== null && !UUID.test(query.attractionId), 'attractionId', 'attractionId debe ser un UUID.')
      .throwIfAny();
    return { search, status: query.status, fromDate: query.fromDate, toDate: query.toDate, attractionId: query.attractionId };
  }

  private async requireUser(uow: UnitOfWork, userId: string): Promise<AdminUserRow> {
    const user = await uow.admin.getUser(userId);
    if (!user) throw new NotFoundError('El usuario no existe.');
    return user;
  }

  private async requireRole(uow: UnitOfWork, roleId: string): Promise<AdminRoleRow> {
    const role = (await uow.admin.listRoles()).find((r) => r.id === roleId);
    if (!role) throw new NotFoundError('El rol no existe.');
    return role;
  }

  private async audit(uow: UnitOfWork, actor: AuthenticatedUser, action: string, resourceType: string, resourceId: string, reason: string | null) {
    const user = await uow.users.getByIdentity(actor.issuer, actor.subject);
    await uow.auditEvents.add({
      id: newId(),
      actorUserId: user?.id ?? null,
      actorSubject: actor.subject,
      action,
      resourceType,
      resourceId,
      reason: reason?.slice(0, 500) ?? null,
      createdAt: this.clock.utcNow,
    });
  }
}

function validateCapacity(capacity: number): void {
  new ValidationErrors()
    .when(!Number.isInteger(capacity) || capacity < 0 || capacity > MAX_SLOT_CAPACITY, 'capacity', `capacity debe ser un entero entre 0 y ${MAX_SLOT_CAPACITY}.`)
    .throwIfAny();
}
