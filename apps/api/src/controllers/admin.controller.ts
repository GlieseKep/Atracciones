import { Body, Controller, Delete, Get, Inject, Param, Patch, Post, Put, Query, Res } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiTags } from '@nestjs/swagger';
import { LocalPermissions, type AdminListQuery as ListQuery, type AuthenticatedUser, type BusinessServices } from '@atracciones/business';
import {
  AdminListQuery,
  AdminReportQuery,
  CreateSlotRequest,
  RoleAssignmentRequest,
  UpdateSlotCapacityRequest,
  UpdateUserStatusRequest,
} from '@atracciones/contracts';
import type { Response } from 'express';
import { CurrentUser, RequirePermission, RequireScope, Scopes } from '../auth/auth';
import { BUSINESS } from '../config';
import { ParseResourceId } from '../http/request-helpers';
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
  constructor(@Inject(BUSINESS) private readonly business: BusinessServices) {}

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

  @Get('reports/sales')
  @ApiOperation({ summary: 'Ventas por atracción y por día, y reservas confirmadas por atracción' })
  sales(@Query() query: AdminReportQuery, @CurrentUser() user: AuthenticatedUser) {
    return this.business.admin.salesReport(user, query.fromDate ?? null, query.toDate ?? null);
  }
}
