import { existsSync } from 'node:fs';
import { join } from 'node:path';

/** Configuración del API leída de variables de entorno (App Settings en Azure). Se valida al arrancar. */
export interface ApiConfig {
  port: number;
  database: { url: string; ssl: boolean; migrationsRun: boolean; seedCatalog: boolean; availabilityDays: number };
  auth: { jwtSecret: string; issuer: string; audience: string; requireVerifiedEmail: boolean };
  corsOrigins: string[];
  swaggerEnabled: boolean;
  publicApiUrl: string | null;
  trustProxy: boolean;
  rateLimit: { permitLimit: number; windowSeconds: number };
  business: {
    timeZone: string;
    holdMinutes: number;
    maxPaymentAttemptsPerOrder: number;
    idempotencyRetentionHours: number;
    pageTokenSecret: string | null;
  };
}

/** Carga `apps/api/.env` si existe (desarrollo local). En Azure las variables vienen de App Settings. */
export function loadEnvFile(): void {
  const file = join(__dirname, '..', '.env');
  if (existsSync(file)) process.loadEnvFile(file);
}

const bool = (value: string | undefined, fallback: boolean) => (value === undefined || value === '' ? fallback : value.toLowerCase() === 'true');
const int = (value: string | undefined, fallback: number) => (value === undefined || value === '' ? fallback : Number(value));
const list = (value: string | undefined) => (value ?? '').split(',').map((v) => v.trim().replace(/\/$/, '')).filter(Boolean);

export function readConfig(env: NodeJS.ProcessEnv = process.env): ApiConfig {
  const errors: string[] = [];
  const required = (name: string) => {
    const value = env[name]?.trim();
    if (!value) errors.push(`Falta la variable ${name}.`);
    return value ?? '';
  };

  const config: ApiConfig = {
    port: int(env.PORT, 5276),
    database: {
      url: required('DATABASE_URL'),
      ssl: bool(env.DATABASE_SSL, false),
      migrationsRun: bool(env.DB_MIGRATIONS_RUN, true),
      seedCatalog: bool(env.DB_SEED_CATALOG, true),
      availabilityDays: int(env.DB_AVAILABILITY_DAYS, 60),
    },
    auth: {
      jwtSecret: required('AUTH_JWT_SECRET'),
      issuer: env.AUTH_ISSUER?.trim() || 'tourgirls-auth',
      audience: env.AUTH_AUDIENCE?.trim() || 'tourgirls-api',
      // dev-auth no verifica correos: por defecto no se exige email_verified (ver AZURE_DEPLOY.md).
      requireVerifiedEmail: bool(env.REQUIRE_VERIFIED_EMAIL, false),
    },
    corsOrigins: list(env.CORS_ORIGINS),
    swaggerEnabled: bool(env.SWAGGER_ENABLED, true),
    publicApiUrl: env.PUBLIC_API_URL?.trim().replace(/\/$/, '') || null,
    trustProxy: bool(env.TRUST_PROXY, false),
    rateLimit: { permitLimit: int(env.RATE_LIMIT_PERMIT, 100), windowSeconds: int(env.RATE_LIMIT_WINDOW_SECONDS, 60) },
    business: {
      timeZone: env.APP_TIMEZONE?.trim() || 'America/Guayaquil',
      holdMinutes: int(env.HOLD_MINUTES, 15),
      maxPaymentAttemptsPerOrder: int(env.MAX_PAYMENT_ATTEMPTS, 3),
      idempotencyRetentionHours: int(env.IDEMPOTENCY_RETENTION_HOURS, 24),
      pageTokenSecret: env.PAGE_TOKEN_SECRET?.trim() || null,
    },
  };

  if (config.auth.jwtSecret && config.auth.jwtSecret.length < 32) {
    errors.push('AUTH_JWT_SECRET debe tener al menos 32 caracteres.');
  }
  for (const [name, value] of Object.entries({ PORT: config.port, RATE_LIMIT_PERMIT: config.rateLimit.permitLimit, DB_AVAILABILITY_DAYS: config.database.availabilityDays })) {
    if (!Number.isInteger(value) || value <= 0) errors.push(`${name} debe ser un entero positivo.`);
  }
  if (errors.length > 0) {
    throw new Error(`Configuración inválida del API:\n - ${errors.join('\n - ')}`);
  }
  return config;
}

export const API_CONFIG = Symbol('API_CONFIG');
export const BUSINESS = Symbol('BUSINESS');
export const DATA_SOURCE = Symbol('DATA_SOURCE');
