# Propuesta de corrección del contrato OpenAPI

> Este documento propone cambios sobre el contrato actual sin modificar `CONTRATO.md`. Cada recomendación incluye prioridad, impacto y criterio de aceptación.

## 1. Prioridades

| Prioridad | Objetivo |
|---|---|
| P0 | Corregir errores que impiden interpretar correctamente el contrato. |
| P1 | Completar reglas de seguridad, validación y errores. |
| P2 | Homogeneizar convenciones y mejorar la documentación. |

## 2. Correcciones P0

### 2.1 Consolidar `/atracciones/{id}`

**Problema:** La ruta aparece definida dos veces con operaciones repetidas.

**Cambio propuesto:**

- Mantener una sola definición de `/atracciones/{id}`.
- Agrupar `GET`, `PUT`, `PATCH` y `DELETE` dentro de ese bloque.
- Eliminar el segundo bloque duplicado.
- Verificar que las respuestas y parámetros de ambas definiciones sean idénticos.

**Criterio de aceptación:**

- La ruta aparece exactamente una vez.
- Cada operación corresponde a una sola definición.
- OpenAPI no reporta rutas duplicadas.

### 2.2 Definir los campos obligatorios de `CreateAtraccionRequest`

**Problema:** El esquema permite objetos de creación incompletos.

**Cambio propuesto:**

Agregar como obligatorios al menos:

- `name`
- `long_description`
- `duration`
- `price`
- `categories`
- `locations`
- `product_type`

Definir también restricciones adicionales, por ejemplo:

- `name`: longitud mínima y máxima.
- `duration`: formato de duración válido.
- `price`: mayor que cero.
- `categories`: lista no vacía.
- `locations`: al menos una ubicación.

**Criterio de aceptación:**

- El esquema declara correctamente los campos obligatorios.
- La validación de tipos y rangos coincide con la implementación.
- Los escenarios inválidos producen `400 Problem Details`.

### 2.3 Completar `UpdateAtraccionRequest`

**Problema:** El esquema está vacío y no describe cambios parciales.

**Cambio propuesto:**

- Definir las propiedades modificables.
- Permitir `null` solamente cuando el negocio lo soporte.
- Usar `additionalProperties: false` para evitar campos desconocidos.
- Documentar las propiedades que son solo de lectura.
- Determinar si `PATCH` admite cambios parciales o si mapeará a una actualización completa.

**Criterio de aceptación:**

- Cada propiedad contiene tipo y restricciones.
- La operación acepta solo campos definidos.
- El comportamiento de valores nulos queda documentado.

### 2.4 Definir el scope `attractions:cancel`

**Problema:** La operación de cancelación exige un scope que no se declara.

**Cambio propuesto:**

Agregar al esquema OAuth2:

```yaml
attractions:cancel: Cancelar reservas
```

Actualizar la documentación y cualquier prueba de seguridad que valide scopes.

**Criterio de aceptación:**

- El scope está declarado en OAuth2.
- La operación de cancelación usa exactamente el scope definido.
- Un cliente sin ese scope recibe `403`.

### 2.5 Eliminar o completar `attractions:webhooks`

**Problema:** El scope existe, pero no existe ninguna operación webhooks.

**Cambio propuesto:**

Opción A: eliminar el scope si no se implementará en esta fase.

Opción B: definir completamente las operaciones pendiente, incluyendo:

- `GET /webhooks`
- `POST /webhooks`
- `DELETE /webhooks/{id}`
- Validación del payload recibido.
- Reintentos y estados de entrega.

**Criterio de aceptación:**

- El scope corresponde a una funcionalidad presente en el contrato.
- No existe un scope declarado sin una operación asociada.

## 3. Correcciones P1

### 3.1 Definir respuestas de errores de seguridad

Agregar las respuestas:

- `401 Unauthorized`
- `403 Forbidden`
- `429 Too Many Requests`
- `500 Internal Server Error`
- `503 Service Unavailable`

Cada respuesta debe usar `application/problem+json` y la estructura `ProblemDetails`.

**Criterio de aceptación:**

- Cada código de error tiene una definición explícita.
- El contrato documenta el escenario que produce cada código.
- Los clientes reciben una estructura consistente.

### 3.2 Completar las respuestas de conflicto de idempotencia

Definir un código específico para idempotencia, por ejemplo:

- `409 Conflict` para recursos o cupos incompatibles.
- `422 Unprocessable Entity` si se quiere distinguir errores de validación.

Agregar una explicación sobre:

- Reutilización de una clave idempotencia.
- Clave con payload distinto.
- Clave ya registrada y no disponible.
- Repetición segura de la misma operación.

**Criterio de aceptación:**

- Las respuestas distinguen conflictos de idempotencia y disponibilidad.
- El mismo `Idempotency-Key` devuelve la misma respuesta.
- Una clave distinta puede producir un conflicto si representa otra operación.

### 3.3 Establecer la política de `Idempotency-Key`

**Formato propuesto:**

```yaml
name: Idempotency-Key
in: header
required: true
schema:
  type: string
  format: uuid
  pattern: '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
```

Agregar:

- Tiempo máximo de retención.
- Política de almacenamiento.
- Comportamiento ante claves duplicadas.
- Relación entre reserva, cancelación y respuesta anterior.

**Criterio de aceptación:**

- La cabecera tiene formato válido y está documentado.
- La política de uso está definida exactamente.
- La implementación puede probarse sin interpretar prematuramente la clave.

### 3.4 Definir la propiedad de pertenencia de reservas

El endpoint de historial debe especificar:

- La identidad del usuario autenticado.
- El mecanismo usado para obtener el usuario.
- La relación entre `reservationId` y el usuario.
- Parámetros de filtrado y ordenamiento.

**Criterio de aceptación:**

- Un usuario no puede consultar reservas de otro usuario.
- El contrato define explícitamente la autorización.
- Los parámetros de consulta quedan documentados.

### 3.5 Añadir parámetros de paginación al historial

Para `GET /atracciones/reservations` agregar:

- `limit`
- `offset`
- `status`
- `fromDate`
- `toDate`
- `sort`

Definir límites válidos y valores predeterminados.

**Criterio de aceptación:**

- Los parámetros tienen tipos y restricciones.
- La respuesta distingue el número de elementos y páginas.
- El contrato evita listas sin límite.

### 3.6 Definir una convención global de nombres

Se recomienda usar exclusivamente camelCase para:

- Campos de solicitud.
- Campos de respuesta.
- Campos de paginación.
- Identificadores y nombres de propiedades.

Ejemplos:

- `availableSpots` en lugar de `available_spots`.
- `reservationId` en lugar de `reservation_id`.
- `totalPrice` en lugar de `total_price`.
- `totalItems` y `currentPage` ya son consistentes.

**Criterio de aceptación:**

- Todas las propiedades usan una misma convención.
- Los ejemplos y la implementación siguen la misma convención.
- El código generado no necesita transformaciones manuales.

### 3.7 Completar el modelo de disponibilidad

Agregar:

- Formato de hora.
- Zona horaria.
- Fecha local o UTC.
- Estado de cada franja horaria.
- Posibles valores de disponibilidad.

**Criterio de aceptación:**

- Cada franja horaria tiene un identificador y semántica claros.
- El cliente conoce el significado de cada valor devuelto.
- La zona horaria no depende de una suposición implícita.

### 3.8 Completar `Location`

Definir:

- `city` como ID, código o nombre de ciudad.
- `country` como código ISO o nombre completo.
- `type` como enum con valores concretos.
- `address` como dirección válida.
- `coordinates` como conjunto de latitud y longitud.

**Criterio de aceptación:**

- Todos los campos tienen tipo y formato definidos.
- Los valores de enumeración son explícitos.
- La representación geográfica es unívoca.

### 3.9 Formalizar `Price`

Modificar `Price` para definir:

- Moneda obligatoria.
- Código ISO 4217.
- Máximo de dos decimales o criterio explícito.
- Valores positivos.
- Alcance de `total` en la respuesta.

Agregar ejemplos de precios para USD, EUR y otra moneda utilizada por el sistema.

**Criterio de aceptación:**

- La representación monetaria es válida según el estándar elegido.
- Los valores negativos y nulos quedan rechazados.
- Los ejemplos reflejan tipos y valores reales.

### 3.10 Completar `ReservationRequest`

Agregar validaciones:

- `date`: fecha futura o fecha válida.
- `time`: formato de hora.
- `ticket_count`: entero mayor que cero.
- `customer_name`: no vacío y longitud máxima.
- `customer_email`: formato de correo.

Agregar `required` para todos los campos necesarios.

**Criterio de aceptación:**

- Cada campo obligatorio está declarado en `required`.
- Las validaciones corresponden al comportamiento esperado.
- Los datos inválidos producen respuestas `400` uniformes.

### 3.11 Definir el semántica de actualización y eliminación

Para `PUT`:

- Indicar si reemplaza el recurso completo.
- Definir el comportamiento de campos omitidos.
- Especificar si la respuesta debe ser `200`, `204` o `201`.

Para `DELETE`:

- Definir eliminación física o lógica.
- Definir si se puede reactivar.
- Especificar si existen reservas asociadas.

**Criterio de aceptación:**

- El cliente sabe qué campos se reemplazan y cuáles se omiten.
- La decisión de borrado puede implementarse sin ambigüedad.

## 4. Correcciones P2

### 4.1 Completar `ProblemDetails`

Agregar definiciones para:

- `type`: URI estable.
- `title`: mensaje legible.
- `status`: código HTTP.
- `detail`: descripción específica.
- `instance`: identificador de la operación o recurso.

Agregar ejemplos de cada tipo de error.

### 4.2 Completar `Rating`

Definir si una atracción puede tener:

- Una sola valoración.
- Múltiples valoraciones.
- Valoraciones por fuente.
- Valoraciones agregadas.

Agregar propiedades para fecha, fuente y versión si se necesitan.

### 4.3 Completar `Url`

Definir qué campos son obligatorios y qué valores se aceptan.

Ejemplo:

```yaml
web:
  type: string
  format: uri
  nullable: true
app:
  type: string
  format: uri
  nullable: true
```

### 4.4 Definir `free_cancellation`

Reemplazar el booleano por una política explícita, por ejemplo:

```yaml
cancellation_policy:
  type: object
  properties:
    free_until:
      type: string
      format: date-time
    refund_percentage:
      type: number
      minimum: 0
      maximum: 100
```

### 4.5 Completar `SearchAtraccionesRequest`

Definir:

- Valores permitidos para `currency`.
- Restricciones para `cities` y `countries`.
- Tipo de token de paginación.
- Valores válidos de `sort.by`.
- Relaciones entre fechas y filtros.
- Máximo de registros por página.

### 4.6 Completar la documentación de `next_page`

Definir:

- Algoritmo de generación.
- Tiempo de expiración.
- Criterio de invalidez.
- Manejo de tokens manipulados.
- Dirección de uso en clientes.

### 4.7 Definir los estados de reserva

Usar un enum explícito, por ejemplo:

```yaml
status:
  type: string
  enum:
    - PENDING
    - CONFIRMED
    - CANCELLED
    - COMPLETED
```

Si el sistema usa otros estados, deben documentarse todos.

### 4.8 Definir las transiciones de estado

Documentar:

- `PENDING -> CONFIRMED`
- `PENDING -> CANCELLED`
- `CONFIRMED -> CANCELLED`
- `CANCELLED -> REOPENED` si aplica
- `CONFIRMED -> COMPLETED` si aplica

**Criterio de aceptación:**

- No existe un estado que se declare y no tenga transición documentada.
- Las operaciones de reservas solo permiten transiciones válidas.

### 4.9 Definir la respuesta del historial de reservas

Agregar:

- `totalItems`
- `itemsPerPage`
- `currentPage`
- `totalPages`
- `data`

Evitar respuesta de array sin metadatos.

## 5. APIs adicionales para el ecommerce completo

Estas APIs se agregan como propuestas de evolución del contrato y no sustituyen los endpoints existentes de catálogo y reservas. Las nuevas operaciones usan exclusivamente los campos definidos en el plan de base de datos y deben conservar la separación entre identidad, cliente y pasarela.

### 5.1 Convenios comunes

- El servidor OpenAPI conserva `servers: /api/v1`; cada `path` se declara sin repetir ese prefijo. Por ejemplo, `/auth/register` se resuelve como `/api/v1/auth/register`.
- OAuth2 es el único esquema de autenticación. Las solicitudes protegidas envían `Authorization: Bearer <access_token>` emitido por el issuer OAuth2 configurado.
- El usuario autenticado se obtiene de claims verificados del token, especialmente `sub` y `email`, nunca de un identificador enviado en el cuerpo.
- Las rutas de clientes, carrito, pedidos y pagos deben validar que el usuario autenticado sea propietario del recurso.
- Las respuestas exitosas usan JSON y las respuestas de error usan `application/problem+json` con RFC 7807.
- Las operaciones transaccionales requieren la cabecera `Idempotency-Key`.
- No se deben devolver credenciales, tokens ni datos financieros sensibles. La identidad/proveedor gestiona contraseñas y sesiones OAuth2.
- Los campos de auditoría `created_at`, `updated_at`, `expires_at` y `last_activity_at` son solo de lectura cuando apliquen.

### 5.2 `POST /auth/register`

Crear el perfil local del ecommerce y su cliente asociado después de que la persona haya creado su identidad y completado el inicio de sesión en el proveedor OAuth2.

**Autorización:** requiere access token OAuth2 válido. El endpoint utiliza el `sub` y `email` verificados del token; el cliente no puede enviar ni reemplazar esos valores.

**Entrada:**

```json
{
  "billingName": "Nombre para facturación",
  "billingEmail": "facturacion@ejemplo.com",
  "billingAddress": "Dirección de facturación",
  "taxId": "identificación fiscal"
}
```

**Reglas:**

- El perfil local se vincula al claim `sub` único del issuer configurado.
- `email` se obtiene del claim verificado; si falta o no es válido, se rechaza la solicitud.
- Si el registro falla, no se crean registros parciales.
- Se crea el usuario y un cliente asociado mediante una transacción.
- Repetir la operación para el mismo `sub` no crea perfiles o clientes duplicados.

**Respuesta 201:**

- `id`
- `email`
- `status`
- `customerId`
- `createdAt`

### 5.3 Inicio de sesión OAuth2 (sin endpoint propio en esta API)

El inicio de sesión se realiza mediante el flujo `authorizationCode` declarado por el contrato y PKCE para clientes públicos. La autorización interactiva, autenticación de credenciales y emisión/renovación de tokens son responsabilidad del issuer OAuth2. Esta API no define `POST /auth/login`, no recibe contraseñas y no emite tokens simulados.

### 5.4 Cierre de sesión OAuth2 (sin endpoint propio en esta API)

El cliente elimina sus tokens locales y utiliza el mecanismo de cierre/revocación que publique el issuer. Esta API no mantiene sesiones OAuth2 propias ni define `POST /auth/logout` salvo que una decisión futura integre explícitamente un endpoint estándar de revocación.

### 5.5 `GET /users/me`

Obtener la identidad del usuario autenticado.

**Autorización:** access token OAuth2 válido con los scopes requeridos.

**Respuesta 200:**

- `id`
- `email`
- `status`
- `createdAt`
- `updatedAt`

### 5.6 Gestión de contraseña (proveedor OAuth2)

El cambio y recuperación de contraseña se realizan en el proveedor OAuth2; no se exponen endpoints de cambio de contraseña en esta API y la base de datos del ecommerce no almacena `password_hash`.

### 5.7 `GET /customers/me`

Obtener el cliente asociado al usuario autenticado.

**Autorización:** access token OAuth2 válido con los scopes requeridos.

**Respuesta 200:**

- `id`
- `userId`
- `billingName`
- `billingEmail`
- `billingAddress`
- `taxId`
- `paymentMethodReference`
- `createdAt`
- `updatedAt`

### 5.8 `PUT /customers/me`

Actualizar completamente los datos de facturación del cliente.

**Entrada:**

```json
{
  "billingName": "Nuevo nombre",
  "billingEmail": "nuevo@ejemplo.com",
  "billingAddress": "Nueva dirección",
  "taxId": "nueva identificación",
  "paymentMethodReference": "referencia-del-método"
}
```

**Reglas:**

- El cliente debe pertenecer al usuario autenticado.
- `billingEmail` debe tener formato válido.
- `paymentMethodReference` contiene únicamente una referencia local para la simulación de pago.
- La actualización no modifica `userId`, `email` ni el claim `oauthSubject`; las credenciales son propiedad del issuer OAuth2.
- La respuesta debe devolver `updatedAt`.

### 5.9 `GET /carts/me`

Obtener el carrito activo del usuario autenticado.

**Autorización:** access token OAuth2 válido con los scopes requeridos.

**Respuesta 200:**

- `id`
- `customerId`
- `status`
- `createdAt`
- `updatedAt`
- `items`: lista de elementos con su `attractionId`, `quantity`, `unitPriceCurrency` y `unitPriceTotal`.

### 5.10 `POST /carts/me/items`

Agregar una atracción al carrito.

**Entrada:**

```json
{
  "attractionId": "uuid-de-la-atraccion",
  "quantity": 2,
  "unitPriceCurrency": "USD",
  "unitPriceTotal": 50.00
}
```

**Reglas:**

- `quantity` debe ser mayor que cero.
- `unitPriceCurrency` debe ser una moneda válida.
- `unitPriceTotal` debe ser mayor que cero.
- Si la atracción ya existe, se incrementa la cantidad y no se duplican los elementos.
- El usuario solo puede modificar su propio carrito.

**Respuesta 201:**

- `cartId`
- `itemId`
- `attractionId`
- `quantity`
- `unitPriceCurrency`
- `unitPriceTotal`

### 5.11 `PATCH /carts/me/items/{itemId}`

Actualizar la cantidad de un elemento del carrito.

**Entrada:**

```json
{
  "quantity": 3
}
```

**Reglas:**

- `quantity` debe ser mayor que cero.
- El elemento debe pertenecer al carrito del usuario autenticado.
- Si `quantity` es cero, debe eliminarse el elemento en lugar de almacenarlo con cantidad inválida.

### 5.12 `DELETE /carts/me/items/{itemId}`

Eliminar un elemento del carrito.

**Autorización:** access token OAuth2 válido con los scopes requeridos.

**Reglas:**

- El elemento debe pertenecer al carrito del usuario autenticado.
- La operación debe ser idempotente: una eliminación repetida retorna `200` o `204` con un resultado sin cambios.

### 5.13 `POST /checkout/sessions`

Crear una sesión de checkout asociada al carrito activo.

**Entrada:**

```json
{
  "cartId": "uuid-del-carrito",
  "customerId": "uuid-del-cliente"
}
```

**Reglas:**

- `cartId` y `customerId` deben corresponder al usuario autenticado.
- El carrito debe tener estado `ACTIVE`.
- El checkout debe devolver una sesión con `status`, `expiresAt` y `createdAt`.
- El usuario no puede crear checkout para otro cliente.

**Respuesta 201:**

- `id`
- `customerId`
- `cartId`
- `status`
- `expiresAt`
- `createdAt`

### 5.14 `POST /orders`

Crear un pedido a partir del carrito verificado.

**Entrada:**

```json
{
  "cartId": "uuid-del-carrito",
  "checkoutSessionId": "uuid-de-la-sesion",
  "customerId": "uuid-del-cliente"
}
```

**Reglas:**

- La sesión debe estar `ACTIVE` y no expirada.
- El pedido debe conservar `customerId`, `cartId` y `reservationId` cuando exista una reserva asociada.
- El pedido inicia con `CREATED` y registra un `order_event`.
- Un carrito con items vacíos no puede convertirse en pedido.
- Requiere `Idempotency-Key` para evitar pedidos duplicados.

**Respuesta 201:**

- `id`
- `customerId`
- `cartId`
- `reservationId`
- `status`
- `currency`
- `totalAmount`
- `createdAt`

### 5.15 `GET /orders/{orderId}`

Consultar un pedido del usuario autenticado.

**Autorización:** access token OAuth2 válido y propietario del pedido.

**Respuesta 200:**

- `id`
- `customerId`
- `cartId`
- `reservationId`
- `status`
- `currency`
- `totalAmount`
- `createdAt`
- `updatedAt`
- `items`
- `paymentSimulation`

### 5.16 `POST /payments/simulations`

Crear una simulación de pago para un pedido.

**Entrada:**

```json
{
  "orderId": "uuid-del-pedido",
  "paymentMethod": "CARD",
  "amount": 100.00,
  "currency": "USD"
}
```

**Reglas:**

- El pedido debe pertenecer al usuario autenticado.
- `paymentMethod` debe ser uno de los métodos soportados por la simulación.
- `amount` y `currency` deben coincidir con el pedido.
- No se debe almacenar ni devolver datos bancarios reales.
- La simulación debe crearse con estado `PENDING`.
- La operación requiere `Idempotency-Key`.

**Respuesta 201:**

- `id`
- `orderId`
- `paymentMethod`
- `status`
- `amount`
- `currency`
- `gatewayReference`
- `createdAt`

### 5.17 Intentos de pago (internos, sin endpoint público)

Al recibir `POST /payments/simulations`, la API delega la simulación como un único caso de uso de Application. Application determina el resultado según las reglas simuladas y registra el `payment_attempt` correspondiente; el cliente no envía `status`, `responseCode` ni `responseMessage`, y no puede asignarse un resultado `AUTHORIZED`.

### 5.18 Eventos de pago (internos, sin endpoint público)

Application registra de forma inmutable el `payment_event` asociado a cada transición de la simulación. La creación del intento, el evento, el estado de `payment_simulations` y el estado del pedido deben coordinarse transaccionalmente. No se exponen rutas HTTP para que usuarios creen o alteren eventos, ni se llama a proveedores externos.

### 5.19 `GET /orders/{orderId}/events`

Consultar el historial de estados de un pedido.

**Autorización:** access token OAuth2 válido y propietario del pedido.

**Respuesta 200:**

- `orderId`
- `events`: lista con `eventType`, `previousStatus`, `newStatus` y `createdAt`.

### 5.20 `POST /orders/{orderId}/cancel`

Cancelar un pedido.

**Entrada:**

```json
{
  "reason": "Pedido cancelado por el usuario"
}
```

**Reglas:**

- El pedido debe pertenecer al usuario autenticado.
- Solo se permiten transiciones de estado definidas por el negocio.
- La cancelación debe registrar un `order_event` y un movimiento de inventario como `CANCELLED` o `RELEASED`.
- La razón se guarda sin exponer datos internos.
- La operación requiere `Idempotency-Key`.

### 5.21 `GET /customers/me/reservations`

Consultar reservas relacionadas con el usuario autenticado.

**Autorización:** access token OAuth2 válido con los scopes requeridos.

**Parámetros:**

- `limit`: entero entre 1 y 100.
- `offset`: entero mayor o igual a cero.
- `status`: uno o varios estados permitidos.
- `fromDate`: fecha inicial.
- `toDate`: fecha final.
- `sort`: campo utilizado para ordenar.

**Respuesta 200:**

```json
{
  "totalItems": 10,
  "itemsPerPage": 20,
  "currentPage": 1,
  "totalPages": 1,
  "data": []
}
```

**Reglas:**

- Un usuario solo puede consultar sus propias reservas.
- El identificador de reserva debe permanecer vinculado al pedido mediante `reservationId`.
- La respuesta debe incluir metadatos de paginación.

### 5.22 Reglas de ownership y autorización

Para todas las rutas relacionadas con usuarios, clientes, carritos, pedidos y pagos:

1. El token identifica al usuario autenticado.
2. El recurso debe tener una relación directa con ese usuario mediante `user_id` o mediante `customer_id` propietario.
3. No se aceptan identificadores de usuario o cliente en el cuerpo si pueden ser modificados por el cliente.
4. Un usuario no puede consultar, modificar ni cancelar recursos de otro usuario.
5. La autorización debe validarse antes de acceder a datos de facturación o simulaciones de pago.
6. La respuesta debe omitir campos de credenciales, tokens y secretos.

### 5.23 Seguridad mínima para el ecommerce

- Aprovisionamiento de perfil: requiere token OAuth2 válido; autenticación de usuario se realiza en el issuer.
- Clientes, carrito, reservas y pedidos: requieren token OAuth2 válido y scopes correspondientes.
- Simulación de pagos: requiere token OAuth2 válido, scope correspondiente e `Idempotency-Key`.
- Respuestas de seguridad: `401`, `403`, `429`, `500` y `503` usando `application/problem+json`.
- La API valida issuer, audiencia, expiración y scopes; la emisión y revocación corresponden al issuer.
- Esta API no almacena contraseñas ni emite tokens.

## 6. Orden de implementación recomendado

1. Consolidar `/atracciones/{id}`.
2. Corregir OAuth2 y scopes.
3. Completar campos obligatorios y esquemas.
4. Definir errores 401, 403, 429, 500 y 503.
5. Formalizar idempotencia.
6. Homogeneizar nombres de propiedades.
7. Definir políticas de precio, disponibilidad y cancelación.
8. Añadir reglas de paginación y parámetros de consulta.
9. Completar webhooks o eliminarlos del contrato.
10. Integrar el perfil local del ecommerce con el issuer OAuth2.
11. Implementar clientes, carrito y checkout.
12. Implementar pedidos y pagos simulados.
13. Validar el contrato con herramientas OpenAPI.

## 7. Validación propuesta

Antes de implementar, validar el contrato con:

1. Un validador OpenAPI 3.0 o 3.1.
2. Una prueba que compruebe que no existan rutas duplicadas.
3. Una prueba de seguridad que valide los scopes declarados.
4. Una prueba que valide campos obligatorios y tipos.
5. Una prueba de idempotencia mediante el mismo `Idempotency-Key`.
6. Una prueba de contratos de respuesta para errores RFC 7807.
7. Una comparación entre el contrato y las características requeridas para el microservicio.

## 7. Resultado esperado

Con estas correcciones, el contrato se volverá:

- Determinista.
- Consistente en nombres y formatos.
- Compatible con validaciones automáticas.
- Seguro en la gestión de permisos y reservas.
- Útil para generación de clientes y documentación.
- Capaz de admitir implementación y prueba sin suposiciones ambiguas.
