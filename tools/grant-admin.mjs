// Concede el permiso local `catalog:write` (rol `admin`) a una cuenta YA registrada en TourGirls.
//
//   node tools/grant-admin.mjs <correo>
//
// Usa DATABASE_URL (por defecto, la base local de `npm run db:local`). Funciona igual contra Azure Database for
// PostgreSQL: DATABASE_URL="postgres://...azure.com:5432/tourgirls?sslmode=require" node tools/grant-admin.mjs <correo>
//
// La cuenta debe haber iniciado sesión al menos una vez en la web (así existe su perfil en el API). Para que el token
// incluya además el scope `attractions:write`, su correo debe estar en ADMIN_EMAILS del servicio de autenticación.
import { randomUUID } from 'node:crypto';
import pg from 'pg';

const email = process.argv[2];
if (!email) {
  console.error('Uso: node tools/grant-admin.mjs <correo>');
  process.exit(1);
}

const client = new pg.Client({ connectionString: process.env.DATABASE_URL ?? 'postgres://postgres:tourgirls-local@localhost:5433/tourgirls' });
await client.connect();
try {
  await client.query('BEGIN');
  const users = await client.query('SELECT id FROM users WHERE lower(email) = lower($1)', [email]);
  if (users.rowCount === 0) {
    throw new Error(`No hay ningún perfil con el correo ${email}. Inicia sesión una vez en TourGirls con esa cuenta.`);
  }
  await client.query(
    `INSERT INTO roles (id, name, description, created_at) VALUES ($1, 'admin', 'Administrador del catálogo', now())
     ON CONFLICT (name) DO NOTHING`,
    [randomUUID()],
  );
  const { rows: [role] } = await client.query(`SELECT id FROM roles WHERE name = 'admin'`);
  await client.query(`INSERT INTO role_permissions (role_id, permission) VALUES ($1, 'catalog:write') ON CONFLICT DO NOTHING`, [role.id]);
  for (const user of users.rows) {
    await client.query(
      `INSERT INTO user_roles (id, user_id, role_id, assigned_by_user_id, reason, assigned_at, revoked_at)
       SELECT $1, $2, $3, NULL, 'Provisionado con tools/grant-admin.mjs', now(), NULL
        WHERE NOT EXISTS (SELECT 1 FROM user_roles WHERE user_id = $2 AND role_id = $3 AND revoked_at IS NULL)`,
      [randomUUID(), user.id, role.id],
    );
  }
  await client.query('COMMIT');
  console.log(`[grant-admin] ${email} tiene el rol admin con catalog:write.`);
} catch (error) {
  await client.query('ROLLBACK');
  console.error(`[grant-admin] ${error.message}`);
  process.exitCode = 1;
} finally {
  await client.end();
}
