import { useCallback } from 'react';
import { useLocation } from 'react-router-dom';
import { ADMIN_SCOPE } from '@/api/admin';
import { login, logout } from '@/api/auth';
import { hasScope, useAuthStore } from '@/stores/authStore';
import { useUiStore } from '@/stores/uiStore';
import { errorMessage } from '@/utils/api';

export function useAuth() {
  const status = useAuthStore((s) => s.status);
  const claims = useAuthStore((s) => s.claims);
  const user = useAuthStore((s) => s.user);
  const sessionExpired = useAuthStore((s) => s.sessionExpired);
  const notify = useUiStore((s) => s.notify);
  const location = useLocation();

  const signIn = useCallback(
    async (returnTo = location.pathname + location.search) => {
      try {
        await login(returnTo);
      } catch (error) {
        useAuthStore.getState().clear();
        notify(error instanceof Error ? error.message : errorMessage(error), 'error');
      }
    },
    [location.pathname, location.search, notify],
  );

  return {
    status,
    isAuthenticated: status === 'authenticated',
    claims,
    user,
    sessionExpired,
    displayName: claims?.name ?? claims?.email ?? user?.email ?? 'Mi cuenta',
    canManageCatalog: hasScope(claims, ADMIN_SCOPE),
    hasScope: (scope: string) => hasScope(claims, scope),
    signIn,
    signOut: logout,
  };
}
