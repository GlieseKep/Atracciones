import { AttractionCard, AttractionCardSkeleton } from '@/components/attractions/AttractionCard';
import { ButtonLink } from '@/components/common/Button';
import { EmptyState, ErrorState } from '@/components/common/Feedback';
import { useAllAttractions } from '@/hooks/useAttractions';
import { useWishlistStore } from '@/stores/wishlistStore';
import { paths } from '@/utils/routes';

/** Lista de deseos (corazón de Viator), guardada en este navegador. */
export default function WishlistPage() {
  const ids = useWishlistStore((s) => s.ids);
  const catalog = useAllAttractions();
  const items = (catalog.data ?? []).filter((a) => ids.includes(a.id));

  return (
    <div className="page-container pt-8">
      <h1 className="text-3xl">Lista de deseos</h1>
      <p className="mt-1 text-ink-soft">Guarda experiencias para compararlas y reservarlas más tarde.</p>
      <div className="mt-8">
        {catalog.status === 'error' ? (
          <ErrorState error={catalog.error} onRetry={catalog.retry} />
        ) : catalog.status === 'loading' && !catalog.data ? (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
            {[0, 1, 2, 3].map((i) => (
              <AttractionCardSkeleton key={i} />
            ))}
          </div>
        ) : items.length === 0 ? (
          <EmptyState title="Tu lista de deseos está vacía" action={<ButtonLink to={paths.attractions()}>Descubrir experiencias</ButtonLink>}>
            Pulsa el corazón de cualquier experiencia para guardarla aquí.
          </EmptyState>
        ) : (
          <ul className="grid gap-x-5 gap-y-8 sm:grid-cols-2 lg:grid-cols-4">
            {items.map((a) => (
              <li key={a.id}>
                <AttractionCard attraction={a} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
