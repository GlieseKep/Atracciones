import { Body, Controller, Delete, Get, HttpCode, Inject, Param, Patch, Post, Put, Query, Res } from '@nestjs/common';
import { ApiBearerAuth, ApiHeader, ApiOperation, ApiTags } from '@nestjs/swagger';
import { LocalPermissions, type AdminListQuery as ListQuery, type AuthenticatedUser, type BusinessServices } from '@atracciones/business';
import {
  AdminCancelOrderRequest,
  AdminListQuery,
  AdminRefundRequest,
  AdminReportQuery,
  CreateSlotRequest,
  RoleAssignmentRequest,
  UpdateSlotCapacityRequest,
  UpdateUserStatusRequest,
} from '@atracciones/contracts';
import type { Response } from 'express';
import type { DataSource } from 'typeorm';
import { CurrentUser, RequirePermission, RequireScope, Scopes } from '../auth/auth';
import { BUSINESS, DATA_SOURCE } from '../config';
import { METRICS, type RequestMetrics } from '../http/metrics';
import { IDEMPOTENCY_HEADER, IdempotencyKey, ParseResourceId } from '../http/request-helpers';
import { apiPath, toPaged } from '../http/responses';

const toQuery = (q: AdminListQuery): ListQuery => ({
  limit: q.limit,
  offset: q.offset,
  search: q.search ?? null,
  status: q.status ?? null,
  fromDate: q.fromDate ?? null,
  toDate: q.toDate ?? null,
  attractionId: q.attractionId ?? null,
});

const same = <T>(item: T) => item;

const idempotencyDoc = ApiHeader({ name: IDEMPOTENCY_HEADER, required: true, description: 'UUID único por intento lógico de la operación.' });

/**
 * Panel de administración. Exige el scope `attractions:write` y el permiso local `admin:manage` (rol `admin`).
 * Los listados son de solo lectura; los cambios (capacidad, franjas, estado de usuarios y roles) quedan auditados.
 */
@ApiTags('Administración')
@ApiBearerAuth()
@RequireScope(Scopes.Write)
@RequirePermission(LocalPermissions.AdminManage)
@Controller('admin')
export class AdminController {
  constructor(
    @Inject(BUSINESS) private readonly business: BusinessServices,
    @Inject(DATA_SOURCE) private readonly dataSource: DataSource,
    @Inject(METRICS) private readonly metrics: RequestMetrics,
  ) {}

  @Get('summary')
  @ApiOperation({ summary: 'Indicadores generales: clientes, reservas, pedidos, pagos, ingresos y ocupación (30 días)' })
  summary(@CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.summary(user);
  }

  @Get('customers')
  @ApiOperation({ summary: 'Clientes y usuarios con sus totales. status: ACTIVE, LOCKED, DISABLED' })
  async customers(@Query() query: AdminListQuery, @CurrentUser() user: AuthenticatedUser) {
    return toPaged(await this.business.admin.users(user, toQuery(query)), same);
  }

  @Get('customers/:userId')
  @ApiOperation({ summary: 'Detalle de un cliente o usuario' })
  customer(@Param('userId', ParseResourceId) userId: string, @CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.user(user, userId);
  }

  @Put('customers/:userId/status')
  @ApiOperation({ summary: 'Activar, bloquear o desactivar un usuario' })
  setStatus(@Param('userId', ParseResourceId) userId: string, @Body() body: UpdateUserStatusRequest, @CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.setUserStatus(user, userId, body.status, body.reason ?? null);
  }

  @Post('customers/:userId/roles')
  @ApiOperation({ summary: 'Asignar un rol local a un usuario' })
  assignRole(@Param('userId', ParseResourceId) userId: string, @Body() body: RoleAssignmentRequest, @CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.assignRole(user, userId, body.roleId, body.reason ?? null);
  }

  @Delete('customers/:userId/roles/:roleId')
  @ApiOperation({ summary: 'Revocar un rol local (se conserva el historial)' })
  revokeRole(
    @Param('userId', ParseResourceId) userId: string,
    @Param('roleId', ParseResourceId) roleId: string,
    @CurrentUser() user: AuthenticatedUser,
  ) {
    return this.business.admin.revokeRole(user, userId, roleId, null);
  }

  @Get('roles')
  @ApiOperation({ summary: 'Roles locales con sus permisos y número de usuarios' })
  roles(@CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.roles(user);
  }

  @Get('reservations')
  @ApiOperation({ summary: 'Todas las reservas. status: PENDING, CONFIRMED, CANCELLED; fechas = día de la visita' })
  async reservations(@Query() query: AdminListQuery, @CurrentUser() user: AuthenticatedUser) {
    return toPaged(await this.business.admin.reservations(user, toQuery(query)), same);
  }

  @Get('orders')
  @ApiOperation({ summary: 'Todos los pedidos. status: PENDING_PAYMENT, PAID, FULFILLED, CANCELLED, PARTIALLY_REFUNDED, REFUNDED' })
  async orders(@Query() query: AdminListQuery, @CurrentUser() user: AuthenticatedUser) {
    return toPaged(await this.business.admin.orders(user, toQuery(query)), same);
  }

  @Get('orders/:orderId')
  @ApiOperation({ summary: 'Detalle de un pedido: elementos, historial de estados, pagos con intentos y reembolsos, reserva vinculada' })
  order(@Param('orderId', ParseResourceId) orderId: string, @CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.order(user, orderId);
  }

  @Post('orders/:orderId/cancel')
  @HttpCode(200)
  @idempotencyDoc
  @ApiOperation({ summary: 'Cancelar un pedido pendiente de pago (libera cupos y cancela la reserva)' })
  cancelOrder(
    @Param('orderId', ParseResourceId) orderId: string,
    @Body() body: AdminCancelOrderRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() user: AuthenticatedUser,
  ) {
    return this.business.admin.cancelOrder(user, orderId, body.reason, key);
  }

  @Post('orders/:orderId/refunds')
  @HttpCode(200)
  @idempotencyDoc
  @ApiOperation({ summary: 'Reembolso simulado total o parcial de un pedido pagado; el total cancela la reserva' })
  refundOrder(
    @Param('orderId', ParseResourceId) orderId: string,
    @Body() body: AdminRefundRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() user: AuthenticatedUser,
  ) {
    return this.business.admin.refundOrder(user, orderId, body.amount ?? null, body.reason, key);
  }

  @Get('payments')
  @ApiOperation({ summary: 'Pagos simulados con su número de intentos' })
  async payments(@Query() query: AdminListQuery, @CurrentUser() user: AuthenticatedUser) {
    return toPaged(await this.business.admin.payments(user, toQuery(query)), same);
  }

  @Get('availability')
  @ApiOperation({ summary: 'Franjas de disponibilidad. status: AVAILABLE, FULL' })
  async availability(@Query() query: AdminListQuery, @CurrentUser() user: AuthenticatedUser) {
    return toPaged(await this.business.admin.slots(user, toQuery(query)), same);
  }

  @Post('availability')
  @ApiOperation({ summary: 'Crear una franja' })
  async addSlot(@Body() body: CreateSlotRequest, @CurrentUser() user: AuthenticatedUser, @Res({ passthrough: true }) response: Response) {
    const slot = await this.business.admin.addSlot(user, body.attractionId, body.date, body.time, body.capacity);
    response.status(201).location(apiPath(`admin/availability/${slot.id}`));
    return slot;
  }

  @Patch('availability/:slotId')
  @ApiOperation({ summary: 'Cambiar la capacidad de una franja (nunca por debajo de lo reservado)' })
  updateCapacity(@Param('slotId', ParseResourceId) slotId: string, @Body() body: UpdateSlotCapacityRequest, @CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.updateSlotCapacity(user, slotId, body.capacity);
  }

  @Get('observability')
  @ApiOperation({ summary: 'Estado del API y de la base de datos, métricas HTTP en memoria (desde el último reinicio) y errores recientes' })
  async observability() {
    const start = process.hrtime.bigint();
    let database: { status: 'ok' | 'down'; latencyMs: number | null; error: string | null };
    try {
      await this.dataSource.query('SELECT 1');
      database = { status: 'ok', latencyMs: Math.round(Number(process.hrtime.bigint() - start) / 1e5) / 10, error: null };
    } catch (error) {
      database = { status: 'down', latencyMs: null, error: error instanceof Error ? error.message : String(error) };
    }
    return { checkedAt: new Date().toISOString(), database, ...this.metrics.snapshot() };
  }

  @Get('audit')
  @ApiOperation({ summary: 'Registro de auditoría de cambios administrativos y de catálogo. status = tipo de recurso: attraction, availability, order, user' })
  async audit(@Query() query: AdminListQuery, @CurrentUser() user: AuthenticatedUser) {
    return toPaged(await this.business.admin.auditLog(user, toQuery(query)), same);
  }

  @Get('reports/sales')
  @ApiOperation({ summary: 'Ventas por atracción y por día, y reservas confirmadas por atracción' })
  sales(@Query() query: AdminReportQuery, @CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.salesReport(user, query.fromDate ?? null, query.toDate ?? null);
  }
}
