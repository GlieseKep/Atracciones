/**
 * Identidad ya validada por la API a partir del token. `issuer + subject` identifican al usuario;
 * nunca se obtienen del cuerpo de la solicitud.
 */
export interface AuthenticatedUser {
  issuer: string;
  subject: string;
  email: string | null;
  emailVerified: boolean | null;
}

/** Permisos locales consultados además de los scopes del token. */
export const LocalPermissions = {
  CatalogWrite: 'catalog:write',
} as const;
