// PostgreSQL local para desarrollo (sin instalar nada: binarios de `embedded-postgres`).
// Datos persistentes en .data/postgres. Uso: `npm run db:local` (Ctrl+C para detenerlo).
//
// Credenciales SOLO de desarrollo local; en Azure se usa DATABASE_URL de Azure Database for PostgreSQL.
import EmbeddedPostgres from 'embedded-postgres';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const port = Number(process.env.LOCAL_DB_PORT ?? 5433);
const databaseDir = join(root, '.data', 'postgres');
const user = 'postgres';
const password = 'tourgirls-local';
const databases = ['tourgirls'];

// UTF-8 como en Azure (en Windows el valor por defecto sería WIN1252). Solo aplica al crear el clúster.
const initdbFlags = ['--encoding=UTF8', '--locale=C'];
const pg = new EmbeddedPostgres({
  databaseDir, user, password, port, persistent: true, initdbFlags, onLog: () => {}, onError: (e) => console.error(String(e)),
});

if (!existsSync(join(databaseDir, 'PG_VERSION'))) {
  console.log('[db] Inicializando el clúster en .data/postgres ...');
  await pg.initialise();
}
await pg.start();
for (const name of databases) {
  try {
    await pg.createDatabase(name);
    console.log(`[db] Base de datos '${name}' creada.`);
  } catch {
    /* ya existe */
  }
}
console.log(`[db] PostgreSQL listo en el puerto ${port}.`);
console.log(`[db] DATABASE_URL=postgres://${user}:${password}@localhost:${port}/tourgirls`);

const stop = async () => {
  console.log('[db] Deteniendo PostgreSQL ...');
  await pg.stop();
  process.exit(0);
};
process.on('SIGINT', stop);
process.on('SIGTERM', stop);
setInterval(() => {}, 1 << 30);
