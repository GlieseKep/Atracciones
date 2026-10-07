import type { IsoDate, LocalTime, UserStatus } from '@atracciones/domain';
import type { PagedResult, PaginationRequest } from './pagination';

/**
 * Modelos de lectura del área administrativa. Son proyecciones planas (no agregados): combinan varias tablas para
 * listados y reportes y no se usan para modificar el dominio.
 */
export interface AdminMoney {
  currency: string;
  amount: number;
}

export interface AdminSummary {
  attractions: number;
  customers: number;
  activeUsers: number;
  reservationsByStatus: Record<string, number>;
  ordersByStatus: Record<string, number>;
  paymentsByStatus: Record<string, number>;
  /** Ingresos de pedidos pagados o completados, por moneda. */
  revenue: AdminMoney[];
  /** Ocupación de las franjas desde `from` durante los próximos días. */
  upcoming: { slots: number; capacity: number; reserved: number };
}

/** Filtros comunes de los listados. Los campos que un listado no admite se ignoran. */
export interface AdminListFilter {
  search: string | null;
  status: string | null;
  fromDate: IsoDate | null;
  toDate: IsoDate | null;
  attractionId: string | null;
}

export interface AdminUserRow {
  userId: string;
  customerId: string | null;
  email: string;
  name: string | null;
  status: UserStatus;
  billingName: string | null;
  billingEmail: string | null;
  billingAddress: string | null;
  taxId: string | null;
  createdAt: Date;
  reservations: number;
  orders: number;
  totalSpent: number;
  lastActivityAt: Date | null;
  roles: string[];
}

export interface AdminReservationRow {
  id: string;
  attractionId: string;
  attractionName: string;
  customerName: string;
  customerEmail: string;
  date: IsoDate;
  time: LocalTime;
  ticketCount: number;
  total: AdminMoney;
  status: string;
  cancellationReason: string | null;
  createdAt: Date;
  /** Pedido de la compra que originó la reserva; `null` si se reservó sin pagar. */
  orderId: string | null;
  orderStatus: string | null;
}

export interface AdminOrderRow {
  id: string;
  customerEmail: string;
  attractionName: string | null;
  serviceDate: IsoDate | null;
  serviceTime: LocalTime | null;
  quantity: number;
  status: string;
  total: AdminMoney;
  /** Importe ya reembolsado (0 si no hubo reembolsos). */
  refunded: number;
  paymentStatus: string | null;
  /** Fin de la retención de cupos mientras el pedido espera el pago. */
  holdExpiresAt: Date | null;
  reservationId: string | null;
  createdAt: Date;
  updatedAt: Date;
}

export interface AdminPaymentDetail extends Omit<AdminPaymentRow, 'orderId' | 'customerEmail'> {
  refunded: number;
  attemptsDetail: { attemptNumber: number; status: string; responseCode: string; responseMessage: string; createdAt: Date }[];
  events: { eventType: string; createdAt: Date; amount: number | null; reason: string | null }[];
}

export interface AdminOrderDetail extends AdminOrderRow {
  customerName: string | null;
  cancellationReason: string | null;
  reservationStatus: string | null;
  items: {
    attractionId: string;
    attractionName: string;
    serviceDate: IsoDate;
    serviceTime: LocalTime;
    quantity: number;
    unitPrice: AdminMoney;
  }[];
  events: { eventType: string; previousStatus: string | null; newStatus: string; createdAt: Date }[];
  payments: AdminPaymentDetail[];
}

export interface AdminPaymentRow {
  id: string;
  orderId: string;
  customerEmail: string;
  paymentMethod: string;
  status: string;
  amount: AdminMoney;
  gatewayReference: string;
  attempts: number;
  failureReason: string | null;
  createdAt: Date;
  processedAt: Date | null;
}

export interface AdminSlotRow {
  id: string;
  attractionId: string;
  attractionName: string;
  date: IsoDate;
  time: LocalTime;
  capacity: number;
  reserved: number;
}

export interface AdminRoleRow {
  id: string;
  name: string;
  description: string | null;
  permissions: string[];
  users: number;
}

export interface AdminSalesByAttraction {
  attractionId: string;
  attractionName: string;
  orders: number;
  tickets: number;
  revenue: number;
}

export interface AdminSalesByDay {
  date: IsoDate;
  orders: number;
  tickets: number;
  revenue: number;
}

export interface AdminSalesReport {
  currency: string;
  byAttraction: AdminSalesByAttraction[];
  byDay: AdminSalesByDay[];
  reservationsByAttraction: { attractionId: string; attractionName: string; reservations: number; tickets: number }[];
}

/** Consultas y cambios administrativos. Las reglas (quién, qué valores) las aplica Business. */
export interface AdminRepository {
  getSummary(from: IsoDate, days: number): Promise<AdminSummary>;
  listUsers(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminUserRow>>;
  getUser(userId: string): Promise<AdminUserRow | null>;
  listReservations(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminReservationRow>>;
  listOrders(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminOrderRow>>;
  getOrderDetail(orderId: string): Promise<AdminOrderDetail | null>;
  /** Suma de los reembolsos registrados para una simulación de pago. */
  getRefundedAmount(paymentId: string): Promise<number>;
  listPayments(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminPaymentRow>>;
  listSlots(filter: AdminListFilter, page: PaginationRequest): Promise<PagedResult<AdminSlotRow>>;
  getSlot(slotId: string): Promise<AdminSlotRow | null>;
  /** Cambia la capacidad solo si no queda por debajo de los cupos ya reservados. Devuelve `false` si no se aplicó. */
  updateSlotCapacity(slotId: string, capacity: number): Promise<boolean>;
  /** Crea una franja; devuelve `null` si ya existe una para la misma atracción, fecha y hora. */
  addSlot(attractionId: string, date: IsoDate, time: LocalTime, capacity: number): Promise<string | null>;
  listRoles(): Promise<AdminRoleRow[]>;
  setUserStatus(userId: string, status: UserStatus, at: Date): Promise<void>;
  /** Asigna el rol si no lo tiene vigente. Devuelve `false` si ya estaba asignado. */
  assignRole(userId: string, roleId: string, assignedByUserId: string | null, reason: string, at: Date): Promise<boolean>;
  /** Revoca la asignación vigente (conserva el registro). Devuelve `false` si no había ninguna. */
  revokeRole(userId: string, roleId: string, at: Date): Promise<boolean>;
  salesReport(fromDate: IsoDate, toDate: IsoDate): Promise<AdminSalesReport>;
}
