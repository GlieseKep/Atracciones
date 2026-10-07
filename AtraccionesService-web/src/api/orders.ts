import type { CreateOrderRequest, Order, OrderEventsResponse } from '@/types/order';
import { http } from './client';

export const createOrder = (body: CreateOrderRequest, idempotencyKey: string) =>
  http.post<Order>('/orders', body, { idempotencyKey });

export const getOrder = (orderId: string, signal?: AbortSignal) => http.get<Order>(`/orders/${orderId}`, { signal });

export const getOrderEvents = (orderId: string, signal?: AbortSignal) =>
  http.get<OrderEventsResponse>(`/orders/${orderId}/events`, { signal });

export const cancelOrder = (orderId: string, reason: string, idempotencyKey: string) =>
  http.post<Order>(`/orders/${orderId}/cancel`, { reason }, { idempotencyKey });
