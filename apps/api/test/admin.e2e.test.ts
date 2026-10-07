import { randomUUID } from 'node:crypto';
import type { INestApplication } from '@nestjs/common';
import request from 'supertest';
import type { DataSource } from 'typeorm';
import { ALL_SCOPES, grantCatalogWrite, inDays, SEEDED, startApi, token } from './helpers';

let app: INestApplication;
let dataSource: DataSource;
let http: ReturnType<typeof request>;

beforeAll(async () => {
  ({ app, dataSource } = await startApi());
  http = request(app.getHttpServer());
});

afterAll(async () => {
  await app.close();
  await dataSource.destroy();
});

const idem = () => ({ 'Idempotency-Key': randomUUID() });

async function customer(scope = ALL_SCOPES, billingName?: string) {
  const sub = randomUUID();
  const email = `${sub.slice(0, 8)}@admin-tests.com`;
  const auth = `Bearer ${await token({ sub, email, scope })}`;
  await http.post('/api/v1/auth/register').set('Authorization', auth).send(billingName ? { billingName } : {}).expect(201);
  return { sub, email, auth };
}

async function admin() {
  const user = await customer(`${ALL_SCOPES} attractions:write`, 'Admin Pruebas');
  await grantCatalogWrite(dataSource, user.email);
  return user;
}

describe('panel de administración', () => {
  it('exige el scope de escritura y el permiso local admin:manage', async () => {
    const buyer = await customer();
    await http.get('/api/v1/admin/summary').set('Authorization', buyer.auth).expect(403);
    const writer = await customer(`${ALL_SCOPES} attractions:write`);
    const denied = await http.get('/api/v1/admin/customers').set('Authorization', writer.auth).expect(403);
    expect(denied.body.detail).toContain('admin:manage');
    const { auth } = await admin();
    await http.get('/api/v1/admin/summary').set('Authorization', auth).expect(200);
  });

  it('lista clientes, reservas, pedidos y pagos de todos los usuarios', async () => {
    const { auth } = await admin();
    const buyer = await customer(ALL_SCOPES, 'Cliente Visible');
    const purchase = await http
      .post(`/api/v1/attractions/${SEEDED.mitad}/purchase`)
      .set('Authorization', buyer.auth)
      .set(idem())
      .send({ date: inDays(20), time: '09:00', quantity: 2 })
      .expect(201);
    await http
      .post('/api/v1/payments/simulations')
      .set('Authorization', buyer.auth)
      .set(idem())
      .send({ orderId: purchase.body.orderId, paymentMethod: 'CARD', amount: 50, currency: 'USD' })
      .expect(201);

    const customers = await http.get('/api/v1/admin/customers').query({ search: buyer.email }).set('Authorization', auth).expect(200);
    expect(customers.body).toMatchObject({ totalItems: 1, currentPage: 1 });
    expect(customers.body.data[0]).toMatchObject({ email: buyer.email, name: 'Cliente Visible', status: 'ACTIVE', orders: 1, totalSpent: 50, roles: [] });

    const reservations = await http.get('/api/v1/admin/reservations').query({ search: buyer.email }).set('Authorization', auth).expect(200);
    expect(reservations.body.data[0]).toMatchObject({ customerEmail: buyer.email, ticketCount: 2, status: 'CONFIRMED', time: '09:00', date: inDays(20) });

    const orders = await http.get('/api/v1/admin/orders').query({ search: buyer.email, status: 'PAID' }).set('Authorization', auth).expect(200);
    expect(orders.body.data[0]).toMatchObject({ id: purchase.body.orderId, source: 'PURCHASE', quantity: 2, total: { amount: 50 }, paymentStatus: 'SETTLED' });

    const payments = await http.get('/api/v1/admin/payments').query({ search: buyer.email }).set('Authorization', auth).expect(200);
    expect(payments.body.data[0]).toMatchObject({ orderId: purchase.body.orderId, status: 'SETTLED', attempts: 1, amount: { amount: 50 } });

    const summary = await http.get('/api/v1/admin/summary').set('Authorization', auth).expect(200);
    expect(summary.body.customers).toBeGreaterThanOrEqual(2);
    expect(summary.body.revenue[0].amount).toBeGreaterThanOrEqual(50);

    const report = await http.get('/api/v1/admin/reports/sales').set('Authorization', auth).expect(200);
    expect(report.body.byAttraction.find((r: { attractionId: string }) => r.attractionId === SEEDED.mitad).tickets).toBeGreaterThanOrEqual(2);

    await http.get('/api/v1/admin/orders').query({ status: 'NOPE' }).set('Authorization', auth).expect(400);
  });

  it('gestiona la capacidad y crea franjas sin bajar de lo reservado', async () => {
    const { auth } = await admin();
    const buyer = await customer();
    const date = inDays(21);
    await http
      .post(`/api/v1/atracciones/${SEEDED.cotopaxi}/reservations`)
      .set('Authorization', buyer.auth)
      .set(idem())
      .send({ date, time: '14:00', ticketCount: 3, customerName: 'Ana', customerEmail: buyer.email })
      .expect(201);

    const slots = await http
      .get('/api/v1/admin/availability')
      .query({ attractionId: SEEDED.cotopaxi, fromDate: date, toDate: date })
      .set('Authorization', auth)
      .expect(200);
    const slot = slots.body.data.find((s: { time: string }) => s.time === '14:00');
    expect(slot).toMatchObject({ reserved: 3, capacity: 20 });

    const blocked = await http.patch(`/api/v1/admin/availability/${slot.id}`).set('Authorization', auth).send({ capacity: 2 }).expect(409);
    expect(blocked.body.detail).toContain('3');
    const updated = await http.patch(`/api/v1/admin/availability/${slot.id}`).set('Authorization', auth).send({ capacity: 3 }).expect(200);
    expect(updated.body.capacity).toBe(3);
    const full = await http.get('/api/v1/admin/availability').query({ attractionId: SEEDED.cotopaxi, status: 'FULL' }).set('Authorization', auth).expect(200);
    expect(full.body.data.map((s: { id: string }) => s.id)).toContain(slot.id);

    const created = await http
      .post('/api/v1/admin/availability')
      .set('Authorization', auth)
      .send({ attractionId: SEEDED.cotopaxi, date, time: '17:30', capacity: 8 })
      .expect(201);
    expect(created.body).toMatchObject({ time: '17:30', capacity: 8, reserved: 0 });
    await http.post('/api/v1/admin/availability').set('Authorization', auth).send({ attractionId: SEEDED.cotopaxi, date, time: '17:30', capacity: 8 }).expect(409);
    const audit = await dataSource.query(`SELECT action FROM audit_events WHERE resource_id = $1`, [slot.id]);
    expect(audit.map((a: { action: string }) => a.action)).toContain('availability.capacity_changed');
  });

  it('cambia el estado de usuarios y asigna o revoca roles', async () => {
    const me = await admin();
    const target = await customer();
    const [{ id: userId }] = await dataSource.query(`SELECT id FROM users WHERE email = $1`, [target.email]);
    const roles = await http.get('/api/v1/admin/roles').set('Authorization', me.auth).expect(200);
    const adminRole = roles.body.find((r: { name: string }) => r.name === 'admin');
    expect(adminRole.permissions).toEqual(expect.arrayContaining(['catalog:write', 'admin:manage']));

    const granted = await http.post(`/api/v1/admin/customers/${userId}/roles`).set('Authorization', me.auth).send({ roleId: adminRole.id }).expect(201);
    expect(granted.body.roles).toEqual(['admin']);
    const revoked = await http.delete(`/api/v1/admin/customers/${userId}/roles/${adminRole.id}`).set('Authorization', me.auth).expect(200);
    expect(revoked.body.roles).toEqual([]);

    const disabled = await http.put(`/api/v1/admin/customers/${userId}/status`).set('Authorization', me.auth).send({ status: 'DISABLED' }).expect(200);
    expect(disabled.body.status).toBe('DISABLED');
    await http
      .post(`/api/v1/attractions/${SEEDED.mitad}/purchase`)
      .set('Authorization', target.auth)
      .set(idem())
      .send({ date: inDays(22), time: '09:00', quantity: 1 })
      .expect(403);

    const [{ id: myId }] = await dataSource.query(`SELECT id FROM users WHERE email = $1`, [me.email]);
    await http.put(`/api/v1/admin/customers/${myId}/status`).set('Authorization', me.auth).send({ status: 'DISABLED' }).expect(409);
    await http.delete(`/api/v1/admin/customers/${myId}/roles/${adminRole.id}`).set('Authorization', me.auth).expect(409);
  });
});
