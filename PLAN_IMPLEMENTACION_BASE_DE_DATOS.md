# Plan de implementación de la base de datos y datos de las APIs

## 1. Principios

- `CONTRATO.md` es la fuente de verdad para los endpoints y esquemas actuales de catálogo y reservas; `CORRECCIONES_CONTRATO.md` contiene propuestas, no cambios ya aprobados.
- Las tablas auxiliares de ecommerce, identidad local y administración son un modelo de persistencia adicional; no implican que sus endpoints estén aprobados o publicados.
- Ningún atributo nuevo se incorpora a los endpoints existentes sin actualizar primero el contrato propuesto y aprobarlo.
- Las propiedades embebidas se normalizarán solo para evitar datos duplicados; sus atributos permanecerán intactos.
- Las claves, índices y relaciones técnicas necesarias para una base de datos no se consideran nuevos atributos de negocio.
- La implementación puede usar UUID para identificadores, siempre que el contrato ya los defina como `format: uuid`.
- Los datos de reservas se tratarán de forma independiente respecto del catálogo de atracciones.

## 2. Alcance de datos

La base de datos cubrirá estos dominios:

1. **Catálogo de atracciones**: atributos, ubicaciones, fotografías, idiomas, categorías, insignias, servicios incluidos, operador y calificación.
2. **Reservas**: fecha, hora, cantidad de entradas, datos del cliente y estado de la reserva.
3. **Ecommerce**: perfiles locales y clientes, carritos, checkout, pedidos, pagos simulados, reembolsos simulados, disponibilidad, idempotencia y auditoría.

El proveedor OAuth2 conserva credenciales, sesiones y refresh tokens; el ecommerce conserva solo el identificador externo verificado (`issuer` + `sub`) y los datos de perfil necesarios.

Las operaciones de búsqueda, detalle, disponibilidad, creación, consulta, cancelación y paginación se apoyarán en estas entidades.

## 3. Modelo de datos propuesto

### 3.1 Atracciones

La entidad principal representa el recurso `AtraccionResponse` y se utiliza para crear, consultar, actualizar y eliminar atracciones.

| Campo | Tipo sugerido | Uso en el contrato | Observación |
|---|---|---|---|
| `id` | UUID | Identificador de la atracción | Se utiliza en `GET`, `PUT`, `PATCH` y `DELETE`. |
| `name` | Texto | Nombre de la atracción | Campo visible en la respuesta. |
| `long_description` | Texto | Descripción detallada | Campo visible en la respuesta. |
| `duration` | Texto | Duración de la experiencia | Se entrega como texto, sin imponer formato adicional. |
| `price` | Objeto `Price` | Precio de la atracción | Se almacena con su moneda y total. |
| `categories` | Lista de texto | Categorías | Se representa como una relación de múltiples valores. |
| `badges` | Lista de texto | Insignias o etiquetas | Se representa como una relación de múltiples valores. |
| `locations` | Lista de `Location` | Ubicaciones asociadas | Se representa como relación de múltiples valores. |
| `photos` | Lista de `Photo` | Fotografías asociadas | Se representa como relación de múltiples valores. |
| `operator` | Objeto `Operator` | Operador responsable | Se representa como relación con una entidad de operador. |
| `product_type` | Texto | Tipo: `SINGLE_TICKET`, `GUIDED_TOUR` o `PACKAGE` | Se valida mediante la enumeración del contrato. |
| `includes` | Lista de texto | Servicios incluidos | Se representa como relación de múltiples valores. |
| `supported_languages` | Lista de texto | Idiomas soportados | Se representa como relación de múltiples valores. |
| `free_cancellation` | Booleano | Política de cancelación gratuita | Se conserva tal como está definido. |
| `ratings` | Objeto `Rating` | Calificación agregada | Se almacena como datos asociados a la atracción. |
| `url` | Objeto `Url` | URLs web y móvil | Se almacena como datos asociados a la atracción. |
| `_links` | Objeto | Hipervínculos del recurso | Se genera mediante la API, no se persiste como dato de negocio. |

### 3.2 Precios

El contrato define `Price` con dos propiedades:

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `currency` | Texto | Código de moneda de la operación. |
| `total` | Número decimal | Importe total asociado a la atracción o reserva. |

Se recomienda mantener `Price` como una entidad independiente cuando el mismo precio se reutilice en múltiples registros. En este caso, la estructura puede mantenerse como una representación embebida para conservar el contrato sin agregar campos.

### 3.3 Ubicaciones

El contrato define `Location` como un objeto compuesto.

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `address` | Texto | Dirección física. |
| `city` | Texto | Ciudad asociada, según la representación del contrato. |
| `country` | Texto | País. |
| `coordinates` | Objeto `Coordinates` | Coordenadas geográficas. |
| `type` | Texto | Tipo de ubicación. |

La relación entre atracciones y ubicaciones será de muchos a muchos. La entidad de ubicación puede ser reutilizada en otras operaciones del catálogo.

### 3.4 Coordenadas

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `latitude` | Número decimal | Latitud geográfica. |
| `longitude` | Número decimal | Longitud geográfica. |

### 3.5 Operadores

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `id` | Número entero | Identificador del operador. |
| `name` | Texto | Nombre del operador. |

### 3.6 Fotografías

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `url` | Texto con formato URI | URL de la fotografía. |

### 3.7 Calificaciones

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `number_of_reviews` | Número entero | Cantidad total de reseñas. |
| `score` | Número decimal | Puntaje promedio. |

### 3.8 URLs

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `web` | Texto con formato URI | Sitio web de la atracción. |
| `app` | Texto con formato URI | Aplicación asociada. |

### 3.9 Catálogo de valores de lista

Los valores de las listas siguientes se almacenarán en tablas auxiliares, sin agregar campos nuevos a sus registros:

- `categories`
- `badges`
- `includes`
- `supported_languages`

Cada tabla auxiliar contendrá únicamente el valor textual definido por el contrato. La relación con una atracción se mantiene mediante una tabla de asociación.

## 4. Relaciones del catálogo

```text
Atraccion
  ├── 1:N AtraccionCategoria
  ├── 1:N AtraccionBadge
  ├── 1:N AtraccionUbicacion
  ├── 1:N AtraccionFoto
  ├── 1:N AtraccionIdioma
  ├── 1:N AtraccionIncluido
  └── N:1 Operador

Atraccion
  └── 1:1 Rating

Atraccion
  └── 1:1 Url

Atraccion
  └── 1:1 Price

Atraccion
  └── 1:N Reservation
```

Las relaciones anteriores se basan únicamente en los atributos definidos en el contrato. No se agregan campos de auditoría ni de origen en este esquema.

## 5. Entidad de disponibilidad

El endpoint de disponibilidad utiliza los siguientes datos:

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `date` | Fecha | Fecha consultada. |
| `available_spots` | Número entero | Cupos disponibles. |
| `times` | Lista de texto | Franjas horarias disponibles. |

La entidad de disponibilidad puede modelarse como una relación entre una atracción, una fecha y una franja horaria. La fecha y las horas deben persistirse tal como aparecen en el contrato; no se agregan propiedades adicionales.

### 5.1 Representación sugerida

```text
AvailabilitySlot
  ├── attraction_id (FK técnica)
  ├── date
  ├── time
  ├── capacity
  ├── reserved_quantity
  └── version

Resultado de disponibilidad
  ├── date
  ├── available_spots
  └── times
```

`available_spots` se calcula como `capacity - reserved_quantity`; una fila única por atracción, fecha y hora evita asignaciones duplicadas. `capacity`, `reserved_quantity` y `version` son datos internos y no se añaden a las respuestas del contrato actual.

## 6. Entidad de reservas

### 6.1 Reserva

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `reservation_id` | UUID | Identificador de la reserva. |
| `status` | Texto | Estado: `CONFIRMED`, `PENDING` o `CANCELLED`. |
| `ticket_count` | Número entero | Cantidad de entradas. |
| `total_price` | Objeto `Price` | Precio total de la reserva. |
| `date` | Fecha | Fecha de la reserva. |
| `time` | Texto | Hora de la reserva. |
| `customer_name` | Texto | Nombre del cliente. |
| `customer_email` | Texto | Correo del cliente. |

La reserva se relacionará con la atracción mediante una clave técnica externa, sin agregar `attraction_id` como campo del contrato de entrada o salida. La relación puede obtenerse desde el identificador de la ruta y conservarse en la persistencia como una clave foránea.

### 6.2 Estado de reserva

El contrato declara los siguientes estados:

- `CONFIRMED`
- `PENDING`
- `CANCELLED`

Los estados se representarían mediante una enumeración con esos valores. No se incluirán estados adicionales en la base de datos durante esta fase.

### 6.3 Cancelación

`CancelReservationRequest` contiene:

| Campo | Tipo sugerido | Uso en el contrato |
|---|---|---|
| `reason` | Texto | Razón de la cancelación. |

La cancelación se registrará como el mismo recurso de reserva con su `status` actualizado a `CANCELLED`. El texto de la razón se conservará sin agregar más propiedades.

La reserva usa únicamente los estados expuestos por el contrato actual. La retención temporal de cupos pertenece a `checkout_sessions` y no se modela como un estado público nuevo de reserva.

## 7. Mapeo entre endpoints y entidades

### 7.1 `POST /atracciones/search`

**Entrada:** `SearchAtraccionesRequest`

**Datos utilizados:**

- `currency`
- `cities`
- `countries`
- `dates`
- `filters`
- `next_page`
- `rows`
- `sort`

**Base de datos:**

- Busca en las entidades de atracciones y sus ubicaciones.
- Aplica los filtros definidos en el input.
- Devuelve las propiedades de `AtraccionResponse`.

### 7.2 `POST /atracciones/details`

**Entrada:** `DetailsRequest`

**Datos utilizados:**

- `attractions`
- `languages`

**Base de datos:**

- Consulta las atracciones por sus identificadores.
- Filtra los idiomas solicitados.
- Utiliza las relaciones de catálogo para construir el detalle.

### 7.3 `GET /atracciones`

**Datos utilizados:**

- `limit`
- `offset`

**Base de datos:**

- Consulta el catálogo paginado.
- Retorna `PaginatedAtraccionResponse`.
- La respuesta debe incluir los atributos definidos en la regla de paginación.

### 7.4 `POST /atracciones`

**Entrada:** `CreateAtraccionRequest`

**Base de datos:**

- Crea una nueva atracción.
- Crea las relaciones de categorías, badges, inclusiones e idiomas.
- Crea la ubicación, operador y fotografías asociados.
- Guarda el precio y la calificación si se proporciona.

### 7.5 `GET /atracciones/{id}`

**Base de datos:**

- Consulta la atracción por su `id`.
- Recupera los datos asociados en las tablas relacionadas.
- Devuelve un `AtraccionResponse`.

### 7.6 `PUT /atracciones/{id}`

**Entrada:** `CreateAtraccionRequest`

**Base de datos:**

- Sustituye la información de la atracción.
- Reemplaza las relaciones existentes con los nuevos valores.
- Conserva únicamente los datos definidos en el contrato.

### 7.7 `PATCH /atracciones/{id}`

**Entrada:** `UpdateAtraccionRequest`

**Base de datos:**

- Actualiza solo los campos incluidos en la petición.
- No cambia campos omitidos.
- No añade campos que el contrato no declare.

### 7.8 `DELETE /atracciones/{id}`

**Base de datos:**

- Elimina la atracción y sus relaciones del catálogo.
- El comportamiento de eliminación debe definirse con la lógica del contrato antes de implementarse.

### 7.9 `GET /atracciones/{id}/availability`

**Entrada:**

- `id`
- `date`

**Base de datos:**

- Consulta la disponibilidad de la atracción.
- Devuelve `date`, `available_spots` y `times`.

### 7.10 `POST /atracciones/{id}/reservations`

**Entrada:** `ReservationRequest`

**Base de datos:**

- Crea una reserva asociada a la atracción.
- Utiliza `Idempotency-Key` para controlar operaciones duplicadas.
- Guarda la fecha, hora, cantidad, cliente y precio total.

### 7.11 `GET /atracciones/reservations`

**Base de datos:**

- Consulta reservas por el usuario autenticado.
- Aplica los parámetros de filtrado y paginación que se definan.
- Devuelve un arreglo de `ReservationResponse`.

### 7.12 `GET /atracciones/reservations/{reservationId}`

**Base de datos:**

- Consulta la reserva por `reservation_id`.
- Recupera la atracción y el precio asociados.

### 7.13 `POST /atracciones/reservations/{reservationId}/cancel`

**Entrada:** `CancelReservationRequest`

**Base de datos:**

- Obtiene la reserva por `reservation_id`.
- Cambia el estado a `CANCELLED`.
- Almacena la razón de cancelación.
- Devuelve una respuesta de `ReservationResponse`.

## 8. Propiedades y relaciones de persistencia

### 8.1 Identificadores

- `Atraccion.id`: UUID.
- `Reservation.reservation_id`: UUID.
- `Operator.id`: entero.
- Las relaciones entre catálogo y reservas usarán los identificadores indicados por el contrato.

### 8.2 Relación entre catálogo y reservas

La relación se representa mediante una clave foránea técnica en la entidad de reserva, obtenida del parámetro `id` de la ruta y sin incluirla como campo del contrato API.

```text
Atraccion.id 1 ─── N Reserva
```

Esta relación permite consultar todas las reservas de una atracción sin almacenar datos de catálogo dentro de `Reservation`.

### 8.3 Relación entre reserva y precio

```text
Reservation.total_price 1 ─── 1 Reservation
```

El precio se guarda con la reserva para conservar el monto confirmado en el momento de la reserva.

## 9. Índices recomendados

- Índice sobre `Atraccion.id`.
- Índice sobre `Atraccion.product_type`.
- Índice sobre `Atraccion.name`.
- Índice sobre `Location.city`.
- Índice sobre `Reservation.reservation_id`.
- Índice sobre la clave foránea de la reserva con la atracción.
- Índice sobre `Reservation.status`.
- Índice sobre `Reservation.customer_email`.
- Índice sobre la fecha y la clave foránea de disponibilidad.

Los índices no representan campos nuevos del contrato y se utilizan para mejorar el acceso a los datos ya definidos.

## 10. Orden sugerido de implementación

1. Crear tablas de catálogo y sus relaciones.
2. Crear las tablas de ubicaciones, fotografías, operadores y valores de lista.
3. Crear los endpoints de lectura del catálogo.
4. Crear las reglas de búsqueda y paginación.
5. Crear las tablas de disponibilidad.
6. Crear las tablas de reservas.
7. Implementar la creación y consulta de reservas.
8. Implementar la cancelación y el manejo idempotente.
9. Validar que todos los campos de entrada y salida se correspondan con el contrato.
10. Ejecutar pruebas de integridad y de datos.

## 11. Reglas de validación de la base de datos

- `id` y `reservation_id` deben tener formato UUID válido.
- `product_type` debe contener uno de los valores definidos por el contrato.
- `status` debe contener uno de los valores definidos por el contrato.
- `ticket_count` debe ser un número entero mayor que cero.
- `total` debe ser un número decimal válido.
- `currency` debe estar presente en el precio.
- La fecha no puede estar vacía.
- `customer_name` y `customer_email` no pueden quedar vacíos.
- Las relaciones de catálogo deben conservar las asociaciones indicadas por cada entrada del contrato.

## 12. Tablas adicionales para operar como ecommerce independiente

El contrato define catálogo y reservas, pero un ecommerce independiente también necesita persistir usuarios, clientes, compras directas, pedido, pagos simulados, auditoría y seguimiento de eventos. Las tablas nuevas son persistencia adicional y no modifican por sí solas los esquemas API existentes.

Para que un pedido represente una experiencia reservable, el contrato ecommerce propuesto debe incluir fecha, hora y cantidad por item, y la API debe calcular el precio. Esos campos todavía no están acordados en `CORRECCIONES_CONTRATO.md`; la validación de disponibilidad y el precio quedan bloqueados hasta aprobarlos allí. No se alteran los schemas de `CONTRATO.md`.

### 12.1 `users`

Representa la identidad del usuario que interactúa con el ecommerce.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador interno del usuario. |
| `oauth_issuer` | Texto | Issuer validado que emitió la identidad externa. |
| `oauth_subject` | Texto | Claim `sub` inmutable del proveedor OAuth2 asociado al usuario. |
| `email` | Texto | Correo de acceso. |
| `status` | Texto | Estado de la cuenta, por ejemplo `ACTIVE`, `LOCKED` o `DISABLED`. |
| `created_at` | Fecha y hora | Fecha de creación del usuario. |
| `updated_at` | Fecha y hora | Fecha de última actualización. |

**Reglas:** el par `oauth_issuer + oauth_subject` es único y no puede ser elegido por el cliente. La identidad y el correo se obtienen de claims verificados del token OAuth2. Las credenciales, contraseñas y tokens son responsabilidad del proveedor OAuth2 y no se almacenan en esta base de datos.

### 12.2 `customers`

Representa el cliente fiscal y de facturación asociado al usuario.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador interno del cliente. |
| `user_id` | UUID | Usuario propietario del cliente. |
| `billing_name` | Texto | Nombre utilizado para la facturación. |
| `billing_email` | Texto | Correo utilizado para la facturación y notificaciones. |
| `billing_address` | Texto | Dirección de facturación. |
| `tax_id` | Texto | Identificación fiscal, cuando corresponda. |
| `payment_method_reference` | Texto | Referencia del método de pago configurado. |
| `created_at` | Fecha y hora | Fecha de creación del registro. |
| `updated_at` | Fecha y hora | Fecha de última actualización. |

**Reglas:** cada usuario tiene exactamente un cliente. El cliente puede actualizar sus datos de facturación sin alterar los datos de acceso del usuario. El usuario es quien interactúa con el ecommerce y el cliente proporciona los datos para la pasarela simulada.

### 12.3 `purchases`

Representa una compra directa de una atracción en un slot específico y debe quedar asociada a un único pedido.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador de la compra directa. |
| `customer_id` | UUID | Cliente que realizó la compra. |
| `attraction_id` | UUID | Atracción comprada. |
| `service_date` | Fecha | Fecha de la experiencia. |
| `service_time` | Hora | Franja de disponibilidad seleccionada. |
| `quantity` | Número entero | Cantidad solicitada. |
| `unit_price_currency` | Texto | Moneda del precio confirmado por el servidor. |
| `unit_price_total` | Número decimal | Precio unitario confirmado. |
| `total_amount` | Número decimal | Monto total. |
| `status` | Texto | Estado de la compra y su pedido asociado. |
| `request_idempotency_key` | Texto | Clave de idempotencia asociada a la solicitud. |
| `created_at` | Fecha y hora | Fecha de creación. |
| `updated_at` | Fecha y hora | Última modificación. |

**Reglas:** la cantidad debe ser mayor que cero, la disponibilidad debe validarse contra un slot concreto y el precio se calcula exclusivamente en el servidor. Una compra directa puede originar como máximo un pedido confirmado.

### 12.4 `orders`

Representa el pedido generado desde una compra directa. No sustituye la reserva; se mantiene una relación con la entidad de reserva cuando exista una confirmación.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador del pedido. |
| `customer_id` | UUID | Cliente que realizó el pedido. |
| `purchase_id` | UUID | Compra directa origen. |
| `reservation_id` | UUID | Reserva asociada, si se crea una. |
| `status` | Texto | Estado del pedido. |
| `currency` | Texto | Moneda del pedido. |
| `total_amount` | Número decimal | Monto total. |
| `created_at` | Fecha y hora | Fecha de creación. |
| `updated_at` | Fecha y hora | Última actualización. |

Estados de pedido para el primer alcance:

- `PENDING_PAYMENT`: pedido generado y pendiente de pago.
- `PAID`: pago confirmado.
- `FULFILLED`: experiencia entregada o servicio concluido.
- `CANCELLED`: pedido cancelado.
- `PARTIALLY_REFUNDED`: reembolso parcial registrado.
- `REFUNDED`: reembolso total registrado.

El estado `PAYMENT_FAILED` pertenece exclusivamente a un intento de pago y no debe usarse como estado terminal del pedido mientras existan reintentos configurados. Las transiciones permitidas son `PENDING_PAYMENT -> PAID | CANCELLED`; `PAID -> FULFILLED | CANCELLED | PARTIALLY_REFUNDED | REFUNDED`; `PARTIALLY_REFUNDED -> PARTIALLY_REFUNDED | REFUNDED`. Las transiciones no listadas se rechazan.

### 12.6 `order_items`

Representa cada entrada o servicio incluidos en un pedido.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador del elemento del pedido. |
| `order_id` | UUID | Pedido propietario. |
| `attraction_id` | UUID | Atracción del producto. |
| `service_date` | Fecha | Fecha contratada. |
| `service_time` | Hora | Franja contratada. |
| `quantity` | Número entero | Cantidad adquirida. |
| `unit_price_currency` | Texto | Moneda del precio. |
| `unit_price_total` | Número decimal | Precio unitario confirmado. |
| `status` | Texto | Estado del elemento. |

### 12.7 `payment_simulations`

Representa una pasarela de pagos simulada.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador de la simulación de pago. |
| `order_id` | UUID | Pedido asociado. |
| `payment_method` | Texto | Método simulado, por ejemplo `CARD` o `BANK_TRANSFER`. |
| `status` | Texto | Estado de la simulación. |
| `amount` | Número decimal | Monto solicitado. |
| `currency` | Texto | Moneda solicitada. |
| `gateway_reference` | Texto | Referencia generada por la pasarela simulada. |
| `created_at` | Fecha y hora | Fecha y hora de la solicitud. |
| `processed_at` | Fecha y hora | Fecha y hora de procesamiento. |
| `failure_reason` | Texto | Motivo cuando el pago falla. |

Estados iniciales sugeridos: `PENDING`, `AUTHORIZED`, `SETTLED`, `REJECTED`, `FAILED` y `CANCELLED`.
Estados canónicos: `PENDING`, `AUTHORIZED`, `SETTLED`, `REJECTED`, `FAILED`, `CANCELLED`, `PARTIALLY_REFUNDED` y `REFUNDED`. Cada intento fallido puede crear otro intento hasta el límite configurado; solo `SETTLED` liquida el pedido. Un reembolso simulado solo se admite desde un pago `SETTLED` y debe registrar importe, motivo, actor administrativo y evento inmutable. No se almacena información de tarjeta.

### 12.8 `payment_attempts`

Registra cada intento de pago simulado.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador del intento. |
| `payment_simulation_id` | UUID | Pago al que pertenece el intento. |
| `attempt_number` | Número entero | Número de intento. |
| `status` | Texto | Resultado del intento. |
| `response_code` | Texto | Código simulado del proveedor. |
| `response_message` | Texto | Mensaje de respuesta. |
| `created_at` | Fecha y hora | Fecha y hora del intento. |

### 12.9 `payment_events`

Registra eventos emitidos por la pasarela simulada.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador del evento. |
| `payment_simulation_id` | UUID | Pago relacionado. |
| `event_type` | Texto | Tipo de evento, por ejemplo `AUTHORIZED`, `SETTLED` o `REJECTED`. |
| `payload` | JSON | Datos del evento. |
| `created_at` | Fecha y hora | Fecha y hora del evento. |

El payload puede contener únicamente datos de la simulación, sin integrar una pasarela externa real.

### 12.10 `idempotency_keys`

Controla la idempotencia de operaciones transaccionales.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `issuer` | Texto | Issuer OAuth2 verificado. |
| `subject` | Texto | Claim `sub` verificado. |
| `key` | Texto | Clave idempotente proporcionada por el cliente. |
| `operation` | Texto | Operación asociada. |
| `resource_id` | UUID | Recurso o pedido afectado. |
| `request_hash` | Texto | Hash de los datos de la solicitud. |
| `status` | Texto | `IN_PROGRESS` o `COMPLETED`. |
| `http_status` | Entero | Código HTTP original para replay. |
| `response_body` | JSON | Resultado original serializado y sanitizado. |
| `created_at` | Fecha y hora | Fecha y hora de creación. |
| `expires_at` | Fecha y hora | Fecha de expiración. |

La tabla debe permitir una sola respuesta por clave en una operación específica.

La clave única se compone de `issuer + subject + operation + key`. El registro guarda hash canónico de request, estado `IN_PROGRESS`/`COMPLETED`, status HTTP, respuesta reproducible y expiración. Clave repetida con el mismo hash devuelve el resultado previo; payload distinto produce conflicto. El TTL es configuración común, no una regla distinta por tabla.

### 12.11 `inventory_movements`

Registra cambios de disponibilidad de cupos.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador del movimiento. |
| `attraction_id` | UUID | Atracción afectada. |
| `availability_id` | UUID | Franja de disponibilidad afectada. |
| `quantity` | Número entero | Cambio de disponibilidad. |
| `movement_type` | Texto | `CONFIRMED`, `RELEASED` o `CANCELLED`. |
| `purchase_id` | UUID | Compra asociada, cuando aplique. |
| `reservation_id` | UUID | Reserva relacionada. |
| `created_at` | Fecha y hora | Fecha y hora del movimiento. |

No se incluye una tabla auxiliar de stock si la disponibilidad se almacena únicamente por fecha y hora; cuando exista una disponibilidad específica, la relación puede conectarse con la tabla de disponibilidad.

La compra directa no mantiene una sesión de checkout ni una retención independiente; la disponibilidad y el precio se validan dentro de la misma transacción de compra.

### 12.12 `order_events`

### 12.14 `roles` y `user_roles`

`roles` define roles locales autorizados y `user_roles` sus asignaciones. Ninguna tabla gestiona credenciales o scopes del proveedor OAuth2.

| Tabla | Campos mínimos |
|---|---|
| `roles` | `id`, `name` único, `description`, `created_at` |
| `role_permissions` | `role_id`, `permission` con clave única compuesta |
| `user_roles` | `user_id`, `role_id`, `assigned_by_user_id`, `reason`, `assigned_at`, `revoked_at` nullable |

Los roles iniciales son `admin`, `catalog_manager`, `inventory_manager`, `order_manager`, `support_manager` y `finance_manager`. La asignación/revocación se registra también en auditoría.

### 12.15 `refund_simulations`

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador del reembolso simulado. |
| `payment_simulation_id` | UUID | Pago liquidado al que pertenece. |
| `amount` | Decimal | Importe devuelto, mayor que cero y no superior al saldo reembolsable. |
| `currency` | Texto | Debe coincidir con el pago. |
| `status` | Texto | `PENDING`, `SETTLED`, `FAILED` o `CANCELLED`. |
| `reason` | Texto | Motivo obligatorio para auditoría. |
| `created_by_user_id` | UUID | Usuario administrativo autenticado que inició la operación. |
| `created_at` | Fecha y hora | Inicio de la operación. |
| `processed_at` | Fecha y hora | Resultado de la simulación. |

Los reembolsos parciales acumulados no pueden superar el importe liquidado. No se conecta a una pasarela real.

### 12.16 `audit_events`

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador del registro inmutable. |
| `actor_user_id` | UUID | Usuario que realizó la operación. |
| `action` | Texto | Acción administrativa controlada. |
| `resource_type` | Texto | Tipo de recurso. |
| `resource_id` | Texto | Identificador del recurso afectado. |
| `reason` | Texto | Justificación para mutaciones sensibles. |
| `metadata` | JSON sanitizado | Cambios no sensibles, sin tokens ni información financiera. |
| `created_at` | Fecha y hora | Fecha de auditoría. |

### 12.17 `order_events`

Registra los cambios de estado de un pedido.

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| `id` | UUID | Identificador del evento. |
| `order_id` | UUID | Pedido afectado. |
| `event_type` | Texto | Tipo de evento. |
| `previous_status` | Texto | Estado anterior. |
| `new_status` | Texto | Nuevo estado. |
| `created_at` | Fecha y hora | Fecha y hora del evento. |

## 13. Relaciones del ecommerce

```text
users 1 ─── 1 customers
customers 1 ─── N purchases
customers 1 ─── N orders

purchases 1 ─── 0..1 orders
orders 1 ─── N order_items
orders 1 ─── N payment_simulations
orders 1 ─── N order_events

payment_simulations 1 ─── N payment_attempts
payment_simulations 1 ─── N payment_events

Atraccion 1 ─── N purchases
Atraccion 1 ─── N order_items
Atraccion 1 ─── N inventory_movements
Reservation 1 ─── 0..1 orders
```

La relación `users 1 ─── 1 customers` representa la propiedad del cliente por el usuario. La relación `Reservation 1 ─── 1 orders` debe ser una asociación opcional y debe utilizar el identificador de reserva disponible en el contrato.

Cada compra directa produce como máximo un pedido; la validación de disponibilidad, el precio, el pago y la creación del pedido se coordinan dentro de una sola unidad de trabajo local.

## 14. Reglas de negocio y estados del ecommerce

1. Un usuario local se vincula mediante `issuer + sub` de un token OAuth2 validado; la API no administra contraseñas ni almacena tokens.
2. Un usuario tiene exactamente un cliente asociado.
3. El cliente almacena los datos de facturación y referencias de pago simuladas, no las credenciales del usuario.
4. El usuario puede actualizar sus datos de acceso, pero no debe poder modificar la identidad del cliente mediante la autenticación.
5. Una compra directa referencia una sola atracción, fecha, horario y cantidad.
6. La cantidad solicitada debe ser positiva y válida para la disponibilidad concreta.
7. El pedido debe originarse en una compra directa validada.
8. El pago simulado genera una `payment_simulation` y sus intentos.
9. Un pedido y un pago solo cambian mediante transiciones permitidas y registran cada cambio en eventos inmutables.
10. La disponibilidad se confirma durante la misma transacción del pedido y puede liberarse mediante cancelación o expiración.
11. Cancelación permitida revierte la reserva de cupos una sola vez; nunca se actualiza disponibilidad fuera de la transacción de estado.
12. Los eventos de pago y auditoría son inmutables.
13. La misma política de idempotencia se aplica en API, Application y persistencia.
14. Las operaciones protegidas requieren access token OAuth2 vigente, scopes y, para administración, permiso local; emisión y revocación de tokens pertenecen al proveedor.
15. Roles locales iniciales: `admin`, `catalog_manager`, `inventory_manager`, `order_manager`, `support_manager` y `finance_manager`; los permisos se asignan explícitamente y todas las mutaciones admin generan auditoría.

Las tablas `roles`, `user_roles`, `refund_simulations` y `audit_events` forman parte del modelo requerido para el panel administrativo. Los scopes OAuth2 siguen siendo responsabilidad del issuer y no se guardan como roles locales.

El ecommerce solo se considera funcional cuando hay restricciones únicas y pruebas concurrentes para checkout, cupos, pedido e idempotencia; los roles y permisos no se infieren desde datos enviados por el cliente.

## 15. Orden de implementación

1. Crear la tabla de usuarios con la restricción única del claim `oauth_subject`.
2. Crear la tabla de clientes y la relación uno a uno con usuarios.
3. Crear las tablas de compras directas y elementos relacionados.
4. Crear las tablas de pedido y elementos del pedido.
5. Crear la tabla de disponibilidad histórica y movimientos de inventario.
6. Crear las tablas de pago simulado, intentos y eventos.
7. Crear la tabla de idempotencia.
8. Crear los eventos de pedido y sus transiciones.
9. Integrar la API con el issuer OAuth2 y aprovisionar el perfil local desde claims validados.
10. Implementar la validación de disponibilidad, precio y creación de compra.
11. Implementar la creación de pedidos desde la compra directa.
12. Implementar la simulación de pagos y sus estados.
13. Implementar el enlace entre pedido, reserva y disponibilidad.
14. Ejecutar pruebas de integridad, idempotencia, autorización OAuth2 y estados.

## 16. Criterio de aceptación del ecommerce

La implementación estará completa cuando:

- Un usuario autenticado mediante OAuth2 puede aprovisionar su perfil local con un claim `sub` validado y único.
- Un usuario tiene exactamente un cliente asociado.
- Un cliente puede actualizar sus datos de facturación sin modificar los datos de acceso del usuario.
- Un cliente puede crear y modificar un carrito.
- Un carrito puede convertirse en un pedido.
- El pago simulado puede quedar en estados `PENDING`, `AUTHORIZED`, `SETTLED`, `FAILED` o `CANCELLED`.
- La pasarela simulada puede generar intentos y eventos sin integrarse con un proveedor externo.
- Un pedido puede registrarse con su historial de estados.
- Las reservas y pedidos mantienen una relación verificable.
- La disponibilidad se ajusta mediante movimientos de inventario.
- La idempotencia evita ejecuciones duplicadas.
- La API no guarda ni emite tokens; valida vigencia y scopes del access token OAuth2.
- Las transacciones de pedido y pago pueden revertirse sin alterar el contrato API.

## 17. Criterio de aceptación del modelo

La implementación estará completa cuando:

- Cada campo de entrada y salida del contrato tenga una representación persistida.
- Las tablas nuevas no agregan campos a los endpoints existentes.
- Todas las tablas técnicas sean necesarias para el ecommerce independiente.
- Un usuario y su cliente permanezcan vinculados mediante una relación uno a uno.
- Los datos de facturación y pasarela se separen de la identidad OAuth2; no se persisten credenciales ni tokens.
- Los pasos de checkout, pago y reserva pueden ejecutarse sin una pasarela real.
- Los estados y eventos puedan auditarse mediante los registros de eventos.
- El modelo puede generar un pedido y una reserva sin repetir información del contrato.
