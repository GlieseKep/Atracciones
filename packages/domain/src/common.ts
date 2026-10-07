import { randomUUID } from 'node:crypto';

/** Fecha local `YYYY-MM-DD` (equivale a DateOnly). */
export type IsoDate = string;
/** Hora local `HH:mm` (equivale a TimeOnly). */
export type LocalTime = string;

/**
 * Violación de un invariante de dominio. Business valida antes de invocar al dominio, por lo que esta excepción
 * actúa como última defensa.
 */
export class DomainError extends Error {
  constructor(
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = 'DomainError';
  }
}

export const newId = (): string => randomUUID();

export const Guard = {
  notBlank(value: string | null | undefined, field: string, maxLength: number): string {
    if (value === null || value === undefined || value.trim() === '') {
      throw new DomainError('REQUIRED', `${field} es obligatorio.`);
    }
    const trimmed = value.trim();
    if (trimmed.length > maxLength) {
      throw new DomainError('TOO_LONG', `${field} admite como máximo ${maxLength} caracteres.`);
    }
    return trimmed;
  },
  optional(value: string | null | undefined, field: string, maxLength: number): string | null {
    return value === null || value === undefined || value.trim() === '' ? null : Guard.notBlank(value, field, maxLength);
  },
  positive(value: number, field: string): number {
    if (!Number.isInteger(value) || value <= 0) {
      throw new DomainError('NOT_POSITIVE', `${field} debe ser mayor que cero.`);
    }
    return value;
  },
};

const CURRENCY = /^[A-Z]{3}$/;

/** Redondeo a céntimos que evita errores binarios de coma flotante (0.1 + 0.2). */
export const roundCents = (value: number): number => Math.round((value + Number.EPSILON) * 100) / 100;

export const hasAtMostTwoDecimals = (value: number): boolean =>
  Number.isFinite(value) && Math.abs(roundCents(value) - value) < 1e-9;

/** Importe positivo con moneda ISO 4217 y máximo dos decimales. */
export class Money {
  readonly currency: string;
  readonly amount: number;

  constructor(currency: string, amount: number) {
    if (!Money.isValidCurrency(currency)) {
      throw new DomainError('INVALID_CURRENCY', 'La moneda debe ser un código ISO 4217.');
    }
    if (!(amount > 0) || !hasAtMostTwoDecimals(amount)) {
      throw new DomainError('INVALID_AMOUNT', 'El importe debe ser positivo y tener como máximo dos decimales.');
    }
    this.currency = currency;
    this.amount = roundCents(amount);
  }

  multiply(quantity: number): Money {
    return new Money(this.currency, roundCents(this.amount * Guard.positive(quantity, 'quantity')));
  }

  equals(other: Money): boolean {
    return this.currency === other.currency && this.amount === other.amount;
  }

  static isValidCurrency(currency: string | null | undefined): currency is string {
    return typeof currency === 'string' && CURRENCY.test(currency);
  }
}
