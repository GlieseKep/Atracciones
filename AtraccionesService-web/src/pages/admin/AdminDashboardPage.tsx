import { Link } from 'react-router-dom';
import { listAttractions } from '@/api/attractions';
import { ErrorState } from '@/components/common/Feedback';
import { useAsync } from '@/hooks/useAsync';
import { ADMIN_SECTIONS } from './AdminLayout';

/** Indicadores agregados disponibles en el contrato actual y accesos según permisos. */
export default function AdminDashboardPage() {
  const catalog = useAsync((signal) => listAttractions(100, 0, signal), []);
  const items = catalog.data?.data ?? [];
  const reviews = items.reduce((sum, a) => sum + (a.ratings?.numberOfReviews ?? 0), 0);
  const avg = items.length ? items.reduce((s, a) => s + a.price.total, 0) / items.length : 0;

  const stats = [
    { label: 'Atracciones publicadas', value: catalog.data?.meta.totalItems ?? '—' },
    { label: 'Con cancelación gratuita', value: items.filter((a) => a.freeCancellation).length },
    { label: 'Opiniones acumuladas', value: reviews.toLocaleString('es-EC') },
    { label: 'Precio medio', value: avg ? `US$ ${avg.toFixed(2)}` : '—' },
  ];

  return (
    <div>
      <h1 className="text-3xl">Dashboard</h1>
      {catalog.status === 'error' ? (
        <ErrorState error={catalog.error} onRetry={catalog.retry} className="mt-6" />
      ) : (
        <dl className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {stats.map((s) => (
            <div key={s.label} className="card-surface p-5">
              <dt className="text-sm text-ink-soft">{s.label}</dt>
              <dd className="mt-1 text-3xl font-extrabold">{catalog.status === 'loading' ? <span className="skeleton inline-block h-8 w-16" /> : s.value}</dd>
            </div>
          ))}
        </dl>
      )}
      <h2 className="mt-10 text-xl">Secciones</h2>
      <ul className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {ADMIN_SECTIONS.slice(1).map(({ to, label, icon: Icon, ready }) => (
          <li key={to}>
            <Link to={to} className="card-surface flex items-center gap-3 p-4 hover:shadow-card">
              <Icon size={22} className="text-brand-500" aria-hidden="true" />
              <span className="flex-1 font-semibold">{label}</span>
              {!ready && <span className="rounded-full bg-surface px-2 py-0.5 text-xs text-ink-muted">Pendiente de contrato</span>}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
