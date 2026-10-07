import { randomUUID } from 'node:crypto';
import type { INestApplication } from '@nestjs/common';
import { SignJWT } from 'jose';
import type { DataSource } from 'typeorm';
import { createApp, initializeDatabase } from '../src/bootstrap';
import { readConfig } from '../src/config';

export const SECRET = 'secreto-de-pruebas-con-mas-de-32-caracteres';
export const ISSUER = 'tourgirls-auth';
export const AUDIENCE = 'tourgirls-api';
export const ALL_SCOPES = 'attractions:read attractions:book attractions:cancel';

export async function startApi(): Promise<{ app: INestApplication; dataSource: DataSource }> {
  const config = readConfig({
    DATABASE_URL: process.env.TEST_DATABASE_URL,
    AUTH_JWT_SECRET: SECRET,
    AUTH_ISSUER: ISSUER,
    AUTH_AUDIENCE: AUDIENCE,
    RATE_LIMIT_PERMIT: '100000',
    DB_AVAILABILITY_DAYS: '30',
    PAGE_TOKEN_SECRET: Buffer.alloc(32, 3).toString('base64'),
  } as NodeJS.ProcessEnv);
  const dataSource = await initializeDatabase(config);
  const app = await createApp(config, dataSource);
  await app.init();
  return { app, dataSource };
}

/** Token equivalente al de dev-auth, firmado con el secreto compartido de pruebas. */
export async function token(options: { sub?: string; email?: string; scope?: string; issuer?: string; secret?: string; expiresIn?: string } = {}) {
  return new SignJWT({ email: options.email ?? `${options.sub ?? 'u'}@example.com`, email_verified: false, scope: options.scope ?? ALL_SCOPES })
    .setProtectedHeader({ alg: 'HS256' })
    .setSubject(options.sub ?? randomUUID())
    .setIssuer(options.issuer ?? ISSUER)
    .setAudience(AUDIENCE)
    .setIssuedAt()
    .setExpirationTime(options.expiresIn ?? '1h')
    .sign(new TextEncoder().encode(options.secret ?? SECRET));
}

/** Fecha `YYYY-MM-DD` dentro de `days` días (siempre futura en la zona de negocio). */
export const inDays = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);

export const SEEDED = {
  teleferico: 'a1b2c3d4-0001-4000-8000-000000000001',
  mitad: 'a1b2c3d4-0002-4000-8000-000000000002',
  cotopaxi: 'a1b2c3d4-0003-4000-8000-000000000003',
  quilotoa: 'de000000-0004-4000-8000-000000000004',
  mindo: 'de000000-0007-4000-8000-000000000007',
};

export async function grantCatalogWrite(dataSource: DataSource, email: string): Promise<void> {
  await dataSource.query(`INSERT INTO roles (id, name, created_at) VALUES ($1, 'admin', now()) ON CONFLICT (name) DO NOTHING`, [randomUUID()]);
  const [role] = await dataSource.query(`SELECT id FROM roles WHERE name = 'admin'`);
  await dataSource.query(`INSERT INTO role_permissions (role_id, permission) VALUES ($1, 'catalog:write') ON CONFLICT DO NOTHING`, [role.id]);
  await dataSource.query(
    `INSERT INTO user_roles (id, user_id, role_id, reason, assigned_at) SELECT $1, id, $2, 'pruebas', now() FROM users WHERE email = $3`,
    [randomUUID(), role.id, email],
  );
}
