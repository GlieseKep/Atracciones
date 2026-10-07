import { createHash, createHmac, randomBytes, timingSafeEqual } from 'node:crypto';
import { ValidationError } from '../errors';
import type { BusinessOptions } from './options';
import { canonicalJson } from './transactions';

interface TokenPayload {
  offset: number;
  criteria: string;
  expiresAt: number;
}

/**
 * Token opaco `nextPage`: contiene el offset, un hash de los criterios de búsqueda y la expiración, firmado con
 * HMAC-SHA256. Un token manipulado, expirado o usado con otros criterios produce 400.
 */
export class PageTokenService {
  private readonly key: Buffer;
  private readonly lifetimeMs: number;

  constructor(options: BusinessOptions, private readonly now: () => Date = () => new Date()) {
    this.key = options.pageTokenSecret ? Buffer.from(options.pageTokenSecret, 'base64') : randomBytes(32);
    this.lifetimeMs = options.pageTokenLifetimeMinutes * 60_000;
  }

  create(offset: number, criteriaHash: string): string {
    const payload: TokenPayload = {
      offset,
      criteria: criteriaHash,
      expiresAt: Math.floor((this.now().getTime() + this.lifetimeMs) / 1000),
    };
    const body = Buffer.from(JSON.stringify(payload));
    return `${body.toString('base64url')}.${this.sign(body).toString('base64url')}`;
  }

  readOffset(token: string, criteriaHash: string): number {
    const [encodedBody, encodedSignature, ...rest] = token.split('.');
    if (encodedBody && encodedSignature && rest.length === 0) {
      const body = Buffer.from(encodedBody, 'base64url');
      const signature = Buffer.from(encodedSignature, 'base64url');
      const expected = this.sign(body);
      if (body.length > 0 && signature.length === expected.length && timingSafeEqual(signature, expected)) {
        try {
          const data = JSON.parse(body.toString('utf8')) as TokenPayload;
          if (
            Number.isInteger(data.offset) &&
            data.offset >= 0 &&
            data.criteria === criteriaHash &&
            data.expiresAt * 1000 > this.now().getTime()
          ) {
            return data.offset;
          }
        } catch {
          /* token corrupto: se informa abajo */
        }
      }
    }
    throw new ValidationError('nextPage es inválido, expiró o no corresponde a los criterios de búsqueda.');
  }

  static hashCriteria(criteria: unknown): string {
    return createHash('sha256').update(canonicalJson(criteria)).digest('hex').slice(0, 16).toUpperCase();
  }

  private sign(body: Buffer): Buffer {
    return createHmac('sha256', this.key).update(body).digest();
  }
}
