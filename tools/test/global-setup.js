// Arranca un PostgreSQL embebido efímero para las pruebas e2e (puerto y directorio propios: no toca los datos locales).
const { mkdtempSync } = require('node:fs');
const { tmpdir } = require('node:os');
const { join } = require('node:path');

module.exports = async () => {
  const { default: EmbeddedPostgres } = await import('embedded-postgres');
  const port = Number(process.env.TEST_DB_PORT ?? 54330);
  const pg = new EmbeddedPostgres({
    databaseDir: mkdtempSync(join(tmpdir(), 'tourgirls-test-')),
    user: 'postgres',
    password: 'tests',
    port,
    persistent: false,
    onLog: () => {},
  });
  await pg.initialise();
  await pg.start();
  await pg.createDatabase('tourgirls_test');
  globalThis.__EMBEDDED_PG__ = pg;
  process.env.TEST_DATABASE_URL = `postgres://postgres:tests@localhost:${port}/tourgirls_test`;
};
