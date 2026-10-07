import 'reflect-metadata';
import { Logger, type INestApplication } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import type { NestExpressApplication } from '@nestjs/platform-express';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { BusinessClock } from '@atracciones/business';
import { createDataSource, ensureAvailability, seedCatalog } from '@atracciones/data-access';
import type { DataSource } from 'typeorm';
import { AppModule, requestId } from './app.module';
import { SCOPE_DESCRIPTIONS } from './auth/auth';
import type { ApiConfig } from './config';
import { RequestMetrics } from './http/metrics';
import { apiValidationPipe } from './http/request-helpers';
import { API_PREFIX } from './http/responses';

/** Conecta a PostgreSQL, aplica migraciones pendientes y carga el catálogo inicial si está habilitado. */
export async function initializeDatabase(config: ApiConfig): Promise<DataSource> {
  const logger = new Logger('Database');
  const dataSource = createDataSource({ url: config.database.url, ssl: config.database.ssl, migrationsRun: config.database.migrationsRun });
  await dataSource.initialize();
  if (config.database.seedCatalog) {
    const created = await seedCatalog(dataSource);
    const today = new BusinessClock(config.business.timeZone).today;
    const slots = await ensureAvailability(dataSource, today, config.database.availabilityDays);
    logger.log(`Catálogo inicial: ${created} atracciones nuevas, ${slots} franjas nuevas.`);
  }
  return dataSource;
}

/** Crea y configura la aplicación HTTP (sin escuchar), reutilizable en pruebas. */
export async function createApp(config: ApiConfig, dataSource: DataSource): Promise<INestApplication> {
  const metrics = new RequestMetrics();
  const app = await NestFactory.create<NestExpressApplication>(AppModule.register(config, dataSource, metrics), {
    logger: ['error', 'warn', 'log'],
    bodyParser: true,
  });
  app.use(requestId);
  app.use(metrics.middleware);
  app.useBodyParser('json', { limit: '256kb' });
  if (config.trustProxy) app.set('trust proxy', 1);
  app.disable('x-powered-by');
  app.setGlobalPrefix(API_PREFIX, { exclude: ['health', 'docs', 'docs-json', 'openapi/v1.json'] });
  app.useGlobalPipes(apiValidationPipe());
  app.enableCors({
    origin: config.corsOrigins.length > 0 ? config.corsOrigins : false,
    methods: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'],
    allowedHeaders: ['Authorization', 'Content-Type', 'Idempotency-Key'],
    exposedHeaders: ['Location', 'Retry-After', 'X-Request-Id'],
    maxAge: 600,
  });

  if (config.swaggerEnabled) {
    // `POST /auth/login` es del servicio de autenticación (otra aplicación), no de este API.
    const login = config.publicAuthUrl
      ? `[POST /auth/login](${config.publicAuthUrl}/docs) en el Swagger del servicio de autenticación (${config.publicAuthUrl}/docs)`
      : '`POST /auth/login` del servicio de autenticación (tiene su propio Swagger en `/docs`)';
    const builder = new DocumentBuilder()
      .setTitle('API de Atracciones · TourGirls')
      .setDescription(
        'Catálogo, disponibilidad, reservas, compra directa, pedidos y pagos simulados. ' +
          'La lectura del catálogo y la disponibilidad es pública. Para lo demás, obtén un token con ' +
          `${login}, copia \`access_token\` y pégalo en **Authorize** (sin escribir "Bearer"). ` +
          'Las operaciones que reservan, compran, pagan, cancelan o reembolsan exigen la cabecera `Idempotency-Key` (un UUID por intento). ' +
          `Scopes: ${Object.entries(SCOPE_DESCRIPTIONS).map(([s, d]) => `\`${s}\` (${d})`).join(', ')}.`,
      )
      .setVersion('v1')
      .addBearerAuth({ type: 'http', scheme: 'bearer', bearerFormat: 'JWT' });
    if (config.publicApiUrl) builder.addServer(config.publicApiUrl);
    const document = SwaggerModule.createDocument(app, builder.build());
    SwaggerModule.setup('docs', app, document, { jsonDocumentUrl: 'openapi/v1.json', swaggerOptions: { persistAuthorization: false } });
  }
  return app;
}
