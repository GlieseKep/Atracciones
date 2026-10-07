import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsNumber, IsOptional, IsString, IsUUID, Length, Max, MaxLength, Min } from 'class-validator';
import { IsIsoDate, IsLocalTime } from './common';

/** Filtros de los listados de `/admin/*`. Cada listado documenta qué valores de `status` admite. */
export class AdminListQuery {
  @ApiPropertyOptional({ default: 20, minimum: 1, maximum: 100 }) @IsOptional() @Type(() => Number) @IsInt() @Min(1) @Max(100) limit = 20;
  @ApiPropertyOptional({ default: 0, minimum: 0 }) @IsOptional() @Type(() => Number) @IsInt() @Min(0) offset = 0;
  @ApiPropertyOptional({ description: 'Texto a buscar (correo, nombre, atracción o referencia).', maxLength: 100 })
  @IsOptional() @IsString() @MaxLength(100) search?: string;
  @ApiPropertyOptional() @IsOptional() @IsString() @MaxLength(30) status?: string;
  @ApiPropertyOptional({ example: '2026-01-01' }) @IsOptional() @IsIsoDate() fromDate?: string;
  @ApiPropertyOptional({ example: '2026-12-31' }) @IsOptional() @IsIsoDate() toDate?: string;
  @ApiPropertyOptional({ format: 'uuid' }) @IsOptional() @IsUUID() attractionId?: string;
}

export class AdminReportQuery {
  @ApiPropertyOptional({ example: '2026-01-01', description: 'Por defecto, 29 días antes de toDate.' }) @IsOptional() @IsIsoDate() fromDate?: string;
  @ApiPropertyOptional({ example: '2026-01-30', description: 'Por defecto, hoy.' }) @IsOptional() @IsIsoDate() toDate?: string;
}

export class UpdateSlotCapacityRequest {
  @ApiProperty({ minimum: 0, maximum: 10000 }) @IsInt() @Min(0) @Max(10000) capacity!: number;
}

export class CreateSlotRequest {
  @ApiProperty({ format: 'uuid' }) @IsUUID() attractionId!: string;
  @ApiProperty({ example: '2026-12-24' }) @IsIsoDate() date!: string;
  @ApiProperty({ example: '09:00' }) @IsLocalTime() time!: string;
  @ApiProperty({ minimum: 0, maximum: 10000 }) @IsInt() @Min(0) @Max(10000) capacity!: number;
}

export class UpdateUserStatusRequest {
  @ApiProperty({ enum: ['ACTIVE', 'LOCKED', 'DISABLED'] }) @IsIn(['ACTIVE', 'LOCKED', 'DISABLED']) status!: string;
  @ApiPropertyOptional({ maxLength: 500 }) @IsOptional() @IsString() @Length(1, 500) reason?: string;
}

export class RoleAssignmentRequest {
  @ApiProperty({ format: 'uuid' }) @IsUUID() roleId!: string;
  @ApiPropertyOptional({ maxLength: 500 }) @IsOptional() @IsString() @Length(1, 500) reason?: string;
}

/** Cancelación administrativa de un pedido pendiente de pago. */
export class AdminCancelOrderRequest {
  @ApiProperty({ maxLength: 500, example: 'Solicitado por el cliente por teléfono' }) @IsString() @Length(1, 500) reason!: string;
}

/** Reembolso simulado. Sin `amount` se devuelve todo lo pendiente (reembolso total). */
export class AdminRefundRequest {
  @ApiPropertyOptional({ example: 10, minimum: 0.01, description: 'Importe a devolver; si se omite, el total pendiente.' })
  @IsOptional()
  @IsNumber({ maxDecimalPlaces: 2 }, { message: 'amount admite como máximo dos decimales.' })
  @Min(0.01, { message: 'amount debe ser mayor que cero.' })
  amount?: number;

  @ApiProperty({ maxLength: 500, example: 'Visita cancelada por mal tiempo' }) @IsString() @Length(1, 500) reason!: string;
}