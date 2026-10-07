import { Attraction } from '@atracciones/domain';
import type { DataSource } from 'typeorm';
import { AttractionEntity } from '../entities/catalog.entities';
import { TypeOrmUnitOfWork } from '../unit-of-work';
import { SEED_ATTRACTIONS } from './catalog-data';

export const SLOT_TIMES = ['09:00', '14:00'];
export const SLOT_CAPACITY = 20;

/** Carga el catálogo inicial si la base no tiene atracciones (idempotente). */
export async function seedCatalog(dataSource: DataSource): Promise<number> {
  if ((await dataSource.getRepository(AttractionEntity).count()) > 0) return 0;
  await dataSource.transaction(async (manager) => {
    const uow = new TypeOrmUnitOfWork(manager);
    for (const seed of SEED_ATTRACTIONS) {
      await uow.attractions.add(Attraction.restore(seed.id, seed.details, seed.rating, null));
    }
  });
  return SEED_ATTRACTIONS.length;
}

/**
 * Garantiza franjas (09:00 y 14:00, 20 cupos) para los próximos `days` días de todas las atracciones.
 * Solo inserta las que faltan, así que puede ejecutarse en cada arranque sin tocar los cupos ya reservados.
 * `today` es la fecha local de negocio (`YYYY-MM-DD`).
 */
export async function ensureAvailability(dataSource: DataSource, today: string, days: number): Promise<number> {
  const rows: unknown[] = await dataSource.query(
    `INSERT INTO attraction_availability (id, attraction_id, date, time, capacity, reserved_quantity, version)
     SELECT gen_random_uuid(), a.id, d::date, t, $3, 0, 0
       FROM attractions a
       CROSS JOIN generate_series($1::date, $1::date + ($2::int - 1), interval '1 day') AS d
       CROSS JOIN unnest($4::time[]) AS t
     ON CONFLICT (attraction_id, date, time) DO NOTHING
     RETURNING id`,
    [today, days, SLOT_CAPACITY, SLOT_TIMES],
  );
  return rows.length;
}
