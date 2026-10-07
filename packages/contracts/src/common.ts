import { ApiProperty } from '@nestjs/swagger';
import { IsNumber, Matches, Max, Min, registerDecorator, type ValidationOptions } from 'class-validator';

export const PATTERNS = {
  currency: /^[A-Z]{3}$/,
  country: /^[A-Z]{2}$/,
  localTime: /^([01][0-9]|2[0-3]):[0-5][0-9]$/,
  isoDuration: /^P(?!$)(\d+D)?(T(?=\d)(\d+H)?(\d+M)?)?$/,
  isoDate: /^\d{4}-\d{2}-\d{2}$/,
  /** Referencia de pago simulado: alfanumérica y nunca solo dígitos (para impedir números de tarjeta). */
  paymentReference: /^(?!\d+$)[A-Za-z0-9_-]{1,64}$/,
};

/** Fecha `YYYY-MM-DD` que existe en el calendario (equivale a DateOnly). */
export function IsIsoDate(options?: ValidationOptions): PropertyDecorator {
  return (target, propertyName) =>
    registerDecorator({
      name: 'isIsoDate',
      target: target.constructor,
      propertyName: propertyName as string,
      options: { message: `${String(propertyName)} debe ser una fecha YYYY-MM-DD válida.`, ...options },
      validator: {
        validate(value: unknown) {
          if (typeof value !== 'string' || !PATTERNS.isoDate.test(value)) return false;
          const [y, m, d] = value.split('-').map(Number);
          const date = new Date(Date.UTC(y, m - 1, d));
          return date.getUTCFullYear() === y && date.getUTCMonth() === m - 1 && date.getUTCDate() === d;
        },
      },
    });
}

/** Hora local de la franja en formato 24 h `HH:mm`. */
export const IsLocalTime = () => Matches(PATTERNS.localTime, { message: 'time debe tener formato HH:mm.' });

/** Representación monetaria: moneda ISO 4217 obligatoria, importe positivo con un máximo de dos decimales. */
export class PriceDto {
  @ApiProperty({ example: 'USD', pattern: '^[A-Z]{3}$' })
  @Matches(PATTERNS.currency, { message: 'currency debe ser un código ISO 4217 de tres letras mayúsculas.' })
  currency!: string;

  @ApiProperty({ example: 25.5, minimum: 0.01 })
  @IsNumber({ maxDecimalPlaces: 2 }, { message: 'total admite como máximo dos decimales.' })
  @Min(0.01, { message: 'total debe ser mayor que cero.' })
  @Max(99999999.99)
  total!: number;
}

/** Respuesta paginada con metadatos. Evita arrays sin límite. */
export class PagedResponse<T> {
  @ApiProperty() totalItems!: number;
  @ApiProperty() itemsPerPage!: number;
  @ApiProperty() currentPage!: number;
  @ApiProperty() totalPages!: number;
  data!: T[];
}
