import type { Attraction, Customer, Money, PaymentMethod, PaymentStatus, User } from '@atracciones/domain';
import type { UnitOfWork, UnitOfWorkFactory } from '@atracciones/data-management';
import { ForbiddenError } from '../errors';
import type { AuthenticatedUser } from './auth';

/** Usuario y cliente locales resueltos desde la identidad validada. */
export interface CustomerContext {
  user: User;
  customer: Customer;
}

/** Resuelve el cliente propietario a partir de `issuer + subject`. Nunca usa identificadores enviados por el cliente. */
export class CustomerResolver {
  /** Devuelve `null` si el perfil no está aprovisionado; lanza 403 si el usuario está inactivo. */
  async find(uow: UnitOfWork, identity: AuthenticatedUser): Promise<CustomerContext | null> {
    const user = await uow.users.getByIdentity(identity.issuer, identity.subject);
    if (!user) return null;
    if (!user.isActive) {
      throw new ForbiddenError('El perfil del usuario no está activo.', ForbiddenError.USER_INACTIVE);
    }
    const customer = await uow.customers.getByUserId(user.id);
    return customer ? { user, customer } : null;
  }

  /** Exige un perfil aprovisionado para operaciones transaccionales. */
  async require(uow: UnitOfWork, identity: AuthenticatedUser): Promise<CustomerContext> {
    const context = await this.find(uow, identity);
    if (!context) {
      throw new ForbiddenError(
        'El usuario autenticado no tiene un perfil de cliente aprovisionado. Debe registrarse antes de operar.',
        ForbiddenError.PROFILE_NOT_REGISTERED,
      );
    }
    return context;
  }
}

/** Calcula precios en el servidor; los precios enviados por el cliente nunca se usan. */
export const PriceCalculator = {
  unitPrice: (attraction: Attraction): Money => attraction.details.price,
  total: (attraction: Attraction, quantity: number): Money => attraction.details.price.multiply(quantity),
};

/** Permisos locales efectivos; un usuario inactivo o sin perfil no tiene permisos. */
export class LocalPermissionService {
  constructor(private readonly units: UnitOfWorkFactory) {}

  async hasPermission(user: AuthenticatedUser, permission: string): Promise<boolean> {
    const uow = this.units.create();
    const local = await uow.users.getByIdentity(user.issuer, user.subject);
    if (!local?.isActive) return false;
    return (await uow.userRoles.getEffectivePermissions(local.id)).has(permission);
  }

  /** Defensa en profundidad tras la API. */
  async ensure(user: AuthenticatedUser, permission: string): Promise<void> {
    if (!(await this.hasPermission(user, permission))) {
      throw new ForbiddenError(`El usuario no tiene el permiso local '${permission}'.`);
    }
  }
}

/** Resultado determinista de un intento de pago simulado. */
export interface PaymentSimulationOutcome {
  status: PaymentStatus;
  responseCode: string;
  responseMessage: string;
}

/** Decide el resultado de la pasarela simulada. No llama a proveedores externos. */
export interface PaymentSimulationPolicy {
  evaluate(amount: Money, method: PaymentMethod, attemptNumber: number): PaymentSimulationOutcome;
}

/**
 * Política determinista basada en los céntimos del importe (al estilo de las tarjetas de prueba):
 * `.51` → REJECTED (fondos insuficientes), `.52` → FAILED (error de pasarela), cualquier otro → AUTHORIZED.
 */
export const deterministicPaymentPolicy: PaymentSimulationPolicy = {
  evaluate(amount) {
    const cents = Math.round(amount.amount * 100) % 100;
    if (cents === 51) return { status: 'REJECTED', responseCode: '51', responseMessage: 'Fondos insuficientes (simulado).' };
    if (cents === 52) return { status: 'FAILED', responseCode: '96', responseMessage: 'Error de la pasarela simulada.' };
    return { status: 'AUTHORIZED', responseCode: '00', responseMessage: 'Aprobado (simulado).' };
  },
};
