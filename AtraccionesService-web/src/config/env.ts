/** Configuración pública del frontend. Nunca contiene secretos: el cliente OAuth2 es público (PKCE). */

export type CatalogSource = 'auto' | 'api' | 'demo';

function read(name: keyof ImportMetaEnv, fallback = ''): string {
  const value = import.meta.env[name];
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : fallback;
}

function catalogSource(): CatalogSource {
  const value = read('VITE_CATALOG_SOURCE', 'auto');
  return value === 'api' || value === 'demo' ? value : 'auto';
}

export const env = {
  apiBaseUrl: read('VITE_API_BASE_URL', 'http://localhost:5276/api/v1').replace(/\/$/, ''),
  catalogSource: catalogSource(),
  oauth: {
    authorizationUrl: read('VITE_OAUTH_AUTHORIZATION_URL'),
    tokenUrl: read('VITE_OAUTH_TOKEN_URL'),
    logoutUrl: read('VITE_OAUTH_LOGOUT_URL'),
    clientId: read('VITE_OAUTH_CLIENT_ID'),
    audience: read('VITE_OAUTH_AUDIENCE'),
    redirectUri: read('VITE_OAUTH_REDIRECT_URI', `${window.location.origin}/auth/callback`),
    scopes: read('VITE_OAUTH_SCOPES', 'openid profile email attractions:read attractions:book attractions:cancel'),
  },
} as const;

export const isOAuthConfigured = () =>
  env.oauth.authorizationUrl !== '' && env.oauth.tokenUrl !== '' && env.oauth.clientId !== '';
