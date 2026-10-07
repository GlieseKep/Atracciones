import { useEffect, useRef, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { LogIn, ShieldAlert } from 'lucide-react';
import { Button, ButtonLink } from '@/components/common/Button';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { isOAuthConfigured } from '@/config/env';
import { useAuth } from '@/hooks/useAuth';

/**
 * Rutas privadas: sin sesión válida se redirige al inicio externo de autenticación (plan §10).
 * Con `scope`, además exige ese permiso en los claims; el servidor sigue siendo quien autoriza.
 */
export function ProtectedRoute({ children, scope }: { children: ReactNode; scope?: string }) {
  const { isAuthenticated, status, signIn, hasScope, sessionExpired } = useAuth();
  const location = useLocation();
  const attempted = useRef(false);
  const configured = isOAuthConfigured();

  useEffect(() => {
    if (!isAuthenticated && status !== 'authenticating' && configured && !attempted.current) {
      attempted.current = true;
      void signIn(location.pathname + location.search);
    }
  }, [isAuthenticated, status, configured, signIn, location.pathname, location.search]);

  if (!isAuthenticated) {
    return (
      <div className="page-container flex min-h-[50vh] flex-col items-center justify-center text-center">
        {configured ? (
          <>
            <LoadingSpinner size={32} className="text-brand-500" label="Redirigiendo al inicio de sesión…" />
            <p className="mt-4 font-semibold" aria-hidden="true">
              {sessionExpired ? 'Tu sesión expiró. ' : ''}Redirigiendo al inicio de sesión…
            </p>
            <Button variant="secondary" className="mt-5" onClick={() => signIn(location.pathname + location.search)}>
              <LogIn size={16} aria-hidden="true" /> Iniciar sesión
            </Button>
          </>
        ) : (
          <>
            <ShieldAlert size={40} className="text-danger" aria-hidden="true" />
            <h1 className="mt-4 text-2xl">Inicio de sesión no disponible</h1>
            <p className="mt-2 max-w-md text-ink-soft">
              Esta sección requiere una cuenta, pero el proveedor OAuth2 no está configurado en este entorno.
            </p>
            <ButtonLink to="/" className="mt-5">
              Volver al inicio
            </ButtonLink>
          </>
        )}
      </div>
    );
  }

  if (scope && !hasScope(scope)) {
    return (
      <div className="page-container flex min-h-[50vh] flex-col items-center justify-center text-center">
        <ShieldAlert size={40} className="text-danger" aria-hidden="true" />
        <h1 className="mt-4 text-2xl">No tienes permiso para ver esta sección</h1>
        <p className="mt-2 max-w-md text-ink-soft">Tu cuenta no tiene los permisos de administración necesarios.</p>
        <ButtonLink to="/" className="mt-5">
          Volver al inicio
        </ButtonLink>
      </div>
    );
  }

  return <>{children}</>;
}
