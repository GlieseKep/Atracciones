import type { PagedResponse } from '@/types/api';
import type { Reservation, ReservationListParams, ReservationRequest } from '@/types/reservation';
import { http } from './client';

export const createReservation = (attractionId: string, body: ReservationRequest, idempotencyKey: string) =>
  http.post<Reservation>(`/atracciones/${attractionId}/reservations`, body, { idempotencyKey });

export const cancelReservation = (reservationId: string, reason: string, idempotencyKey: string) =>
  http.post<Reservation>(`/atracciones/reservations/${reservationId}/cancel`, { reason }, { idempotencyKey });

export const listReservations = (params: ReservationListParams = {}, signal?: AbortSignal) =>
  http.get<PagedResponse<Reservation>>('/atracciones/reservations', { query: { ...params }, signal });

export const getReservation = (reservationId: string, signal?: AbortSignal) =>
  http.get<Reservation>(`/atracciones/reservations/${reservationId}`, { signal });
