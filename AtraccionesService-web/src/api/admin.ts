import { createAttraction, deleteAttraction, patchAttraction, replaceAttraction } from './attractions';

/**
 * Endpoints administrativos aprobados en el contrato actual, agrupados por permiso.
 * Solo existe la gestión de catálogo (scope `attractions:write` + permiso local `catalog.write`).
 * Disponibilidad, pedidos, pagos, clientes, usuarios y reportes administrativos aún no tienen endpoints:
 * la interfaz los muestra como pendientes en lugar de inventar datos.
 */
export const catalogAdmin = {
  create: createAttraction,
  replace: replaceAttraction,
  patch: patchAttraction,
  remove: deleteAttraction,
};

export const ADMIN_SCOPE = 'attractions:write';
