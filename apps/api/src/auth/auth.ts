import { createParamDecorator, type CanActivate, type ExecutionContext, Inject, Injectable, SetMetadata } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { ForbiddenError, UnauthorizedError, type AuthenticatedUser, type BusinessServices } from '@atracciones/business';
import type { Request } from 'express';
import { jwtVerify, errors as joseErrors } from 'jose';
import { API_CONFIG, BUSINESS, type ApiConfig } from '../config';

/** Scopes del contrato. */
export const Scopes = {
  Read: 'attractions:read',
  Book: 'attractions:book',
  Write: 'attractions:write',
  Cancel: 'attractions:cancel',
} as const;

export const SCOPE_DESCRIPTIONS: Record<string, string> = {
  [Scopes.Read]: 'Leer catálogo, disponibilidad, perfil, reservas y pedidos propios',
  [Scopes.Book]: 'Hacer reservas, compras, pedidos y pagos simulados',
  [Scopes.Write]: 'Crear y mantener inventario',
  [Scopes.Cancel]: 'Cancelar reservas y pedidos',
};

const SCOPE_KEY = 'requiredScope';
const PERMISSION_KEY = 'requiredPermission';
const PUBLIC_KEY = 'isPublic';

/** Exige un token válido con el scope indicado. */
export const RequireScope = (scope: string) => SetMetadata(SCOPE_KEY, scope);
/** Además del scope, exige un permiso local (rol asignado en la base de datos). */
export const RequirePermission = (permission: string) => SetMetadata(PERMISSION_KEY, permission);
/** Ruta anónima (health, documentación). */
export const Public = () => SetMetadata(PUBLIC_KEY, true);

export interface AuthenticatedRequest extends Request {
  id?: string;
  user?: AuthenticatedUser;
  scopes?: string[];
}

/** Identidad de los claims verificados del token. Nunca se toma del cuerpo de la solicitud. */
export const CurrentUser = createParamDecorator((_: unknown, context: ExecutionContext): AuthenticatedUser => {
  const user = context.switchToHttp().getRequest<AuthenticatedRequest>().user;
  if (!user) throw new UnauthorizedError('Se requiere un access token válido.');
  return user;
});

/**
 * Valida el JWT HS256 emitido por dev-auth (firma con el secreto compartido, `iss`, `aud`, `exp`) y comprueba el
 * scope de la operación. Las escrituras de catálogo además consultan el permiso local (fail-closed).
 */
@Injectable()
export class AuthGuard implements CanActivate {
  private readonly key: Uint8Array;

  constructor(
    private readonly reflector: Reflector,
    @Inject(API_CONFIG) private readonly config: ApiConfig,
    @Inject(BUSINESS) private readonly business: BusinessServices,
  ) {
    this.key = new TextEncoder().encode(config.auth.jwtSecret);
  }

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const targets = [context.getHandler(), context.getClass()];
    if (this.reflector.getAllAndOverride<boolean>(PUBLIC_KEY, targets)) return true;

    const request = context.switchToHttp().getRequest<AuthenticatedRequest>();
    const header = request.headers.authorization;
    const match = header ? /^Bearer\s+(\S+)$/i.exec(header) : null;
    if (!match) throw new UnauthorizedError('Se requiere un access token válido.');

    let claims: Record<string, unknown>;
    try {
      ({ payload: claims } = await jwtVerify(match[1], this.key, {
        algorithms: ['HS256'],
        issuer: this.config.auth.issuer,
        audience: this.config.auth.audience,
        clockTolerance: 60,
        requiredClaims: ['sub', 'exp'],
      }));
    } catch (error) {
      throw new UnauthorizedError(
        error instanceof joseErrors.JWTExpired ? 'El access token expiró.' : 'El access token no es válido.',
      );
    }

    const subject = typeof claims.sub === 'string' ? claims.sub.trim() : '';
    if (!subject) throw new UnauthorizedError("El access token no contiene el claim 'sub'.");
    request.user = {
      issuer: String(claims.iss),
      subject,
      email: typeof claims.email === 'string' ? claims.email : null,
      emailVerified: typeof claims.email_verified === 'boolean' ? claims.email_verified : null,
    };
    request.scopes = [claims.scope, claims.scp]
      .flatMap((v) => (Array.isArray(v) ? v : typeof v === 'string' ? v.split(' ') : []))
      .filter((s): s is string => typeof s === 'string' && s !== '');

    const scope = this.reflector.getAllAndOverride<string | undefined>(SCOPE_KEY, targets);
    if (scope && !request.scopes.includes(scope)) {
      throw new ForbiddenError(`El access token no tiene el scope '${scope}'.`, 'INSUFFICIENT_SCOPE');
    }
    const permission = this.reflector.getAllAndOverride<string | undefined>(PERMISSION_KEY, targets);
    if (permission && !(await this.business.permissions.hasPermission(request.user, permission))) {
      throw new ForbiddenError(`El usuario no tiene el permiso local '${permission}'.`);
    }
    return true;
  }
}
