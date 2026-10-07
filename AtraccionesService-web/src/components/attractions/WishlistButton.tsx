import { Heart } from 'lucide-react';
import { useWishlistStore } from '@/stores/wishlistStore';
import { useUiStore } from '@/stores/uiStore';

export function WishlistButton({ id, name, className = '' }: { id: string; name: string; className?: string }) {
  const saved = useWishlistStore((s) => s.ids.includes(id));
  const toggle = useWishlistStore((s) => s.toggle);
  const notify = useUiStore((s) => s.notify);
  return (
    <button
      type="button"
      aria-pressed={saved}
      aria-label={saved ? `Quitar ${name} de la lista de deseos` : `Guardar ${name} en la lista de deseos`}
      onClick={(e) => {
        e.preventDefault();
        e.stopPropagation();
        toggle(id);
        notify(saved ? 'Eliminada de tu lista de deseos.' : 'Guardada en tu lista de deseos.', 'success');
      }}
      className={`inline-flex h-9 w-9 items-center justify-center rounded-full bg-white/95 text-ink shadow-card transition hover:scale-105 ${className}`}
    >
      <Heart size={18} aria-hidden="true" className={saved ? 'fill-brand-500 text-brand-500' : ''} />
    </button>
  );
}
