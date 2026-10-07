# Auditoría de inconsistencias del contrato OpenAPI

> Documento de análisis estático. **No modifica el archivo CONTRATO.md.**

## Resumen ejecutivo

El contrato define una API de catálogo y reservas con OAuth2, paginación, idempotencia y errores RFC 7807. Durante la revisión se detectaron inconsistencias importantes en la estructura de rutas, el diseño de seguridad, los esquemas de respuesta y la definición de los errores.

## Hallazgos encontrados

### 1. La ruta `/atracciones/{id}` aparece duplicada

**Severidad:** Alta

Hay dos bloques independientes con la misma ruta:

- Uno contiene `get`, `put`, `patch` y `delete`.
- Otro contiene un segundo `put`, `patch` y `delete`.

Esto es una inconsistencia estructural. En OpenAPI, una ruta debe definirse una sola vez y cada operación debe estar dentro del mismo bloque. La segunda aparición puede provocar que el parser o el generador de documentación considere una definición duplicada o sobrescriba el bloque original.

**Referencia:** Sección `paths` de `CONTRATO.md`.

### 2. La ruta `/atracciones/{id}` debe quedar definida una sola vez

**Severidad:** Alta

Aunque el archivo contiene dos bloques con la misma ruta, el contrato no establece claramente cuál de ellos es la definición definitiva. La API no puede tener dos definiciones distintas para el mismo recurso sin provocar ambigüedad.

**Referencia:** Sección `paths` de `CONTRATO.md`.

### 3. Falta el scope `attractions:cancel` en el esquema OAuth2

**Severidad:** Alta

La operación de cancelación exige el scope `attractions:cancel`, pero el esquema `OAuth2Security` no declara ese scope.

- Operación de cancelación: `attractions:cancel`
- Scopes disponibles: `attractions:read`, `attractions:book`, `attractions:write`, `attractions:webhooks`

Esta discrepancia significa que un cliente autenticado con los scopes declarados no podría obtener el permiso necesario para cancelar una reserva, aunque la operación lo solicite.

**Referencia:**

- `paths./atracciones/reservations/{reservationId}/cancel.security`
- `components.securitySchemes.OAuth2Security.flows.authorizationCode.scopes`

### 4. El scope `attractions:webhooks` está declarado, pero no hay endpoints webhooks en el contrato

**Severidad:** Media

El contrato declara `attractions:webhooks`, pero no existen rutas ni operaciones que gestionen webhooks. Tampoco se define un recurso de suscripciones, eventos o callbacks.

La existencia de un scope sin consumirlo puede indicar que la especificación conserva requisitos de otro proyecto o de un diseño incompleto.

**Referencia:** `components.securitySchemes.OAuth2Security.flows.authorizationCode.scopes`.

### 5. El contrato no declara el scope de lectura para el endpoint de salud

**Severidad:** Baja

El endpoint `/atracciones/health` no especifica una política de seguridad, aunque los demás endpoints de lectura requieren `attractions:read`.

Esto no es necesariamente un problema si el healthcheck debe ser público, pero el contrato no expresa esa intención. La política debe quedar explícita para evitar que se interprete que la operación requiere autenticación.

**Referencia:** `paths./atracciones/health`.

### 6. El endpoint de detalle masivo devuelve un esquema incorrecto

**Severidad:** Alta

El endpoint `/atracciones/details` responde con:

- `SearchAtraccionesResponse`

Sin embargo, la operación está diseñada para consultar múltiples detalles de atracciones, no necesariamente devolver una búsqueda completa. El esquema de respuesta no representa explícitamente el conjunto de detalles solicitados por el cliente.

La respuesta puede estar incompleta para validar las propiedades de cada detalle o para distinguir entre los elementos encontrados y los que no existen.

**Referencia:** `paths./atracciones/details.responses.200.content.application/json.schema`.

### 7. El esquema `UpdateAtraccionRequest` está vacío

**Severidad:** Alta

La operación `PATCH /atracciones/{id}` acepta `UpdateAtraccionRequest`, pero ese esquema no contiene ninguna propiedad ni reglas de validación.

Esto hace que una petición parcial no tenga una estructura definida. Además, el contrato no especifica:

- Campos permitidos.
- Campos obligatorios.
- Tipos de los campos.
- Validaciones parciales.
- Comportamiento para valores nulos o campos desconocidos.

**Referencia:** `components.schemas.UpdateAtraccionRequest`.

### 8. El esquema `ProblemDetails` no declara `detail` ni `instance` como obligatorios

**Severidad:** Media

La descripción RFC 7807 usa `detail` e `instance` como campos opcionales, por lo que no es necesario exigirlos. Sin embargo, el contrato no establece un formato uniforme para los errores cuando `detail` o `instance` no se envían.

Para una implementación consistente, convendría definir ejemplos o restricciones para los mensajes de error, incluidos códigos de error y trazabilidad.

**Referencia:** `components.schemas.ProblemDetails`.

### 9. El error `409` usa una respuesta genérica, pero no distingue conflictos de idempotencia

**Severidad:** Media

La respuesta `ProblemDetails409` está vinculada a todos los posibles conflictos, incluyendo errores de idempotencia y conflictos de disponibilidad.

El contrato no define qué diferencia a un conflicto de disponibilidad de uno de idempotencia. Un cliente no puede distinguir si el problema fue:

- Cupo agotado.
- Reserva duplicada.
- Recurso ya cancelado.
- Cancelación con una clave idempotencia diferente.

**Referencia:** `paths.*.responses.409` y `components.responses.ProblemDetails409`.

### 10. El campo `Idempotency-Key` se define como UUID, pero el contrato no define su comportamiento en respuestas conflictivas

**Severidad:** Media

La cabecera se exige para operaciones transaccionales y se declara como UUID. Sin embargo, el contrato no especifica:

- Cómo se debe devolver la misma respuesta para la misma clave.
- Qué ocurre si se reutiliza una clave con datos distintos.
- Qué código HTTP debe devolver cuando la clave ya fue usada.
- Si el campo es obligatorio para todos los clientes o solo para algunos.

**Referencia:** Parámetros `Idempotency-Key` en las operaciones de creación y cancelación.

### 11. El endpoint `GET /atracciones/reservations` no especifica el propietario de las reservas

**Severidad:** Alta

El endpoint devuelve el historial de reservas del usuario, pero no define:

- Cómo se identifica al usuario.
- Qué información de usuario se utiliza.
- Si se deben filtrar por usuario, estado o fecha.
- Qué parámetros de paginación admite.
- Qué estrategia se utiliza para evitar filtrar reservas de otros usuarios.

El contrato asume que la autenticación permite identificar al usuario, pero esa relación no queda formalizada.

**Referencia:** `paths./atracciones/reservations.get`.

### 12. El endpoint de reservas no incluye filtros, paginación ni orden

**Severidad:** Media

El endpoint `GET /atracciones/reservations` presenta una respuesta de array sin parámetros para:

- Estado de la reserva.
- Fecha de creación.
- Fecha de inicio.
- Ordenamiento.
- Paginación.

Esto puede provocar listas enormes o resultados no deterministas en producción.

**Referencia:** `paths./atracciones/reservations.get`.

### 13. La respuesta de paginación tiene nombres inconsistentes

**Severidad:** Alta

`PaginatedAtraccionResponse` usa la propiedad `meta` con nombres de campo en camelCase:

- `totalItems`
- `itemCount`
- `itemsPerPage`
- `totalPages`
- `currentPage`

Sin embargo, otras respuestas usan atributos con snake_case, por ejemplo:

- `available_spots`
- `reservation_id`
- `total_price`

El contrato no define una convención global para los nombres de las propiedades. Esto puede generar incompatibilidades en clientes y pruebas.

**Referencia:** `components.schemas.PaginatedAtraccionResponse` y `components.schemas.AvailabilityResponse`.

### 14. La respuesta de disponibilidad usa nombres inconsistentes

**Severidad:** Media

`AvailabilityResponse` usa `available_spots` y `times`, mientras que el resto del contrato utiliza tanto camelCase como snake_case.

La propiedad `times` puede representar franjas horarias, pero no se define el formato de la hora, el huso horario ni la zona local.

**Referencia:** `components.schemas.AvailabilityResponse`.

### 15. La respuesta de reservas utiliza nombres inconsistentes

**Severidad:** Media

`ReservationResponse` utiliza:

- `reservation_id`
- `total_price`

mientras que el resto del contrato utiliza nombres en camelCase.

El ejemplo de respuesta no permite determinar si el servicio debe aplicar una convención global o mantener una convención específica por recurso.

**Referencia:** `components.schemas.ReservationResponse`.

### 16. Los campos de ubicación tienen tipos inconsistentes

**Severidad:** Media

El esquema `Location` define:

- `address`: string
- `city`: integer
- `country`: string
- `coordinates`: objeto
- `type`: string

El campo `city` usa un entero, pero el nombre `city` sugiere un identificador o un nombre. El contrato no explica si `city` es un ID numérico, un código ISO 3166-2 o un nombre de ciudad. Tampoco se declara qué representa `type`.

**Referencia:** `components.schemas.Location`.

### 17. El campo `country` en `Location` no tiene formato ni restricciones

**Severidad:** Media

`country` es una cadena, pero el contrato no define si debe ser:

- Código ISO 3166-1 alfa-2.
- Código ISO 3166-1 alfa-3.
- Nombre completo del país.
- Identificador numérico.

Esto puede causar que el mismo dato se represente de maneras diferentes.

**Referencia:** `components.schemas.Location.properties.country`.

### 18. El formato del precio no está completamente definido

**Severidad:** Media

`Price` contiene:

- `currency`: string
- `total`: number

No se especifica:

- Moneda obligatoria.
- Número de decimales.
- Restricciones sobre valores negativos.
- Si `total` representa precios existentes, impuestos, descuentos o el total final.

Además, el contrato no establece una lista permitida de monedas ni sus códigos ISO.

**Referencia:** `components.schemas.Price`.

### 19. El campo `date` de disponibilidad no incluye zona horaria ni formato de hora

**Severidad:** Media

La consulta `/atracciones/{id}/availability` usa:

- Parámetro de fecha `date`.
- `format: date`.

La disponibilidad puede depender de horarios específicos, pero no se especifica si el día se interpreta en UTC, en la zona horaria de la atracción o en la zona local del usuario.

**Referencia:** `paths./atracciones/{id}/availability.parameters.date`.

### 20. La operación de disponibilidad no define errores de validación para fechas inválidas

**Severidad:** Baja

El endpoint devuelve `400` únicamente al referenciar `ProblemDetails400` en la ruta. Esa respuesta está definida globalmente, pero el contrato no especifica qué valores de fecha son inválidos ni cómo se reportan.

**Referencia:** `paths./atracciones/{id}/availability.responses`.

### 21. El valor `free_cancellation` no tiene una propiedad de política de cancelación

**Severidad:** Media

El contrato registra `free_cancellation` como booleano, pero no define:

- Si aplica a todas las fechas.
- Si aplica a todos los productos.
- Si existe una fecha límite.
- Si existe una política de reembolso.
- Cuál es la tarifa compensatoria cuando la cancelación no es gratuita.

**Referencia:** `components.schemas.CreateAtraccionRequest.free_cancellation` y `components.schemas.AtraccionResponse.free_cancellation`.

### 22. La propiedad `ratings` puede tener un solo objeto, mientras que la respuesta puede contener varias clasificaciones

**Severidad:** Media

`AtraccionResponse` contiene `ratings` como un único objeto:

- `number_of_reviews`
- `score`

No se define si cada producto tiene una sola valoración o si podría tener múltiples fuentes, fechas o versiones. El modelo tampoco expresa si la calificación puede ser nula.

**Referencia:** `components.schemas.Rating` y `components.schemas.AtraccionResponse.ratings`.

### 23. La propiedad `url` se define como un objeto, pero puede contener campos vacíos

**Severidad:** Baja

`Url` contiene opciones para web y móvil, pero no marca ninguno como obligatorio. El contrato no define qué ocurre cuando una atracción no dispone de una URL de aplicación o de sitio web.

**Referencia:** `components.schemas.Url`.

### 24. El esquema `CreateAtraccionRequest` no exige ningún campo obligatorio

**Severidad:** Alta

El objeto de creación contiene propiedades que parecen esenciales, como:

- `name`
- `long_description`
- `duration`
- `price`
- `product_type`

Sin embargo, no aparecen declarados en `required`.

Esto permite enviar una atracción incompleta y puede provocar registros inválidos o fallos posteriores en la capa de aplicación.

**Referencia:** `components.schemas.CreateAtraccionRequest`.

### 25. No se permite identificar de forma explícita que un recurso ya creado fue importado o creado manualmente

**Severidad:** Baja

El modelo de producto no incluye un campo de origen, estado, auditoría o creación. El contrato no especifica:

- Quién creó la atracción.
- Cuándo fue creada.
- Si el recurso se puede desactivar.
- Si el recurso es administrable.

**Referencia:** `components.schemas.CreateAtraccionRequest` y `components.schemas.AtraccionResponse`.

### 26. `SearchAtraccionesRequest` contiene filtros incompletos y sin requisitos

**Severidad:** Media

El esquema incluye:

- `currency`
- `cities`
- `countries`
- `dates`
- `filters`
- `next_page`
- `rows`
- `sort`

No se establecen restricciones para `rows`, `cities`, `countries`, `dates` ni `sort`. Tampoco se define qué ocurre si los filtros se combinan de formas inconsistentes.

**Referencia:** `components.schemas.SearchAtraccionesRequest`.

### 27. `next_page` no tiene formato ni control de seguridad

**Severidad:** Media

`next_page` se describe como un token opaco, pero no se define:

- Si es base64uRL-safe.
- Si tiene expiración.
- Si debe ser generado por el servidor.
- Si puede contener datos sensibles.

El término "opaco" indica que el cliente no debe interpretarlo, pero el contrato no define cómo debe tratarse un token inválido o expirado.

**Referencia:** `components.schemas.SearchAtraccionesRequest.next_page` y `components.schemas.SearchAtraccionesResponse.metadata.next_page`.

### 28. No se declara explícitamente el formato de la paginación en `GET /atracciones`

**Severidad:** Media

`limit` y `offset` son parámetros de consulta, pero el contrato no especifica:

- Máximo de `limit`.
- Valor mínimo de `limit` y `offset`.
- Validación contra un número máximo de resultados.
- Si `offset` debe ser cero y no negativo.
- Si la respuesta puede devolver menos resultados que el límite.

**Referencia:** `paths./atracciones.get.parameters.limit` y `paths./atracciones.get.parameters.offset`.

### 29. El contrato no define un esquema para el `Location` de reservas

**Severidad:** Media

La reserva solicita `date`, `time`, `ticket_count` y datos del cliente, pero no incluye una referencia explícita a la atracción ni su ubicación. La relación entre la reserva y la atracción está representada por el parámetro `id` de la ruta, pero no queda formalizada en el esquema de entrada.

**Referencia:** `components.schemas.ReservationRequest` y `paths./atracciones/{id}/reservations`.

### 30. El precio de la reserva se representa como un objeto, igual que el precio del catálogo

**Severidad:** Baja

`ReservationResponse.total_price` usa `Price`, pero el contrato no define si el precio incluye impuestos, descuento, tarifa de servicio, moneda o monto devuelto.

**Referencia:** `components.schemas.ReservationResponse.total_price`.

### 31. El campo `customer_email` no tiene formato de correo electrónico

**Severidad:** Media

`ReservationRequest.customer_email` es una cadena sin un formato `email` o una expresión de validación.

Esto puede permitir correos electrónicos inválidos y provocar fallos en los canales de confirmación y comunicación.

**Referencia:** `components.schemas.ReservationRequest.properties.customer_email`.

### 32. El campo `customer_name` no tiene límites ni validación

**Severidad:** Baja

`customer_name` es una cadena libre. El contrato no define:

- Longitud máxima.
- Espacios o caracteres permitidos.
- Requisitos de identidad para clientes.

**Referencia:** `components.schemas.ReservationRequest.properties.customer_name`.

### 33. La operación `POST /atracciones/{id}/reservations` no define `Idempotency-Key` en el cuerpo

**Severidad:** Baja

La cabecera `Idempotency-Key` se usa para evitar compras duplicadas, pero el contrato no define si el cliente puede obtener la respuesta anterior desde un endpoint de consulta mediante el mismo identificador.

**Referencia:** `paths./atracciones/{id}/reservations.parameters.Idempotency-Key`.

### 34. Falta una respuesta para errores 401 y 403

**Severidad:** Media

El contrato exige OAuth2 en las operaciones, pero no define respuestas para credenciales inválidas o permisos insuficientes.

Los clientes no pueden distinguir entre:

- Token expirado.
- Token inválido.
- Scope insuficiente.
- Usuario no autorizado.

**Referencia:** `components.responses` y operaciones protegidas.

### 35. `PUT /atracciones/{id}` no especifica la semántica de reemplazo

**Severidad:** Media

La operación usa `204 No Content`, pero el contrato no declara si:

- Reemplaza todo el recurso.
- Borra campos no enviados.
- Requiere que el cliente proporcione todos los campos.
- Devuelve el recurso actualizado o un indicador de éxito.

**Referencia:** `paths./atracciones/{id}.put`.

### 36. `DELETE /atracciones/{id}` no declara una política de eliminación o respuesta 404

**Severidad:** Media

El contrato declara `204` para una eliminación exitosa y `404` para no encontrado. Sin embargo, no define:

- Si la eliminación es física o lógica.
- Si el recurso puede reactivarse.
- Si existe un período de borrado o auditoría.
- Si una reserva asociada impide la eliminación.

**Referencia:** `paths./atracciones/{id}.delete`.

### 37. Falta una definición para los códigos HTTP 500, 503 y 429

**Severidad:** Media

El contrato define `400`, `404` y `409`, pero no contempla errores internos, indisponibilidad del servicio ni límites de tasa.

Esto puede dejar a los clientes sin una respuesta esperable ante fallos temporales o límites de uso.

**Referencia:** `components.responses` y operaciones de la API.

### 38. No hay operaciones de administración ni configuración para los webhooks

**Severidad:** Media

El contrato permite un scope de webhooks, pero no incluye:

- Listado de webhooks.
- Creación de webhooks.
- Eliminación de webhooks.
- Eventos de suscripción.
- Confirmación del payload.
- Reintentos y expiración.

El scope y la documentación no tienen un uso funcional coherente.

**Referencia:** `components.securitySchemes.OAuth2Security.scopes` y la sección `paths`.

### 39. Los documentos de documentación están parcialmente duplicados

**Severidad:** Baja

La ruta `/atracciones/{id}` aparece dos veces y varias operaciones de catálogo tienen definiciones repetidas. El contrato contiene procedimientos de actualización y eliminación duplicados.

Esto aumenta el riesgo de que el contrato se vuelva inconsistente durante futuras modificaciones y hace más difícil mantener una única fuente de verdad.

**Referencia:** Sección `paths` de `CONTRATO.md`.

## Posibles mejoras requeridas antes de implementar

1. Consolidar las operaciones duplicadas de `/atracciones/{id}`.
2. Añadir el scope `attractions:cancel` al esquema OAuth2.
3. Eliminar o implementar el scope `attractions:webhooks`.
4. Definir los campos obligatorios de `CreateAtraccionRequest`.
5. Completar `UpdateAtraccionRequest` con propiedades parciales y validaciones.
6. Definir una convención global de nombres: camelCase o snake_case.
7. Definir `Price`, `Location`, `Rating` y `Url` con reglas de negocio.
8. Añadir contratos completos para 401, 403, 429, 500 y 503.
9. Definir semántica de paginación y tokens de página.
10. Definir formato y política de `Idempotency-Key`.
11. Formalizar las relaciones de pertenencia entre reservas, atracciones y usuarios.
12. Añadir ejemplos completos y valores de respuesta para cada endpoint.

## Conclusión

El contrato tiene una base sólida para comenzar, pero contiene inconsistencias estructurales importantes. Las más críticas son la duplicación de `/atracciones/{id}`, la ausencia del scope `attractions:cancel`, el esquema vacío de actualización parcial y la falta de campos obligatorios en la creación.

Antes de implementar la API, conviene corregir estas inconsistencias y completar la definición de los códigos de error, seguridad, paginación y esquemas de negocio.
