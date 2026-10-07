import 'reflect-metadata';
import { Logger, Module, ValidationPipe, type DynamicModule, type INestApplication } from '@nestjs/common';
import { APP_FILTER, APP_GUARD, NestFactory } from '@nestjs/core';
import type { NestExpressApplication } from '@nestjs/platform-express';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { ThrottlerGuard, ThrottlerModule } from '@nestjs/throttler';
import type { ValidationError } from 'class-validator';
import { Pool } from 'pg';
import { AccountsController, AccountService, AuthProblem, AuthProblemFilter, ensureSchema } from './accounts';
import { AUTH_CONFIG, loadEnvFile, PG_POOL, readConfig, type AuthConfig } from './config';

@Module({})
class AuthModule {
  static register(config: AuthConfig, pool: Pool): DynamicModule {
    return {
      module: AuthModule,
      imports: [ThrottlerModule.forRoot([{ ttl: 60_000, limit: 60 }])],
      controllers: [AccountsController],
      providers: [
        AccountService,
        { provide: AUTH_CONFIG, useValue: config },
        { provide: PG_POOL, useValue: pool },
        { provide: APP_FILTER, useClass: AuthProblemFilter },
        { provide: APP_GUARD, useClass: ThrottlerGuard },
      ],
    };
  }
}

const flatten = (errors: ValidationError[]): Record<string, string[]> =>
  Object.fromEntries(
    errors.map((e) => [
      e.property,
      Object.entries(e.constraints ?? {}).map(([rule, message]) => (rule === 'whitelistValidation' ? `La propiedad '${e.property}' no está permitida.` : message)),
    ]),
  );

export async function createAuthApp(config: AuthConfig): Promise<{ app: INestApplication; pool: Pool }> {
  const pool = new Pool({ connectionString: config.databaseUrl, ssl: config.databaseSsl ? { rejectUnauthorized: true } : undefined, max: 5 });
  await ensureSchema(pool);
  const app = await NestFactory.create<NestExpressApplication>(AuthModule.register(config, pool), { logger: ['error', 'warn', 'log'] });
  if (config.trustProxy) app.set('trust proxy', 1);
  app.disable('x-powered-by');
  app.useBodyParser('json', { limit: '16kb' });
  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      forbidNonWhitelisted: true,
      exceptionFactory: (errors) => new AuthProblem(400, 'VALIDATION_ERROR', 'Revisa los datos del formulario.', flatten(errors)),
    }),
  );
  app.enableCors({
    origin: config.corsOrigins.length > 0 ? config.corsOrigins : false,
    methods: ['GET', 'POST'],
    allowedHeaders: ['Content-Type'],
    maxAge: 600,
  });
  app.enableShutdownHooks();

  if (config.swaggerEnabled) {
    const document = SwaggerModule.createDocument(
      app,
      new DocumentBuilder()
        .setTitle('Autenticación · TourGirls')
        .setDescription(
          'Registro e inicio de sesión con correo y contraseña. Devuelve el JWT que exige el API de atracciones: ' +
            'copia `access_token` y pégalo en **Authorize** del Swagger del API (`/docs` del App Service del API).',
        )
        .setVersion('v1')
        .build(),
    );
    SwaggerModule.setup('docs', app, document, { jsonDocumentUrl: 'openapi.json' });
  }
  return { app, pool };
}

async function main(): Promise<void> {
  loadEnvFile();
  const config = readConfig();
  const { app } = await createAuthApp(config);
  // Sin host explícito: doble pila IPv4/IPv6 (`localhost` resuelve primero a ::1).
  await app.listen(config.port);
  new Logger('dev-auth').log(`Servicio de autenticación escuchando en el puerto ${config.port}.`);
}

if (require.main === module) {
  main().catch((error: unknown) => {
    console.error(error instanceof Error ? error.message : error);
    process.exit(1);
  });
}
