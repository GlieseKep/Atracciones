# Despliegue de TourGirls en Azure

| Componente | Carpeta | Recurso de Azure | Comando de inicio |
|---|---|---|---|
| API REST + Swagger | `apps/api` | App Service (Linux, Node 22) | `node apps/api/dist/main.js` |
| Autenticación (dev-auth) | `apps/auth` | App Service en el **mismo plan** | `node apps/auth/dist/main.js` |
| Base de datos | — | Azure Database for PostgreSQL – Flexible Server | — |
| Web | `AtraccionesService-web` | Static Web Apps (Free) | — |

Flujo: la web llama a dev-auth (`/auth/register`, `/auth/login`) y recibe un JWT firmado con `AUTH_JWT_SECRET`; con ese
token llama al API, que verifica la firma, el emisor (`AUTH_ISSUER`) y la audiencia (`AUTH_AUDIENCE`).
**Los tres valores deben ser idénticos en ambas apps.**

Los nombres de ejemplo son `tourgirls-api`, `tourgirls-auth`, `tourgirls-web` y `tourgirls-db`. Si los tuyos son
distintos, ajusta las URLs en las variables de abajo y en `AtraccionesService-web/.env.production`.

## 1. Recursos

En el portal (o con `az`), dentro de un grupo de recursos `rg-tourgirls` y **la misma región** para todo:

1. **PostgreSQL Flexible Server** — Desarrollo/pruebas, **Burstable B1ms**, 32 GiB, PostgreSQL 16 o superior,
   autenticación de PostgreSQL. En *Redes*: acceso público y **permitir servicios de Azure**. Crea la base `tourgirls`.
2. **App Service plan** Linux **B1** y dos Web Apps con pila **Node 22 LTS**: `tourgirls-api` y `tourgirls-auth`.
   Activa **Always On** en ambas.
3. **Static Web App** (plan Free) para la web.

## 2. Secreto compartido

```powershell
node -e "console.log(require('crypto').randomBytes(48).toString('base64url'))"
```

Guárdalo: va **solo** en las App Settings de las dos Web Apps, nunca en el repositorio.

## 3. Variables de entorno (App Service → Configuración → Variables de entorno)

Cadena de conexión (codifica caracteres especiales de la contraseña, p. ej. `@` → `%40`):
`postgres://USUARIO:CONTRASEÑA@tourgirls-db.postgres.database.azure.com:5432/tourgirls`

**API (`tourgirls-api`)**

| Nombre | Valor |
|---|---|
| `DATABASE_URL` | la cadena de conexión |
| `DATABASE_SSL` | `true` |
| `AUTH_JWT_SECRET` | el secreto del paso 2 |
| `AUTH_ISSUER` | `tourgirls-auth` |
| `AUTH_AUDIENCE` | `tourgirls-api` |
| `CORS_ORIGINS` | URL de la Static Web App, sin `/` final |
| `TRUST_PROXY` | `true` |
| `SWAGGER_ENABLED` | `true` |
| `PUBLIC_API_URL` | `https://tourgirls-api.azurewebsites.net` |
| `SCM_DO_BUILD_DURING_DEPLOYMENT` | `true` |

Opcionales: `DB_SEED_CATALOG` (`true` por defecto: carga las 10 atracciones de ejemplo si la base está vacía y mantiene
franjas para los próximos `DB_AVAILABILITY_DAYS`, 60 por defecto), `REQUIRE_VERIFIED_EMAIL` (`false` por defecto, porque
dev-auth no verifica correos), `RATE_LIMIT_PERMIT`, `APP_TIMEZONE`, `HOLD_MINUTES`, `PAGE_TOKEN_SECRET`.

**Autenticación (`tourgirls-auth`)**

| Nombre | Valor |
|---|---|
| `DATABASE_URL` | la misma cadena de conexión |
| `DATABASE_SSL` | `true` |
| `AUTH_JWT_SECRET` | **el mismo** secreto |
| `AUTH_ISSUER` / `AUTH_AUDIENCE` | `tourgirls-auth` / `tourgirls-api` |
| `CORS_ORIGINS` | URL de la web y URL del API (para Swagger), separadas por coma |
| `ADMIN_EMAILS` | correos que reciben el scope de administración (opcional) |
| `TRUST_PROXY` | `true` |
| `SCM_DO_BUILD_DURING_DEPLOYMENT` | `true` |

> No definas `NODE_ENV=production`: Azure compila en el servidor y necesita las dependencias de desarrollo
> (TypeScript). `PORT` tampoco: App Service lo define y las apps lo leen.

En **Configuración general → Comando de inicio** pon el de la tabla inicial y en **Comprobación de estado** la ruta `/health`.

## 4. Publicar el backend

```powershell
powershell -ExecutionPolicy Bypass -File tools\package-backend.ps1
az webapp deploy -g rg-tourgirls -n tourgirls-api  --src-path out\backend.zip --type zip
az webapp deploy -g rg-tourgirls -n tourgirls-auth --src-path out\backend.zip --type zip
```

Azure instala dependencias y ejecuta `npm run build`. Al arrancar, el API aplica las migraciones pendientes y carga el
catálogo inicial. Comprueba:

- `https://tourgirls-api.azurewebsites.net/health` → `{"status":"ok"}`
- `https://tourgirls-api.azurewebsites.net/docs` → Swagger
- `https://tourgirls-auth.azurewebsites.net/health` → `{"status":"ok"}`

## 5. Publicar la web

Revisa `AtraccionesService-web/.env.production` (`VITE_API_URL`, `VITE_AUTH_URL`). En la Static Web App: ubicación de la
aplicación `/AtraccionesService-web`, salida `dist`. `public/staticwebapp.config.json` ya hace que rutas como
`/admin` o `/perfil` no den 404 al recargar.

## 6. Primer administrador

1. Regístrate en la web con un correo incluido en `ADMIN_EMAILS` de dev-auth.
2. Inicia sesión una vez (crea tu perfil en el API).
3. Desde tu PC (con tu IP permitida en las reglas de red de PostgreSQL):
   ```powershell
   $env:DATABASE_URL = "postgres://USUARIO:CONTRASEÑA@tourgirls-db.postgres.database.azure.com:5432/tourgirls?sslmode=require"
   node tools/grant-admin.mjs tu-correo@ejemplo.com
   ```
4. Cierra sesión y vuelve a entrar: el panel `/admin` ya permite editar el catálogo.

## Problemas frecuentes

| Síntoma | Causa | Solución |
|---|---|---|
| La app no arranca y el log dice "Configuración inválida" | Falta una variable o el secreto tiene menos de 32 caracteres | Revisar el paso 3 |
| 401 en el API tras iniciar sesión | `AUTH_JWT_SECRET`, `AUTH_ISSUER` o `AUTH_AUDIENCE` distintos entre apps | Igualarlos |
| La web muestra errores de conexión | `CORS_ORIGINS` sin la URL exacta de la web, o `VITE_*` mal al compilar | Revisar ambos |
| Error de conexión a PostgreSQL | Falta `DATABASE_SSL=true` o permitir servicios de Azure en Redes | Paso 1 y 3 |
| El build falla en Azure por TypeScript | `NODE_ENV=production` definido | Quitarlo |
