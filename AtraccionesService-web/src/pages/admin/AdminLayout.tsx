import { NavLink, Outlet } from 'react-router-dom';
import { BarChart3, CalendarRange, ClipboardList, CreditCard, LayoutDashboard, Map, Receipt, UserCog, Users } from 'lucide-react';

export const ADMIN_SECTIONS = [
  { to: '/admin', label: 'Dashboard', icon: LayoutDashboard, end: true, ready: true },
  { to: '/admin/catalogo', label: 'Catálogo', icon: Map, ready: true },
  { to: '/admin/disponibilidad', label: 'Disponibilidad', icon: CalendarRange, ready: false },
  { to: '/admin/reservas', label: 'Reservas', icon: ClipboardList, ready: false },
  { to: '/admin/pedidos', label: 'Pedidos', icon: Receipt, ready: false },
  { to: '/admin/pagos', label: 'Pagos', icon: CreditCard, ready: false },
  { to: '/admin/clientes', label: 'Clientes', icon: Users, ready: false },
  { to: '/admin/usuarios', label: 'Usuarios y roles', icon: UserCog, ready: false },
  { to: '/admin/reportes', label: 'Reportes', icon: BarChart3, ready: false },
];

/** Layout del área administrativa. La ocultación en el frontend no sustituye la autorización del servidor. */
export default function AdminLayout() {
  return (
    <div className="page-container pt-8">
      <p className="text-sm font-semibold uppercase tracking-wider text-brand-500">Administración</p>
      <div className="mt-4 grid grid-cols-1 gap-8 lg:grid-cols-[220px_1fr]">
        <nav aria-label="Administración">
          <ul className="scrollbar-none flex gap-1 overflow-x-auto lg:flex-col">
            {ADMIN_SECTIONS.map(({ to, label, icon: Icon, end, ready }) => (
              <li key={to} className="shrink-0">
                <NavLink
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    `flex items-center gap-2.5 rounded-sm px-3 py-2 text-sm font-semibold ${
                      isActive ? 'bg-brand-50 text-brand-600' : 'hover:bg-surface'
                    } ${ready ? '' : 'text-ink-muted'}`
                  }
                >
                  <Icon size={17} aria-hidden="true" /> {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
        <div className="min-w-0">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
