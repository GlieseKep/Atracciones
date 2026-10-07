# Interoperabilidad · API de Atracciones TourGirls

Este documento identifica los **contratos y endpoints** que el servicio de atracciones ofrece a otros sistemas, y las
reglas que un integrador debe seguir. Va dirigido a equipos que integren otros servicios del marketplace (búsqueda,
pagos, notificaciones, facturación), a socios B2B (agencias, operadores) y a un futuro API Gateway.

Documentos relacionados:
- [ARQUITECTURA.md](ARQUITECTURA.md): arquitectura, modelo de datos y seguridad.
- [EVENTOS.md](EVENTOS.md): integración asíncrona por eventos y webhooks.

---

## 1. Contratos publicados

| Contrato | Ubicación | Uso |
|---|---|---|
| **OpenAPI 3.0 (fuente de verdad)** | `GET /openapi/v1.json` → https://tourgirls-api-mms-hagtfccfdddceheu.westus2-01.azurewebsites.net/openapi/v1.json | Se genera desde el código: siempre coincide con lo desplegado. Sirve para generar clientes (OpenAPI Generator, NSwag, Kiota) y para pruebas de contrato |
| **Swagger UI** | `GET /docs` | Exploración y pruebas manuales con **Authorize** (Bearer) |
| **Contrato de diseño** | [CONTRATO.md](CONTRATO.md) (OpenAPI 3.0.3, v1.2.0) | Contrato API-first original de catálogo y reservas; las mejoras se registraron en `ALLASGOZ.md` y `CORRECCIONES_CONTRATO.md` |
| **DTOs tipados** | `packages/contracts` | Definición con validaciones (`class-validator`), reutilizable por servicios TypeScript |
| **Eventos (diseño)** | [EVENTOS.md](EVENTOS.md) | Catálogo de eventos, formato CloudEvents, AsyncAPI y webhooks |

> Si `CONTRATO.md` y `/openapi/v1.json` difieren, manda **`/openapi/v1.json`**. `CONTRATO.md` documenta la versión
> de diseño v1.2.0 y no incluye compras, pedidos, pagos ni administración, que se agregaron después.

**URL base de producción:** `https://tourgirls-api-mms-hagtfccfdddceheu.westus2-01.azurewebsites.net/api/v1`.
Detrás de un API Gateway, la ruta pública recomendada es `https://<gateway>/atracciones/api/v1`.

---

## 2. Endpoints de integración

**Estabilidad:**
- **Estable**: no tendrá cambios incompatibles dentro de `v1`.
- **Provisional**: puede cambiar en una versión menor, con aviso.
- **Interno**: solo para la web y el panel de TourGirls; no se garantiza a terceros.

### 2.1 Consulta de catálogo y disponibilidad

| Endpoint | Consumidor previsto | Scope | Estabilidad |
|---|---|---|---|
| `POST /atracciones/search` | Buscador global del marketplace, agregadores, metabuscadores | `attractions:read` | Estable |
| `POST /atracciones/details` | Servicios que necesitan varias fichas por id (carrito global, comparadores, recomendaciones) | `attractions:read` | Estable |
| `GET /atracciones` | Sincronización por lotes del catálogo (paginado) | `attractions:read` | Estable |
| `GET /atracciones/{id}` | Ficha de detalle, enlaces profundos | `attractions:read` | Estable |
| `GET /atracciones/{id}/availability?date=YYYY-MM-DD` | Canales de venta externos, chatbots, agencias | `attractions:read` | Estable |

Ejemplo de búsqueda:

```http
POST /api/v1/atracciones/search
Authorization: Bearer <token>
Content-Type: application/json

{
  "cities": ["Quito"],
  "countries": ["EC"],
  "dates": { "startDate": "2026-11-01", "endDate": "2026-11-07" },
  "filters": { "rating": { "minimumReviewScore": 4 } },
  "sort": { "by": "rating_desc" },
  "rows": 20
}
```

La respuesta incluye `data[]`, `metadata.totalResults` y `metadata.nextPage`. Para la página siguiente se envía el
mismo cuerpo con `nextPage`; el token está firmado, ligado a los criterios y dura 15 minutos.

### 2.2 Reservas (socios B2B)

| Endpoint | Consumidor previsto | Scope | Estabilidad |
|---|---|---|---|
| `POST /atracciones/{id}/reservations` | Agencias y operadores que reservan sin cobro en línea | `attractions:book` | Estable |
| `GET /atracciones/reservations` | Historial del socio autenticado (filtros `status`, `fromDate`, `toDate`, `sort`) | `attractions:read` | Estable |
| `GET /atracciones/reservations/{reservationId}` | Consulta puntual | `attractions:read` | Estable |
| `POST /atracciones/reservations/{reservationId}/cancel` | Cancelación por el socio | `attractions:cancel` | Estable |

Cada reserva pertenece a la identidad del token (`iss` + `sub`). Un socio solo ve y cancela sus propias reservas.

### 2.3 Venta, pedidos y pagos

| Endpoint | Consumidor previsto | Scope | Estabilidad |
|---|---|---|---|
| `POST /attractions/{attractionId}/purchase` | Checkout de otro frontal (app móvil, kiosco) | `attractions:book` | Estable |
| `POST /orders` | Igual que el anterior, en dos pasos (pedido y luego pago) | `attractions:book` | Estable |
| `GET /orders/{orderId}` | Facturación, atención al cliente | `attractions:read` | Estable |
| `GET /orders/{orderId}/events` | Conciliación y seguimiento del ciclo de vida | `attractions:read` | Estable |
| `POST /orders/{orderId}/cancel` | Cancelación de pedidos pendientes | `attractions:cancel` | Estable |
| `POST /payments/simulations` | **Pasarela simulada**: se reemplazará por una pasarela real | `attractions:book` | **Provisional** |

**Plan para la pasarela real:**
1. El pedido (`POST /orders`) seguirá igual.
2. `POST /payments/simulations` se sustituirá por `POST /payments` y devolverá una URL o token de la pasarela.
3. La confirmación llegará por un webhook entrante `POST /payments/webhooks/{proveedor}`, firmado por la pasarela.
4. Mientras tanto, se publican los eventos `order.paid` y `payment.failed` (ver [EVENTOS.md](EVENTOS.md)), que no
   cambian al pasar a la pasarela real.

### 2.4 Identidad del usuario final

| Endpoint | Consumidor previsto | Scope | Estabilidad |
|---|---|---|---|
| `POST /auth/register` (API) | Cualquier frontal, tras obtener el token: crea el perfil local (idempotente) | `attractions:book` | Estable |
| `GET /users/me`, `GET/PUT /customers/me` | Frontales que muestran o editan datos de facturación | `read` / `book` | Estable |
| `POST /auth/register`, `POST /auth/login` (**dev-auth**) | Solo la web de TourGirls | — | **Interno**, sustituible por OAuth2/OIDC |

### 2.5 Operación y administración

| Endpoint | Consumidor previsto | Requisito | Estabilidad |
|---|---|---|---|
| `GET /health` | Balanceadores, App Service, monitorización | Público | Estable |
| `/admin/*` (resumen, clientes, reservas, pedidos, pagos, disponibilidad, roles, reportes) | Panel de TourGirls; a futuro, BI y backoffice | `attractions:write` + permiso local `admin:manage` | **Interno** |
| `POST/PUT/PATCH/DELETE /atracciones` | Panel; a futuro, importación desde un PIM u operador | `attractions:write` + `catalog:write` | Provisional para terceros |

---

## 3. Reglas comunes para integradores

### 3.1 Autenticación y autorización

**Hoy:**
- Bearer JWT HS256 emitido por dev-auth, con `iss=tourgirls-auth`, `aud=tourgirls-api`, `sub`, `exp` y `scope`
  (separados por espacio).
- Duración de 1 hora.

**Para sistemas (máquina a máquina), diseño previsto:**
- Flujo OAuth2 **client credentials** contra un proveedor OIDC (Entra ID, Auth0, Keycloak), que reemplaza dev-auth.
- El API valida firma, `iss` y `aud`, igual que hoy. Para cambiar de proveedor solo hay que cambiar la clave y el
  emisor configurados (`AUTH_JWT_SECRET` o JWKS, y `AUTH_ISSUER`).
- Cada sistema recibe **solo los scopes que necesita**:

| Scope | Permite |
|---|---|
| `attractions:read` | Leer catálogo, disponibilidad y reservas o pedidos propios |
| `attractions:book` | Reservar, comprar y pagar |
| `attractions:cancel` | Cancelar reservas o pedidos propios |
| `attractions:write` | Administración (además requiere permiso local en base de datos) |
| `attractions:webhooks` | *(Diseño)* Gestionar suscripciones de webhooks; ver [EVENTOS.md](EVENTOS.md) |

**Errores de autenticación:** sin token o con token inválido se responde **401** con `WWW-Authenticate: Bearer`. Con
un scope insuficiente se responde **403** con `code: INSUFFICIENT_SCOPE`.

### 3.2 Idempotencia (obligatoria en todas las mutaciones)

```http
Idempotency-Key: 3f6c1b7e-2d4a-4c0e-9a51-7b2f0e8d1c44
```

- Debe ser un UUID **por intento lógico**. Los reintentos por timeout o error de red usan **la misma** clave.
- Misma clave y mismo cuerpo: se devuelve la **misma respuesta original**, sin repetir efectos.
- Misma clave y otro cuerpo: **409** `IDEMPOTENCY_KEY_REUSED`.
- Misma clave mientras la primera solicitud sigue en proceso: **409** `IDEMPOTENCY_IN_PROGRESS`; reintentar más tarde.
- Las claves se recuerdan **24 horas** por identidad (`iss` + `sub`) y operación.

### 3.3 Formato de datos

| Dato | Formato |
|---|---|
| JSON | Propiedades en `camelCase`; se rechazan propiedades desconocidas (400) |
| Fechas | `YYYY-MM-DD` (fecha local de la atracción, zona America/Guayaquil) |
| Hora de franja | `HH:mm` 24 h, hora local |
| Instantes | ISO 8601 con zona (`2026-10-07T15:04:05.000Z`) |
| Dinero | `{ "currency": "USD", "total": 25.5 }`, ISO 4217 con un máximo de 2 decimales; el precio lo fija el servidor |
| País | ISO 3166-1 alfa-2 (`EC`) |
| Duración | ISO 8601 (`PT2H30M`) |
| Identificadores | UUID |
| Enlaces | Las creaciones devuelven `Location`; las atracciones incluyen enlaces HATEOAS |

### 3.4 Paginación

| Tipo | Parámetros | Respuesta |
|---|---|---|
| Listados | `limit` (1–100, por defecto 20) y `offset` | `{ data, totalItems, itemsPerPage, currentPage, totalPages }` |
| Catálogo (`GET /atracciones`) | Igual | `{ data, meta: { totalItems, itemCount, itemsPerPage, totalPages, currentPage } }` |
| Búsqueda | `rows` y `nextPage` (token firmado) | Paginación por cursor |

### 3.5 Errores (RFC 7807)

```json
{
  "type": "urn:atracciones:problems:insufficient-availability",
  "title": "Conflicto",
  "status": 409,
  "detail": "No hay cupos suficientes para la franja solicitada.",
  "code": "INSUFFICIENT_AVAILABILITY",
  "instance": "/api/v1/attractions/…/purchase",
  "traceId": "c466e3e1-7a69-46ce-aeb4-f2be97ac4eb0"
}
```

- El integrador debe decidir según **`code`**, que es estable, y no según `detail`, cuyo texto puede cambiar.
- Los errores de validación añaden `errors: { campo: [mensajes] }`.
- `traceId` coincide con la cabecera `X-Request-Id`; sirve para pedir soporte.

| HTTP | Códigos habituales | ¿Reintentar? |
|---|---|---|
| 400 | `VALIDATION_ERROR` | No; corregir la solicitud |
| 401 | `UNAUTHORIZED` | Renovar el token |
| 403 | `INSUFFICIENT_SCOPE`, `FORBIDDEN`, `PROFILE_NOT_REGISTERED`, `USER_INACTIVE` | No |
| 404 | `NOT_FOUND` | No |
| 409 | `INSUFFICIENT_AVAILABILITY`, `SLOT_UNAVAILABLE`, `INVALID_STATE_TRANSITION`, `IDEMPOTENCY_KEY_REUSED`, `IDEMPOTENCY_IN_PROGRESS`, `ORDER_HOLD_EXPIRED`, `PAYMENT_ATTEMPTS_EXCEEDED` | Solo `IDEMPOTENCY_IN_PROGRESS`, con la misma clave |
| 422 | `AMOUNT_MISMATCH` (pago) | No |
| 429 | `RATE_LIMITED` | Sí, tras `Retry-After` |
| 5xx | `INTERNAL_ERROR` | Sí, con backoff y la misma `Idempotency-Key` |

### 3.6 Límites y cabeceras

| Elemento | Detalle |
|---|---|
| Rate limit | 100 solicitudes por minuto por identidad (`sub`) o por IP sin token; al superarlo, **429** con `Retry-After` |
| Tamaño máximo del cuerpo | 256 KB |
| Cabeceras expuestas por CORS | `Location`, `Retry-After`, `X-Request-Id` |
| Orígenes permitidos | Se configuran en `CORS_ORIGINS`; para integraciones de servidor a servidor no aplica CORS |

---

## 4. Política de versiones

1. **La versión va en la URL:** `/api/v1`. Una versión mayor (`/api/v2`) se publica solo si hay **cambios
   incompatibles**: quitar o renombrar campos o endpoints, cambiar tipos o hacer obligatorio un campo opcional.
2. **Cambios compatibles dentro de v1:** nuevos endpoints, campos opcionales en las solicitudes y campos nuevos en las
   respuestas. **Los clientes deben ignorar los campos desconocidos de las respuestas.**
3. **Obsolescencia:** un endpoint que va a retirarse responde con las cabeceras `Deprecation: true` y
   `Sunset: <fecha>` (RFC 8594) durante **al menos 6 meses**. Durante ese tiempo `v1` y `v2` conviven.
4. **Contrato versionado:** cada versión publica su OpenAPI (`/openapi/v1.json`, `/openapi/v2.json`). Recomendación:
   guardar una copia en el repositorio y comprobar en CI que los cambios no rompen el contrato (`oasdiff breaking`).
5. **Eventos:** se versionan en el nombre del tipo (`ec.tourgirls.order.paid.v1`); ver [EVENTOS.md](EVENTOS.md).

---

## 5. Escenarios de integración previstos

| Escenario | Cómo se integra | Contratos |
|---|---|---|
| **Buscador global del marketplace** | Sincroniza el catálogo con `GET /atracciones` (lotes) y se mantiene al día con los eventos `attraction.*` | §2.1, EVENTOS §3 |
| **Servicio de pagos real** | Recibe el pedido, cobra y confirma por webhook entrante; TourGirls publica `order.paid` | §2.3 |
| **Notificaciones (correo, SMS)** | Se suscribe a `reservation.confirmed`, `order.paid`, `order.refunded` y `user.registered` | EVENTOS §3 |
| **Facturación electrónica (SRI)** | Consume `order.paid` y `order.refunded`; consulta `GET /orders/{id}` para el detalle | §2.3, EVENTOS §3 |
| **Agencias y operadores (B2B)** | Client credentials con scopes `read` + `book` + `cancel`; reservan con `/atracciones/{id}/reservations` y reciben webhooks | §2.2, EVENTOS §5 |
| **BI / reportes** | Consume eventos para un almacén analítico; a futuro, `/admin/reports/*` bajo un scope de solo lectura | §2.5 |
| **App móvil** | Usa los mismos endpoints que la web, con el mismo token y la misma idempotencia | §2.1–2.4 |

---

## 6. Checklist para un nuevo integrador

- [ ] Obtener credenciales y los scopes mínimos necesarios.
- [ ] Generar el cliente desde `/openapi/v1.json`.
- [ ] Enviar `Authorization: Bearer` y renovar el token antes de que expire (1 h).
- [ ] Generar un `Idempotency-Key` por intento lógico y reutilizarlo en los reintentos.
- [ ] Tratar los errores por `code` y registrar el `traceId`.
- [ ] Respetar el `429`/`Retry-After` y usar backoff exponencial en los `5xx`.
- [ ] Ignorar los campos desconocidos de las respuestas.
- [ ] Si consume eventos o webhooks: validar la firma y descartar duplicados por `id` (ver [EVENTOS.md](EVENTOS.md)).
