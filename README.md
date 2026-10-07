# TourGirls · Atracciones de Ecuador

Portal para descubrir y reservar tours, entradas y experiencias en Ecuador: catálogo con búsqueda y disponibilidad por
franja, reservas, compra directa con pedidos y pagos simulados, y administración del catálogo.

| Parte | Tecnología | Carpeta |
|---|---|---|
| API REST (`/api/v1`) | NestJS 11 + TypeORM + PostgreSQL | `apps/api` |
| Autenticación (registro / login) | NestJS + PostgreSQL, JWT HS256 | `apps/auth` |
| Frontend | React 19 + Vite + Tailwind | `AtraccionesService-web` |

## Arquitectura

Monorepo con npm workspaces y una capa por paquete (`PLAN_IMPLEMENTACION_*.md`):

```
packages/
  domain/           Agregados, invariantes y transiciones de estado (sin dependencias)
  data-management/  Contratos de persistencia: repositorios, unidad de trabajo, paginación
  business/         Casos de uso: validación, idempotencia, transacciones, reloj de negocio
  data-access/      PostgreSQL con TypeORM: entidades, repositorios, migraciones, catálogo inicial
  contracts/        DTOs HTTP validados (class-validator) y documentados (OpenAPI)
apps/
  api/              NestJS: rutas, scopes, permisos locales, errores RFC 7807, rate limit, Swagger
  auth/             dev-auth: cuentas con contraseña (scrypt) que emiten el JWT del API
AtraccionesService-web/   Portal web
tools/              PostgreSQL local embebido, alta de administradores, soporte de pruebas
```

Garantías principales:

- **Idempotencia** en todas las mutaciones (`Idempotency-Key`): repetir devuelve la misma respuesta sin duplicar efectos.
- **Sin sobreventa**: la reserva de cupos es una actualización condicional atómica en PostgreSQL.
- **Errores RFC 7807** con `code` estable y `traceId`; nunca se exponen detalles internos.
- **Autorización** por scopes del token (`attractions:read|book|cancel|write`) y, para administrar el catálogo,
  además un permiso local en base de datos.

## Desarrollo local

Requisitos: Node.js 22 o superior. No hace falta instalar PostgreSQL: se usa uno embebido.

```bat
start-local.cmd          :: instala, compila y arranca PostgreSQL, auth, API y web
start-local.cmd -Stop    :: detiene todo
```

| Servicio | URL |
|---|---|
| Frontend | http://localhost:5173 |
| Swagger del API | http://localhost:5276/docs |
| Autenticación | http://localhost:5280 |
| PostgreSQL | `postgres://postgres:tourgirls-local@localhost:5433/tourgirls` |

La primera ejecución crea `apps/api/.env` y `apps/auth/.env` (ignorados por git) con un secreto JWT compartido.
Para usar tu propio PostgreSQL, cambia `DATABASE_URL` en esos archivos.

**Administrador local:** regístrate en la web como `admin@tourgirls.test` (está en `ADMIN_EMAILS`), inicia sesión
una vez y ejecuta `node tools/grant-admin.mjs admin@tourgirls.test`. Después cierra sesión y vuelve a entrar.

### Swagger

1. `POST http://localhost:5280/auth/login` con `{ "email": "...", "password": "..." }`.
2. Copia `access_token`, pulsa **Authorize** en http://localhost:5276/docs y pégalo.

## Scripts

| Comando | Uso |
|---|---|
| `npm run build` | Compila todos los paquetes, el API y auth |
| `npm test` | Pruebas unitarias y e2e (estas últimas con un PostgreSQL embebido temporal) |
| `npm run db:local` | Solo el PostgreSQL local |
| `npm run migration:generate -- packages/data-access/src/migrations/Nombre` | Nueva migración a partir de las entidades |

Las migraciones se aplican solas al arrancar el API (`DB_MIGRATIONS_RUN=true`).

## Despliegue

El sistema está publicado en Azure y se despliega solo con cada push a `main` (workflows en `.github/workflows/`):

| Servicio | URL |
|---|---|
| Frontend | https://red-pebble-08e84d70f.1.azurestaticapps.net |
| Swagger del API | https://tourgirls-api-mms-hagtfccfdddceheu.westus2-01.azurewebsites.net/docs |
| Salud del API | https://tourgirls-api-mms-hagtfccfdddceheu.westus2-01.azurewebsites.net/health |

La URL base del API es `https://tourgirls-api-mms-hagtfccfdddceheu.westus2-01.azurewebsites.net/api/v1`: es solo el
prefijo de los endpoints (no tiene página propia) y todos exigen un token; para probarlos, usa Swagger.

Guía paso a paso para Azure (App Service + Azure Database for PostgreSQL + Static Web Apps): [DESPLIEGUE_AZURE.md](DESPLIEGUE_AZURE.md).
