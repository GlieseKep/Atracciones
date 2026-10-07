import { Money } from '@atracciones/domain';
import { ValidationError } from './errors';
import { BusinessClock } from './shared/clock';
import { deterministicPaymentPolicy } from './shared/customers';
import { mergePatch } from './shared/mappers';
import { DEFAULT_BUSINESS_OPTIONS, validateBusinessOptions } from './shared/options';
import { PageTokenService } from './shared/page-tokens';
import { canonicalJson, IdempotencyService } from './shared/transactions';
import { isIsoDate, SlotRequestValidator, validateAttraction, validateBilling } from './shared/validation';
import type { AttractionData } from './models';

const fixedNow = (iso: string) => () => new Date(iso);

describe('BusinessClock', () => {
  // 2026-10-07T03:30Z = 2026-10-06 22:30 en Guayaquil (UTC-5).
  const clock = new BusinessClock('America/Guayaquil', fixedNow('2026-10-07T03:30:00Z'));

  it('usa la fecha local de negocio, no la UTC', () => {
    expect(clock.today).toBe('2026-10-06');
    expect(clock.localNow).toBe('2026-10-06T22:30:00');
  });

  it('considera futura una franja solo si su hora local no llegó', () => {
    expect(clock.isFuture('2026-10-06', '23:00')).toBe(true);
    expect(clock.isFuture('2026-10-06', '22:00')).toBe(false);
    expect(clock.isFuture('2026-10-07', '09:00')).toBe(true);
  });
});

describe('validación', () => {
  it('valida fechas reales del calendario', () => {
    expect(isIsoDate('2026-02-28')).toBe(true);
    expect(isIsoDate('2026-02-30')).toBe(false);
    expect(isIsoDate('28/02/2026')).toBe(false);
  });

  it('exige franjas futuras, HH:mm y cantidades dentro del límite', () => {
    const validator = new SlotRequestValidator(new BusinessClock('America/Guayaquil', fixedNow('2026-10-07T15:00:00Z')), DEFAULT_BUSINESS_OPTIONS);
    expect(() => validator.validate('2026-12-01', '09:00', 2)).not.toThrow();
    expect(() => validator.validate('2026-10-07', '09:00', 2)).toThrow(ValidationError);
    expect(() => validator.validate('2026-12-01', '9:00', 2)).toThrow(ValidationError);
    expect(() => validator.validate('2026-12-01', '09:00', 101)).toThrow(ValidationError);
  });

  it('nunca acepta un número de tarjeta como referencia de pago', () => {
    const base = { billingName: 'Ana', billingEmail: 'ana@example.com', billingAddress: 'Quito' };
    expect(() => validateBilling({ ...base, paymentMethodReference: '4111111111111111' }, true)).toThrow(ValidationError);
    expect(() => validateBilling({ ...base, paymentMethodReference: 'mi-tarjeta' }, true)).not.toThrow();
  });

  it('agrupa los errores de una atracción por campo', () => {
    const data: AttractionData = {
      name: 'ab', longDescription: 'x', duration: '3 horas', price: { currency: 'usd', total: 0 }, categories: [],
      badges: [], locations: [{ address: 'A', city: 'Quito', country: 'Ecuador', latitude: 1, longitude: null, type: null }],
      photoUrls: ['ftp://x'], operator: null, productType: 'TOUR', includes: [], supportedLanguages: ['ES'], freeCancellation: false,
    };
    try {
      validateAttraction(data);
      fail('debía lanzar');
    } catch (error) {
      expect(error).toBeInstanceOf(ValidationError);
      expect(Object.keys((error as ValidationError).errors).sort()).toEqual(
        ['categories', 'duration', 'locations[0].coordinates', 'locations[0].country', 'name', 'photos', 'price.currency', 'price.total', 'productType', 'supportedLanguages'].sort(),
      );
    }
  });

  it('rechaza opciones de negocio inválidas', () => {
    expect(() => validateBusinessOptions({ ...DEFAULT_BUSINESS_OPTIONS, timeZone: 'Marte/Olympus' })).toThrow();
    expect(() => validateBusinessOptions({ ...DEFAULT_BUSINESS_OPTIONS, holdMinutes: 0 })).toThrow();
  });
});

describe('idempotencia', () => {
  it('produce el mismo hash para payloads equivalentes con distinto orden de claves', () => {
    expect(canonicalJson({ b: 1, a: { d: [1, 2], c: undefined } })).toBe('{"a":{"d":[1,2]},"b":1}');
    expect(IdempotencyService.hash({ a: 1, b: 2 })).toBe(IdempotencyService.hash({ b: 2, a: 1 }));
    expect(IdempotencyService.hash({ a: 1 })).not.toBe(IdempotencyService.hash({ a: 2 }));
  });
});

describe('PageTokenService', () => {
  const options = { ...DEFAULT_BUSINESS_OPTIONS, pageTokenSecret: Buffer.alloc(32, 7).toString('base64') };

  it('devuelve el offset solo para los mismos criterios y antes de expirar', () => {
    let now = new Date('2026-10-07T12:00:00Z');
    const tokens = new PageTokenService(options, () => now);
    const token = tokens.create(20, 'ABC');
    expect(tokens.readOffset(token, 'ABC')).toBe(20);
    expect(() => tokens.readOffset(token, 'OTRO')).toThrow(ValidationError);
    now = new Date('2026-10-07T12:16:00Z');
    expect(() => tokens.readOffset(token, 'ABC')).toThrow(ValidationError);
  });

  it('rechaza tokens manipulados', () => {
    const tokens = new PageTokenService(options);
    const [body, signature] = tokens.create(0, 'ABC').split('.');
    const forged = Buffer.from(JSON.stringify({ offset: 999, criteria: 'ABC', expiresAt: 9999999999 })).toString('base64url');
    expect(() => tokens.readOffset(`${forged}.${signature}`, 'ABC')).toThrow(ValidationError);
    expect(() => tokens.readOffset(`${body}`, 'ABC')).toThrow(ValidationError);
  });
});

describe('pasarela simulada', () => {
  it('decide el resultado por los céntimos del importe', () => {
    expect(deterministicPaymentPolicy.evaluate(new Money('USD', 10.51), 'CARD', 1).status).toBe('REJECTED');
    expect(deterministicPaymentPolicy.evaluate(new Money('USD', 10.52), 'CARD', 1).status).toBe('FAILED');
    expect(deterministicPaymentPolicy.evaluate(new Money('USD', 10.5), 'CARD', 1).status).toBe('AUTHORIZED');
  });
});

describe('mergePatch', () => {
  it('conserva los valores no enviados o nulos', () => {
    const current = { name: 'A', badges: ['x'], freeCancellation: true } as unknown as AttractionData;
    expect(mergePatch(current, { name: 'B', badges: null, freeCancellation: undefined })).toMatchObject({ name: 'B', badges: ['x'], freeCancellation: true });
  });
});
