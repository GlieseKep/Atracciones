import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { AttractionCard } from '@/components/attractions/AttractionCard';
import { AvailabilitySelector } from '@/components/reservation/AvailabilitySelector';
import { SearchBar } from '@/components/search/SearchBar';
import { Navbar } from '@/components/layout/Navbar';
import { DEMO_ATTRACTIONS } from '@/features/attractions/demoCatalog';
import { ProtectedRoute } from '@/features/auth/ProtectedRoute';
import { useAuthStore } from '@/stores/authStore';
import { useWishlistStore } from '@/stores/wishlistStore';
import { today } from '@/utils/dates';

const loginMock = vi.fn();
vi.mock('@/features/auth/oauth', async (original) => ({
  ...(await original<typeof import('@/features/auth/oauth')>()),
  beginLogin: (returnTo: string) => loginMock(returnTo),
}));
vi.mock('@/config/env', async (original) => {
  const mod = await original<typeof import('@/config/env')>();
  return { ...mod, isOAuthConfigured: () => true };
});

function LocationProbe() {
  const location = useLocation();
  return <p data-testid="location">{location.pathname + location.search}</p>;
}

const teleferico = DEMO_ATTRACTIONS[0];

beforeEach(() => {
  loginMock.mockReset();
  useAuthStore.getState().clear();
  useWishlistStore.setState({ ids: [] });
});

describe('AttractionCard', () => {
  it('muestra nombre, valoración, duración, cancelación y precio "desde"', () => {
    render(
      <MemoryRouter>
        <AttractionCard attraction={teleferico} />
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: teleferico.name })).toHaveAttribute('href', `/atracciones/${teleferico.id}`);
    expect(screen.getByRole('img', { name: /Valoración 4,6 de 5/ })).toBeInTheDocument();
    expect(screen.getByText('2 h')).toBeInTheDocument();
    expect(screen.getByText('Cancelación gratuita')).toBeInTheDocument();
    expect(screen.getByText('US$ 9,50')).toBeInTheDocument();
    expect(screen.getByText('Más vendido')).toBeInTheDocument();
  });

  it('guarda la atracción en la lista de deseos', async () => {
    render(
      <MemoryRouter>
        <AttractionCard attraction={teleferico} />
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole('button', { name: /Guardar .* lista de deseos/ }));
    expect(useWishlistStore.getState().ids).toContain(teleferico.id);
    expect(screen.getByRole('button', { name: /Quitar .* lista de deseos/ })).toHaveAttribute('aria-pressed', 'true');
  });
});

describe('SearchBar', () => {
  it('convierte un destino conocido en filtro de ciudad', async () => {
    render(
      <MemoryRouter>
        <SearchBar />
        <LocationProbe />
      </MemoryRouter>,
    );
    await userEvent.type(screen.getByLabelText('¿Adónde vas?'), 'quito');
    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
    expect(screen.getByTestId('location')).toHaveTextContent('/atracciones?ciudad=Quito');
  });

  it('busca texto libre', async () => {
    render(
      <MemoryRouter>
        <SearchBar variant="compact" />
        <LocationProbe />
      </MemoryRouter>,
    );
    await userEvent.type(screen.getByLabelText('Buscar lugares y cosas que hacer'), 'volcán');
    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
    expect(screen.getByTestId('location')).toHaveTextContent('/atracciones?q=volc%C3%A1n');
  });
});

describe('AvailabilitySelector', () => {
  it('lista franjas y deshabilita las que no tienen plazas suficientes', async () => {
    const onChange = vi.fn();
    render(<AvailabilitySelector attractionId={teleferico.id} date={today()} quantity={100} value="" onChange={onChange} />);
    const radios = await screen.findAllByRole('radio');
    expect(radios).toHaveLength(2);
    radios.forEach((r) => expect(r).toBeDisabled());
  });

  it('pide fecha antes de mostrar horarios', () => {
    render(<AvailabilitySelector attractionId={teleferico.id} date="" quantity={1} value="" onChange={vi.fn()} />);
    expect(screen.getByText('Elige una fecha para ver los horarios.')).toBeInTheDocument();
  });
});

describe('Navbar', () => {
  it('muestra "Iniciar sesión" sin sesión y el menú de cuenta con sesión', () => {
    const { unmount } = render(
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>,
    );
    expect(screen.getByRole('button', { name: /Iniciar sesión/ })).toBeInTheDocument();
    unmount();

    useAuthStore.getState().setSession('t', Date.now() + 60_000, { sub: 'u', name: 'Ana', scopes: [], roles: [] });
    render(
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>,
    );
    expect(screen.getByRole('button', { name: /Ana/ })).toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'Principal' })).getByText('Mis reservas')).toBeInTheDocument();
  });
});

describe('ProtectedRoute', () => {
  const renderAt = (path: string, scope?: string) =>
    render(
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="/perfil"
            element={
              <ProtectedRoute scope={scope}>
                <p>Contenido privado</p>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>,
    );

  it('redirige al inicio de sesión externo conservando la ruta de regreso', () => {
    renderAt('/perfil?tab=reservas');
    expect(screen.queryByText('Contenido privado')).not.toBeInTheDocument();
    expect(loginMock).toHaveBeenCalledWith('/perfil?tab=reservas');
  });

  it('muestra el contenido con sesión válida', () => {
    useAuthStore.getState().setSession('t', Date.now() + 60_000, { sub: 'u', scopes: ['attractions:read'], roles: [] });
    renderAt('/perfil');
    expect(screen.getByText('Contenido privado')).toBeInTheDocument();
  });

  it('deniega rutas administrativas sin el scope requerido', () => {
    useAuthStore.getState().setSession('t', Date.now() + 60_000, { sub: 'u', scopes: ['attractions:read'], roles: [] });
    renderAt('/perfil', 'attractions:write');
    expect(screen.queryByText('Contenido privado')).not.toBeInTheDocument();
    expect(screen.getByText('No tienes permiso para ver esta sección')).toBeInTheDocument();
  });
});
