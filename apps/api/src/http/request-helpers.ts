import { createParamDecorator, type ExecutionContext, type PipeTransform, ValidationPipe } from '@nestjs/common';
import { NotFoundError, ValidationError } from '@atracciones/business';
import type { ValidationError as ClassValidatorError } from 'class-validator';
import type { Request } from 'express';
import { HttpProblem } from './problems';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const IDEMPOTENCY_HEADER = 'Idempotency-Key';

/**
 * `Idempotency-Key` obligatoria: un único UUID distinto de cero. Una cabecera ausente o inválida produce 400
 * `INVALID_IDEMPOTENCY_KEY` antes de ejecutar el caso de uso.
 */
export const IdempotencyKey = createParamDecorator((_: unknown, context: ExecutionContext): string => {
  const raw = context.switchToHttp().getRequest<Request>().headers['idempotency-key'];
  let error: string | null = null;
  if (raw === undefined || (typeof raw === 'string' && raw.trim() === '')) {
    error = `La cabecera '${IDEMPOTENCY_HEADER}' es obligatoria en esta operación.`;
  } else if (Array.isArray(raw) || raw.includes(',') || !UUID.test(raw.trim()) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(raw.trim())) {
    error = `La cabecera '${IDEMPOTENCY_HEADER}' debe contener un único UUID con formato xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.`;
  }
  if (error) throw new HttpProblem(400, 'INVALID_IDEMPOTENCY_KEY', 'Idempotency-Key inválida', error);
  return (raw as string).trim().toLowerCase();
});

/** Identificador de ruta UUID; uno con formato inválido se trata como recurso inexistente (como `{id:guid}`). */
export class ParseResourceId implements PipeTransform<string, string> {
  transform(value: string): string {
    if (!UUID.test(value)) throw new NotFoundError('El recurso solicitado no existe.');
    return value.toLowerCase();
  }
}

/** Errores de class-validator agrupados por ruta de campo (`locations[0].city`). */
function flatten(errors: ClassValidatorError[], parent = ''): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  for (const error of errors) {
    const path = parent ? (/^\d+$/.test(error.property) ? `${parent}[${error.property}]` : `${parent}.${error.property}`) : error.property;
    if (error.constraints) {
      result[path] = Object.entries(error.constraints).map(([rule, message]) =>
        rule === 'whitelistValidation' ? `La propiedad '${path}' no está permitida.` : message,
      );
    }
    Object.assign(result, flatten(error.children ?? [], path));
  }
  return result;
}

/**
 * Validación global: transforma tipos, rechaza propiedades desconocidas (p. ej. `sub` o datos de tarjeta en el cuerpo)
 * y devuelve 400 `VALIDATION_ERROR` con los errores por campo.
 */
export const apiValidationPipe = () =>
  new ValidationPipe({
    transform: true,
    whitelist: true,
    forbidNonWhitelisted: true,
    transformOptions: { enableImplicitConversion: false },
    exceptionFactory: (errors) => new ValidationError('La solicitud contiene datos inválidos.', flatten(errors)),
  });
