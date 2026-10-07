// SOLO DESARROLLO LOCAL. Provisiona el usuario "dev-admin-1" del emisor de desarrollo como administrador
// del catálogo (rol `admin` con el permiso local `catalog:write`) en la base SQLite de desarrollo.
//
// Requiere que el emisor (tools/dev-oauth/server.mjs) y el API estén en marcha. Es idempotente.
//   node tools/dev-oauth/grant-admin.mjs [ruta-a-atracciones-dev.db]

import { createHash, randomBytes, randomUUID } from 'node:crypto';
import { dirname, join } from 'node:path';
import { DatabaseSync } from 'node:sqlite';
import { fileURLToPath } from 'node:url';

const API = process.env.API_URL ?? 'http://localhost:5276';
const ISSUER_URL = process.env.DEV_OAUTH_URL ?? 'http://localhost:5280';
const ISSUER = process.env.DEV_OAUTH_ISSUER ?? 'https://localhost/dev-issuer';
const SUBJECT = 'dev-admin-1';
const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const dbPath = process.argv[2] ?? join(root, 'AtraccionesService.API', 'atracciones-dev.db');

async function waitFor(url, label) {
  for (let i = 0; i < 90; i++) {
    try {
      const r = await fetch(url);
      if (r.status < 500) return;
    } catch {
      /* aún arrancando */
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(`${label} no responde en ${url}`);
}

async function adminToken() {
  const verifier = randomBytes(32).toString('base64url');
  const redirectUri = 'http://localhost:5173/auth/callback';
  const authorize = await fetch(`${ISSUER_URL}/authorize`, {
    method: 'POST',
    redirect: 'manual',
    body: new URLSearchParams({
      response_type: 'code',
      client_id: 'atracciones-web',
      redirect_uri: redirectUri,
      code_challenge: createHash('sha256').update(verifier).digest('base64url'),
      code_challenge_method: 'S256',
      user: SUBJECT,
    }),
  });
  const code = new URL(authorize.headers.get('location')).searchParams.get('code');
  const token = await fetch(`${ISSUER_URL}/token`, {
    method: 'POST',
    body: new URLSearchParams({ grant_type: 'authorization_code', code, redirect_uri: redirectUri, client_id: 'atracciones-web', code_verifier: verifier }),
  });
  return (await token.json()).access_token;
}

/** Formato de EF Core DateTimeOffsetToBinaryConverter: ((ticks / 1000) << 11) | offsetMinutes. */
function efNow() {
  const ticks = BigInt(Date.now()) * 10_000n + 621_355_968_000_000_000n;
  return (ticks / 1000n) << 11n;
}

const guid = () => randomUUID().toUpperCase();

await waitFor(`${ISSUER_URL}/.well-known/openid-configuration`, 'El emisor de desarrollo');
await waitFor(`${API}/health`, 'El API');

// 1. Perfil local del administrador (el API lo crea a partir de los claims del token).
const token = await adminToken();
const register = await fetch(`${API}/api/v1/auth/register`, {
  method: 'POST',
  headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
  body: JSON.stringify({ billingName: 'Admin de desarrollo', billingEmail: 'admin.dev@example.test', billingAddress: 'Quito' }),
});
if (![200, 201, 409].includes(register.status)) {
  throw new Error(`No se pudo registrar el perfil del administrador (HTTP ${register.status}): ${await register.text()}`);
}

// 2. Rol y asignación en la base de desarrollo.
const db = new DatabaseSync(dbPath);
db.exec('BEGIN');
try {
  const user = db.prepare('SELECT Id FROM users WHERE OauthIssuer = ? AND OauthSubject = ?').get(ISSUER, SUBJECT);
  if (!user) throw new Error('No se encontró el usuario dev-admin-1 en la base de datos.');

  let role = db.prepare('SELECT Id FROM roles WHERE Name = ?').get('admin');
  if (!role) {
    role = { Id: guid() };
    db.prepare('INSERT INTO roles (Id, Name, Description, CreatedAt) VALUES (?, ?, ?, ?)').run(
      role.Id,
      'admin',
      'Administrador del catálogo (desarrollo local)',
      efNow(),
    );
  }
  db.prepare('INSERT OR IGNORE INTO role_permissions (RoleId, Permission) VALUES (?, ?)').run(role.Id, 'catalog:write');

  const active = db
    .prepare('SELECT Id FROM user_roles WHERE UserId = ? AND RoleId = ? AND RevokedAt IS NULL')
    .get(user.Id, role.Id);
  if (!active) {
    db.prepare('INSERT INTO user_roles (Id, UserId, RoleId, AssignedByUserId, Reason, AssignedAt, RevokedAt) VALUES (?, ?, ?, NULL, ?, ?, NULL)').run(
      guid(),
      user.Id,
      role.Id,
      'Provisionado por tools/dev-oauth/grant-admin.mjs (solo desarrollo)',
      efNow(),
    );
  }
  db.exec('COMMIT');
  console.log(`[grant-admin] dev-admin-1 (${user.Id}) tiene el rol admin con catalog:write.`);
} catch (error) {
  db.exec('ROLLBACK');
  throw error;
} finally {
  db.close();
}
