import { existsSync } from 'node:fs';
import { join } from 'node:path';

/** Configuración de dev-auth. `AUTH_JWT_SECRET`, `AUTH_ISSUER` y `AUTH_AUDIENCE` deben coincidir con los del API. */
export interface AuthConfig {
  port: number;
  databaseUrl: string;
  databaseSsl: boolean;
  jwtSecret: string;
  issuer: string;
  audience: string;
  tokenLifetimeSeconds: number;
  corsOrigins: string[];
  adminEmails: string[];
  trustProxy: boolean;
  /** Swagger UI en `/docs` (por defecto activo). */
  swaggerEnabled: boolean;
}

export function loadEnvFile(): void {
  const file = join(__dirname, '..', '.env');
  if (existsSync(file)) process.loadEnvFile(file);
}

const list = (value: string | undefined) => (value ?? '').split(',').map((v) => v.trim().replace(/\/$/, '')).filter(Boolean);

export function readConfig(env: NodeJS.ProcessEnv = process.env): AuthConfig {
  const errors: string[] = [];
  const required = (name: string) => {
    const value = env[name]?.trim();
    if (!value) errors.push(`Falta la variable ${name}.`);
    return value ?? '';
  };
  const config: AuthConfig = {
    port: Number(env.PORT ?? 5280),
    databaseUrl: required('DATABASE_URL'),
    databaseSsl: (env.DATABASE_SSL ?? '').toLowerCase() === 'true',
    jwtSecret: required('AUTH_JWT_SECRET'),
    issuer: env.AUTH_ISSUER?.trim() || 'tourgirls-auth',
    audience: env.AUTH_AUDIENCE?.trim() || 'tourgirls-api',
    tokenLifetimeSeconds: Number(env.TOKEN_LIFETIME_SECONDS ?? 3600),
    corsOrigins: list(env.CORS_ORIGINS),
    adminEmails: list(env.ADMIN_EMAILS).map((e) => e.toLowerCase()),
    trustProxy: (env.TRUST_PROXY ?? '').toLowerCase() === 'true',
    swaggerEnabled: (env.SWAGGER_ENABLED ?? 'true').toLowerCase() !== 'false',
  };
  if (config.jwtSecret && config.jwtSecret.length < 32) errors.push('AUTH_JWT_SECRET debe tener al menos 32 caracteres.');
  if (!Number.isInteger(config.port) || config.port <= 0) errors.push('PORT debe ser un entero positivo.');
  if (errors.length > 0) throw new Error(`Configuración inválida de dev-auth:\n - ${errors.join('\n - ')}`);
  return config;
}

export const AUTH_CONFIG = Symbol('AUTH_CONFIG');
export const PG_POOL = Symbol('PG_POOL');
