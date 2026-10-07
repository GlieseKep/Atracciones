import { Customer, User } from '@atracciones/domain';
import type { UnitOfWork, UnitOfWorkFactory } from '@atracciones/data-management';
import { ConflictError, NotFoundError } from '../errors';
import type { BillingData, CustomerResult, RegisteredUserResult, RegisterUserCommand, UserResult } from '../models';
import type { AuthenticatedUser } from '../shared/auth';
import type { BusinessClock } from '../shared/clock';
import type { CustomerResolver } from '../shared/customers';
import type { TransactionRunner } from '../shared/transactions';
import { isEmail, validateBilling, ValidationErrors } from '../shared/validation';

/** Aprovisiona el perfil local a partir de claims verificados. No almacena contraseñas, tokens ni credenciales. */
export class UserProfileService {
  constructor(
    private readonly units: UnitOfWorkFactory,
    private readonly transactions: TransactionRunner,
    private readonly clock: BusinessClock,
  ) {}

  async register(command: RegisterUserCommand): Promise<RegisteredUserResult> {
    new ValidationErrors()
      .when(!isEmail(command.email), 'email', 'El claim email del token no es un correo válido.')
      .when(command.user.emailVerified === false, 'email', 'El claim email del token no está verificado.')
      .throwIfAny();
    validateBilling(command.billing, false);
    try {
      return await this.transactions.execute(async (uow) => {
        const existing = await this.findExisting(uow, command.user);
        if (existing) return existing;
        const now = this.clock.utcNow;
        const user = User.register(command.user.issuer, command.user.subject, command.email, now);
        const customer = Customer.createFor(user, now);
        customer.updateBilling(command.billing, now);
        // Usuario y cliente se crean en la misma transacción: no quedan registros parciales.
        await uow.users.add(user);
        await uow.customers.add(customer);
        return { id: user.id, email: user.email, status: user.status, customerId: customer.id, createdAt: user.createdAt, created: true };
      });
    } catch (error) {
      // Registro concurrente del mismo sub: la unicidad issuer + subject garantiza un único perfil.
      if (error instanceof ConflictError && error.code === ConflictError.CONCURRENCY_CONFLICT) {
        const existing = await this.findExisting(this.units.create(), command.user);
        if (existing) return existing;
      }
      throw error;
    }
  }

  async getCurrent(identity: AuthenticatedUser): Promise<UserResult> {
    const user = await this.units.create().users.getByIdentity(identity.issuer, identity.subject);
    if (!user) throw new NotFoundError('El perfil local no existe.');
    return { id: user.id, email: user.email, status: user.status, createdAt: user.createdAt, updatedAt: user.updatedAt };
  }

  private async findExisting(uow: UnitOfWork, identity: AuthenticatedUser): Promise<RegisteredUserResult | null> {
    const user = await uow.users.getByIdentity(identity.issuer, identity.subject);
    if (!user) return null;
    const customer = await uow.customers.getByUserId(user.id);
    if (!customer) throw new ConflictError('PROFILE_INCOMPLETE', 'El perfil existe sin cliente asociado.');
    return { id: user.id, email: user.email, status: user.status, customerId: customer.id, createdAt: user.createdAt, created: false };
  }
}

const customerNotFound = () => new NotFoundError('El cliente no existe. Debe aprovisionar su perfil.');

const toCustomerResult = (c: Customer): CustomerResult => ({
  id: c.id,
  userId: c.userId,
  billingName: c.billingName,
  billingEmail: c.billingEmail,
  billingAddress: c.billingAddress,
  taxId: c.taxId,
  paymentMethodReference: c.paymentMethodReference,
  createdAt: c.createdAt,
  updatedAt: c.updatedAt,
});

/** Cliente y facturación del usuario autenticado. */
export class CustomerService {
  constructor(
    private readonly units: UnitOfWorkFactory,
    private readonly transactions: TransactionRunner,
    private readonly customers: CustomerResolver,
    private readonly clock: BusinessClock,
  ) {}

  async getCurrent(user: AuthenticatedUser): Promise<CustomerResult> {
    const context = await this.customers.find(this.units.create(), user);
    if (!context) throw customerNotFound();
    return toCustomerResult(context.customer);
  }

  /** Reemplaza los datos de facturación; no modifica usuario, email ni identidad. */
  async updateCurrent(user: AuthenticatedUser, billing: BillingData): Promise<CustomerResult> {
    validateBilling(billing, true);
    return this.transactions.execute(async (uow) => {
      const context = await this.customers.find(uow, user);
      if (!context) throw customerNotFound();
      context.customer.updateBilling(billing, this.clock.utcNow);
      await uow.customers.update(context.customer);
      return toCustomerResult(context.customer);
    });
  }
}
