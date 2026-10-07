import type { MigrationInterface, QueryRunner } from 'typeorm';

/**
 * Rol `admin` con los permisos locales del área administrativa: `catalog:write` (catálogo) y `admin:manage`
 * (clientes, reservas, pedidos, pagos, disponibilidad, roles y reportes). Si el rol ya existía, solo añade el permiso.
 */
export class AdminRole1791400000000 implements MigrationInterface {
  name = 'AdminRole1791400000000';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(
      `INSERT INTO roles (id, name, description, created_at)
       VALUES (gen_random_uuid(), 'admin', 'Administrador de TourGirls', now())
       ON CONFLICT (name) DO NOTHING`,
    );
    await queryRunner.query(
      `INSERT INTO role_permissions (role_id, permission)
       SELECT r.id, p.permission FROM roles r CROSS JOIN (VALUES ('catalog:write'), ('admin:manage')) AS p(permission)
        WHERE r.name = 'admin'
       ON CONFLICT DO NOTHING`,
    );
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(
      `DELETE FROM role_permissions WHERE permission = 'admin:manage' AND role_id IN (SELECT id FROM roles WHERE name = 'admin')`,
    );
  }
}
