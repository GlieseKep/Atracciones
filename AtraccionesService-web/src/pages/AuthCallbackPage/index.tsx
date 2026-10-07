import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ensureProfile, handleCallback } from '@/api/auth';
import { Button } from '@/components/common/Button';
import { Alert } from '@/components/common/Feedback';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { useAuthStore } from '@/stores/authStore';
import { errorMessage } from '@/utils/api';
import { safeReturnPath } from '@/utils/routes';

/** Procesa el retorno del issuer OAuth2, crea el perfil local si hace falta y redirige a una ruta interna segura. */
export default function AuthCallbackPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return; // StrictMode monta dos veces: el código solo puede canjearse una vez.
    started.current = true;
    // Quita code/state de la barra de direcciones y del historial.
    window.history.replaceState(null, '', window.location.pathname);

    (async () => {
      try {
        const returnTo = await handleCallback(params);
        try {
          useAuthStore.getState().setUser(await ensureProfile());
        } catch {
          /* el perfil se reintentará al usarlo; la sesión OAuth2 ya es válida */
        }
        navigate(safeReturnPath(returnTo), { replace: true });
      } catch (e) {
        useAuthStore.getState().clear();
        setError(e instanceof Error && e.name !== 'ApiError' ? e.message : errorMessage(e));
      }
    })();
  }, [navigate, params]);

  return (
    <div className="page-container flex min-h-[50vh] flex-col items-center justify-center text-center">
      {error ? (
        <div className="max-w-md">
          <Alert tone="error">{error}</Alert>
          <Button className="mt-6" onClick={() => navigate('/', { replace: true })}>
            Volver al inicio
          </Button>
        </div>
      ) : (
        <>
          <LoadingSpinner size={36} className="text-brand-500" label="Iniciando sesión…" />
          <p className="mt-4 text-lg font-semibold" aria-hidden="true">
            Iniciando sesión…
          </p>
        </>
      )}
    </div>
  );
}
