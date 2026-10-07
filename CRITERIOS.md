# Evaluación de criterios · TourGirls

Revisión del código del repositorio y del despliegue en Azure, hecha el 07/10/2026 sobre el commit `2cff3b8`.
Actualizada después de añadir [ARQUITECTURA.md](ARQUITECTURA.md), [INTEROPERABILIDAD.md](INTEROPERABILIDAD.md) y
[EVENTOS.md](EVENTOS.md).

## Resumen

| # | Criterio | Resultado | Puntos | Evidencia principal |
|---|---|---|---|---|
| 1 | Sistema desplegado y accesible públicamente en la nube | ✅ Cumple | 1 | URLs públicas en Azure, CI/CD |
| 2 | Sistema de administración funcional (CRUD, gestión operativa, navegación) | ✅ Cumple | 1 | Panel `/admin`, 9 secciones |
| 3 | Marketplace web funcional (consulta, publicación, flujo de venta) | ✅ Cumple | 1 | Búsqueda, compra, pago simulado |
| 4 | APIs implementadas y documentadas con OpenAPI/Swagger | ✅ Cumple | 1 | `/docs`, `/openapi/v1.json` |
| 5 | Base de datos operativa que soporta la solución | ✅ Cumple | 1 | PostgreSQL en Azure, migraciones |
| 6 | Diseño API-first y preparación para integración futura | ✅ Cumple | 1 | `CONTRATO.md`, `packages/contracts` |
| 7 | Contratos o endpoints identificados y documentados para interoperabilidad | ✅ Cumple | 1 | [INTEROPERABILIDAD.md](INTEROPERABILIDAD.md) |
| 8 | Diseño preliminar de eventos o servicios para integración futura (SOA/EDA) | ✅ Cumple (diseño) | 1 | [EVENTOS.md](EVENTOS.md) |
| 9 | Documentación técnica mínima (arquitectura, modelo de datos, APIs) | ✅ Cumple | 1 | [ARQUITECTURA.md](ARQUITECTURA.md) + Swagger |

**Estimación:** 9 de 9. Ya no queda ningún criterio incumplido. Las observaciones de cada sección son mejoras
opcionales y no afectan a la calificación. El criterio 8 se cumple como **diseño preliminar**, que es lo que pide la
rúbrica: el outbox, Service Bus y los webhooks no están implementados en el código.

---

## 1. Sistema desplegado y accesible públicamente — ✅ Cumple

**Evidencia** (comprobado con peticiones HTTP el 07/10/2026; todas respondieron 200):

| Componente | Servicio de Azure | URL pública |
|---|---|---|
| Web | Static Web Apps | https://red-pebble-08e84d70f.1.azurestaticapps.net |
| API REST | App Service (Linux, Node) | https://tourgirls-api-mms-hagtfccfdddceheu.westus2-01.azurewebsites.net/health |
| Swagger | App Service | https://tourgirls-api-mms-hagtfccfdddceheu.westus2-01.azurewebsites.net/docs |
| Autenticación | App Service (mismo plan) | https://tourgirls-auth-mms-akcxfugse5c6g6bd.westus2-01.azurewebsites.net/health |
| Base de datos | Azure Database for PostgreSQL – Flexible Server | `tourgirls-db-mms` (privada; solo la usan las apps) |

- **Despliegue continuo.** Cada push a `main` despliega solo, con tres workflows en `.github/workflows/`:
  - `deploy-api.yml` y `deploy-auth.yml` compilan, empaquetan con `tools/package-backend.mjs` y publican en App Service;
  - el workflow de Static Web Apps publica la web.
- **Navegación interna.** Las rutas internas de la web, como `/admin/pedidos`, no dan 404 al recargar gracias a
  `public/staticwebapp.config.json`.

**Observaciones (no impiden cumplir):**
- Las apps están en West US 2 y la base de datos en otra región (por su IP, Brasil Sur). Funciona, pero cada consulta
  suma latencia; conviene tener todo en la misma región.
- Sin uso, la primera respuesta puede tardar unos segundos si **Always On** no está activado.

---

## 2. Sistema de administración funcional — ✅ Cumple

**Evidencia.** Hay un panel en `/admin` con navegación lateral y nueve secciones. Lo protegen dos controles:
- en la web, `ProtectedRoute` exige el scope `attractions:write`;
- en el servidor, se exigen además los permisos locales `catalog:write` y `admin:manage`.

| Sección | Funcionalidad | Código |
|---|---|---|
| Dashboard | Ingresos, clientes, reservas, pedidos, pagos fallidos y ocupación a 30 días, con acceso a cada sección | `AdminDashboardPage.tsx`, `GET /admin/summary` |
| Catálogo | **CRUD completo**: alta, edición total (PUT) y parcial (PATCH), baja (bloqueada si hay reservas activas) y listado paginado | `AdminCatalogPage.tsx`, `POST/PUT/PATCH/DELETE /atracciones` |
| Disponibilidad | Franjas con ocupación, cambio de capacidad (nunca por debajo de lo reservado) y alta de franjas | `AdminAvailabilityPage.tsx`, `/admin/availability` |
| Reservas | Reservas de todos los clientes con búsqueda, filtro de estado y fechas; enlace al pedido de origen | `AdminOperationsPages.tsx`, `/admin/reservations` |
| Pedidos | Detalle con historial y pagos, cancelación de pendientes y reembolsos simulados totales o parciales | `AdminOrdersPage.tsx`, `/admin/orders/*` |
| Pagos | Simulaciones de pago con intentos y estado | `/admin/payments` |
| Clientes | Totales por cliente; activar, bloquear o desactivar la cuenta | `AdminCustomersPage.tsx`, `/admin/customers` |
| Usuarios y roles | Roles con sus permisos; asignar o revocar el rol `admin` | `/admin/roles`, `/admin/customers/{id}/roles` |
| Reportes | Ventas por día y por atracción, ticket medio y reservas confirmadas por rango | `AdminReportsPage.tsx`, `/admin/reports/sales` |

**Garantías del lado del servidor:**
- Los cambios administrativos quedan registrados en `audit_events`.
- Las operaciones de dinero (cancelar y reembolsar) usan `Idempotency-Key`.
- Un administrador no puede desactivarse ni quitarse su propio rol.
- La cobertura está en `apps/api/test/admin.e2e.test.ts`: 6 pruebas contra PostgreSQL real.

---

## 3. Marketplace web funcional — ✅ Cumple

**Consulta:**
- inicio, listado y búsqueda por destino, fechas y valoración, con filtros y orden;
- detalle de la atracción con fotos, operador, incluidos e idiomas;
- disponibilidad por franja;
- favoritos.

Páginas: `HomePage`, `AttractionsPage`, `SearchPage`, `AttractionDetailPage`, `WishlistPage`.

**Publicación:** el catálogo se publica desde el panel (criterio 2). Lo publicado aparece en el marketplace con el mismo
identificador.

**Flujo de venta definido y completo:**
1. Registro o inicio de sesión (`/registro`, `/login`) contra el servicio de autenticación (JWT).
2. Elegir fecha, franja y número de entradas.
3. **Compra directa** (`POST /attractions/{id}/purchase`). Crea el pedido `PENDING_PAYMENT` con una retención de cupos
   temporal y una reserva `PENDING`.
4. **Pago simulado** (`POST /payments/simulations`) con tarjeta o transferencia. Si se aprueba, el pedido pasa a `PAID`
   y la reserva a `CONFIRMED`. Los importes terminados en .51 o .52 simulan un rechazo y un fallo.
5. Consulta del pedido y de su historial (`OrderPage`), y cancelación de la reserva o del pedido pendiente.
6. Alternativa sin pago: reserva directa (`ReservationPage`).

**Garantías:**
- Precios calculados en el servidor.
- Sin sobreventa: `UPDATE` condicional atómico.
- Idempotencia en cada mutación.
- Pruebas e2e de compra, pago, cancelación, concurrencia e idempotencia en `apps/api/test/api.e2e.test.ts`.

**Observación:** sin iniciar sesión, la web muestra el **catálogo de demostración** (`demoCatalog.ts`). El contrato
exige el scope `attractions:read` incluso para leer, y con sesión los datos vienen del API. Si el evaluador navega sin
cuenta, no está viendo la base de datos. **Mejora:** marcar `GET /atracciones`, `POST /atracciones/search`,
`GET /atracciones/{id}` y `/availability` como `@Public()` en `catalog.controllers.ts`, para que el catálogo público
también salga de PostgreSQL.

---

## 4. APIs implementadas y documentadas con OpenAPI/Swagger — ✅ Cumple

**Evidencia:**
- **API REST en NestJS** con prefijo versionado `/api/v1` y **35 rutas** publicadas en `/openapi/v1.json` (OpenAPI 3.0).
- **Swagger UI** en `/docs`, con autenticación Bearer (**Authorize**) y la URL pública del servidor (`PUBLIC_API_URL`).
- **Grupos de endpoints:** Catálogo, Reservas, Identidad, Clientes, Compras, Pedidos, Pagos y Administración.
- **Documentación de cada endpoint:** los DTO del paquete `packages/contracts` documentan esquemas, ejemplos y
  restricciones con `@ApiProperty`, y los validan con `class-validator`.
- **Errores** estandarizados RFC 7807 (`application/problem+json`) con `code` estable y `traceId`.

**Observaciones y mejoras:**
- Los endpoints de `/admin/*` documentan la operación y los parámetros, pero **no el esquema de respuesta**. Devuelven
  objetos sin `@ApiResponse({ type })`. **Mejora:** crear clases de respuesta en `packages/contracts/src/admin.ts` y
  anotarlas.
- **dev-auth** (`/auth/register`, `/auth/login`) no tiene Swagger propio; solo está explicado en el README.
  **Mejora:** añadir `SwaggerModule` en `apps/auth/src/main.ts`.
- Redoc es opcional; se puede servir el mismo `/openapi/v1.json` con Redoc si se pide.

---

## 5. Base de datos operativa — ✅ Cumple

**Evidencia:**
- **PostgreSQL** administrado en Azure (Flexible Server, versión 18.6). Comprobé la conexión con SSL a la base
  `tourgirls`.
- **Esquema versionado con migraciones TypeORM** (`packages/data-access/src/migrations`). Se aplican solas al arrancar
  el API: `InitialSchema` (29 tablas) y `AdminRole`.
- **Integridad en la base:**
  - claves foráneas;
  - restricciones `CHECK` de estados, cantidades e importes;
  - unicidades (franja por atracción, fecha y hora; identidad OAuth; clave de idempotencia);
  - índices por consulta frecuente.
- **Concurrencia:**
  - la reserva de cupos es un `UPDATE ... WHERE reserved_quantity + n <= capacity`;
  - los conflictos se traducen a 409;
  - una prueba con solicitudes simultáneas verifica que no hay sobreventa.
- **Datos iniciales:** 10 atracciones y franjas para los próximos 60 días (`seed/`).
- **Uso real verificado:** registro, catálogo, compra y pago funcionan contra la base de Azure. La cuenta
  `admin@ejemplo.com` tiene sus permisos guardados ahí.
- **Pruebas:** las e2e corren contra un PostgreSQL real embebido; pasan 51 pruebas del backend.

---

## 6. Diseño API-first y preparación para integración — ✅ Cumple

**Evidencia:**
- **El contrato existió antes que el código.** `CONTRATO.md` es una especificación OpenAPI 3.0.3 del
  "Microservicio de Atracciones del Marketplace Turístico". Lo revisaron `ALLASGOZ.md` (auditoría de inconsistencias),
  `CORRECCIONES_CONTRATO.md` (correcciones P0/P1/P2) y `AUDITORIA_CONTRATOS_ECOMMERCE.md`, y la implementación sigue
  esas correcciones.
- **El contrato es una capa propia del código.** `packages/contracts` contiene los DTO HTTP, separados del dominio
  (`packages/domain`) y de la persistencia (`packages/data-access`). La API se puede cambiar sin tocar las reglas de
  negocio. Ver [ARQUITECTURA.md §3](ARQUITECTURA.md#3-backend-monorepo-por-capas).
- **Convenciones listas para integrar:**
  - versión en la URL (`/api/v1`);
  - JSON en camelCase;
  - fechas ISO 8601, monedas ISO 4217, países ISO 3166 e identificadores UUID;
  - paginación uniforme;
  - `Location` en las creaciones y enlaces HATEOAS en las atracciones;
  - errores RFC 7807;
  - `Idempotency-Key` en todas las mutaciones;
  - OAuth2/JWT con scopes;
  - rate limit y `X-Request-Id`.
- **Servicios separados:** el API y el servicio de autenticación se comunican solo por un JWT estándar, de modo que el
  proveedor de identidad se puede sustituir por uno OIDC.
- **Contrato vigente aclarado.** [INTEROPERABILIDAD.md §1](INTEROPERABILIDAD.md#1-contratos-publicados) establece que
  la fuente de verdad es `/openapi/v1.json` y que `CONTRATO.md` es la versión de diseño v1.2.0. Así queda resuelta la
  diferencia entre los dos.

**Mejora opcional:**
1. Exportar el OpenAPI generado a un archivo versionado en el repositorio.
2. Añadir a CI una comprobación de cambios incompatibles (`oasdiff breaking`).

---

## 7. Contratos o endpoints para interoperabilidad futura — ✅ Cumple

**Evidencia:**
- **Contratos formales publicados:** `/openapi/v1.json` (OpenAPI 3.0, 35 rutas, generado desde el código y publicado
  en Azure), `CONTRATO.md` (diseño) y los DTO de `packages/contracts`.
- **[INTEROPERABILIDAD.md](INTEROPERABILIDAD.md)** identifica de forma explícita:
  - los **endpoints de integración**, agrupados en catálogo, disponibilidad, reservas B2B, venta, pedidos, pagos,
    identidad y operación, cada uno con su **consumidor previsto**, scope y **nivel de estabilidad** (estable,
    provisional o interno);
  - las **reglas comunes**: autenticación (incluido el diseño client credentials para sistemas), idempotencia,
    formatos, paginación, errores RFC 7807 con la tabla de códigos y cuándo reintentar, rate limit y cabeceras;
  - la **política de versiones**: `/api/v1`, cambios compatibles, cabeceras `Deprecation`/`Sunset` y convivencia de
    versiones durante 6 meses;
  - el **plan para sustituir la pasarela simulada** por una real sin romper a los consumidores;
  - los **escenarios de integración** previstos (buscador global, pagos, notificaciones, facturación electrónica,
    agencias B2B, BI y app móvil) y un checklist para nuevos integradores.
- **El scope `attractions:webhooks` ya tiene destino:** queda documentado en INTEROPERABILIDAD §3.1 y diseñado en
  [EVENTOS.md §5](EVENTOS.md#5-webhooks-para-socios-completa-el-scope-attractionswebhooks).

**Mejora opcional:** añadir a Swagger los esquemas de respuesta de `/admin/*` (hoy solo se documentan operación y
parámetros) y publicar el Swagger de dev-auth. Ambos endpoints están clasificados como internos, así que no afectan a
este criterio.

---

## 8. Diseño preliminar de eventos o servicios (SOA/EDA) — ✅ Cumple (diseño)

**Lo implementado, que sirve de base:**
- **Servicios separados (SOA):** API de atracciones y servicio de identidad, desplegados por separado y comunicados
  por un token estándar.
- **Eventos de dominio registrados en la misma transacción que el cambio:**
  - `order_events`: creación, pago, cancelación y reembolsos;
  - `payment_events`: payload `jsonb`;
  - `inventory_movements`;
  - `audit_events`.
- `GET /orders/{id}/events` expone el historial de un pedido.

**Diseño preliminar documentado en [EVENTOS.md](EVENTOS.md):**

| Elemento | Sección |
|---|---|
| Vista de servicios: productores y consumidores (notificaciones, facturación, buscador, BI, webhooks) y responsabilidades | §1 |
| Relación con lo ya implementado: cada tabla de eventos actual es un punto de emisión | §2 |
| **Catálogo de 13 eventos de integración** con productor, momento, método del código que lo emite y consumidores | §3 |
| Formato **CloudEvents 1.0**, ejemplos de payload y reglas de evolución (`.v1` → `.v2`) | §4 |
| **Webhooks** para socios: API de suscripciones, firma HMAC-SHA256, reintentos y deduplicación | §5 |
| Garantía de entrega con el patrón **Transactional Outbox** y **Azure Service Bus** (tabla SQL, secuencia, orden por recurso, DLQ) | §6 |
| Especificación **AsyncAPI 3.0** (borrador) con canales, mensajes y esquemas | §7 |
| Plan de implementación por fases y pruebas previstas | §8 |

**Aclaración:** la rúbrica pide un **diseño preliminar**, y eso es lo que se entrega. El outbox, la publicación en
Service Bus y los webhooks **no están implementados** en el código. Si se quisiera demostrar en funcionamiento, la
fase 1–2 de EVENTOS §8 (tabla `outbox_events` y emisión de `order.paid`) es el paso más corto.

---

## 9. Documentación técnica mínima — ✅ Cumple

| Tema | Documento | Contenido |
|---|---|---|
| **Arquitectura** | [ARQUITECTURA.md](ARQUITECTURA.md) §1–4 | Diagrama de componentes en Azure, relación entre servicios, capas del monorepo con sus dependencias, mecanismos transversales (transacciones, idempotencia, concurrencia, errores) y estructura del frontend |
| **Flujos** | ARQUITECTURA §5 | Diagramas de secuencia de inicio de sesión y de compra con pago; cancelación y reembolso; máquinas de estado |
| **Modelo de datos** | ARQUITECTURA §6 | Diagrama entidad-relación (Mermaid), tablas por área, restricciones de integridad; la fuente de verdad son las migraciones |
| **APIs** | ARQUITECTURA §7, Swagger `/docs`, `/openapi/v1.json`, [INTEROPERABILIDAD.md](INTEROPERABILIDAD.md) | Endpoints por grupo con su scope, documentación interactiva y reglas de uso |
| **Seguridad** | ARQUITECTURA §8 | JWT, scopes, permisos locales, propiedad de los datos, contraseñas, CORS, rate limit y secretos |
| **Despliegue y operación** | ARQUITECTURA §9, `DESPLIEGUE_AZURE.md`, `AZURE_DEPLOY.md`, `README.md` | CI/CD, arranque, entorno local y pruebas |
| **Eventos** | [EVENTOS.md](EVENTOS.md) | Integración asíncrona |

**Documentos anteriores:** ARQUITECTURA.md aclara al inicio que los `PLAN_IMPLEMENTACION_*.md` corresponden al diseño
.NET original y que, ante cualquier diferencia, manda ARQUITECTURA.md. Así ya no confunden al evaluador.

**Mejora opcional:** mover los `PLAN_IMPLEMENTACION_*.md` a una carpeta `docs/historico/` y enlazar los tres
documentos nuevos desde el `README.md`.

---

## Mejoras opcionales (no afectan a la calificación)

| Prioridad | Mejora | Criterio relacionado | Esfuerzo | Requiere código |
|---|---|---|---|---|
| 1 | Hacer pública la lectura del catálogo para que un visitante sin cuenta vea los datos reales de PostgreSQL y no el catálogo de demostración | 3 | Bajo | Sí |
| 2 | Enlazar ARQUITECTURA, INTEROPERABILIDAD y EVENTOS desde el `README.md` | 9 | Muy bajo | No |
| 3 | Esquemas de respuesta en `/admin/*` y Swagger en dev-auth | 4, 7 | Bajo | Sí |
| 4 | Exportar el OpenAPI a un archivo versionado y comprobar cambios incompatibles en CI | 6 | Bajo | Sí (CI) |
| 5 | Implementar la fase 1–2 del outbox (`order.paid`) como demostración de EDA | 8 | Medio | Sí |
| 6 | Mover los planes .NET a `docs/historico/` | 9 | Muy bajo | No |
