import type { INestApplication } from '@nestjs/common';
import { decodeJwt, jwtVerify } from 'jose';
import type { Pool } from 'pg';
import request from 'supertest';
import { readConfig } from '../src/config';
import { createAuthApp } from '../src/main';

const SECRET = 'secreto-de-pruebas-con-mas-de-32-caracteres';
let app: INestApplication;
let pool: Pool;
let http: ReturnType<typeof request>;

beforeAll(async () => {
  const config = readConfig({
    DATABASE_URL: process.env.TEST_DATABASE_URL,
    AUTH_JWT_SECRET: SECRET,
    ADMIN_EMAILS: 'jefa@tourgirls.test',
  } as NodeJS.ProcessEnv);
  ({ app, pool } = await createAuthApp(config));
  await app.init();
  http = request(app.getHttpServer());
});

afterAll(async () => {
  await app.close();
  await pool.end();
});

const unique = () => `u${Date.now()}${Math.floor(Math.random() * 1e6)}@example.com`;

describe('dev-auth', () => {
  it('registra, inicia sesión y emite un JWT HS256 válido para el API', async () => {
    const email = unique();
    const registered = await http.post('/auth/register').send({ name: 'Ana Viajera', email, password: 'Tour2026!' }).expect(201);
    expect(registered.body).toMatchObject({ token_type: 'Bearer', expires_in: 3600, user: { email, name: 'Ana Viajera' } });

    const login = await http.post('/auth/login').send({ email: email.toUpperCase(), password: 'Tour2026!' }).expect(200);
    const { payload } = await jwtVerify(login.body.access_token, new TextEncoder().encode(SECRET), { issuer: 'tourgirls-auth', audience: 'tourgirls-api' });
    expect(payload).toMatchObject({ sub: registered.body.user.id, email, email_verified: false, scope: 'attractions:read attractions:book attractions:cancel' });
  });

  it('publica Swagger en /docs con el registro y el inicio de sesión', async () => {
    await http.get('/docs').expect(200);
    const spec = await http.get('/openapi.json').expect(200);
    expect(Object.keys(spec.body.paths).sort()).toEqual(['/auth/login', '/auth/register']);
    expect(spec.body.paths['/auth/login'].post.responses['200']).toBeDefined();
    expect(spec.body.components.schemas.LoginRequest.properties).toHaveProperty('password');
  });

  it('concede attractions:write solo a los correos de ADMIN_EMAILS', async () => {
    const res = await http.post('/auth/register').send({ name: 'Jefa', email: 'jefa@tourgirls.test', password: 'Admin2026!' }).expect(201);
    expect(decodeJwt(res.body.access_token).scope).toContain('attractions:write');
  });

  it('valida la contraseña y no permite correos duplicados', async () => {
    const email = unique();
    const weak = await http.post('/auth/register').send({ name: 'Ana', email, password: 'abc' }).expect(400);
    expect(weak.body.errors).toHaveProperty('password');
    await http.post('/auth/register').send({ name: 'Ana', email, password: 'Tour2026!' }).expect(201);
    const duplicated = await http.post('/auth/register').send({ name: 'Ana', email: email.toUpperCase(), password: 'Tour2026!' }).expect(409);
    expect(duplicated.body.code).toBe('EMAIL_ALREADY_REGISTERED');
  });

  it('responde igual a correos inexistentes y contraseñas incorrectas, y bloquea tras 5 intentos', async () => {
    const email = unique();
    await http.post('/auth/register').send({ name: 'Ana', email, password: 'Tour2026!' }).expect(201);
    const missing = await http.post('/auth/login').send({ email: unique(), password: 'Tour2026!' }).expect(401);
    const wrong = await http.post('/auth/login').send({ email, password: 'Incorrecta1' }).expect(401);
    expect(missing.body.detail).toBe(wrong.body.detail);
    for (let i = 0; i < 4; i++) await http.post('/auth/login').send({ email, password: 'Incorrecta1' }).expect(401);
    const locked = await http.post('/auth/login').send({ email, password: 'Tour2026!' }).expect(429);
    expect(locked.body.code).toBe('ACCOUNT_LOCKED');
    expect(Number(locked.headers['retry-after'])).toBeGreaterThan(0);
  });

  it('rechaza propiedades desconocidas y guarda la contraseña con hash', async () => {
    await http.post('/auth/register').send({ name: 'Ana', email: unique(), password: 'Tour2026!', role: 'admin' }).expect(400);
    const { rows } = await pool.query(`SELECT password_hash FROM auth_accounts LIMIT 1`);
    expect(rows[0].password_hash).toMatch(/^scrypt\$16384\$8\$1\$/);
  });
});
