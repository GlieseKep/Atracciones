import { Body, Controller, Get, HttpCode, Inject, Param, Post, Put, Query, Res } from '@nestjs/common';
import { ApiBearerAuth, ApiHeader, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { ValidationError, type AuthenticatedUser, type BusinessServices } from '@atracciones/business';
import {
  CancelOrderRequest,
  CancelReservationRequest,
  CreateOrderRequest,
  CreatePaymentSimulationRequest,
  CreatePurchaseRequest,
  CustomerResponse,
  OrderEventsResponse,
  OrderResponse,
  PaymentSimulationResponse,
  PurchaseResponse,
  RegisterProfileRequest,
  RegisterProfileResponse,
  ReservationListQuery,
  ReservationRequest,
  ReservationResponse,
  UpdateCustomerRequest,
  UserResponse,
  type PagedResponse,
} from '@atracciones/contracts';
import type { Response } from 'express';
import { CurrentUser, RequireScope, Scopes } from '../auth/auth';
import { API_CONFIG, BUSINESS, type ApiConfig } from '../config';
import { IDEMPOTENCY_HEADER, IdempotencyKey, ParseResourceId } from '../http/request-helpers';
import {
  apiPath,
  toCustomerResponse,
  toOrderEventsResponse,
  toOrderResponse,
  toPaged,
  toPaymentResponse,
  toPurchaseResponse,
  toRegisterResponse,
  toReservationResponse,
  toUserResponse,
} from '../http/responses';

const idempotencyDoc = ApiHeader({ name: IDEMPOTENCY_HEADER, required: true, description: 'UUID único por intento lógico de la operación.' });

const toReservationQuery = (query: ReservationListQuery, user: AuthenticatedUser) => ({
  user,
  limit: query.limit,
  offset: query.offset,
  status: query.status ?? null,
  fromDate: query.fromDate ?? null,
  toDate: query.toDate ?? null,
  sortDescending: (query.sort ?? '-date').startsWith('-'),
});

@ApiTags('Reservas')
@ApiBearerAuth()
@Controller('atracciones')
export class ReservationsController {
  constructor(@Inject(BUSINESS) private readonly business: BusinessServices) {}

  @Post(':id/reservations')
  @RequireScope(Scopes.Book)
  @idempotencyDoc
  @ApiOperation({ summary: 'Reservar una franja (se confirma al crearse)' })
  @ApiResponse({ status: 201, type: ReservationResponse })
  async create(
    @Param('id', ParseResourceId) id: string,
    @Body() body: ReservationRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() user: AuthenticatedUser,
    @Res({ passthrough: true }) response: Response,
  ): Promise<ReservationResponse> {
    const result = await this.business.reservations.create({ attractionId: id, ...body, idempotencyKey: key, user });
    response.status(201).location(apiPath(`atracciones/reservations/${result.reservationId}`));
    return toReservationResponse(result);
  }

  @Post('reservations/:reservationId/cancel')
  @HttpCode(200)
  @RequireScope(Scopes.Cancel)
  @idempotencyDoc
  @ApiOperation({ summary: 'Cancelar una reserva propia' })
  @ApiResponse({ status: 200, type: ReservationResponse })
  async cancel(
    @Param('reservationId', ParseResourceId) reservationId: string,
    @Body() body: CancelReservationRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() user: AuthenticatedUser,
  ): Promise<ReservationResponse> {
    return toReservationResponse(await this.business.reservations.cancel({ reservationId, reason: body.reason, idempotencyKey: key, user }));
  }

  @Get('reservations')
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Historial de reservas del usuario autenticado' })
  async list(@Query() query: ReservationListQuery, @CurrentUser() user: AuthenticatedUser): Promise<PagedResponse<ReservationResponse>> {
    return toPaged(await this.business.reservations.list(toReservationQuery(query, user)), toReservationResponse);
  }

  @Get('reservations/:reservationId')
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Detalle de una reserva propia' })
  @ApiResponse({ status: 200, type: ReservationResponse })
  async get(@Param('reservationId', ParseResourceId) reservationId: string, @CurrentUser() user: AuthenticatedUser): Promise<ReservationResponse> {
    return toReservationResponse(await this.business.reservations.get(reservationId, user));
  }
}

/**
 * Aprovisionamiento del perfil local tras autenticarse en dev-auth. El registro con contraseña y la emisión de tokens
 * pertenecen al servicio de autenticación.
 */
@ApiTags('Identidad')
@ApiBearerAuth()
@Controller()
export class IdentityController {
  constructor(
    @Inject(BUSINESS) private readonly business: BusinessServices,
    @Inject(API_CONFIG) private readonly config: ApiConfig,
  ) {}

  @Post('auth/register')
  @RequireScope(Scopes.Book)
  @ApiOperation({ summary: 'Aprovisionar el perfil local del usuario autenticado (idempotente)' })
  @ApiResponse({ status: 201, type: RegisterProfileResponse })
  @ApiResponse({ status: 200, type: RegisterProfileResponse, description: 'El perfil ya existía.' })
  async register(
    @Body() body: RegisterProfileRequest,
    @CurrentUser() identity: AuthenticatedUser,
    @Res({ passthrough: true }) response: Response,
  ): Promise<RegisterProfileResponse> {
    // Política del despliegue: si el emisor no verifica correos, su estado no se evalúa.
    const user = this.config.auth.requireVerifiedEmail ? identity : { ...identity, emailVerified: null };
    if (!user.email || user.emailVerified === false) {
      throw new ValidationError("El access token no contiene un claim 'email' verificado y válido.");
    }
    const result = await this.business.profiles.register({
      user,
      email: user.email,
      billing: { billingName: body.billingName, billingEmail: body.billingEmail, billingAddress: body.billingAddress, taxId: body.taxId },
    });
    if (result.created) response.status(201).location(apiPath('users/me'));
    else response.status(200);
    return toRegisterResponse(result);
  }

  @Get('users/me')
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Perfil local del usuario autenticado' })
  @ApiResponse({ status: 200, type: UserResponse })
  async me(@CurrentUser() user: AuthenticatedUser): Promise<UserResponse> {
    return toUserResponse(await this.business.profiles.getCurrent(user));
  }

  @Get('customers/me')
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Datos de facturación del usuario autenticado' })
  @ApiResponse({ status: 200, type: CustomerResponse })
  async customer(@CurrentUser() user: AuthenticatedUser): Promise<CustomerResponse> {
    return toCustomerResponse(await this.business.customers.getCurrent(user));
  }

  @Put('customers/me')
  @RequireScope(Scopes.Book)
  @ApiOperation({ summary: 'Reemplazar los datos de facturación' })
  @ApiResponse({ status: 200, type: CustomerResponse })
  async updateCustomer(@Body() body: UpdateCustomerRequest, @CurrentUser() user: AuthenticatedUser): Promise<CustomerResponse> {
    return toCustomerResponse(await this.business.customers.updateCurrent(user, { ...body }));
  }

  @Get('customers/me/reservations')
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Reservas del usuario autenticado' })
  async reservations(@Query() query: ReservationListQuery, @CurrentUser() user: AuthenticatedUser): Promise<PagedResponse<ReservationResponse>> {
    return toPaged(await this.business.reservations.list(toReservationQuery(query, user)), toReservationResponse);
  }
}

@ApiTags('Ecommerce')
@ApiBearerAuth()
@Controller()
export class EcommerceController {
  constructor(@Inject(BUSINESS) private readonly business: BusinessServices) {}

  @Post('attractions/:attractionId/purchase')
  @RequireScope(Scopes.Book)
  @idempotencyDoc
  @ApiOperation({ summary: 'Compra directa de una franja (sin carrito)' })
  @ApiResponse({ status: 201, type: PurchaseResponse })
  async purchase(
    @Param('attractionId', ParseResourceId) attractionId: string,
    @Body() body: CreatePurchaseRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() user: AuthenticatedUser,
    @Res({ passthrough: true }) response: Response,
  ): Promise<PurchaseResponse> {
    const result = await this.business.purchases.create({
      attractionId, date: body.date, time: body.time, quantity: body.quantity, paymentMethod: body.paymentMethod ?? null, idempotencyKey: key, user,
    });
    response.status(201).location(apiPath(`orders/${result.orderId}`));
    return toPurchaseResponse(result);
  }

  @Post('orders')
  @RequireScope(Scopes.Book)
  @idempotencyDoc
  @ApiOperation({ summary: 'Crear un pedido pendiente de pago con retención de cupos' })
  @ApiResponse({ status: 201, type: OrderResponse })
  async createOrder(
    @Body() body: CreateOrderRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() user: AuthenticatedUser,
    @Res({ passthrough: true }) response: Response,
  ): Promise<OrderResponse> {
    const result = await this.business.orders.create({ ...body, attractionId: body.attractionId.toLowerCase(), idempotencyKey: key, user });
    response.status(201).location(apiPath(`orders/${result.id}`));
    return toOrderResponse(result);
  }

  @Get('orders/:orderId')
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Detalle de un pedido propio' })
  @ApiResponse({ status: 200, type: OrderResponse })
  async order(@Param('orderId', ParseResourceId) orderId: string, @CurrentUser() user: AuthenticatedUser): Promise<OrderResponse> {
    return toOrderResponse(await this.business.orders.get(orderId, user));
  }

  @Get('orders/:orderId/events')
  @RequireScope(Scopes.Read)
  @ApiOperation({ summary: 'Historial de estados de un pedido propio' })
  @ApiResponse({ status: 200, type: OrderEventsResponse })
  async events(@Param('orderId', ParseResourceId) orderId: string, @CurrentUser() user: AuthenticatedUser): Promise<OrderEventsResponse> {
    return toOrderEventsResponse(await this.business.orders.getEvents(orderId, user));
  }

  @Post('orders/:orderId/cancel')
  @HttpCode(200)
  @RequireScope(Scopes.Cancel)
  @idempotencyDoc
  @ApiOperation({ summary: 'Cancelar un pedido pendiente de pago' })
  @ApiResponse({ status: 200, type: OrderResponse })
  async cancelOrder(
    @Param('orderId', ParseResourceId) orderId: string,
    @Body() body: CancelOrderRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() user: AuthenticatedUser,
  ): Promise<OrderResponse> {
    return toOrderResponse(await this.business.orders.cancel({ orderId, reason: body.reason, idempotencyKey: key, user }));
  }

  @Post('payments/simulations')
  @RequireScope(Scopes.Book)
  @idempotencyDoc
  @ApiOperation({ summary: 'Simular el pago de un pedido (el resultado lo decide el servidor)' })
  @ApiResponse({ status: 201, type: PaymentSimulationResponse })
  async simulate(
    @Body() body: CreatePaymentSimulationRequest,
    @IdempotencyKey() key: string,
    @CurrentUser() user: AuthenticatedUser,
    @Res({ passthrough: true }) response: Response,
  ): Promise<PaymentSimulationResponse> {
    const result = await this.business.payments.simulate({ ...body, orderId: body.orderId.toLowerCase(), idempotencyKey: key, user });
    // El estado del pago se consulta mediante el pedido.
    response.status(201).location(apiPath(`orders/${result.orderId}`));
    return toPaymentResponse(result);
  }
}
