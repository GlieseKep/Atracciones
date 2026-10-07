/** Base de los errores de negocio. `code` es un identificador estable que la API publica en ProblemDetails. */
export class BusinessError extends Error {
  constructor(
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = new.target.name;
  }
}

/** Datos inválidos o faltantes (400). */
export class ValidationError extends BusinessError {
  constructor(
    message: string,
    readonly errors: Record<string, string[]> = {},
  ) {
    super('VALIDATION_ERROR', message);
  }
}

/** Recurso inexistente o no visible para el usuario (404). */
export class NotFoundError extends BusinessError {
  constructor(message: string) {
    super('NOT_FOUND', message);
  }
}

/** Identidad ausente o inválida (401). */
export class UnauthorizedError extends BusinessError {
  constructor(message: string) {
    super('UNAUTHORIZED', message);
  }
}

/** Usuario autenticado sin permiso sobre la operación o recurso (403). */
export class ForbiddenError extends BusinessError {
  static readonly PROFILE_NOT_REGISTERED = 'PROFILE_NOT_REGISTERED';
  static readonly USER_INACTIVE = 'USER_INACTIVE';

  constructor(message: string, code = 'FORBIDDEN') {
    super(code, message);
  }
}

/** Estado, disponibilidad u operación incompatible (409). */
export class ConflictError extends BusinessError {
  static readonly IDEMPOTENCY_KEY_REUSED = 'IDEMPOTENCY_KEY_REUSED';
  static readonly IDEMPOTENCY_IN_PROGRESS = 'IDEMPOTENCY_IN_PROGRESS';
  static readonly INSUFFICIENT_AVAILABILITY = 'INSUFFICIENT_AVAILABILITY';
  static readonly INVALID_STATE_TRANSITION = 'INVALID_STATE_TRANSITION';
  static readonly CONCURRENCY_CONFLICT = 'CONCURRENCY_CONFLICT';
  static readonly SLOT_UNAVAILABLE = 'SLOT_UNAVAILABLE';
}

/** Pago simulado rechazado o inválido (422). */
export class PaymentSimulationError extends BusinessError {}
