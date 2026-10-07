/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_CATALOG_SOURCE?: string;
  readonly VITE_OAUTH_AUTHORIZATION_URL?: string;
  readonly VITE_OAUTH_TOKEN_URL?: string;
  readonly VITE_OAUTH_LOGOUT_URL?: string;
  readonly VITE_OAUTH_CLIENT_ID?: string;
  readonly VITE_OAUTH_AUDIENCE?: string;
  readonly VITE_OAUTH_REDIRECT_URI?: string;
  readonly VITE_OAUTH_SCOPES?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
