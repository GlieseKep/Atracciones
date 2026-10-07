import { Guard, newId } from './common';

export const USER_STATUSES = ['ACTIVE', 'LOCKED', 'DISABLED'] as const;
export type UserStatus = (typeof USER_STATUSES)[number];

/**
 * Perfil local vinculado a una identidad del emisor (`oauthIssuer + oauthSubject`, únicos).
 * No almacena contraseñas ni tokens.
 */
export class User {
  readonly oauthIssuer: string;
  readonly oauthSubject: string;
  readonly email: string;

  private constructor(
    readonly id: string,
    issuer: string,
    subject: string,
    email: string,
    readonly status: UserStatus,
    readonly createdAt: Date,
    readonly updatedAt: Date,
  ) {
    this.oauthIssuer = Guard.notBlank(issuer, 'issuer', 300);
    this.oauthSubject = Guard.notBlank(subject, 'subject', 300);
    this.email = Guard.notBlank(email, 'email', 254);
  }

  get isActive(): boolean {
    return this.status === 'ACTIVE';
  }

  static register(issuer: string, subject: string, email: string, now: Date): User {
    return new User(newId(), issuer, subject, email, 'ACTIVE', now, now);
  }

  static restore(id: string, issuer: string, subject: string, email: string, status: UserStatus, createdAt: Date, updatedAt: Date): User {
    return new User(id, issuer, subject, email, status, createdAt, updatedAt);
  }
}

export interface BillingValues {
  billingName?: string | null;
  billingEmail?: string | null;
  billingAddress?: string | null;
  taxId?: string | null;
  paymentMethodReference?: string | null;
}

/** Datos de facturación del cliente asociado a un usuario (relación 1:1). */
export class Customer {
  billingName: string | null = null;
  billingEmail: string | null = null;
  billingAddress: string | null = null;
  taxId: string | null = null;
  /** Referencia local de la simulación de pago; nunca un dato financiero real. */
  paymentMethodReference: string | null = null;
  private _updatedAt: Date;

  private constructor(
    readonly id: string,
    readonly userId: string,
    readonly createdAt: Date,
  ) {
    this._updatedAt = createdAt;
  }

  get updatedAt(): Date {
    return this._updatedAt;
  }

  static createFor(user: User, now: Date): Customer {
    return new Customer(newId(), user.id, now);
  }

  static restore(id: string, userId: string, billing: BillingValues, createdAt: Date, updatedAt: Date): Customer {
    const customer = new Customer(id, userId, createdAt);
    customer.apply(billing);
    customer._updatedAt = updatedAt;
    return customer;
  }

  updateBilling(billing: BillingValues, now: Date): void {
    this.apply(billing);
    this._updatedAt = now;
  }

  private apply(billing: BillingValues): void {
    this.billingName = Guard.optional(billing.billingName, 'billingName', 200);
    this.billingEmail = Guard.optional(billing.billingEmail, 'billingEmail', 254);
    this.billingAddress = Guard.optional(billing.billingAddress, 'billingAddress', 300);
    this.taxId = Guard.optional(billing.taxId, 'taxId', 30);
    this.paymentMethodReference = Guard.optional(billing.paymentMethodReference, 'paymentMethodReference', 64);
  }
}
