import { LogIn, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/common/Button';
import { Modal } from '@/components/common/Modal';
import { useAuth } from '@/hooks/useAuth';
import { useUiStore } from '@/stores/uiStore';

/** Diálogo para iniciar sesión antes de reservar o comprar (Viator lo pide al pagar). */
export function LoginPrompt() {
  const prompt = useUiStore((s) => s.loginPrompt);
  const close = useUiStore((s) => s.closeLoginPrompt);
  const { signIn } = useAuth();

  return (
    <Modal
      open={!!prompt}
      onClose={close}
      title="Inicia sesión para continuar"
      size="sm"
      footer={
        <>
          <Button variant="tertiary" onClick={close}>
            Ahora no
          </Button>
          <Button
            onClick={() => {
              const returnTo = prompt?.returnTo;
              close();
              void signIn(returnTo);
            }}
          >
            <LogIn size={18} aria-hidden="true" /> Iniciar sesión
          </Button>
        </>
      }
    >
      <p className="text-ink-soft">{prompt?.reason ?? 'Necesitas una cuenta para reservar y comprar experiencias.'}</p>
      <p className="mt-4 flex items-start gap-2 text-sm text-ink-soft">
        <ShieldCheck size={18} className="mt-0.5 shrink-0 text-success" aria-hidden="true" />
        Te redirigiremos al proveedor de identidad seguro (OAuth2). Nunca guardamos tu contraseña.
      </p>
    </Modal>
  );
}
