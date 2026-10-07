import { randomUUID } from 'node:crypto';
import type { IsoDate, LocalTime, UserStatus } from '@atracciones/domain';
import {
  PagedResult,
  type AdminListFilter,
  type AdminOrderDetail,
  type AdminOrderRow,
  type AdminPaymentRow,
  type AdminRepository,
  type AdminReservationRow,
  type AdminRoleRow,
  type AdminSalesReport,
  type AdminSlotRow,
  type AdminSummary,
  type AdminUserRow,
  type PaginationRequest,
} from '@atracciones/data-management';
import type { EntityManager } from 'typeorm';

/** Estados de pedido que cuentan como venta (ingreso cobrado). */
const SOLD_ORDER_STATUSES = `('PAID', 'FULFILLED', 'PARTIALLY_REFUNDED')`;

type Row = Record<string, unknown>;

/** `payment_events.payload` como objeto jsonb (las filas antiguas pueden guardar el JSON como texto escalar). */
const PAYLOAD = `(CASE WHEN jsonb_typeof(e.payload) = 'string' THEN (e.payload #>> '{}')::jsonb ELSE e.payload END)`;

/** Acumula condiciones `WHERE` con parámetros posicionales (`$1`, `$2`, ...). */
class Where {
  readonly params: unknown[] = [];
  private readonly parts: string[] = [];

  add(condition: (p: (value: unknown) => string) => string): this {
    this.parts.push(condition((value) => `$${this.params.push(value)}`));
    return this;
  }

  when(test: unknown, condition: (p: (value: unknown) => string) => string): this {
    return test === null || test === undefined || test === '' ? this : this.add(condition);
  }

  toString(): string {
    return this.parts.length ? `WHERE ${this.parts.join(' AND ')}` : '';
  }
}

const like = (search: string) => `%${search.replace(/[\\%_]/g, (c) => `\\${c}`)}%`;
const num = (value: unknown) => (value === null || value === undefined ? 0 : Number(value));
/** TypeORM devuelve `[filas, afectadas]` para UPDATE/DELETE y solo las filas para INSERT/SELECT. */
const returned = (result: unknown): unknown[] =>
  Array.isArray(result) && result.length === 2 && Array.isArray(result[0]) && typeof result[1] === 'number' ? result[0] : (result as unknown[]);
const countBy = (rows: Row[]) => Object.fromEntries(rows.map((r) => [String(r.key), num(r.count)]));

/**
 * Lecturas y cambios del área administrativa con SQL explícito: los listados combinan varias tablas y se paginan en
 * PostgreSQL. Las fechas (`date`, `time`) se devuelven como texto `YYYY-MM-DD` / `HH:mm` y los importes como números.
 */
export class TypeOrmAdminRepository implements AdminRepository {
  constructor(private readonly manager: EntityManager) {}

  private async page<T>(from: string, where: Where, select: string, orderBy: string, page: PaginationRequest, map: (row: Row) => T) {
    const [{ total }] = await this.manager.query(`SELECT count(*)::int AS total ${from} ${where}`, where.params);
    const rows: Row[] = await this.manager.query(
      `SELECT ${select} ${from} ${where} ORDER BY ${orderBy} LIMIT ${Number(page.limit)} OFFSET ${Number(page.offset)}`,
      where.params,
    );
    return PagedResult.create(rows.map(map), num(total), page);
  }

  async getSummary(from: IsoDate, days: number): Promise<AdminSummary> {
    const q = (sql: string, params: unknown[] = []) => this.manager.query(sql, params) as Promise<Row[]>;
    const [[totals], reservations, orders, payments, revenue, [upcoming]] = await Promise.all([
      q(`SELECT (SELECT count(*)::int FROM attractions) AS attractions,
                (SELECT count(*)::int FROM customers c
                  WHERE NOT EXISTS (SELECT 1 FROM user_roles ur WHERE ur.user_id = c.user_id AND ur.revoked_at IS NULL)) AS customers,
                (SELECT count(*)::int FROM users WHERE status = 'ACTIVE') AS active_users`),
      q(`SELECT status AS key, count(*)::int AS count FROM reservations GROUP BY status`),
      q(`SELECT status AS key, count(*)::int AS count FROM orders GROUP BY status`),
      q(`SELECT status AS key, count(*)::int AS count FROM payment_simulations GROUP BY status`),
      q(`SELECT currency, sum(total_amount)::float8 AS amount FROM orders WHERE status IN ${SOLD_ORDER_STATUSES} GROUP BY currency ORDER BY currency`),
      q(
        `SELECT count(*)::int AS slots, coalesce(sum(capacity), 0)::int AS capacity, coalesce(sum(reserved_quantity), 0)::int AS reserved
           FROM attraction_availability WHERE date >= $1::date AND date < $1::date + $2::int`,
        [from, days],
      ),
    ]);
    return {
      attractions: num(totals.attractions),
      customers: num(totals.customers),
      activeUsers: num(totals.active_users),
      reservationsByStatus: countBy(reservations),
      ordersByStatus: countBy(orders),
      paymentsByStatus: countBy(payments),
      revenue: revenue.map((r) => ({ currency: String(r.currency), amount: num(r.amount) })),
      upcoming: { slots: num(upcoming.slots), capacity: num(upcoming.capacity), reserved: num(upcoming.reserved) },
    };
  }

  private static readonly USER_FROM = `FROM users u LEFT JOIN customers c ON c.user_id = u.id`;
  private static readonly USER_SELECT = `
    u.id AS user_id, c.id AS customer_id, u.email, u.status, u.created_at,
    c.billing_name, c.billing_email, c.billing_address, c.tax_id,
    (SELECT r.customer_name FROM reservations r WHERE r.customer_id = c.id ORDER BY r.created_at DESC LIMIT 1) AS last_name,
    (SELECT count(*)::int FROM reservations r WHERE r.customer_id = c.id) AS reservations,
    (SELECT count(*)::int FROM orders o WHERE o.customer_id = c.id) AS orders,
    (SELECT coalesce(sum(o.total_amount), 0)::float8 FROM orders o WHERE o.customer_id = c.id AND o.status IN ${SOLD_ORDER_STATUSES}) AS total_spent,
    GREATEST(
      (SELECT max(r.created_at) FROM reservations r WHERE r.customer_id = c.id),
      (SELECT max(o.updated_at) FROM orders o WHERE o.customer_id = c.id)
    ) AS last_activity,
    coalesce((SELECT array_agg(ro.name ORDER BY ro.name) FROM user_roles ur JOIN roles ro ON ro.id = ur.role_id
               WHERE ur.user_id = u.id AND ur.revoked_at IS NULL), '{}') AS roles`;

  private static toUserRow(r: Row): AdminUserRow {
    return {
      userId: String(r.user_id),
      customerId: (r.customer_id as string | null) ?? null,
      email: String(r.email),
      name: (r.billing_name as string | null) ?? (r.last_name as string | null) ?? null,
      status: r.status as UserStatus,
      billingName: (r.billing_name as string | null) ?? null,
      billingEmail: (r.billing_email as string | null) ?? null,
      billingAddress: (r.billing_address as string | null) ?? null,
      taxId: (r.tax_id as string | null) ?? null,
      createdAt: r.created_at as Date,
      reservations: num(r.reservations),
      orders: num(r.orders),
      totalSpent: num(r.total_spent),
      lastActivityAt: (r.last_activity as Date | null) ?? null,
      roles: (r.roles as string[] | null) ?? [],
    };
  }

  listUsers(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminUserRow>> {
    const where = new Where()
      .when(filter.search, (p) => {
        const s = p(like(filter.search!));
        return `(u.email ILIKE ${s} OR c.billing_name ILIKE ${s} OR c.tax_id ILIKE ${s}
                 OR EXISTS (SELECT 1 FROM reservations r WHERE r.customer_id = c.id AND r.customer_name ILIKE ${s}))`;
      })
      .when(filter.status, (p) => `u.status = ${p(filter.status)}`);
    return this.page(TypeOrmAdminRepository.USER_FROM, where, TypeOrmAdminRepository.USER_SELECT, 'u.created_at DESC', page, TypeOrmAdminRepository.toUserRow);
  }

  async getUser(userId: string): Promise<AdminUserRow | null> {
    const rows: Row[] = await this.manager.query(
      `SELECT ${TypeOrmAdminRepository.USER_SELECT} ${TypeOrmAdminRepository.USER_FROM} WHERE u.id = $1`,
      [userId],
    );
    return rows.length ? TypeOrmAdminRepository.toUserRow(rows[0]) : null;
  }

  listReservations(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminReservationRow>> {
    const where = new Where()
      .when(filter.search, (p) => {
        const s = p(like(filter.search!));
        return `(r.customer_email ILIKE ${s} OR r.customer_name ILIKE ${s} OR a.name ILIKE ${s})`;
      })
      .when(filter.status, (p) => `r.status = ${p(filter.status)}`)
      .when(filter.fromDate, (p) => `r.date >= ${p(filter.fromDate)}::date`)
      .when(filter.toDate, (p) => `r.date <= ${p(filter.toDate)}::date`)
      .when(filter.attractionId, (p) => `r.attraction_id = ${p(filter.attractionId)}::uuid`);
    return this.page(
      `FROM reservations r
         LEFT JOIN attractions a ON a.id = r.attraction_id
         LEFT JOIN orders o ON o.reservation_id = r.id`,
      where,
      `r.id, r.attraction_id, a.name AS attraction_name, r.customer_name, r.customer_email,
       to_char(r.date, 'YYYY-MM-DD') AS date, to_char(r.time, 'HH24:MI') AS time, r.ticket_count,
       r.total_currency, r.total_amount::float8 AS total_amount, r.status, r.cancellation_reason, r.created_at,
       o.id AS order_id, o.status AS order_status`,
      'r.created_at DESC',
      page,
      (r) => ({
        id: String(r.id),
        attractionId: String(r.attraction_id),
        attractionName: (r.attraction_name as string | null) ?? '(atracción eliminada)',
        customerName: String(r.customer_name),
        customerEmail: String(r.customer_email),
        date: String(r.date),
        time: String(r.time),
        ticketCount: num(r.ticket_count),
        total: { currency: String(r.total_currency), amount: num(r.total_amount) },
        status: String(r.status),
        cancellationReason: (r.cancellation_reason as string | null) ?? null,
        createdAt: r.created_at as Date,
        orderId: (r.order_id as string | null) ?? null,
        orderStatus: (r.order_status as string | null) ?? null,
      }),
    );
  }

  private static readonly ORDER_FROM = `FROM orders o
         JOIN customers c ON c.id = o.customer_id
         JOIN users u ON u.id = c.user_id
         LEFT JOIN LATERAL (
           SELECT oi.attraction_id, a.name AS attraction_name, oi.service_date, oi.service_time,
                  (SELECT sum(x.quantity)::int FROM order_items x WHERE x.order_id = o.id) AS quantity
             FROM order_items oi LEFT JOIN attractions a ON a.id = oi.attraction_id
            WHERE oi.order_id = o.id ORDER BY oi.service_date, oi.service_time LIMIT 1
         ) i ON true
         LEFT JOIN LATERAL (
           SELECT ps.status FROM payment_simulations ps WHERE ps.order_id = o.id ORDER BY ps.created_at DESC LIMIT 1
         ) p ON true
         LEFT JOIN LATERAL (
           SELECT coalesce(sum((${PAYLOAD} ->> 'refundAmount')::numeric), 0)::float8 AS amount
             FROM payment_simulations ps JOIN payment_events e ON e.payment_simulation_id = ps.id
            WHERE ps.order_id = o.id AND ${PAYLOAD} ? 'refundAmount'
         ) rf ON true`;

  private static readonly ORDER_SELECT = `o.id, u.email, c.billing_name, o.status, o.currency, o.total_amount::float8 AS total_amount,
       o.hold_expires_at, o.reservation_id, o.cancellation_reason, o.created_at, o.updated_at,
       i.attraction_name, to_char(i.service_date, 'YYYY-MM-DD') AS service_date, to_char(i.service_time, 'HH24:MI') AS service_time,
       coalesce(i.quantity, 0) AS quantity, p.status AS payment_status, rf.amount AS refunded`;

  private static toOrderRow(r: Row): AdminOrderRow {
    return {
      id: String(r.id),
      customerEmail: String(r.email),
      attractionName: (r.attraction_name as string | null) ?? null,
      serviceDate: (r.service_date as string | null) ?? null,
      serviceTime: (r.service_time as string | null) ?? null,
      quantity: num(r.quantity),
      status: String(r.status),
      total: { currency: String(r.currency), amount: num(r.total_amount) },
      refunded: num(r.refunded),
      paymentStatus: (r.payment_status as string | null) ?? null,
      holdExpiresAt: (r.hold_expires_at as Date | null) ?? null,
      reservationId: (r.reservation_id as string | null) ?? null,
      createdAt: r.created_at as Date,
      updatedAt: r.updated_at as Date,
    };
  }

  listOrders(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminOrderRow>> {
    const where = new Where()
      .when(filter.search, (p) => {
        const s = p(like(filter.search!));
        return `(u.email ILIKE ${s} OR o.id::text ILIKE ${s} OR i.attraction_name ILIKE ${s})`;
      })
      .when(filter.status, (p) => `o.status = ${p(filter.status)}`)
      .when(filter.fromDate, (p) => `o.created_at >= ${p(filter.fromDate)}::date`)
      .when(filter.toDate, (p) => `o.created_at < ${p(filter.toDate)}::date + 1`)
      .when(filter.attractionId, (p) => `i.attraction_id = ${p(filter.attractionId)}::uuid`);
    return this.page(TypeOrmAdminRepository.ORDER_FROM, where, TypeOrmAdminRepository.ORDER_SELECT, 'o.created_at DESC', page, TypeOrmAdminRepository.toOrderRow);
  }

  async getOrderDetail(orderId: string): Promise<AdminOrderDetail | null> {
    const rows: Row[] = await this.manager.query(
      `SELECT ${TypeOrmAdminRepository.ORDER_SELECT}, r.status AS reservation_status, r.customer_name
         ${TypeOrmAdminRepository.ORDER_FROM} LEFT JOIN reservations r ON r.id = o.reservation_id
        WHERE o.id = $1`,
      [orderId],
    );
    if (!rows.length) return null;
    const row = rows[0];
    const [items, events, payments, attempts, paymentEvents] = await Promise.all([
      this.manager.query(
        `SELECT oi.attraction_id, coalesce(a.name, '(atracción eliminada)') AS attraction_name,
                to_char(oi.service_date, 'YYYY-MM-DD') AS service_date, to_char(oi.service_time, 'HH24:MI') AS service_time,
                oi.quantity, oi.unit_price_currency, oi.unit_price_amount::float8 AS unit_price_amount
           FROM order_items oi LEFT JOIN attractions a ON a.id = oi.attraction_id
          WHERE oi.order_id = $1 ORDER BY oi.service_date, oi.service_time`,
        [orderId],
      ) as Promise<Row[]>,
      this.manager.query(
        `SELECT event_type, previous_status, new_status, created_at FROM order_events WHERE order_id = $1 ORDER BY created_at, id`,
        [orderId],
      ) as Promise<Row[]>,
      this.manager.query(
        `SELECT id, payment_method, status, amount::float8 AS amount, currency, gateway_reference, failure_reason, created_at, processed_at
           FROM payment_simulations WHERE order_id = $1 ORDER BY created_at`,
        [orderId],
      ) as Promise<Row[]>,
      this.manager.query(
        `SELECT pa.payment_simulation_id, pa.attempt_number, pa.status, pa.response_code, pa.response_message, pa.created_at
           FROM payment_attempts pa JOIN payment_simulations ps ON ps.id = pa.payment_simulation_id
          WHERE ps.order_id = $1 ORDER BY pa.attempt_number`,
        [orderId],
      ) as Promise<Row[]>,
      this.manager.query(
        `SELECT e.payment_simulation_id, e.event_type, e.created_at,
                (${PAYLOAD} ->> 'refundAmount')::float8 AS refund_amount, ${PAYLOAD} ->> 'reason' AS reason
           FROM payment_events e JOIN payment_simulations ps ON ps.id = e.payment_simulation_id
          WHERE ps.order_id = $1 ORDER BY e.created_at, e.id`,
        [orderId],
      ) as Promise<Row[]>,
    ]);
    return {
      ...TypeOrmAdminRepository.toOrderRow(row),
      customerName: (row.billing_name as string | null) ?? (row.customer_name as string | null) ?? null,
      cancellationReason: (row.cancellation_reason as string | null) ?? null,
      reservationStatus: (row.reservation_status as string | null) ?? null,
      items: items.map((i) => ({
        attractionId: String(i.attraction_id),
        attractionName: String(i.attraction_name),
        serviceDate: String(i.service_date),
        serviceTime: String(i.service_time),
        quantity: num(i.quantity),
        unitPrice: { currency: String(i.unit_price_currency), amount: num(i.unit_price_amount) },
      })),
      events: events.map((e) => ({
        eventType: String(e.event_type),
        previousStatus: (e.previous_status as string | null) ?? null,
        newStatus: String(e.new_status),
        createdAt: e.created_at as Date,
      })),
      payments: payments.map((p) => {
        const own = (rows: Row[]) => rows.filter((x) => x.payment_simulation_id === p.id);
        const evs = own(paymentEvents);
        return {
          id: String(p.id),
          paymentMethod: String(p.payment_method),
          status: String(p.status),
          amount: { currency: String(p.currency), amount: num(p.amount) },
          gatewayReference: String(p.gateway_reference),
          attempts: own(attempts).length,
          failureReason: (p.failure_reason as string | null) ?? null,
          createdAt: p.created_at as Date,
          processedAt: (p.processed_at as Date | null) ?? null,
          refunded: evs.reduce((sum, e) => sum + num(e.refund_amount), 0),
          attemptsDetail: own(attempts).map((a) => ({
            attemptNumber: num(a.attempt_number),
            status: String(a.status),
            responseCode: String(a.response_code),
            responseMessage: String(a.response_message),
            createdAt: a.created_at as Date,
          })),
          events: evs.map((e) => ({
            eventType: String(e.event_type),
            createdAt: e.created_at as Date,
            amount: e.refund_amount === null ? null : num(e.refund_amount),
            reason: (e.reason as string | null) ?? null,
          })),
        };
      }),
    };
  }

  async getRefundedAmount(paymentId: string): Promise<number> {
    const [row]: Row[] = await this.manager.query(
      `SELECT coalesce(sum((${PAYLOAD} ->> 'refundAmount')::numeric), 0)::float8 AS amount
         FROM payment_events e WHERE e.payment_simulation_id = $1 AND ${PAYLOAD} ? 'refundAmount'`,
      [paymentId],
    );
    return num(row.amount);
  }

  listPayments(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminPaymentRow>> {
    const where = new Where()
      .when(filter.search, (p) => {
        const s = p(like(filter.search!));
        return `(u.email ILIKE ${s} OR ps.gateway_reference ILIKE ${s} OR ps.order_id::text ILIKE ${s})`;
      })
      .when(filter.status, (p) => `ps.status = ${p(filter.status)}`)
      .when(filter.fromDate, (p) => `ps.created_at >= ${p(filter.fromDate)}::date`)
      .when(filter.toDate, (p) => `ps.created_at < ${p(filter.toDate)}::date + 1`);
    return this.page(
      `FROM payment_simulations ps
         JOIN orders o ON o.id = ps.order_id
         JOIN customers c ON c.id = o.customer_id
         JOIN users u ON u.id = c.user_id`,
      where,
      `ps.id, ps.order_id, u.email, ps.payment_method, ps.status, ps.amount::float8 AS amount, ps.currency, ps.gateway_reference,
       ps.failure_reason, ps.created_at, ps.processed_at,
       (SELECT count(*)::int FROM payment_attempts pa WHERE pa.payment_simulation_id = ps.id) AS attempts`,
      'ps.created_at DESC',
      page,
      (r) => ({
        id: String(r.id),
        orderId: String(r.order_id),
        customerEmail: String(r.email),
        paymentMethod: String(r.payment_method),
        status: String(r.status),
        amount: { currency: String(r.currency), amount: num(r.amount) },
        gatewayReference: String(r.gateway_reference),
        attempts: num(r.attempts),
        failureReason: (r.failure_reason as string | null) ?? null,
        createdAt: r.created_at as Date,
        processedAt: (r.processed_at as Date | null) ?? null,
      }),
    );
  }

  private static readonly SLOT_FROM = `FROM attraction_availability s JOIN attractions a ON a.id = s.attraction_id`;
  private static readonly SLOT_SELECT = `s.id, s.attraction_id, a.name AS attraction_name, to_char(s.date, 'YYYY-MM-DD') AS date,
    to_char(s.time, 'HH24:MI') AS time, s.capacity, s.reserved_quantity`;

  private static toSlotRow(r: Row): AdminSlotRow {
    return {
      id: String(r.id),
      attractionId: String(r.attraction_id),
      attractionName: String(r.attraction_name),
      date: String(r.date),
      time: String(r.time),
      capacity: num(r.capacity),
      reserved: num(r.reserved_quantity),
    };
  }

  listSlots(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminSlotRow>> {
    const where = new Where()
      .when(filter.search, (p) => `a.name ILIKE ${p(like(filter.search!))}`)
      .when(filter.attractionId, (p) => `s.attraction_id = ${p(filter.attractionId)}::uuid`)
      .when(filter.fromDate, (p) => `s.date >= ${p(filter.fromDate)}::date`)
      .when(filter.toDate, (p) => `s.date <= ${p(filter.toDate)}::date`);
    if (filter.status === 'FULL') where.add(() => 's.reserved_quantity >= s.capacity');
    if (filter.status === 'AVAILABLE') where.add(() => 's.reserved_quantity < s.capacity');
    return this.page(
      TypeOrmAdminRepository.SLOT_FROM,
      where,
      TypeOrmAdminRepository.SLOT_SELECT,
      's.date, s.time, a.name',
      page,
      TypeOrmAdminRepository.toSlotRow,
    );
  }

  async getSlot(slotId: string): Promise<AdminSlotRow | null> {
    const rows: Row[] = await this.manager.query(
      `SELECT ${TypeOrmAdminRepository.SLOT_SELECT} ${TypeOrmAdminRepository.SLOT_FROM} WHERE s.id = $1`,
      [slotId],
    );
    return rows.length ? TypeOrmAdminRepository.toSlotRow(rows[0]) : null;
  }

  async updateSlotCapacity(slotId: string, capacity: number): Promise<boolean> {
    // Condicional y atómico: nunca deja la capacidad por debajo de lo ya reservado, aunque haya reservas concurrentes.
    const rows = returned(await this.manager.query(
      `UPDATE attraction_availability SET capacity = $2, version = version + 1
        WHERE id = $1 AND reserved_quantity <= $2 RETURNING id`,
      [slotId, capacity],
    ));
    return rows.length === 1;
  }

  async addSlot(attractionId: string, date: IsoDate, time: LocalTime, capacity: number): Promise<string | null> {
    const rows: Row[] = await this.manager.query(
      `INSERT INTO attraction_availability (id, attraction_id, date, time, capacity, reserved_quantity, version)
       VALUES ($1, $2, $3, $4, $5, 0, 0)
       ON CONFLICT (attraction_id, date, time) DO NOTHING RETURNING id`,
      [randomUUID(), attractionId, date, time, capacity],
    );
    return rows.length ? String(rows[0].id) : null;
  }

  async listRoles(): Promise<AdminRoleRow[]> {
    const rows: Row[] = await this.manager.query(
      `SELECT r.id, r.name, r.description,
              coalesce((SELECT array_agg(rp.permission ORDER BY rp.permission) FROM role_permissions rp WHERE rp.role_id = r.id), '{}') AS permissions,
              (SELECT count(*)::int FROM user_roles ur WHERE ur.role_id = r.id AND ur.revoked_at IS NULL) AS users
         FROM roles r ORDER BY r.name`,
    );
    return rows.map((r) => ({
      id: String(r.id),
      name: String(r.name),
      description: (r.description as string | null) ?? null,
      permissions: (r.permissions as string[] | null) ?? [],
      users: num(r.users),
    }));
  }

  async setUserStatus(userId: string, status: UserStatus, at: Date): Promise<void> {
    await this.manager.query(`UPDATE users SET status = $2, updated_at = $3 WHERE id = $1`, [userId, status, at]);
  }

  async assignRole(userId: string, roleId: string, assignedByUserId: string | null, reason: string, at: Date): Promise<boolean> {
    const rows: unknown[] = await this.manager.query(
      `INSERT INTO user_roles (id, user_id, role_id, assigned_by_user_id, reason, assigned_at, revoked_at)
       SELECT $1, $2, $3, $4, $5, $6, NULL
        WHERE NOT EXISTS (SELECT 1 FROM user_roles WHERE user_id = $2 AND role_id = $3 AND revoked_at IS NULL)
       RETURNING id`,
      [randomUUID(), userId, roleId, assignedByUserId, reason, at],
    );
    return rows.length === 1;
  }

  async revokeRole(userId: string, roleId: string, at: Date): Promise<boolean> {
    const rows = returned(await this.manager.query(
      `UPDATE user_roles SET revoked_at = $3 WHERE user_id = $1 AND role_id = $2 AND revoked_at IS NULL RETURNING id`,
      [userId, roleId, at],
    ));
    return rows.length > 0;
  }

  async salesReport(fromDate: IsoDate, toDate: IsoDate): Promise<AdminSalesReport> {
    const range = [fromDate, toDate];
    const sold = `o.status IN ${SOLD_ORDER_STATUSES} AND o.created_at >= $1::date AND o.created_at < $2::date + 1`;
    const [byAttraction, byDay, reservations, currency] = await Promise.all([
      this.manager.query(
        `SELECT oi.attraction_id, coalesce(a.name, '(atracción eliminada)') AS attraction_name,
                count(DISTINCT o.id)::int AS orders, sum(oi.quantity)::int AS tickets,
                sum(oi.quantity * oi.unit_price_amount)::float8 AS revenue
           FROM orders o JOIN order_items oi ON oi.order_id = o.id LEFT JOIN attractions a ON a.id = oi.attraction_id
          WHERE ${sold}
          GROUP BY oi.attraction_id, a.name ORDER BY revenue DESC`,
        range,
      ) as Promise<Row[]>,
      this.manager.query(
        `SELECT to_char(o.created_at::date, 'YYYY-MM-DD') AS date, count(DISTINCT o.id)::int AS orders,
                coalesce(sum(oi.quantity), 0)::int AS tickets, coalesce(sum(oi.quantity * oi.unit_price_amount), 0)::float8 AS revenue
           FROM orders o JOIN order_items oi ON oi.order_id = o.id
          WHERE ${sold}
          GROUP BY o.created_at::date ORDER BY o.created_at::date`,
        range,
      ) as Promise<Row[]>,
      this.manager.query(
        `SELECT r.attraction_id, coalesce(a.name, '(atracción eliminada)') AS attraction_name,
                count(*)::int AS reservations, sum(r.ticket_count)::int AS tickets
           FROM reservations r LEFT JOIN attractions a ON a.id = r.attraction_id
          WHERE r.status = 'CONFIRMED' AND r.date BETWEEN $1::date AND $2::date
          GROUP BY r.attraction_id, a.name ORDER BY reservations DESC`,
        range,
      ) as Promise<Row[]>,
      this.manager.query(`SELECT currency FROM orders GROUP BY currency ORDER BY count(*) DESC LIMIT 1`) as Promise<Row[]>,
    ]);
    return {
      currency: currency.length ? String(currency[0].currency) : 'USD',
      byAttraction: byAttraction.map((r) => ({
        attractionId: String(r.attraction_id),
        attractionName: String(r.attraction_name),
        orders: num(r.orders),
        tickets: num(r.tickets),
        revenue: num(r.revenue),
      })),
      byDay: byDay.map((r) => ({ date: String(r.date), orders: num(r.orders), tickets: num(r.tickets), revenue: num(r.revenue) })),
      reservationsByAttraction: reservations.map((r) => ({
        attractionId: String(r.attraction_id),
        attractionName: String(r.attraction_name),
        reservations: num(r.reservations),
        tickets: num(r.tickets),
      })),
    };
  }
}
