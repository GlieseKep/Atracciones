import 'reflect-metadata';
import { DataSource, type DataSourceOptions } from 'typeorm';
import { ENTITIES } from './entities';
import { MIGRATIONS } from './migrations';

export interface DatabaseSettings {
  /** `postgres://usuario:clave@host:5432/base` */
  url: string;
  /** Azure Database for PostgreSQL exige TLS. */
  ssl?: boolean;
  /** Ejecuta las migraciones pendientes al iniciar. */
  migrationsRun?: boolean;
  logging?: boolean;
}

export function dataSourceOptions(settings: DatabaseSettings): DataSourceOptions {
  return {
    type: 'postgres',
    url: settings.url,
    ssl: settings.ssl ? { rejectUnauthorized: true } : undefined,
    entities: ENTITIES,
    migrations: MIGRATIONS,
    migrationsTableName: 'schema_migrations',
    migrationsRun: settings.migrationsRun ?? false,
    synchronize: false,
    logging: settings.logging ? ['error', 'warn', 'migration'] : ['error', 'migration'],
    extra: { max: 10 },
  };
}

export function createDataSource(settings: DatabaseSettings): DataSource {
  return new DataSource(dataSourceOptions(settings));
}

/** Instancia usada por la CLI de TypeORM (`migration:generate`, `migration:run`). */
export default createDataSource({
  url: process.env.DATABASE_URL ?? 'postgres://postgres:postgres@localhost:5433/tourgirls',
  ssl: process.env.DATABASE_SSL === 'true',
});
