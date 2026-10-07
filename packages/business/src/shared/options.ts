/** Parámetros de negocio configurables. */
export interface BusinessOptions {
  /** Zona horaria IANA en la que se interpretan fechas y horas de las franjas. */
  timeZone: string;
  /** Máximo de entradas por reserva, compra o pedido. */
  maxTicketsPerOperation: number;
  /** Duración de la retención de cupos de un pedido pendiente de pago. */
  holdMinutes: number;
  /** Intentos de pago simulado permitidos por pedido. */
  maxPaymentAttemptsPerOrder: number;
  /** Retención de las claves de idempotencia. */
  idempotencyRetentionHours: number;
  /** Clave HMAC (Base64) para firmar `nextPage`. Si está vacía se genera una por proceso. */
  pageTokenSecret?: string | null;
  pageTokenLifetimeMinutes: number;
}

export const DEFAULT_BUSINESS_OPTIONS: BusinessOptions = {
  timeZone: 'America/Guayaquil',
  maxTicketsPerOperation: 100,
  holdMinutes: 15,
  maxPaymentAttemptsPerOrder: 3,
  idempotencyRetentionHours: 24,
  pageTokenSecret: null,
  pageTokenLifetimeMinutes: 15,
};

export function validateBusinessOptions(options: BusinessOptions): BusinessOptions {
  const positives: (keyof BusinessOptions)[] = [
    'maxTicketsPerOperation', 'holdMinutes', 'maxPaymentAttemptsPerOrder', 'idempotencyRetentionHours', 'pageTokenLifetimeMinutes',
  ];
  for (const key of positives) {
    const value = options[key];
    if (typeof value !== 'number' || !Number.isInteger(value) || value <= 0) {
      throw new Error(`La opción de negocio '${key}' debe ser un entero positivo.`);
    }
  }
  try {
    new Intl.DateTimeFormat('en-CA', { timeZone: options.timeZone });
  } catch {
    throw new Error(`'${options.timeZone}' no es una zona horaria IANA válida.`);
  }
  return options;
}
