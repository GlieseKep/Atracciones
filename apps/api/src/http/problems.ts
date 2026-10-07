import { ArgumentsHost, Catch, HttpException, HttpStatus, Logger, type ExceptionFilter } from '@nestjs/common';
import {
  BusinessError,
  ConflictError,
  ForbiddenError,
  NotFoundError,
  PaymentSimulationError,
  UnauthorizedError,
  ValidationError,
} from '@atracciones/business';
import type { Request, Response } from 'express';

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  code: string;
  traceId: string;
  errors?: Record<string, string[]>;
}

/** Error HTTP propio del host (p. ej. cabecera inválida) con código estable. */
export class HttpProblem extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly title: string,
    message: string,
  ) {
    super(message);
  }
}

const HTTP_DEFAULTS: Record<number, [string, string]> = {
  400: ['BAD_REQUEST', 'Solicitud inválida'],
  401: ['UNAUTHORIZED', 'No autenticado'],
  403: ['FORBIDDEN', 'Acceso denegado'],
  404: ['NOT_FOUND', 'Recurso no encontrado'],
  405: ['METHOD_NOT_ALLOWED', 'Método no permitido'],
  413: ['PAYLOAD_TOO_LARGE', 'Cuerpo demasiado grande'],
  415: ['UNSUPPORTED_MEDIA_TYPE', 'Tipo de contenido no soportado'],
  429: ['RATE_LIMITED', 'Demasiadas solicitudes'],
};

const toType = (code: string) => `urn:atracciones:problems:${code.toLowerCase().replace(/_/g, '-')}`;

/**
 * Traduce cualquier excepción a `application/problem+json` (RFC 7807) con `code` estable y `traceId`.
 * Nunca expone trazas ni detalles internos en errores 5xx.
 */
@Catch()
export class ProblemDetailsFilter implements ExceptionFilter {
  private readonly logger = new Logger('Problems');

  catch(exception: unknown, host: ArgumentsHost): void {
    const http = host.switchToHttp();
    const request = http.getRequest<Request & { id?: string }>();
    const response = http.getResponse<Response>();
    if (response.headersSent) return;

    const problem = this.map(exception, request);
    if (problem.status >= 500) {
      this.logger.error(`Error no controlado procesando ${request.method} ${request.originalUrl}`, exception instanceof Error ? exception.stack : String(exception));
    }
    if (problem.status === 401) response.setHeader('WWW-Authenticate', 'Bearer');
    if (exception instanceof HttpException && problem.status === 429) {
      const retry = (exception.getResponse() as { retryAfter?: number } | string);
      if (typeof retry === 'object' && retry.retryAfter) response.setHeader('Retry-After', String(retry.retryAfter));
    }
    response.status(problem.status).type('application/problem+json').send(JSON.stringify(problem));
  }

  private map(exception: unknown, request: Request & { id?: string }): ProblemDetails {
    const base = { instance: request.originalUrl.split('?')[0], traceId: request.id ?? '' };
    const business = (status: number, title: string, error: BusinessError): ProblemDetails => ({
      ...base,
      type: toType(error.code),
      title,
      status,
      detail: error.message,
      code: error.code,
      ...(error instanceof ValidationError && Object.keys(error.errors).length > 0 ? { errors: error.errors } : {}),
    });

    if (exception instanceof ValidationError) return business(400, 'Solicitud inválida', exception);
    if (exception instanceof UnauthorizedError) return business(401, 'No autenticado', exception);
    if (exception instanceof ForbiddenError) return business(403, 'Acceso denegado', exception);
    if (exception instanceof NotFoundError) return business(404, 'Recurso no encontrado', exception);
    if (exception instanceof ConflictError) return business(409, 'Conflicto', exception);
    if (exception instanceof PaymentSimulationError) return business(422, 'Pago simulado rechazado', exception);
    if (exception instanceof BusinessError) return business(409, 'Regla de negocio incumplida', exception);

    if (exception instanceof HttpProblem) {
      return { ...base, type: toType(exception.code), title: exception.title, status: exception.status, detail: exception.message, code: exception.code };
    }
    if (exception instanceof HttpException) {
      const status = exception.getStatus();
      const [code, title] = HTTP_DEFAULTS[status] ?? (status >= 500 ? ['INTERNAL_ERROR', 'Error interno del servidor'] : ['HTTP_ERROR', 'Error de la solicitud']);
      const detail =
        status === 404 ? 'El recurso solicitado no existe.'
        : status === 429 ? 'Se superó el límite de solicitudes. Reintente más tarde.'
        : status >= 500 ? 'Se produjo un error inesperado.'
        : exception.message;
      return { ...base, type: toType(code), title, status, detail, code };
    }
    // JSON malformado de body-parser.
    if (exception instanceof SyntaxError && 'body' in exception) {
      return { ...base, type: toType('VALIDATION_ERROR'), title: 'Solicitud inválida', status: 400, detail: 'El cuerpo no es un JSON válido.', code: 'VALIDATION_ERROR' };
    }
    return {
      ...base,
      type: toType('INTERNAL_ERROR'),
      title: 'Error interno del servidor',
      status: HttpStatus.INTERNAL_SERVER_ERROR,
      detail: 'Se produjo un error inesperado.',
      code: 'INTERNAL_ERROR',
    };
  }
}
