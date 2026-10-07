import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

/**
 * Lista de deseos local (corazón de las tarjetas, como en Viator). Solo guarda ids de atracciones públicas:
 * no hay endpoint de favoritos en el contrato, así que vive en el navegador.
 */
interface WishlistState {
  ids: string[];
  toggle: (id: string) => void;
  has: (id: string) => boolean;
}

export const useWishlistStore = create<WishlistState>()(
  persist(
    (set, get) => ({
      ids: [],
      toggle: (id) =>
        set((state) => ({ ids: state.ids.includes(id) ? state.ids.filter((x) => x !== id) : [...state.ids, id] })),
      has: (id) => get().ids.includes(id),
    }),
    {
      name: 'ea.wishlist',
      storage: createJSONStorage(() => {
        try {
          return localStorage;
        } catch {
          return sessionStorage;
        }
      }),
    },
  ),
);
