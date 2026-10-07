import { FileClock } from 'lucide-react';

interface Props {
  title: string;
  purpose: string;
}

/**
 * Vistas administrativas previstas en el plan (§7.11) cuyos endpoints aún no existen en el contrato aprobado.
 * Se muestran como pendientes para no inventar datos ni acciones sin autorización del servidor.
 */
export default function AdminPendingPage({ title, purpose }: Props) {
  return (
    <div>
      <h1 className="text-3xl">{title}</h1>
      <div className="mt-6 flex flex-col items-center rounded-md border border-dashed border-line px-6 py-14 text-center">
        <FileClock size={40} className="text-brand-400" aria-hidden="true" />
        <h2 className="mt-4 text-lg">Pendiente de aprobación del contrato de API</h2>
        <p className="mt-2 max-w-lg text-ink-soft">{purpose}</p>
        <p className="mt-2 max-w-lg text-sm text-ink-muted">
          Esta vista se habilitará cuando el contrato publique los endpoints administrativos y sus scopes correspondientes.
        </p>
      </div>
    </div>
  );
}
