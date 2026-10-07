# AtraccionesService-web · TourGirls

Portal web para descubrir y reservar atracciones de Ecuador. React 19 + TypeScript + Vite + Tailwind CSS 3,
con el diseño de las páginas de destino de Viator y la paleta rosada del plan
([PLAN_IMPLEMENTACION_WEB.md](../PLAN_IMPLEMENTACION_WEB.md)).

## Puesta en marcha local (todo el entorno)

Desde la raíz del repositorio:

```bat
start-local.cmd            :: arranca emisor OAuth2, API y frontend en ventanas separadas
start-local.cmd -Stop      :: detiene los tres
```

| Servicio | URL |
|---|---|
| Frontend | http://localhost:5173 |
| Swagger del API | http://localhost:5276/swagger (botón **Authorize** → login con PKCE) |
| Emisor OAuth2 de desarrollo | http://localhost:5280 (`tools/dev-oauth/server.mjs`) |

El emisor de desarrollo ofrece dos usuarios: **Cliente de desarrollo** (el del seed, con una compra pagada) y
**Admin de desarrollo** (con `attractions:write`; el script le asigna el rol `admin` en la base SQLite mediante
`tools/dev-oauth/grant-admin.mjs`). Es solo para desarrollo: en producción se configura el issuer real.

Solo el frontend: `npm install && npm run dev` (usa `.env.development`; para otros entornos copia `.env.example` a `.env`).

| Script | Uso |
|---|---|
| `npm run dev` | Servidor de desarrollo |
| `npm run build` | Comprobación de tipos y build de producción en `dist/` |
| `npm run lint` | ESLint |
| `npm test` | Vitest (unitarias y de componentes) |

## Catálogo y autenticación

- El contrato exige `attractions:read` incluso para leer el catálogo. Con `VITE_CATALOG_SOURCE=auto` (por defecto),
  sin sesión se muestra un catálogo de demostración (`src/features/attractions/demoCatalog.ts`) y con sesión, el API.
  Las tres primeras atracciones de demostración comparten id con el seed de desarrollo del API.
- Login con OAuth2 Authorization Code + PKCE. El access token vive solo en memoria: recargar la pestaña cierra la sesión.
- Reservas, compras, pedidos y pagos siempre usan el API, con `Idempotency-Key` por intento lógico de mutación.

## Estructura

```
src/
  api/          clientes HTTP por recurso (client.ts centraliza token, errores e idempotencia)
  app/          router, layout y rutas
  components/   common, layout, attractions, search, reservation, purchase, profile, ui
  features/     auth (PKCE, rutas protegidas), attractions (filtros, catálogo), content
  hooks/        useAuth, useAttractions, useReservations, usePurchase, usePagination, useAsync
  pages/        una carpeta por página; admin/ agrupa el área administrativa
  stores/       Zustand: auth, compra, UI, favoritos, pedidos recientes
  styles/       tokens, globales y componentes
  utils/        formato, fechas, validación (zod), errores, idempotencia
```

## Docker

```bash
docker build -t atracciones-web --build-arg VITE_API_BASE_URL=https://api.example/api/v1 \
  --build-arg VITE_OAUTH_CLIENT_ID=atracciones-web ... .
docker run -p 8080:80 atracciones-web
```

Fotografías de demostración: Wikimedia Commons (CC BY, CC BY-SA, CC0 y dominio público).
