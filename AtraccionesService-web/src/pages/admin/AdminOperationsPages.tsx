import { Link } from 'react-router-dom';
import { adminApi, type AdminOrder, type AdminPayment, type AdminReservation } from '@/api/admin';
import { Badge } from '@/components/common/Badge';
import { OrderStatusBadge, PaymentStatusBadge, ReservationStatusBadge } from '@/components/profile/StatusBadge';
import { formatDate, formatDateTime } from '@/utils/dates';
import { formatMoney, shortCode } from '@/utils/formatters';
import { paths } from '@/utils/routes';
import { AdminTable, Sub, type Column } from './AdminTable';

const RESERVATION_STATUSES = [
  { value: 'CONFIRMED', label: 'Confirmada' },
  { value: 'PENDING', label: 'Pendiente' },
  { value: 'CANCELLED', label: 'Cancelada' },
];

const ORDER_STATUSES = [
  { value: 'PENDING_PAYMENT', label: 'Pendiente de pago' },
  { value: 'PAID', label: 'Pagado' },
  { value: 'FULFILLED', label: 'Completado' },
  { value: 'CANCELLED', label: 'Cancelado' },
  { value: 'PARTIALLY_REFUNDED', label: 'Reembolso parcial' },
  { value: 'REFUNDED', label: 'Reembolsado' },
];

const PAYMENT_STATUSES = [
  { value: 'SETTLED', label: 'Liquidado' },
  { value: 'AUTHORIZED', label: 'Autorizado' },
  { value: 'PENDING', label: 'Pendiente' },
  { value: 'REJECTED', label: 'Rechazado' },
  { value: 'FAILED', label: 'Fallido' },
  { value: 'CANCELLED', label: 'Cancelado' },
  { value: 'REFUNDED', label: 'Reembolsado' },
];

const reservationColumns: Column<AdminReservation>[] = [
  { header: 'Código', cell: (r) => <span className="font-mono text-xs">{shortCode(r.id)}</span> },
  {
    header: 'Atracción',
    cell: (r) => (
      <Link to={paths.attraction(r.attractionId)} className="font-semibold hover:underline">
        {r.attractionName}
      </Link>
    ),
  },
  { header: 'Cliente', cell: (r) => <>{r.customerName}<Sub>{r.customerEmail}</Sub></> },
  { header: 'Visita', cell: (r) => <>{formatDate(r.date)}<Sub>{r.time}</Sub></> },
  { header: 'Entradas', cell: (r) => r.ticketCount, align: 'right' },
  { header: 'Total', cell: (r) => formatMoney(r.total.amount, r.total.currency), align: 'right' },
  {
    header: 'Estado',
    cell: (r) => (
      <>
        <ReservationStatusBadge status={r.status} />
        {r.cancellationReason && <Sub>{r.cancellationReason}</Sub>}
      </>
    ),
  },
  { header: 'Creada', cell: (r) => <span className="text-xs">{formatDateTime(r.createdAt)}</span> },
];

/** Reservas de todos los clientes (incluidas las que genera la compra directa). */
export function AdminReservationsPage() {
  return (
    <AdminTable
      title="Reservas"
      load={adminApi.reservations}
      columns={reservationColumns}
      rowKey={(r) => r.id}
      searchPlaceholder="Cliente, correo o atracción"
      statuses={RESERVATION_STATUSES}
      dateLabel="Visita"
    />
  );
}

const orderColumns: Column<AdminOrder>[] = [
  { header: 'Pedido', cell: (o) => <span className="font-mono text-xs">{shortCode(o.id)}</span> },
  { header: 'Cliente', cell: (o) => o.customerEmail },
  {
    header: 'Atracción',
    cell: (o) => (
      <>
        {o.attractionName ?? '—'}
        {o.serviceDate && <Sub>{`${formatDate(o.serviceDate)} · ${o.serviceTime} · ${o.quantity} ${o.quantity === 1 ? 'entrada' : 'entradas'}`}</Sub>}
      </>
    ),
  },
  { header: 'Origen', cell: (o) => <Badge tone="neutral">{o.source === 'PURCHASE' ? 'Compra directa' : 'Reserva'}</Badge> },
  { header: 'Total', cell: (o) => formatMoney(o.total.amount, o.total.currency), align: 'right' },
  { header: 'Estado', cell: (o) => <OrderStatusBadge status={o.status} /> },
  { header: 'Pago', cell: (o) => (o.paymentStatus ? <PaymentStatusBadge status={o.paymentStatus} /> : <span className="text-ink-muted">Sin pago</span>) },
  { header: 'Creado', cell: (o) => <span className="text-xs">{formatDateTime(o.createdAt)}</span> },
];

export function AdminOrdersPage() {
  return (
    <AdminTable
      title="Pedidos"
      load={adminApi.orders}
      columns={orderColumns}
      rowKey={(o) => o.id}
      searchPlaceholder="Correo, código o atracción"
      statuses={ORDER_STATUSES}
      dateLabel="Creado"
    />
  );
}

const paymentColumns: Column<AdminPayment>[] = [
  { header: 'Referencia', cell: (p) => <span className="font-mono text-xs">{p.gatewayReference}</span> },
  { header: 'Pedido', cell: (p) => <span className="font-mono text-xs">{shortCode(p.orderId)}</span> },
  { header: 'Cliente', cell: (p) => p.customerEmail },
  { header: 'Método', cell: (p) => (p.paymentMethod === 'CARD' ? 'Tarjeta' : 'Transferencia') },
  { header: 'Importe', cell: (p) => formatMoney(p.amount.amount, p.amount.currency), align: 'right' },
  { header: 'Intentos', cell: (p) => p.attempts, align: 'right' },
  {
    header: 'Estado',
    cell: (p) => (
      <>
        <PaymentStatusBadge status={p.status} />
        {p.failureReason && <Sub>{p.failureReason}</Sub>}
      </>
    ),
  },
  { header: 'Fecha', cell: (p) => <span className="text-xs">{formatDateTime(p.processedAt ?? p.createdAt)}</span> },
];

/** Simulaciones de pago. No hay datos de tarjeta: solo la referencia simulada. */
export function AdminPaymentsPage() {
  return (
    <AdminTable
      title="Pagos"
      load={adminApi.payments}
      columns={paymentColumns}
      rowKey={(p) => p.id}
      searchPlaceholder="Correo, referencia o pedido"
      statuses={PAYMENT_STATUSES}
      dateLabel="Fecha"
    />
  );
}
