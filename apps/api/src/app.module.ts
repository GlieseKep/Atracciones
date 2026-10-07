import { Controller, Get, Inject, type DynamicModule, Module } from '@nestjs/common';
import { APP_FILTER, APP_GUARD } from '@nestjs/core';
import { ApiExcludeController } from '@nestjs/swagger';
import { ThrottlerGuard, ThrottlerModule } from '@nestjs/throttler';
import { createBusinessServices } from '@atracciones/business';
import { TypeOrmUnitOfWorkFactory } from '@atracciones/data-access';
import { randomUUID } from 'node:crypto';
import type { NextFunction, Request, Response } from 'express';
import type { DataSource } from 'typeorm';
import { AuthGuard, Public, type AuthenticatedRequest } from './auth/auth';
import { API_CONFIG, BUSINESS, DATA_SOURCE, type ApiConfig } from './config';
import { AttractionsController } from './controllers/catalog.controllers';
import { EcommerceController, IdentityController, ReservationsController } from './controllers/operation.controllers';
import { ProblemDetailsFilter } from './http/problems';

@ApiExcludeController()
@Controller()
export class HealthController {
  constructor(@Inject(DATA_SOURCE) private readonly dataSource: DataSource) {}

  @Public()
  @Get(['health', 'atracciones/health'])
  async health() {
    await this.dataSource.query('SELECT 1');
    return { status: 'ok' };
  }
}

/** Rate limit por identidad (`sub`) o, sin token, por IP. */
class IdentityThrottlerGuard extends ThrottlerGuard {
  protected override async getTracker(request: Record<string, unknown>): Promise<string> {
    const req = request as unknown as AuthenticatedRequest;
    return req.user ? `sub:${req.user.issuer}|${req.user.subject}` : `ip:${req.ip}`;
  }
}

@Module({})
export class AppModule {
  static register(config: ApiConfig, dataSource: DataSource): DynamicModule {
    const business = createBusinessServices(new TypeOrmUnitOfWorkFactory(dataSource), config.business);
    return {
      module: AppModule,
      imports: [ThrottlerModule.forRoot([{ ttl: config.rateLimit.windowSeconds * 1000, limit: config.rateLimit.permitLimit }])],
      // ReservationsController antes que AttractionsController: `/atracciones/reservations` no debe capturarse como `/atracciones/:id`.
      controllers: [HealthController, ReservationsController, AttractionsController, IdentityController, EcommerceController],
      providers: [
        { provide: API_CONFIG, useValue: config },
        { provide: DATA_SOURCE, useValue: dataSource },
        { provide: BUSINESS, useValue: business },
        { provide: APP_FILTER, useClass: ProblemDetailsFilter },
        // Orden: primero autenticación (resuelve `sub`), después el límite de tasa por identidad.
        { provide: APP_GUARD, useClass: AuthGuard },
        { provide: APP_GUARD, useClass: IdentityThrottlerGuard },
      ],
    };
  }
}

/** Identificador por solicitud: `traceId` de los errores, `requestId` de las búsquedas y cabecera `X-Request-Id`. */
export function requestId(req: Request & { id?: string }, res: Response, next: NextFunction): void {
  req.id = randomUUID();
  res.setHeader('X-Request-Id', req.id);
  next();
}
