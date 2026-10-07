import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsEmail, IsIn, IsInt, IsNumber, IsOptional, IsString, IsUUID, Length, Matches, Max, MaxLength, Min } from 'class-validator';
import { IsIsoDate, IsLocalTime, PATTERNS, PriceDto } from './common';

export const RESERVATION_STATUSES = ['PENDING', 'CONFIRMED', 'CANCELLED'] as const;
export const ORDER_STATUSES = ['PENDING_PAYMENT', 'PAID', 'FULFILLED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'] as const;
export const PAYMENT_METHODS = ['CARD', 'BANK_TRANSFER'] as const;
export const PAYMENT_STATUSES = ['PENDING', 'AUTHORIZED', 'SETTLED', 'REJECTED', 'FAILED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'] as const;

// ---------------------------------------------------------------- Reservas

/** Cuerpo de `POST /atracciones/{id}/reservations`. */
export class ReservationRequest {
  @ApiProperty({ example: '2026-12-01' }) @IsIsoDate() date!: string;
  @ApiProperty({ example: '09:00' }) @IsLocalTime() time!: string;
  @ApiProperty({ minimum: 1, maximum: 100 }) @IsInt() @Min(1) @Max(100) ticketCount!: number;
  @ApiProperty({ maxLength: 200 }) @IsString() @Length(1, 200) customerName!: string;
  @ApiProperty({ maxLength: 254 }) @IsEmail() @MaxLength(254) customerEmail!: string;
}

export class CancelReservationRequest {
  @ApiProperty({ maxLength: 500 }) @IsString() @Length(1, 500) reason!: string;
}

export class ReservationResponse {
  @ApiProperty({ format: 'uuid' }) reservationId!: string;
  @ApiProperty({ format: 'uuid' }) attractionId!: string;
  @ApiProperty({ enum: RESERVATION_STATUSES }) status!: string;
  @ApiProperty({ example: '2026-12-01' }) date!: string;
  @ApiProperty({ example: '09:00' }) time!: string;
  @ApiProperty() ticketCount!: number;
  @ApiProperty({ type: PriceDto }) totalPrice!: PriceDto;
}

/** Historial de reservas: la propiedad siempre se resuelve con el usuario autenticado. */
export class ReservationListQuery {
  @ApiPropertyOptional({ default: 20, minimum: 1, maximum: 100 }) @IsOptional() @Type(() => Number) @IsInt() @Min(1) @Max(100) limit = 20;
  @ApiPropertyOptional({ default: 0, minimum: 0 }) @IsOptional() @Type(() => Number) @IsInt() @Min(0) offset = 0;
  @ApiPropertyOptional({ enum: RESERVATION_STATUSES }) @IsOptional() @IsIn(RESERVATION_STATUSES) status?: string;
  @ApiPropertyOptional({ example: '2026-01-01' }) @IsOptional() @IsIsoDate() fromDate?: string;
  @ApiPropertyOptional({ example: '2026-12-31' }) @IsOptional() @IsIsoDate() toDate?: string;

  @ApiPropertyOptional({ enum: ['date', '-date'], default: '-date' })
  @IsOptional()
  @Matches(/^-?date$/, { message: "sort admite 'date' o '-date'." })
  sort = '-date';
}

// ---------------------------------------------------------------- Identidad y clientes

/** Cuerpo de `POST /auth/register`. La identidad (`sub`, `email`) se toma exclusivamente de los claims del token. */
export class RegisterProfileRequest {
  @ApiPropertyOptional({ maxLength: 200 }) @IsOptional() @IsString() @Length(1, 200) billingName?: string;
  @ApiPropertyOptional({ maxLength: 254 }) @IsOptional() @IsEmail() @MaxLength(254) billingEmail?: string;
  @ApiPropertyOptional({ maxLength: 300 }) @IsOptional() @IsString() @Length(1, 300) billingAddress?: string;
  @ApiPropertyOptional({ maxLength: 30 }) @IsOptional() @IsString() @Length(1, 30) taxId?: string;
}

export class RegisterProfileResponse {
  @ApiProperty({ format: 'uuid' }) id!: string;
  @ApiProperty() email!: string;
  @ApiProperty() status!: string;
  @ApiProperty({ format: 'uuid' }) customerId!: string;
  @ApiProperty() createdAt!: string;
}

export class UserResponse {
  @ApiProperty({ format: 'uuid' }) id!: string;
  @ApiProperty() email!: string;
  @ApiProperty() status!: string;
  @ApiProperty() createdAt!: string;
  @ApiProperty() updatedAt!: string;
}

export class CustomerResponse {
  @ApiProperty({ format: 'uuid' }) id!: string;
  @ApiProperty({ format: 'uuid' }) userId!: string;
  @ApiPropertyOptional({ nullable: true }) billingName!: string | null;
  @ApiPropertyOptional({ nullable: true }) billingEmail!: string | null;
  @ApiPropertyOptional({ nullable: true }) billingAddress!: string | null;
  @ApiPropertyOptional({ nullable: true }) taxId!: string | null;
  @ApiPropertyOptional({ nullable: true }) paymentMethodReference!: string | null;
  @ApiProperty() createdAt!: string;
  @ApiProperty() updatedAt!: string;
}

/** Cuerpo de `PUT /customers/me`: reemplaza todos los datos de facturación. */
export class UpdateCustomerRequest {
  @ApiProperty({ maxLength: 200 }) @IsString() @Length(1, 200) billingName!: string;
  @ApiProperty({ maxLength: 254 }) @IsEmail() @MaxLength(254) billingEmail!: string;
  @ApiProperty({ maxLength: 300 }) @IsString() @Length(1, 300) billingAddress!: string;
  @ApiPropertyOptional({ maxLength: 30 }) @IsOptional() @IsString() @Length(1, 30) taxId?: string;

  @ApiPropertyOptional({ description: 'Referencia local para la simulación de pago; nunca un número de tarjeta.' })
  @IsOptional()
  @Matches(PATTERNS.paymentReference, { message: 'paymentMethodReference debe ser una referencia alfanumérica, no un dato financiero.' })
  paymentMethodReference?: string;
}

// ---------------------------------------------------------------- Ecommerce

/**
 * Cuerpo de `POST /attractions/{attractionId}/purchase`. El precio lo calcula el servidor. Con `paymentMethod`
 * el pago simulado se procesa en la misma operación; sin él se crea un pedido `PENDING_PAYMENT` con retención de cupos.
 */
export class CreatePurchaseRequest {
  @ApiProperty({ example: '2026-12-01' }) @IsIsoDate() date!: string;
  @ApiProperty({ example: '09:00' }) @IsLocalTime() time!: string;
  @ApiProperty({ minimum: 1, maximum: 100 }) @IsInt() @Min(1) @Max(100) quantity!: number;
  @ApiPropertyOptional({ enum: PAYMENT_METHODS }) @IsOptional() @IsIn(PAYMENT_METHODS) paymentMethod?: string;
}

export class PaymentSummaryDto {
  @ApiProperty({ format: 'uuid' }) id!: string;
  @ApiProperty({ enum: PAYMENT_METHODS }) paymentMethod!: string;
  @ApiProperty({ enum: PAYMENT_STATUSES }) status!: string;
  @ApiPropertyOptional({ nullable: true }) gatewayReference!: string | null;
}

export class PurchaseResponse {
  @ApiProperty({ format: 'uuid' }) purchaseId!: string;
  @ApiProperty({ format: 'uuid' }) orderId!: string;
  @ApiProperty({ format: 'uuid' }) attractionId!: string;
  @ApiProperty() date!: string;
  @ApiProperty() time!: string;
  @ApiProperty() quantity!: number;
  @ApiProperty({ type: PriceDto }) unitPrice!: PriceDto;
  @ApiProperty() totalAmount!: number;
  @ApiProperty() currency!: string;
  @ApiProperty({ enum: ORDER_STATUSES }) status!: string;
  @ApiPropertyOptional({ nullable: true, format: 'uuid' }) reservationId!: string | null;
  @ApiPropertyOptional({ type: PaymentSummaryDto, nullable: true }) payment!: PaymentSummaryDto | null;
  @ApiPropertyOptional({ nullable: true, description: 'Fin de la retención temporal de cupos si el pago no se confirmó.' }) holdExpiresAt!: string | null;
  @ApiProperty() createdAt!: string;
}

/** Cuerpo de `POST /orders`: crea un pedido `PENDING_PAYMENT` para una franja concreta, sin carrito. */
export class CreateOrderRequest {
  @ApiProperty({ format: 'uuid' }) @IsUUID('all') attractionId!: string;
  @ApiProperty({ example: '2026-12-01' }) @IsIsoDate() date!: string;
  @ApiProperty({ example: '09:00' }) @IsLocalTime() time!: string;
  @ApiProperty({ minimum: 1, maximum: 100 }) @IsInt() @Min(1) @Max(100) quantity!: number;
}

export class OrderItemResponse {
  @ApiProperty({ format: 'uuid' }) id!: string;
  @ApiProperty({ format: 'uuid' }) attractionId!: string;
  @ApiProperty() date!: string;
  @ApiProperty() time!: string;
  @ApiProperty() quantity!: number;
  @ApiProperty({ type: PriceDto }) unitPrice!: PriceDto;
  @ApiProperty() status!: string;
}

export class OrderResponse {
  @ApiProperty({ format: 'uuid' }) id!: string;
  @ApiProperty({ format: 'uuid' }) customerId!: string;
  @ApiPropertyOptional({ nullable: true }) purchaseId!: string | null;
  @ApiPropertyOptional({ nullable: true }) reservationId!: string | null;
  @ApiProperty({ enum: ORDER_STATUSES }) status!: string;
  @ApiProperty() currency!: string;
  @ApiProperty() totalAmount!: number;
  @ApiProperty() createdAt!: string;
  @ApiProperty() updatedAt!: string;
  @ApiProperty({ type: [OrderItemResponse] }) items!: OrderItemResponse[];
  @ApiPropertyOptional({ type: PaymentSummaryDto, nullable: true }) paymentSimulation!: PaymentSummaryDto | null;
}

export class OrderEventResponse {
  @ApiProperty() eventType!: string;
  @ApiPropertyOptional({ nullable: true }) previousStatus!: string | null;
  @ApiProperty() newStatus!: string;
  @ApiProperty() createdAt!: string;
}

export class OrderEventsResponse {
  @ApiProperty({ format: 'uuid' }) orderId!: string;
  @ApiProperty({ type: [OrderEventResponse] }) events!: OrderEventResponse[];
}

export class CancelOrderRequest {
  @ApiProperty({ maxLength: 500 }) @IsString() @Length(1, 500) reason!: string;
}

/** Cuerpo de `POST /payments/simulations`. El cliente no envía estados, códigos de respuesta ni datos de tarjeta. */
export class CreatePaymentSimulationRequest {
  @ApiProperty({ format: 'uuid' }) @IsUUID('all') orderId!: string;
  @ApiProperty({ enum: PAYMENT_METHODS }) @IsIn(PAYMENT_METHODS) paymentMethod!: string;

  @ApiProperty({ minimum: 0.01 })
  @IsNumber({ maxDecimalPlaces: 2 }, { message: 'amount admite como máximo dos decimales.' })
  @Min(0.01, { message: 'amount debe ser mayor que cero.' })
  @Max(99999999.99)
  amount!: number;

  @ApiProperty({ example: 'USD' }) @Matches(PATTERNS.currency, { message: 'currency debe ser un código ISO 4217.' }) currency!: string;
}

export class PaymentSimulationResponse {
  @ApiProperty({ format: 'uuid' }) id!: string;
  @ApiProperty({ format: 'uuid' }) orderId!: string;
  @ApiProperty({ enum: PAYMENT_METHODS }) paymentMethod!: string;
  @ApiProperty({ enum: PAYMENT_STATUSES }) status!: string;
  @ApiProperty() amount!: number;
  @ApiProperty() currency!: string;
  @ApiPropertyOptional({ nullable: true }) gatewayReference!: string | null;
  @ApiProperty() createdAt!: string;
}
