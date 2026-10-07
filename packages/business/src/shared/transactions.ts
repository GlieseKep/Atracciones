import { createHash } from 'node:crypto';
import { DomainError } from '@atracciones/domain';
import { ConcurrencyError, type UnitOfWork, type UnitOfWorkFactory } from '@atracciones/data-management';
import { BusinessError, ConflictError, ValidationError } from '../errors';
import type { AuthenticatedUser } from './auth';
import type { BusinessOptions } from './options';

/**
 * Ejecuta una mutación dentro de una transacción: confirma al terminar o revierte ante cualquier error.
 * Traduce errores de dominio y de concurrencia a errores de negocio.
 */
export class TransactionRunner {
  constructor(private readonly units: UnitOfWorkFactory) {}

  async execute<T>(work: (uow: UnitOfWork) => Promise<T>): Promise<T> {
    try {
      return await this.units.transaction(work);
    } catch (error) {
      throw translate(error);
    }
  }
}

function translate(error: unknown): unknown {
  if (error instanceof BusinessError) return error;
  if (error instanceof DomainError) {
    if (error.code === 'INVALID_STATE_TRANSITION') return new ConflictError(ConflictError.INVALID_STATE_TRANSITION, error.message);
    if (error.code === 'INSUFFICIENT_AVAILABILITY') return new ConflictError(ConflictError.INSUFFICIENT_AVAILABILITY, error.message);
    return new ValidationError(error.message);
  }
  if (error instanceof ConcurrencyError) {
    return new ConflictError(ConflictError.CONCURRENCY_CONFLICT, 'El recurso fue modificado por otra operación. Reintente.');
  }
  return error;
}

/** Nombres de operación usados en la identidad de idempotencia. */
export const IdempotentOperations = {
  CreateAttraction: 'create-attraction',
  ReplaceAttraction: 'replace-attraction',
  PatchAttraction: 'patch-attraction',
  DeleteAttraction: 'delete-attraction',
  CreateReservation: 'create-reservation',
  CancelReservation: 'cancel-reservation',
  CreatePurchase: 'create-purchase',
  CreateOrder: 'create-order',
  CancelOrder: 'cancel-order',
  SimulatePayment: 'simulate-payment',
} as const;

/** JSON con claves ordenadas: dos payloads equivalentes producen el mismo hash. */
export function canonicalJson(value: unknown): string {
  if (value === null || typeof value !== 'object') return JSON.stringify(value) ?? 'null';
  if (value instanceof Date) return JSON.stringify(value.toISOString());
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(',')}]`;
  const entries = Object.entries(value as Record<string, unknown>)
    .filter(([, v]) => v !== undefined)
    .sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
  return `{${entries.map(([k, v]) => `${JSON.stringify(k)}:${canonicalJson(v)}`).join(',')}}`;
}

const ISO_INSTANT = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$/;

/** Restaura los instantes (`Date`) de una respuesta guardada para que el replay sea idéntico al original. */
const reviveDates = (_key: string, value: unknown): unknown =>
  typeof value === 'string' && ISO_INSTANT.test(value) ? new Date(value) : value;

/** Ejecuta una mutación una sola vez por identidad `issuer + subject + operation + key`. */
export class IdempotencyService {
  constructor(
    private readonly transactions: TransactionRunner,
    private readonly options: BusinessOptions,
    private readonly now: () => Date = () => new Date(),
  ) {}

  /**
   * Primera solicitud: ejecuta `action` en la transacción y guarda su resultado.
   * Repetición con el mismo payload: devuelve el resultado original sin repetir efectos.
   * Misma clave con otro payload: 409 IDEMPOTENCY_KEY_REUSED; repetición aún en proceso: 409 IDEMPOTENCY_IN_PROGRESS.
   */
  execute<T>(
    user: AuthenticatedUser,
    operation: string,
    key: string,
    payload: unknown,
    action: (uow: UnitOfWork) => Promise<T>,
  ): Promise<T> {
    const hash = IdempotencyService.hash(payload);
    return this.transactions.execute(async (uow) => {
      const now = this.now();
      const existing = await uow.idempotency.getByIdentity(user.issuer, user.subject, operation, key);
      if (existing && existing.expiresAt.getTime() > now.getTime()) {
        if (existing.requestHash !== hash) {
          throw new ConflictError(
            ConflictError.IDEMPOTENCY_KEY_REUSED,
            'La Idempotency-Key ya se utilizó con un payload distinto para esta operación.',
          );
        }
        if (existing.status === 'IN_PROGRESS' || existing.responseBody === null) {
          throw new ConflictError(ConflictError.IDEMPOTENCY_IN_PROGRESS, 'Una solicitud con la misma Idempotency-Key todavía se está procesando.');
        }
        return JSON.parse(existing.responseBody, reviveDates) as T;
      }
      if (existing) {
        // Clave expirada: solo se reutiliza después de limpiarla.
        await uow.idempotency.removeExpired(now);
      }
      const record = {
        issuer: user.issuer,
        subject: user.subject,
        operation,
        key,
        requestHash: hash,
        status: 'IN_PROGRESS' as const,
        resourceId: null,
        responseBody: null,
        createdAt: now,
        expiresAt: new Date(now.getTime() + this.options.idempotencyRetentionHours * 3_600_000),
      };
      if (!(await uow.idempotency.tryCreateInProgress(record))) {
        throw new ConflictError(ConflictError.IDEMPOTENCY_IN_PROGRESS, 'Una solicitud con la misma Idempotency-Key todavía se está procesando.');
      }
      const result = await action(uow);
      await uow.idempotency.complete({ ...record, status: 'COMPLETED', responseBody: JSON.stringify(result ?? null) });
      return result;
    });
  }

  /** SHA-256 del JSON canónico del payload (sin identidad ni clave). */
  static hash(payload: unknown): string {
    return createHash('sha256').update(canonicalJson(payload)).digest('hex').toUpperCase();
  }
}
