// Emisor OAuth2/OIDC SOLO PARA DESARROLLO LOCAL. No usar en producción.
//
// Implementa Authorization Code + PKCE (S256), metadatos OIDC y JWKS para que AtraccionesService.API
// valide los tokens igual que con un issuer real. Sin dependencias: `node tools/dev-oauth/server.mjs`.
//
// - Solo acepta redirect_uri en localhost / 127.0.0.1.
// - La clave de firma se guarda en .dev-signing-key.pem (ignorado por git) para que los tokens
//   sigan siendo válidos al reiniciar.

import { createHash, createPrivateKey, createPublicKey, generateKeyPairSync, randomBytes, randomUUID, sign } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.DEV_OAUTH_PORT ?? 5280);
const ISSUER = process.env.DEV_OAUTH_ISSUER ?? 'https://localhost/dev-issuer';
const AUDIENCE = process.env.DEV_OAUTH_AUDIENCE ?? 'atracciones-api';
const BASE_URL = `http://localhost:${PORT}`;
const TOKEN_LIFETIME = 60 * 60;
const CLIENTS = new Set(['atracciones-web', 'atracciones-swagger']);

const READER = ['attractions:read', 'attractions:book', 'attractions:cancel'];
const USERS = [
  {
    sub: 'dev-customer-1',
    email: 'cliente.dev@example.test',
    name: 'Cliente de desarrollo',
    scopes: READER,
    note: 'Usuario del seed: ya tiene una compra pagada y una reserva.',
  },
  {
    sub: 'dev-admin-1',
    email: 'admin.dev@example.test',
    name: 'Admin de desarrollo',
    scopes: [...READER, 'attractions:write'],
    note: 'Incluye attractions:write. Para editar el catálogo ejecuta antes `node tools/dev-oauth/grant-admin.mjs`.',
  },
];

// ---------------------------------------------------------------- claves
const here = dirname(fileURLToPath(import.meta.url));
const keyFile = join(here, '.dev-signing-key.pem');
if (!existsSync(keyFile)) {
  const { privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  writeFileSync(keyFile, privateKey.export({ type: 'pkcs8', format: 'pem' }));
}
const privateKey = createPrivateKey(readFileSync(keyFile));
const publicJwk = createPublicKey(privateKey).export({ format: 'jwk' });
const KID = createHash('sha256').update(publicJwk.n).digest('base64url').slice(0, 16);

const b64url = (value) => Buffer.from(typeof value === 'string' ? value : JSON.stringify(value)).toString('base64url');

function signJwt(payload) {
  const head = b64url({ alg: 'RS256', typ: 'JWT', kid: KID });
  const body = b64url(payload);
  const signature = sign('RSA-SHA256', Buffer.from(`${head}.${body}`), privateKey).toString('base64url');
  return `${head}.${body}.${signature}`;
}

// ---------------------------------------------------------------- utilidades HTTP
const codes = new Map(); // code -> { clientId, redirectUri, challenge, user, nonce, expiresAt }

function isLocalUrl(value) {
  try {
    const url = new URL(value);
    return ['localhost', '127.0.0.1'].includes(url.hostname) && ['http:', 'https:'].includes(url.protocol);
  } catch {
    return false;
  }
}

function cors(req, res) {
  const origin = req.headers.origin;
  if (origin && isLocalUrl(origin)) {
    res.setHeader('Access-Control-Allow-Origin', origin);
    res.setHeader('Vary', 'Origin');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  }
}

function json(res, status, body) {
  res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(body));
}

function html(res, status, body) {
  res.writeHead(status, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(body);
}

const escapeHtml = (s) => String(s).replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);

async function readForm(req) {
  let raw = '';
  for await (const chunk of req) raw += chunk;
  return new URLSearchParams(raw);
}

// ---------------------------------------------------------------- endpoints
function metadata(res) {
  json(res, 200, {
    issuer: ISSUER,
    authorization_endpoint: `${BASE_URL}/authorize`,
    token_endpoint: `${BASE_URL}/token`,
    jwks_uri: `${BASE_URL}/jwks`,
    end_session_endpoint: `${BASE_URL}/logout`,
    response_types_supported: ['code'],
    grant_types_supported: ['authorization_code'],
    code_challenge_methods_supported: ['S256'],
    subject_types_supported: ['public'],
    id_token_signing_alg_values_supported: ['RS256'],
    token_endpoint_auth_methods_supported: ['none'],
    scopes_supported: ['openid', 'profile', 'email', ...READER, 'attractions:write'],
  });
}

function validateAuthorize(params) {
  if (params.get('response_type') !== 'code') return 'response_type debe ser "code".';
  if (!CLIENTS.has(params.get('client_id') ?? '')) return `client_id desconocido. Usa: ${[...CLIENTS].join(', ')}.`;
  if (!isLocalUrl(params.get('redirect_uri') ?? '')) return 'redirect_uri debe apuntar a localhost.';
  if (!params.get('code_challenge') || params.get('code_challenge_method') !== 'S256') return 'Se requiere PKCE con S256.';
  return null;
}

function authorizePage(params) {
  const error = validateAuthorize(params);
  if (error) return `<p class="err">${escapeHtml(error)}</p>`;
  const hidden = [...params.entries()]
    .map(([k, v]) => `<input type="hidden" name="${escapeHtml(k)}" value="${escapeHtml(v)}">`)
    .join('');
  const options = USERS.map(
    (u, i) => `<label class="user"><input type="radio" name="user" value="${u.sub}" ${i === 0 ? 'checked' : ''}>
      <span><strong>${escapeHtml(u.name)}</strong><br><small>${escapeHtml(u.email)} · ${u.scopes.join(' ')}</small><br><small>${escapeHtml(u.note)}</small></span></label>`,
  ).join('');
  return `<form method="post" action="/authorize">${hidden}
    <p>La aplicación <code>${escapeHtml(params.get('client_id'))}</code> solicita acceso a tu cuenta.</p>
    ${options}
    <button type="submit">Iniciar sesión</button>
    <a href="${escapeHtml(params.get('redirect_uri'))}?error=access_denied&state=${encodeURIComponent(params.get('state') ?? '')}">Cancelar</a>
  </form>`;
}

const page = (content) => `<!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Dev OAuth · Atracciones</title><style>
body{font-family:system-ui,sans-serif;background:#FFF9F7;color:#332326;display:grid;place-items:center;min-height:100vh;margin:0;padding:16px}
main{background:#fff;border:1px solid #E7E1E2;border-radius:16px;padding:28px;max-width:460px;width:100%;box-shadow:0 8px 28px rgba(51,35,38,.12)}
h1{font-size:20px;margin:0 0 4px}.tag{font-size:12px;color:#B93D4A;font-weight:600}
.user{display:flex;gap:12px;align-items:flex-start;border:1px solid #E7E1E2;border-radius:12px;padding:12px;margin:10px 0;cursor:pointer}
.user:has(input:checked){border-color:#C94D6B;background:#FBE8E8}small{color:#6B4D50}
button{background:#C94D6B;color:#fff;border:0;border-radius:999px;padding:12px 22px;font-weight:600;font-size:15px;cursor:pointer;margin-top:8px}
a{margin-left:14px;color:#6B4D50}.err{color:#B93D4A}code{background:#F6F4F4;padding:1px 5px;border-radius:4px}
</style></head><body><main><p class="tag">EMISOR OAUTH2 DE DESARROLLO · NO USAR EN PRODUCCIÓN</p><h1>Iniciar sesión</h1>${content}</main></body></html>`;

async function authorizePost(req, res) {
  const form = await readForm(req);
  const error = validateAuthorize(form);
  const user = USERS.find((u) => u.sub === form.get('user'));
  if (error || !user) return html(res, 400, page(`<p class="err">${escapeHtml(error ?? 'Usuario no válido.')}</p>`));

  const code = randomBytes(24).toString('base64url');
  codes.set(code, {
    clientId: form.get('client_id'),
    redirectUri: form.get('redirect_uri'),
    challenge: form.get('code_challenge'),
    nonce: form.get('nonce'),
    user,
    expiresAt: Date.now() + 2 * 60 * 1000,
  });
  const target = new URL(form.get('redirect_uri'));
  target.searchParams.set('code', code);
  if (form.get('state')) target.searchParams.set('state', form.get('state'));
  res.writeHead(302, { Location: target.toString() });
  res.end();
}

async function token(req, res) {
  const form = await readForm(req);
  if (form.get('grant_type') !== 'authorization_code') return json(res, 400, { error: 'unsupported_grant_type' });

  const entry = codes.get(form.get('code') ?? '');
  codes.delete(form.get('code') ?? '');
  if (!entry || entry.expiresAt < Date.now()) return json(res, 400, { error: 'invalid_grant', error_description: 'Código inválido o expirado.' });
  if (entry.redirectUri !== form.get('redirect_uri') || entry.clientId !== form.get('client_id')) {
    return json(res, 400, { error: 'invalid_grant', error_description: 'redirect_uri o client_id no coinciden.' });
  }
  const challenge = createHash('sha256').update(form.get('code_verifier') ?? '').digest('base64url');
  if (challenge !== entry.challenge) return json(res, 400, { error: 'invalid_grant', error_description: 'PKCE no válido.' });

  const now = Math.floor(Date.now() / 1000);
  const { user } = entry;
  const common = { iss: ISSUER, sub: user.sub, iat: now, nbf: now, exp: now + TOKEN_LIFETIME, email: user.email, email_verified: true, name: user.name };
  const accessToken = signJwt({ ...common, aud: AUDIENCE, client_id: entry.clientId, scope: user.scopes.join(' '), jti: randomUUID() });
  const idToken = signJwt({ ...common, aud: entry.clientId, ...(entry.nonce ? { nonce: entry.nonce } : {}) });

  json(res, 200, { access_token: accessToken, id_token: idToken, token_type: 'Bearer', expires_in: TOKEN_LIFETIME, scope: user.scopes.join(' ') });
}

function logout(req, res, url) {
  const target = url.searchParams.get('post_logout_redirect_uri');
  if (target && isLocalUrl(target)) {
    res.writeHead(302, { Location: target });
    return res.end();
  }
  html(res, 200, page('<p>Sesión cerrada.</p>'));
}

createServer(async (req, res) => {
  cors(req, res);
  const url = new URL(req.url ?? '/', BASE_URL);
  try {
    if (req.method === 'OPTIONS') {
      res.writeHead(204);
      return res.end();
    }
    if (req.method === 'GET' && url.pathname === '/.well-known/openid-configuration') return metadata(res);
    if (req.method === 'GET' && url.pathname === '/jwks') return json(res, 200, { keys: [{ ...publicJwk, kid: KID, alg: 'RS256', use: 'sig' }] });
    if (req.method === 'GET' && url.pathname === '/authorize') return html(res, 200, page(authorizePage(url.searchParams)));
    if (req.method === 'POST' && url.pathname === '/authorize') return await authorizePost(req, res);
    if (req.method === 'POST' && url.pathname === '/token') return await token(req, res);
    if (url.pathname === '/logout') return logout(req, res, url);
    json(res, 404, { error: 'not_found' });
  } catch (error) {
    console.error(error);
    json(res, 500, { error: 'server_error' });
  }
}).listen(PORT, 'localhost', () => {
  console.log(`[dev-oauth] Emisor de desarrollo en ${BASE_URL} (issuer "${ISSUER}", audience "${AUDIENCE}")`);
});
