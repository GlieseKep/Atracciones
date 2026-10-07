import { env, isOAuthConfigured } from '@/config/env';
import { useAuthStore, type SessionClaims } from '@/stores/authStore';
import { safeReturnPath } from '@/utils/routes';
import { codeChallenge, decodeJwtPayload, randomString } from './pkce';

/**
 * OAuth2 Authorization Code + PKCE contra el issuer externo. No hay login local ni tokens simulados.
 * Solo el estado temporal del flujo (state, verifier, ruta de regreso) vive en sessionStorage y se elimina
 * al procesar el callback; el access token queda únicamente en memoria.
 */

const PENDING_KEY = 'ea.oauth.pending';

interface PendingLogin {
  state: string;
  verifier: string;
  returnTo: string;
}

export class OAuthError extends Error {}

export async function beginLogin(returnTo: string): Promise<void> {
  if (!isOAuthConfigured()) {
    throw new OAuthError('El inicio de sesión no está configurado (revisa las variables VITE_OAUTH_*).');
  }
  const pending: PendingLogin = { state: randomString(16), verifier: randomString(48), returnTo: safeReturnPath(returnTo) };
  sessionStorage.setItem(PENDING_KEY, JSON.stringify(pending));

  const url = new URL(env.oauth.authorizationUrl);
  url.searchParams.set('response_type', 'code');
  url.searchParams.set('client_id', env.oauth.clientId);
  url.searchParams.set('redirect_uri', env.oauth.redirectUri);
  url.searchParams.set('scope', env.oauth.scopes);
  url.searchParams.set('state', pending.state);
  url.searchParams.set('code_challenge', await codeChallenge(pending.verifier));
  url.searchParams.set('code_challenge_method', 'S256');
  if (env.oauth.audience) url.searchParams.set('audience', env.oauth.audience);

  useAuthStore.getState().setAuthenticating();
  window.location.assign(url.toString());
}

/** Procesa `?code&state` del retorno del issuer y devuelve la ruta interna a la que volver. */
export async function completeLogin(params: URLSearchParams): Promise<string> {
  const raw = sessionStorage.getItem(PENDING_KEY);
  sessionStorage.removeItem(PENDING_KEY);

  const issuerError = params.get('error');
  if (issuerError) throw new OAuthError('El proveedor de identidad canceló o rechazó el inicio de sesión.');

  const pending = raw ? (JSON.parse(raw) as PendingLogin) : null;
  const code = params.get('code');
  if (!pending || !code || params.get('state') !== pending.state) {
    throw new OAuthError('La respuesta de inicio de sesión no es válida o expiró. Inténtalo de nuevo.');
  }

  const response = await fetch(env.oauth.tokenUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded', Accept: 'application/json' },
    body: new URLSearchParams({
      grant_type: 'authorization_code',
      code,
      redirect_uri: env.oauth.redirectUri,
      client_id: env.oauth.clientId,
      code_verifier: pending.verifier,
    }),
  }).catch(() => {
    throw new OAuthError('No pudimos contactar con el proveedor de identidad.');
  });

  if (!response.ok) throw new OAuthError('El proveedor de identidad no emitió la sesión.');
  const body = (await response.json()) as { access_token?: string; expires_in?: number; id_token?: string };
  if (!body.access_token) throw new OAuthError('El proveedor de identidad no emitió la sesión.');

  const expiresAt = Date.now() + Math.max(0, (body.expires_in ?? 3600) - 30) * 1000;
  useAuthStore.getState().setSession(body.access_token, expiresAt, claimsFrom(body.access_token, body.id_token));
  return pending.returnTo;
}

export function logout(): void {
  useAuthStore.getState().clear();
  if (env.oauth.logoutUrl) {
    const url = new URL(env.oauth.logoutUrl);
    url.searchParams.set('client_id', env.oauth.clientId);
    url.searchParams.set('post_logout_redirect_uri', window.location.origin);
    window.location.assign(url.toString());
  }
}

export function claimsFrom(accessToken: string, idToken?: string): SessionClaims {
  const access = decodeJwtPayload(accessToken) ?? {};
  const id = (idToken && decodeJwtPayload(idToken)) || {};
  const pick = (key: string) => (typeof id[key] === 'string' ? id[key] : access[key]) as string | undefined;
  const scopes = [access.scope, access.scp]
    .flatMap((v) => (Array.isArray(v) ? v : typeof v === 'string' ? v.split(' ') : []))
    .filter((s): s is string => typeof s === 'string' && s !== '');
  const roles = [access.roles, access.role]
    .flatMap((v) => (Array.isArray(v) ? v : typeof v === 'string' ? [v] : []))
    .filter((s): s is string => typeof s === 'string');
  return {
    sub: String(access.sub ?? id.sub ?? ''),
    email: pick('email'),
    name: pick('name') ?? pick('given_name'),
    scopes,
    roles,
  };
}
