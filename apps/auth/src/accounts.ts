import { Body, Catch, Controller, Get, HttpCode, HttpException, Inject, Injectable, Post, type ArgumentsHost, type ExceptionFilter } from '@nestjs/common';
import { ApiExcludeEndpoint, ApiOperation, ApiProperty, ApiPropertyOptional, ApiResponse, ApiTags } from '@nestjs/swagger';
import { Throttle } from '@nestjs/throttler';
import { IsBoolean, IsEmail, IsOptional, IsString, Length, Matches, MaxLength } from 'class-validator';
import type { Response } from 'express';
import { SignJWT } from 'jose';
import { randomUUID } from 'node:crypto';
import type { Pool } from 'pg';
import { AUTH_CONFIG, PG_POOL, type AuthConfig } from './config';
import { DUMMY_HASH_PROMISE, hashPassword, verifyPassword } from './passwords';

export const READER_SCOPES = ['attractions:read', 'attractions:book', 'attractions:cancel'];
export const ADMIN_SCOPE = 'attractions:write';
const MAX_FAILED_ATTEMPTS = 5;
const LOCK_MINUTES = 10;

export class RegisterRequest {
  @ApiProperty({ example: 'Fabián Andrade', maxLength: 200 })
  @IsString() @Length(1, 200, { message: 'Indica tu nombre (máximo 200 caracteres).' }) name!: string;
  @ApiProperty({ example: 'fabian@ejemplo.com', maxLength: 254 })
  @IsEmail({}, { message: 'El correo no es válido.' }) @MaxLength(254) email!: string;

  @ApiProperty({ example: 'Glos1Trh', minLength: 8, maxLength: 128, description: 'Al menos una mayúscula, una minúscula y un número.' })
  @IsString()
  @Length(8, 128, { message: 'La contraseña debe tener entre 8 y 128 caracteres.' })
  @Matches(/(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/, { message: 'La contraseña debe incluir mayúscula, minúscula y número.' })
  password!: string;

  @ApiPropertyOptional() @IsOptional() @IsBoolean() acceptTerms?: boolean;
}

export class LoginRequest {
  @ApiProperty({ example: 'fabian@ejemplo.com' }) @IsEmail({}, { message: 'El correo no es válido.' }) @MaxLength(254) email!: string;
  @ApiProperty({ example: 'Glos1Trh' }) @IsString() @Length(1, 128) password!: string;
}

class TokenUserDto {
  @ApiProperty({ format: 'uuid' }) id!: string;
  @ApiProperty() email!: string;
  @ApiProperty() name!: string;
}

/** Respuesta de registro e inicio de sesión. `access_token` es el que se pega en **Authorize** del Swagger del API. */
export class TokenResponse {
  @ApiProperty({ description: 'JWT HS256 para el API de atracciones (cabecera `Authorization: Bearer ...`).' }) access_token!: string;
  @ApiProperty({ enum: ['Bearer'] }) token_type!: 'Bearer';
  @ApiProperty({ example: 3600, description: 'Segundos de validez del token.' }) expires_in!: number;
  @ApiProperty({ example: 'attractions:read attractions:book attractions:cancel', description: 'Los administradores reciben además `attractions:write`.' })
  scope!: string;
  @ApiProperty({ type: TokenUserDto }) user!: TokenUserDto;
}

/** Error con código estable para `application/problem+json`. */
export class AuthProblem extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
    readonly errors?: Record<string, string[]>,
    readonly retryAfterSeconds?: number,
  ) {
    super(message);
  }
}

interface AccountRow {
  id: string;
  email: string;
  name: string;
  password_hash: string;
  failed_attempts: number;
  locked_until: Date | null;
}

/** Crea la tabla de cuentas si no existe (dev-auth es independiente del esquema del API). */
export async function ensureSchema(pool: Pool): Promise<void> {
  await pool.query(`
    CREATE TABLE IF NOT EXISTS auth_accounts (
      id uuid PRIMARY KEY,
      email varchar(254) NOT NULL,
      name varchar(200) NOT NULL,
      password_hash varchar(300) NOT NULL,
      failed_attempts int NOT NULL DEFAULT 0,
      locked_until timestamptz NULL,
      created_at timestamptz NOT NULL DEFAULT now(),
      updated_at timestamptz NOT NULL DEFAULT now()
    );
    CREATE UNIQUE INDEX IF NOT EXISTS ux_auth_accounts_email ON auth_accounts (lower(email));
  `);
}

@Injectable()
export class AccountService {
  private readonly key: Uint8Array;

  constructor(
    @Inject(PG_POOL) private readonly pool: Pool,
    @Inject(AUTH_CONFIG) private readonly config: AuthConfig,
  ) {
    this.key = new TextEncoder().encode(config.jwtSecret);
  }

  async register(request: RegisterRequest): Promise<TokenResponse> {
    const email = request.email.trim();
    const hash = await hashPassword(request.password);
    const id = randomUUID();
    try {
      await this.pool.query('INSERT INTO auth_accounts (id, email, name, password_hash) VALUES ($1, $2, $3, $4)', [id, email, request.name.trim(), hash]);
    } catch (error) {
      if ((error as { code?: string }).code === '23505') {
        throw new AuthProblem(409, 'EMAIL_ALREADY_REGISTERED', `Ya existe una cuenta con el correo ${email}.`);
      }
      throw error;
    }
    return this.issue({ id, email, name: request.name.trim() });
  }

  async login(request: LoginRequest): Promise<TokenResponse> {
    const { rows } = await this.pool.query<AccountRow>('SELECT * FROM auth_accounts WHERE lower(email) = lower($1)', [request.email.trim()]);
    const account = rows[0];
    if (!account) {
      await verifyPassword(request.password, await DUMMY_HASH_PROMISE);
      throw this.invalidCredentials();
    }
    if (account.locked_until && account.locked_until.getTime() > Date.now()) {
      const retry = Math.ceil((account.locked_until.getTime() - Date.now()) / 1000);
      throw new AuthProblem(429, 'ACCOUNT_LOCKED', 'Demasiados intentos fallidos. Vuelve a intentarlo en unos minutos.', undefined, retry);
    }
    if (!(await verifyPassword(request.password, account.password_hash))) {
      await this.pool.query(
        `UPDATE auth_accounts
            SET failed_attempts = failed_attempts + 1,
                locked_until = CASE WHEN failed_attempts + 1 >= $2 THEN now() + ($3 || ' minutes')::interval ELSE locked_until END,
                updated_at = now()
          WHERE id = $1`,
        [account.id, MAX_FAILED_ATTEMPTS, LOCK_MINUTES],
      );
      throw this.invalidCredentials();
    }
    await this.pool.query('UPDATE auth_accounts SET failed_attempts = 0, locked_until = NULL, updated_at = now() WHERE id = $1', [account.id]);
    return this.issue(account);
  }

  /** JWT HS256 con el secreto compartido. `attractions:write` solo para correos de ADMIN_EMAILS. */
  private async issue(account: { id: string; email: string; name: string }): Promise<TokenResponse> {
    const scopes = this.config.adminEmails.includes(account.email.toLowerCase()) ? [...READER_SCOPES, ADMIN_SCOPE] : READER_SCOPES;
    const scope = scopes.join(' ');
    const accessToken = await new SignJWT({ email: account.email, email_verified: false, name: account.name, scope })
      .setProtectedHeader({ alg: 'HS256', typ: 'JWT' })
      .setSubject(account.id)
      .setIssuer(this.config.issuer)
      .setAudience(this.config.audience)
      .setIssuedAt()
      .setJti(randomUUID())
      .setExpirationTime(`${this.config.tokenLifetimeSeconds}s`)
      .sign(this.key);
    return {
      access_token: accessToken,
      token_type: 'Bearer',
      expires_in: this.config.tokenLifetimeSeconds,
      scope,
      user: { id: account.id, email: account.email, name: account.name },
    };
  }

  // Mensaje genérico: no revela si el correo existe.
  private invalidCredentials = () => new AuthProblem(401, 'INVALID_CREDENTIALS', 'Correo o contraseña incorrectos.');
}

@ApiTags('Autenticación')
@Controller()
export class AccountsController {
  constructor(
    private readonly accounts: AccountService,
    @Inject(PG_POOL) private readonly pool: Pool,
  ) {}

  @Get('health')
  @ApiExcludeEndpoint()
  async health() {
    await this.pool.query('SELECT 1');
    return { status: 'ok' };
  }

  /** Crea la cuenta e inicia sesión en la misma operación. */
  @Post('auth/register')
  @Throttle({ default: { limit: 10, ttl: 60_000 } })
  @ApiOperation({ summary: 'Crear una cuenta (devuelve ya el token de sesión)' })
  @ApiResponse({ status: 201, type: TokenResponse })
  @ApiResponse({ status: 400, description: 'Datos inválidos (VALIDATION_ERROR), con el detalle por campo en `errors`.' })
  @ApiResponse({ status: 409, description: 'El correo ya está registrado (EMAIL_ALREADY_REGISTERED).' })
  register(@Body() body: RegisterRequest): Promise<TokenResponse> {
    return this.accounts.register(body);
  }

  @Post('auth/login')
  @HttpCode(200)
  @Throttle({ default: { limit: 20, ttl: 60_000 } })
  @ApiOperation({ summary: 'Iniciar sesión y obtener el access token para el API' })
  @ApiResponse({ status: 200, type: TokenResponse })
  @ApiResponse({ status: 401, description: 'Correo o contraseña incorrectos (INVALID_CREDENTIALS).' })
  @ApiResponse({ status: 429, description: 'Cuenta bloqueada 10 minutos tras 5 intentos fallidos (ACCOUNT_LOCKED), con `Retry-After`.' })
  login(@Body() body: LoginRequest): Promise<TokenResponse> {
    return this.accounts.login(body);
  }
}

/** Errores `application/problem+json` con `code` estable; sin detalles internos. */
@Catch()
export class AuthProblemFilter implements ExceptionFilter {
  catch(exception: unknown, host: ArgumentsHost): void {
    const response = host.switchToHttp().getResponse<Response>();
    const request = host.switchToHttp().getRequest<{ originalUrl: string }>();
    let status = 500;
    let code = 'INTERNAL_ERROR';
    let detail = 'Se produjo un error inesperado.';
    let errors: Record<string, string[]> | undefined;
    if (exception instanceof AuthProblem) {
      ({ status, code } = exception);
      detail = exception.message;
      errors = exception.errors;
      if (exception.retryAfterSeconds) response.setHeader('Retry-After', String(exception.retryAfterSeconds));
    } else if (exception instanceof HttpException) {
      status = exception.getStatus();
      code = status === 429 ? 'RATE_LIMITED' : status === 404 ? 'NOT_FOUND' : 'HTTP_ERROR';
      detail = status === 429 ? 'Demasiadas solicitudes. Espera un momento.' : status === 404 ? 'El recurso solicitado no existe.' : exception.message;
    } else {
      console.error(exception);
    }
    response
      .status(status)
      .type('application/problem+json')
      .send(JSON.stringify({ type: `urn:tourgirls:auth:${code.toLowerCase().replace(/_/g, '-')}`, status, detail, code, instance: request.originalUrl, ...(errors ? { errors } : {}) }));
  }
}
