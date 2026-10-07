import { hasAtMostTwoDecimals, Money, PAYMENT_METHODS, PRODUCT_TYPES, type PaymentMethod } from '@atracciones/domain';
import { ValidationError } from '../errors';
import type { AttractionData, BillingData } from '../models';
import type { BusinessClock } from './clock';
import type { BusinessOptions } from './options';

export const MAX_PAGE_LIMIT = 100;

const SLOT_TIME = /^([01]\d|2[0-3]):[0-5]\d$/;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const ISO_DURATION = /^P(?!$)(\d+D)?(T(?=\d)(\d+H)?(\d+M)?)?$/;
const LANGUAGE_TAG = /^[a-z]{2}(-[A-Z]{2})?$/;
const COUNTRY_CODE = /^[A-Z]{2}$/;
const PAYMENT_REFERENCE = /^(?!\d+$)[A-Za-z0-9_-]{1,64}$/;
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export const isEmail = (value: string): boolean => value.length <= 254 && EMAIL.test(value);
export const isLanguage = (value: string): boolean => LANGUAGE_TAG.test(value);
export const isCountry = (value: string): boolean => COUNTRY_CODE.test(value);
export const isSlotTime = (value: string | null | undefined): value is string => typeof value === 'string' && SLOT_TIME.test(value);

/** `YYYY-MM-DD` que además existe en el calendario. */
export function isIsoDate(value: string | null | undefined): value is string {
  if (typeof value !== 'string' || !ISO_DATE.test(value)) return false;
  const [y, m, d] = value.split('-').map(Number);
  const date = new Date(Date.UTC(y, m - 1, d));
  return date.getUTCFullYear() === y && date.getUTCMonth() === m - 1 && date.getUTCDate() === d;
}

/** Acumula errores por campo y los lanza juntos como `ValidationError`. */
export class ValidationErrors {
  private readonly errors: Record<string, string[]> = {};

  get hasErrors(): boolean {
    return Object.keys(this.errors).length > 0;
  }

  add(field: string, message: string): this {
    (this.errors[field] ??= []).push(message);
    return this;
  }

  when(condition: boolean, field: string, message: string): this {
    return condition ? this.add(field, message) : this;
  }

  throwIfAny(message = 'La solicitud contiene datos inválidos.'): void {
    if (this.hasErrors) throw new ValidationError(message, this.errors);
  }

  requiredText(value: string | null | undefined, field: string, maxLength: number, minLength = 1): this {
    if (value === null || value === undefined || value.trim() === '') return this.add(field, `${field} es obligatorio.`);
    const length = value.trim().length;
    return this.when(length < minLength || length > maxLength, field, `${field} debe tener entre ${minLength} y ${maxLength} caracteres.`);
  }

  optionalText(value: string | null | undefined, field: string, maxLength: number): this {
    return value === null || value === undefined ? this : this.requiredText(value, field, maxLength);
  }

  email(value: string | null | undefined, field: string, required: boolean): this {
    if (value === null || value === undefined || value.trim() === '') {
      return required ? this.add(field, `${field} es obligatorio.`) : this;
    }
    return this.when(!isEmail(value), field, `${field} no tiene un formato de correo válido.`);
  }
}

/** Reglas de catálogo: nombre, descripción, duración, precio, categorías, ubicaciones, idiomas y tipo de producto. */
export function validateAttraction(data: AttractionData): void {
  const errors = new ValidationErrors()
    .requiredText(data.name, 'name', 200, 3)
    .requiredText(data.longDescription, 'longDescription', 5000)
    .when(!data.duration || !ISO_DURATION.test(data.duration), 'duration', 'duration debe ser una duración ISO 8601, por ejemplo PT3H.')
    .when(!Money.isValidCurrency(data.price.currency), 'price.currency', 'currency debe ser un código ISO 4217.')
    .when(!(data.price.total > 0) || !hasAtMostTwoDecimals(data.price.total), 'price.total', 'total debe ser positivo con un máximo de dos decimales.')
    .when(data.categories.length === 0, 'categories', 'Se requiere al menos una categoría.')
    .when(data.categories.some((c) => !c || c.trim() === '' || c.length > 60), 'categories', 'Las categorías no pueden estar vacías ni superar 60 caracteres.')
    .when(data.locations.length === 0, 'locations', 'Se requiere al menos una ubicación.')
    .when(!PRODUCT_TYPES.includes(data.productType as never), 'productType', 'productType no es un tipo de producto soportado.')
    .when(data.supportedLanguages.some((l) => !isLanguage(l)), 'supportedLanguages', "Los idiomas deben ser códigos ISO 639-1, por ejemplo 'es' o 'es-EC'.")
    .when(data.photoUrls.some((u) => !isHttpUrl(u)), 'photos', 'Las fotos deben ser URLs http(s) absolutas.');
  if (data.operator) {
    errors.when(!(data.operator.id > 0), 'operator.id', 'operator.id debe ser positivo.').requiredText(data.operator.name, 'operator.name', 200);
  }
  data.locations.forEach((location, i) => {
    const prefix = `locations[${i}]`;
    errors
      .requiredText(location.address, `${prefix}.address`, 300)
      .requiredText(location.city, `${prefix}.city`, 120)
      .when(!location.country || !isCountry(location.country), `${prefix}.country`, 'country debe ser ISO 3166-1 alfa-2.')
      .when((location.latitude === null) !== (location.longitude === null), `${prefix}.coordinates`, 'Latitud y longitud deben enviarse juntas.')
      .when(location.latitude !== null && (location.latitude < -90 || location.latitude > 90), `${prefix}.coordinates.latitude`, 'Latitud fuera de rango.')
      .when(location.longitude !== null && (location.longitude < -180 || location.longitude > 180), `${prefix}.coordinates.longitude`, 'Longitud fuera de rango.');
  });
  errors.throwIfAny();
}

function isHttpUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

/** Validación de una franja solicitada (reservas, compras y pedidos). */
export class SlotRequestValidator {
  constructor(
    private readonly clock: BusinessClock,
    private readonly options: BusinessOptions,
  ) {}

  /** Valida fecha futura, hora `HH:mm` y cantidad. `extra` añade reglas propias del caso de uso. */
  validate(date: string, time: string, quantity: number, quantityField = 'quantity', extra?: (errors: ValidationErrors) => void): void {
    const errors = new ValidationErrors();
    const max = this.options.maxTicketsPerOperation;
    errors.when(!Number.isInteger(quantity) || quantity < 1 || quantity > max, quantityField, `${quantityField} debe estar entre 1 y ${max}.`);
    if (!isIsoDate(date)) {
      errors.add('date', 'date debe tener formato YYYY-MM-DD.');
    } else if (!isSlotTime(time)) {
      errors.add('time', 'time debe tener formato HH:mm.');
    } else if (!this.clock.isFuture(date, time)) {
      errors.add('date', 'La fecha y hora seleccionadas deben ser futuras.');
    }
    extra?.(errors);
    errors.throwIfAny();
  }
}

/** Datos de facturación: formatos y referencia de pago que nunca puede ser un número de tarjeta. */
export function validateBilling(billing: BillingData, requireCoreFields: boolean): void {
  const errors = new ValidationErrors();
  if (requireCoreFields) {
    errors
      .requiredText(billing.billingName, 'billingName', 200)
      .email(billing.billingEmail, 'billingEmail', true)
      .requiredText(billing.billingAddress, 'billingAddress', 300);
  } else {
    errors
      .optionalText(billing.billingName, 'billingName', 200)
      .email(billing.billingEmail, 'billingEmail', false)
      .optionalText(billing.billingAddress, 'billingAddress', 300);
  }
  errors
    .optionalText(billing.taxId, 'taxId', 30)
    .when(
      billing.paymentMethodReference !== null && billing.paymentMethodReference !== undefined && !PAYMENT_REFERENCE.test(billing.paymentMethodReference),
      'paymentMethodReference',
      'paymentMethodReference debe ser una referencia local alfanumérica, no un dato financiero.',
    )
    .throwIfAny();
}

export function validatePaymentRequest(paymentMethod: string, amount: number, currency: string): PaymentMethod {
  new ValidationErrors()
    .when(!PAYMENT_METHODS.includes(paymentMethod as never), 'paymentMethod', 'paymentMethod no es un método soportado por la simulación.')
    .when(!(amount > 0) || !hasAtMostTwoDecimals(amount), 'amount', 'amount debe ser positivo con un máximo de dos decimales.')
    .when(!Money.isValidCurrency(currency), 'currency', 'currency debe ser un código ISO 4217.')
    .throwIfAny();
  return paymentMethod as PaymentMethod;
}

export function validatePagination(limit: number, offset: number): void {
  new ValidationErrors()
    .when(!Number.isInteger(limit) || limit < 1 || limit > MAX_PAGE_LIMIT, 'limit', `limit debe estar entre 1 y ${MAX_PAGE_LIMIT}.`)
    .when(!Number.isInteger(offset) || offset < 0, 'offset', 'offset no puede ser negativo.')
    .throwIfAny();
}
