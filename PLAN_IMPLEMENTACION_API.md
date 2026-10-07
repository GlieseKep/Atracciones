# Plan de implementación: AtraccionesService.API

## 1. Propósito y alcance

Este plan cubre únicamente la capa HTTP de `AtraccionesService.API`: configuración ASP.NET Core, rutas, controladores, autenticación y autorización en el borde, validación de solicitudes, manejo uniforme de errores, documentación OpenAPI y pruebas de integración asociadas.

La API no contendrá reglas de negocio ni acceso directo a base de datos. Los controladores delegarán en `AtraccionesService.Application`; los contratos HTTP compartidos vivirán en `AtraccionesService.Contracts`. El modelo debe mantenerse alineado con `CONTRATO.md`, los acuerdos de `CORRECCIONES_CONTRATO.md` y los campos del ecommerce definidos en `PLAN_IMPLEMENTACION_BASE_DE_DATOS.md`.

La estructura descrita en el repositorio de referencia se usa únicamente como orientación para separar controladores, middleware y configuración de arranque. No se copiarán funcionalidades ni lógica de dominio de ese repositorio.

## 2. Estado actual y diagnóstico

Actualmente el proyecto API contiene:

- `Program.cs` con configuración mínima y un endpoint de plantilla `GET /weatherforecast`.
- `AtraccionesService.API.csproj` como proyecto ASP.NET Core sobre `net10.0`, nullable habilitado e OpenAPI.
- `AtraccionesService.API.http` con una solicitud al endpoint de plantilla.
- `appsettings.json`, `appsettings.Development.json` y `Properties/launchSettings.json`.
- No hay controladores, middleware propio ni integración de servicios de aplicación.

La primera modificación funcional deberá retirar la ruta y el modelo de plantilla cuando los endpoints reales estén disponibles, evitando dejar rutas ajenas al negocio.

## 3. Responsabilidades de la capa API

La capa API será responsable de:

1. Exponer rutas versionadas y métodos HTTP coherentes con el contrato.
2. Convertir solicitudes HTTP a comandos/consultas de Application y convertir resultados a respuestas HTTP.
3. Validar forma, tipos, campos obligatorios y encabezados; las reglas de negocio pertenecen a Application/Domain.
4. Resolver la identidad autenticada y aplicar autorización/ownership antes de devolver recursos privados.
5. Traducir errores conocidos a respuestas RFC 7807 `application/problem+json`.
6. Publicar OpenAPI/Swagger, health check y configuración por ambiente.
7. Mantener los servicios registrados por inyección de dependencias, sin instanciar infraestructura desde controladores.

Queda fuera de esta capa: consultas SQL/ORM, persistencia, cálculo de precios, transiciones de estados, validación de disponibilidad de negocio y procesamiento de pagos.

## 4. Estructura propuesta de `AtraccionesService.API`

```text
AtraccionesService.API/
├── Controllers/
│   ├── AttractionsController.cs
│   ├── AvailabilityController.cs
│   ├── ReservationsController.cs
│   ├── AuthController.cs
│   ├── UsersController.cs
│   ├── CustomersController.cs
│   ├── PurchasesController.cs
│   ├── OrdersController.cs
│   └── PaymentsController.cs
├── Middleware/
│   └── ProblemDetailsExceptionMiddleware.cs
├── Extensions/
│   └── ApiServiceCollectionExtensions.cs
├── Properties/
│   └── launchSettings.json
├── appsettings.json
├── appsettings.Development.json
├── AtraccionesService.API.http
├── AtraccionesService.API.csproj
└── Program.cs
```

Los DTOs/requests/responses no se duplicarán en `Controllers`; se ubicarán en `AtraccionesService.Contracts`. Las interfaces y servicios que ejecutan casos de uso serán propiedad de `AtraccionesService.Application`.

## 5. Organización de controladores y rutas

El documento OpenAPI conserva `servers: /api/v1`. Cada path se declara sin ese prefijo: por ejemplo, `/atracciones/search` y `/auth/register`; el URL resultante es `/api/v1/atracciones/search` y `/api/v1/auth/register`. No se repite `/api/v1` dentro de `paths`.

### 5.1 Catálogo y disponibilidad

`AttractionsController` expone las operaciones actuales de catálogo:

- `POST /atracciones/search`
- `POST /atracciones/details`
- `GET /atracciones`
- `POST /atracciones`
- `GET /atracciones/{id}`
- `PUT /atracciones/{id}`
- `PATCH /atracciones/{id}`
- `DELETE /atracciones/{id}`

Las operaciones de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) son administrativas y requieren scope de escritura más permiso local de catálogo. Lectura usa el scope de lectura definido por el contrato. La API no confía en roles recibidos en el body.

`AvailabilityController` expone:

- `GET /atracciones/{id}/availability`

Las rutas duplicadas o incompatibles que ya están registradas como hallazgos de contrato deben resolverse documentalmente antes de definir atributos de ruta duplicados en ASP.NET Core.

### 5.2 Reservas

`ReservationsController` expone las operaciones existentes del contrato:

- `POST /atracciones/{id}/reservations`
- `POST /atracciones/reservations/{reservationId}/cancel`
- `GET /atracciones/reservations`
- `GET /atracciones/reservations/{reservationId}`

Las operaciones transaccionales leerán `Idempotency-Key` desde el encabezado y delegarán la garantía de idempotencia en Application/Infrastructure. El usuario autenticado se obtiene de claims/token; nunca se confía en un `userId` enviado en el body para decidir propiedad.

### 5.3 Identidad y sesión

La autenticación interactiva y la emisión de tokens se realizan mediante el proveedor OAuth2 configurado, usando Authorization Code con PKCE para clientes públicos. No se implementa un endpoint propio `/auth/login` ni se emite un token simulado.

`AuthController` expone únicamente el aprovisionamiento del perfil local después de que el usuario se haya autenticado correctamente con OAuth2:

- `POST /auth/register`

`UsersController` expone:

- `GET /users/me`

`POST /auth/register` requiere un token OAuth2 válido y usa sus claims verificados `sub` y `email` para crear el usuario local y su cliente asociado. El password/login, las sesiones, el refresh y la revocación de tokens pertenecen al proveedor OAuth2. La API valida el access token y aplica los scopes requeridos; no acepta contraseñas ni devuelve tokens. `GET /users/me` también requiere token válido.

### 5.4 Cliente y facturación

`CustomersController` expone:

- `GET /customers/me`
- `PUT /customers/me`
- `GET /customers/me/reservations`

El cliente autenticado resuelve su `customerId` desde la relación usuario-cliente. `userId` y `customerId` no se aceptarán como mecanismo para acceder a datos de otro usuario. Los campos de facturación se limitan a los acordados: `billingName`, `billingEmail`, `billingAddress`, `taxId` y `paymentMethodReference`.

### 5.5 Compras directas

`PurchasesController` expone una operación de compra basada en disponibilidad y una sola experiencia seleccionada:

- `POST /attractions/{attractionId}/purchase`

La operación acepta la fecha, la hora y cantidad solicitada, valida la disponibilidad y calcula el precio mediante Application. Si el usuario confirma el pago en esa misma operación, Application crea la orden, reserva o pago directamente, según el estado validado. No se crea ni persiste un carrito ni una sesión de checkout.

Cuando la compra no se confirma inmediatamente, la operación debe permitir una reserva temporal limitada, pero esa reserva debe tener una duración configurada y debe retirarse o transformar antes de que expire. No se documenta una sesión de checkout independiente.

### 5.6 Pedidos y pagos simulados

`OrdersController` expone:

- `POST /orders`
- `GET /orders/{orderId}`
- `GET /orders/{orderId}/events`
- `POST /orders/{orderId}/cancel`

`PaymentsController` expone:

- `POST /payments/simulations`

La API valida la solicitud de simulación y la delega a Application. Application determina el resultado simulado y genera/guarda los registros de `payment_attempts` y `payment_events` como efectos internos del caso de uso; no existen endpoints públicos para escribir intentos o eventos. El resultado se refleja en el recurso `payment_simulations` y en el estado del pedido. No se reciben ni persisten números completos de tarjeta, CVV, credenciales bancarias ni datos reales de proveedores; no hay verificación externa.

La consulta de pago del cliente se realiza mediante `GET /orders/{orderId}` y devuelve únicamente el estado y referencia segura de la simulación. No se crean endpoints cliente para intentos/eventos.

### 5.7 Administración

`AdminController` organiza rutas propuestas para agregarse a `CORRECCIONES_CONTRATO.md` antes de generar OpenAPI. No se publican rutas sin aprobar sus esquemas, scopes y errores.

- `GET /admin/dashboard`.
- `GET /admin/attractions` y `POST /admin/attractions`.
- `GET /admin/attractions/{id}`, `PUT /admin/attractions/{id}`, `PATCH /admin/attractions/{id}`, `DELETE /admin/attractions/{id}`.
- `GET /admin/availability`, `POST /admin/availability`, `PATCH /admin/availability/{id}`.
- `GET /admin/reservations`, `GET /admin/reservations/{reservationId}`, `POST /admin/reservations/{reservationId}/cancel`.
- `GET /admin/orders`, `GET /admin/orders/{orderId}`, `PATCH /admin/orders/{orderId}/status`, `POST /admin/orders/{orderId}/cancel`.
- `GET /admin/payments`, `GET /admin/payments/{paymentId}`, `POST /admin/payments/{paymentId}/refunds`.
- `GET /admin/customers`, `GET /admin/customers/{customerId}`.
- `GET /admin/users`, `GET /admin/users/{userId}`, `PATCH /admin/users/{userId}/status`, `PUT /admin/users/{userId}/roles`.
- `GET /admin/reports/orders`, `/admin/reports/payments`, `/admin/reports/reservations`.

Cada operación requiere access token OAuth2 válido, el scope de API correspondiente y el permiso local más específico. Roles iniciales: `catalog_manager`, `inventory_manager`, `order_manager`, `support_manager`, `finance_manager` y `admin`; `admin` no exime auditoría. Application valida transiciones de estado. Reportes se paginan y omiten PII y datos financieros sensibles. Cada mutación registra actor, acción, recurso, motivo y fecha.

## 6. Convenciones HTTP y seguridad

- Mantener `servers: /api/v1` y declarar cada ruta en `paths` sin el prefijo de versión.
- Usar códigos `200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`, `422` y `500` según el resultado documentado.
- Respuestas de error con `ProblemDetails` y `application/problem+json`.
- Usar `Location` en recursos creados cuando aplique.
- Aplicar `Idempotency-Key` a toda mutación con efectos de negocio, incluyendo compras directas, reservas, pedidos, pagos, cancelaciones, reembolsos y operaciones administrativas. GET no lo requiere.
- Aplicar autorización por operación (lectura/escritura/cancelación) y ownership para cliente, pedido, pago y reserva.
- No registrar en logs contraseñas, hash de contraseña, tokens bearer ni datos de pago.
- Configuración y secretos se cargan por configuración segura/variables de entorno; no se guardan secretos en `appsettings.json`.
- CORS se habilita solo para orígenes explícitamente configurados.

### 6.1 Política OAuth2 acordada

- OAuth2 es el único esquema de autenticación para catálogo, reservas y ecommerce.
- Se conserva el flujo `authorizationCode` definido por OpenAPI; los clientes públicos deben usar PKCE.
- La API valida access tokens emitidos por el `authorizationUrl`/`tokenUrl` configurado y aplica scopes por operación.
- El login, emisión, renovación y revocación de tokens corresponden al proveedor OAuth2. No se aceptan contraseñas en endpoints de este API ni se emiten tokens simulados.
- La API aprovisiona el perfil de ecommerce mediante `POST /auth/register` usando los claims verificados `sub` y `email`.
- Deben añadirse a OpenAPI los scopes que requieran las operaciones ecommerce antes de implementarlas; no se inventan esquemas de seguridad alternativos.
- Los scopes de administración y ecommerce aún no están aprobados en el documento de correcciones; deben definirse allí antes de publicar estas rutas. Además del scope, API consulta roles/permisos locales.
- Los roles locales no crean ni modifican scopes OAuth2 del issuer.

## 7. Middleware y configuración de arranque

`Program.cs` debe quedar limitado al ensamblaje de la aplicación y declarar un orden explícito para:

1. Cargar configuración/servicios.
2. Manejo global de excepciones que produce RFC 7807.
3. HTTPS redirection y CORS configurado.
4. Autenticación.
5. Autorización.
6. Health checks y endpoints de controladores.
7. OpenAPI/Swagger en ambientes permitidos.

`ProblemDetailsExceptionMiddleware` traducirá excepciones de aplicación conocidas a status y ProblemDetails; no debe filtrar stack traces en producción. Errores de validación de entrada se devuelven como `400`; conflictos de estado/idempotencia como `409`; falta de identidad como `401`; falta de permisos/propiedad como `403` o `404` según la política anti-enumeración definida.

`ApiServiceCollectionExtensions` agrupará el registro de servicios propios del host API, sin mover implementaciones de Application o Infrastructure a esta carpeta.

## 8. OpenAPI, health y archivos de desarrollo

- Generar OpenAPI desde el contrato aprobado y mantener esquemas, ejemplos, responses y requisitos de seguridad consistentes.
- Publicar Swagger UI solo en Development (o en el ambiente explícitamente autorizado).
- Mantener un health endpoint independiente del catálogo, preferiblemente `/health`, que no requiera autenticación y no exponga información interna.
- Reemplazar las llamadas de `AtraccionesService.API.http` por ejemplos del API de atracciones, autenticación, cliente, carrito, pedido y pago simulado; no incluir secretos reales.
- Retirar `WeatherForecast.cs` y `/weatherforecast` como parte de la eliminación de la plantilla inicial.

## 9. Fases de implementación

### Fase 1: Preparar el host API

- Eliminar endpoint/modelo de plantilla.
- Habilitar controllers con `servers: /api/v1` y paths relativos.
- Configurar OpenAPI, health check, ProblemDetails y logging sin secretos.
- Verificar que la API inicia y responde en `/health`.

### Fase 2: Publicar el contrato base

- Crear controladores de catálogo, disponibilidad y reservas.
- Usar request/response compartidos de Contracts y delegar cada operación en Application.
- Aplicar validación de encabezados, autorización e idempotencia documentadas.
- Asegurar que no existan rutas ambiguas por duplicados del contrato.

### Fase 3: Incorporar identidad y clientes

- Publicar el aprovisionamiento del perfil y la lectura de perfil usando la identidad OAuth2.
- Delegar login, cierre de sesión y gestión de contraseña al proveedor OAuth2.
- Publicar lectura y actualización de facturación.
- Aplicar ownership usuario-cliente y proteger datos personales.

### Fase 4: Incorporar el flujo de compras directas

- Publicar la compra directa con disponibilidad, fecha, hora, cantidad y pago en una operación transaccional.
- Publicar creación/consulta/cancelación de pedidos e historial de eventos.
- Publicar la solicitud/consulta de pagos simulados; generar intentos y eventos solo dentro de Application.
- Mantener el flujo sin proveedor de pagos real y sin almacenamiento de datos de tarjeta.

### Fase 5: Cerrar contrato y calidad

- Validar las rutas/esquemas con OpenAPI.
- Aprobar primero en el documento de correcciones las rutas administrativas, schemas request/response, roles/scopes, paginación e idempotencia; las rutas no forman parte del contrato publicado hasta esa aprobación.
- Probar autenticación, permisos, ownership, ProblemDetails, idempotencia y códigos HTTP.
- Revisar configuración de ambientes y registros antes de considerar lista la API.

## 10. Pruebas de la capa API

Las pruebas de integración deben iniciar el host API y validar comportamiento HTTP observable:

- Health check responde `200` sin exponer configuración.
- Rutas y métodos publicados corresponden al OpenAPI aprobado.
- Tokens OAuth2 inválidos o expirados reciben `401`; tokens sin scope reciben `403`.
- El aprovisionamiento de perfil usa los claims verificados y no admite una identidad `sub` enviada por el cliente.
- Solicitudes anónimas a rutas protegidas reciben `401`.
- Un usuario no puede leer ni modificar cliente, reserva, pedido o pago ajeno.
- Modelos inválidos reciben `400 ProblemDetails`.
- Excepciones de Application se convierten al código y formato de error acordados.
- Repetición con igual `Idempotency-Key` no duplica una mutación.
- Una compra directa con disponibilidad insuficiente o fecha inválida se rechaza antes del pago.
- Pagos simulados no aceptan ni devuelven datos de tarjeta real.
- Swagger/OpenAPI solo se publica en los ambientes permitidos.
- Una cuenta sin permiso local no puede ejecutar mutaciones administrativas aunque su access token sea válido.
- Un administrador sin el scope OAuth2 requerido recibe `403`.
- Las mutaciones administrativas generan un registro de auditoría.
- No se publican rutas administrativas que no estén en el OpenAPI aprobado.

## 11. Criterios de aceptación

La carpeta `AtraccionesService.API` está lista cuando:

- No expone endpoints de plantilla.
- Todas las operaciones aprobadas tienen ruta, método, autorización y respuesta documentados.
- Los controllers solo coordinan HTTP y casos de uso; no tienen lógica de dominio ni acceso directo a datos.
- Los modelos HTTP compartidos viven en Contracts y coinciden con el contrato aprobado.
- Fallos siguen RFC 7807 y las rutas privadas verifican identidad y ownership.
- OAuth2 es el único esquema; scopes y validación de tokens coinciden con OpenAPI.
- `Idempotency-Key` se valida/delega correctamente.
- El pago permanece simulado y no solicita datos sensibles de tarjeta.
- OpenAPI, health check y pruebas de integración pasan.

## 12. Decisiones acordadas y pendientes

Decisiones acordadas:

1. Conservar OAuth2 como único esquema; no habrá login REST que emita token simulado.
2. Conservar `servers: /api/v1` y declarar paths sin repetir el prefijo.
3. La API inicia la simulación; Application genera y registra intentos y eventos internamente.

Pendientes antes de implementar:

1. Configurar el issuer/proveedor OAuth2 real y validar sus claims de `sub` y `email`.
2. Definir y documentar los scopes OAuth2 requeridos por registro/perfil, compra directa, pedidos y pagos.
3. Confirmar el uso de `403` versus `404` para recursos ajenos.
4. Confirmar que `paymentMethodReference` no representa ni permite recuperar credenciales o datos financieros reales.
5. Aprobar el contrato adicional de administración, scopes exactos, roles locales y payloads de reportes.
6. Definir quién provisiona la primera cuenta `admin` mediante un proceso operativo controlado, sin seed de credenciales.
7. Confirmar límites de tasa y paginación para reportes administrativos.

Este documento es el plan de la capa API; no implementa todavía endpoints ni modifica `CONTRATO.md`.
