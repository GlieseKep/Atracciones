import type { IsoDate, IsoDateTime, PagedResponse } from '@/types/api';
import { createAttraction, deleteAttraction, patchAttraction, replaceAttraction } from './attractions';
import { http } from './client';

/**
 * Endpoints administrativos (`/admin`): scope `attractions:write` + permiso local `admin:manage` (rol `admin`).
 * El catálogo usa los endpoints de `/atracciones` con el permiso `catalog:write`.
 */
export const catalogAdmin = {
  create: createAttraction,
  replace: replaceAttraction,
  patch: patchAttraction,
  remove: deleteAttraction,
};

export const ADMIN_SCOPE = 'attractions:write';

export interface Money {
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
  revenue: Money[];
  upcoming: { slots: number; capacity: number; reserved: number };
}

export type UserStatus = 'ACTIVE' | 'LOCKED' | 'DISABLED';

export interface AdminCustomer {
  userId: string;
  customerId: string | null;
  email: string;
  name: string | null;
  status: UserStatus;
  billingName: string | null;
  billingEmail: string | null;
  billingAddress: string | null;
  taxId: string | null;
  createdAt: IsoDateTime;
  reservations: number;
  orders: number;
  totalSpent: number;
  lastActivityAt: IsoDateTime | null;
  roles: string[];
}

export interface AdminReservation {
  id: string;
  attractionId: string;
  attractionName: string;
  customerName: string;
  customerEmail: string;
  date: IsoDate;
  time: string;
  ticketCount: number;
  total: Money;
  status: 'PENDING' | 'CONFIRMED' | 'CANCELLED';
  cancellationReason: string | null;
  createdAt: IsoDateTime;
}

export interface AdminOrder {
  id: string;
  customerEmail: string;
  attractionName: string | null;
  serviceDate: IsoDate | null;
  serviceTime: string | null;
  quantity: number;
  source: 'PURCHASE' | 'RESERVATION';
  status: 'PENDING_PAYMENT' | 'PAID' | 'FULFILLED' | 'CANCELLED' | 'PARTIALLY_REFUNDED' | 'REFUNDED';
  total: Money;
  paymentStatus: AdminPayment['status'] | null;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

export interface AdminPayment {
  id: string;
  orderId: string;
  customerEmail: string;
  paymentMethod: 'CARD' | 'BANK_TRANSFER';
  status: 'PENDING' | 'AUTHORIZED' | 'SETTLED' | 'REJECTED' | 'FAILED' | 'CANCELLED' | 'PARTIALLY_REFUNDED' | 'REFUNDED';
  amount: Money;
  gatewayReference: string;
  attempts: number;
  failureReason: string | null;
  createdAt: IsoDateTime;
  processedAt: IsoDateTime | null;
}

export interface AdminSlot {
  id: string;
  attractionId: string;
  attractionName: string;
  date: IsoDate;
  time: string;
  capacity: number;
  reserved: number;
}

export interface AdminRole {
  id: string;
  name: string;
  description: string | null;
  permissions: string[];
  users: number;
}

export interface SalesReport {
  fromDate: IsoDate;
  toDate: IsoDate;
  currency: string;
  byAttraction: { attractionId: string; attractionName: string; orders: number; tickets: number; revenue: number }[];
  byDay: { date: IsoDate; orders: number; tickets: number; revenue: number }[];
  reservationsByAttraction: { attractionId: string; attractionName: string; reservations: number; tickets: number }[];
}

export interface AdminListParams {
  limit?: number;
  offset?: number;
  search?: string;
  status?: string;
  fromDate?: IsoDate;
  toDate?: IsoDate;
  attractionId?: string;
}

const list = <T>(path: string) => (params: AdminListParams, signal?: AbortSignal) =>
  http.get<PagedResponse<T>>(path, { query: { ...params }, signal });

export const adminApi = {
  summary: (signal?: AbortSignal) => http.get<AdminSummary>('/admin/summary', { signal }),
  customers: list<AdminCustomer>('/admin/customers'),
  reservations: list<AdminReservation>('/admin/reservations'),
  orders: list<AdminOrder>('/admin/orders'),
  payments: list<AdminPayment>('/admin/payments'),
  availability: list<AdminSlot>('/admin/availability'),
  roles: (signal?: AbortSignal) => http.get<AdminRole[]>('/admin/roles', { signal }),
  salesReport: (fromDate: IsoDate, toDate: IsoDate, signal?: AbortSignal) =>
    http.get<SalesReport>('/admin/reports/sales', { query: { fromDate, toDate }, signal }),
  setUserStatus: (userId: string, status: UserStatus, reason?: string) =>
    http.put<AdminCustomer>(`/admin/customers/${userId}/status`, { status, reason }),
  assignRole: (userId: string, roleId: string, reason?: string) =>
    http.post<AdminCustomer>(`/admin/customers/${userId}/roles`, { roleId, reason }),
  revokeRole: (userId: string, roleId: string) => http.delete<AdminCustomer>(`/admin/customers/${userId}/roles/${roleId}`),
  updateCapacity: (slotId: string, capacity: number) => http.patch<AdminSlot>(`/admin/availability/${slotId}`, { capacity }),
  addSlot: (body: { attractionId: string; date: IsoDate; time: string; capacity: number }) =>
    http.post<AdminSlot>('/admin/availability', body),
};
