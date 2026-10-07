# Auditoría de cohesión y completitud del ecommerce

> Este documento audita los contratos, planes y capas actuales contra `CONTRATO.md` y `CORRECCIONES_CONTRATO.md`. No modifica ningún contrato existente. Identifica brechas que deben resolverse antes de implementar.

## 1. Resumen ejecutivo

El proyecto presenta una base arquitectónica adecuada para una implementación API-first, con separación conceptual entre API, Application, Domain, DataManagement y DataAccess. Sin embargo, el conjunto actual aún no garantiza un commerce completamente funcional.

Los principales riesgos son:

1. El contrato OpenAPI original y la propuesta de correcciones no están integrados como una única fuente de verdad.
2. No existe un contrato formal para el panel administrativo.
3. La administración de catálogo, inventario, pedidos, pagos, clientes y usuarios no está definida en la API.
4. La capa DataManagement no está alineada con la dependencia solicitada por Application.
5. La idempotencia está documentada, pero no está formalizada como contrato y política uniforme.
6. Los procesos de checkout, pago, cancelación, devolución y disponibilidad aún necesitan estados y transacciones explícitos.
7. La referencia de pago y los métodos de pago simulados requieren reglas claras para evitar datos financieros reales.
8. La capa frontend no tiene una definición formal de permisos ni rutas administrativas.

## 2. Estado de cada capa

### 2.1 Contrato API

**Estado:** parcialmente corregido.

El contrato original de `CONTRATO.md` contiene el catálogo, disponibilidad y reservas, pero presenta duplicados y elementos incompletos. `CORRECCIONES_CONTRATO.md` propone amplios endpoints ecommerce, pero aun no se han integrado en un OpenAPI único.

**Revisión:**

- El contrato debe conservar `servers: /api/v1`.
- Los paths deben ser relativos.
- Los campos y respuestas deben usar una convención única.
- Los scopes OAuth2 deben declararse y corresponder a operaciones concretas.
- El `Idempotency-Key` debe ser obligatorio para todas las mutaciones transaccionales.
- La respuesta RFC 7807 debe ser uniforme.

### 2.2 Capa Application

**Estado:** definida como plan, no implementada.

La capa contiene casos de uso para catálogo, disponibilidad, reservas, usuarios, clientes, carrito, checkout, pedidos y pago. El plan define validadores, excepciones y servicios de negocio.

**Revisión:**

- Los commands y queries no están alineados con los contratos HTTP todavía.
- Debe existir una separación estricta entre DTOs HTTP y DTOs de Application.
- Los casos de uso deben emitir eventos de negocio y coordinar transacciones.
- La lógica de ownership debe entregarse a Application y no depender de un controlador.

### 2.3 Capa Domain

**Estado:** no está implementada.

La Layer Domain debe contener agregados, entidades, reglas de dominio y estado. El plan de Domain aún no se ha definido formalmente en la auditoría actual.

**Revisión pendiente:**

- Agregados de catálogo, disponibilidad y reserva.
- Agregados de usuario, cliente y carrito.
- Agregados de pedido, pago y checkout.
- Estados y eventos de dominio.
- Reglas de cancelación, pago, devolución y disponibilidad.

### 2.4 DataManagement

**Estado:** plan creado, sin implementación.

El plan define repositorios y Unit of Work, pero aún requiere que los contratos se revisen con Application y DataAccess.

**Hallazgo crítico:**

La dependencia debe ser:

```text
Application -> DataManagement
DataAccess -> DataManagement -> Domain
```

El diagrama actual de DataManagement puede interpretarse como que `DataManagement` depende de Application, lo cual es contrario al desacoplamiento funcional. Debe corregirse.

### 2.5 DataAccess

**Estado:** plan creado, sin implementación.

El plan define entidades, configuraciones, migraciones y seed. Debe implementar por separado catálogo, ecommerce, identidad y rastreo financiero.

**Hallazgo crítico:**

La capa DataAccess no puede decidir por sí sola la política de idempotencia ni otorgar permisos administrativos. Debe ser un adaptador del contrato de DataManagement.

### 2.6 Frontend

**Estado:** plan creado, sin implementación.

El frontend define las rutas del cliente y las páginas de checkout, perfil y pedidos. No contiene aún una estructura para administración.

## 3. Cohesión funcional

### 3.1 Flujo de catálogo

**Cobertura existente:**

- Búsqueda.
- Detalle.
- Listado paginado.
- Creación, actualización y eliminación de atracciones.
- Disponibilidad.
- Reservas.

**Pendiente:**

- Definir eliminación suave o física.
- Definir creación de recursos con proveedores externos.
- Definir restricciones de autorización de catálogo.
- Especificar el comportamiento cuando una atracción ya está reservada o en pedidos activos.

### 3.2 Flujo de disponibilidad

**Cobertura existente:**

- Fecha, horario, cupos y disponibilidad.
- Registro de inventario histórico.

**Pendiente:**

- Política de capacidad de reserva.
- Protección contra doble reserva.
- Límite de disponibilidad por horario.
- Actualización en bloque transaccional.
- Liberación de cupos en cancelación.

### 3.3 Flujo de reserva

**Cobertura existente:**

- Crear reserva.
- Consultar reserva.
- Cancelar reserva.
- Historial del cliente.
- Idempotencia.

**Pendiente:**

- Decidir si la reserva debe crear un pedido automáticamente.
- Definir el tiempo máximo de confirmación.
- Definir el vínculo con `checkout_sessions` y `orders`.
- Asegurar que `ReservationId` sea único e inmutable.
- Definir la política de expiración y cancelación parcial.

### 3.4 Flujo de checkout

**Cobertura existente:**

- Crear una sesión de checkout.
- Asociarla al carrito y cliente.
- Validar dinero y disponibilidad.

**Pendiente:**

- Confirmar pedido desde checkout.
- Verificar sesión no expirada.
- Impedir la creación de múltiples pedidos por la misma sesión.
- Definir expiración y reintento.
- Asegurar que el carrito se bloquee mientras la sesión está activa.

### 3.5 Flujo de pedido

**Cobertura existente:**

- Crear pedido.
- Consultar pedido.
- Consultar eventos.
- Cancelar pedido.

**Pendiente:**

- Estados completos y transiciones válidas.
- Reembolso o cancelación parcial.
- Reapertura del carrito después de una cancelación.
- Cambio de inventario y de disponibilidad.
- Creación de una reserva vinculada al pedido.
- Persistencia de eventos inmutables.

### 3.6 Flujo de pago simulado

**Cobertura existente:**

- Pago simulado.
- Intentos.
- Eventos.
- Estado del pedido.
- Sin números reales de tarjeta.

**Pendiente:**

- Definir estados exactos y transiciones.
- Definir reintentos y tiempo máximo.
- Definir una regla de rechazo.
- Definir qué ocurre si el pago se autoriza pero el pedido no se confirma.
- Definir pago parcial y cancelación.
- Precisa que `paymentMethodReference` solo contiene una referencia local.

### 3.7 Flujo administrativo

**Estado:** inexistente.

Se requieren endpoints para:

- Administrar usuarios administradores.
- Administrar roles.
- Administrar permisos.
- Administrar catálogo.
- Administrar disponibilidad e inventario.
- Administrar reservas.
- Administrar pedidos y pagos.
- Consultar métricas y reportes.
- Emitir eventos de resolución.

El `panel administrativo` no es solo una interfaz web; necesita endpoints y autorización específicos.

## 4. Idempotencia

### 4.1 Estado actual

La idempotencia está planificada para:

- Reservas.
- Cancelación de reservas.
- Pedidos.
- Pagos simulados.
- Cancelación de pedidos.

### 4.2 Brechas

El documento de correcciones define que `Idempotency-Key` debe existir, pero no declara una política uniforme con:

- Longitud mínima o máxima.
- Formato exacto.
- Tiempo de retención.
- Expiración.
- Comportamiento ante claves duplicadas.
- Identificador de recurso.
- Hash de request y response.
- Código de error acotado.
- Decisión sobre claves repetidas con payload distinto.

La política debe estandarizarse en el contrato OpenAPI y también en Application y DataAccess.

### 4.3 Reglas obligatorias propuestas

1. Cada `Idempotency-Key` pertenece a una operación y a un usuario o cliente.
2. Una clave duplicada con el mismo payload debe devolver la misma respuesta original.
3. Una clave duplicada con payload distinto debe producir `409 Conflict`.
4. Una clave con operación distinta debe producir `409 Conflict`.
5. El tiempo de retención debe ser configurado.
6. El backend debe registrar `request_hash` y `response_hash`.
7. Las operaciones recientes deben poder volver a consultar el resultado original.
8. `POST`, `PATCH`, `DELETE` con efecto transaccional deben llevar `Idempotency-Key`.
9. Las operaciones de consola o admin deben usar una política separada y ser auditable.

## 5. Seguridad y autorización

### 5.1 OAuth2

**Cobertura esperada:**

- Authorization Code con PKCE.
- Access token verificado.
- Identity provider como autoridad.
- Claims `sub` y `email`.

**Deficiencias:**

- Los scopes exactos aún deben definirse.
- No existe una política para roles y permisos administrativos.
- No se define la expiración de access token.
- No se define la política de refresh token.
- La API no tiene una lista oficial de claims autorizados.

### 5.2 Ownership

Los planes de API y Business refieren a ownership de carrito, pedido, pago y reserva. Deben reforzarse con:

- `user_id` no permitido en requests.
- `customer_id` derivado del usuario autenticado.
- Validación previa a acceso o escritura.
- Revisión de ownership en Application.
- Owner y admin tienen diferentes permisos.

### 5.3 Administradores

Se requieren roles explícitos:

- `admin` para gestión completa.
- `catalog_manager` para catálogo y disponibilidad.
- `order_manager` para pedidos y devoluciones.
- `support_manager` para reservas y atención al cliente.
- `finance_manager` para pagos y reportes.
- `customer` para operaciones del ecommerce.

No deben existir roles ambiguos ni botones administrativos visibles para clientes.

## 6. Contratos administrativos requeridos

### 6.1 Autenticación de administración

Debe existir un propósito específico para OAuth2 y un scope de administración, por ejemplo:

```text
admin:read
admin:write
admin:catalog
admin:orders
admin:payments
admin:customers
admin:reports
```

Una persona con un scope de catálogo no debe tener acceso a pago o reportes.

### 6.2 Catalogo administrativo

- `GET /admin/attractions`.
- `POST /admin/attractions`.
- `GET /admin/attractions/{id}`.
- `PUT /admin/attractions/{id}`.
- `PATCH /admin/attractions/{id}`.
- `DELETE /admin/attractions/{id}`.
- `GET /admin/attractions/{id}/availability`.
- `PUT /admin/attractions/{id}/availability`.

### 6.3 Inventario y disponibilidad

- `GET /admin/inventory`.
- `POST /admin/inventory/movements`.
- `POST /admin/availability`.
- `PATCH /admin/availability/{id}`.
- `GET /admin/availability/{id}/history`.

### 6.4 Reservaciones

- `GET /admin/reservations`.
- `GET /admin/reservations/{reservationId}`.
- `PATCH /admin/reservations/{reservationId}`.
- `POST /admin/reservations/{reservationId}/cancel`.

### 6.5 Pedidos y pagos

- `GET /admin/orders`.
- `GET /admin/orders/{orderId}`.
- `PATCH /admin/orders/{orderId}/status`.
- `GET /admin/payments`.
- `GET /admin/payments/{paymentId}`.
- `PATCH /admin/payments/{paymentId}/status`.

### 6.6 Clientes y usuarios

- `GET /admin/customers`.
- `GET /admin/customers/{customerId}`.
- `GET /admin/users`.
- `GET /admin/users/{userId}`.
- `PATCH /admin/users/{userId}/status`.

### 6.7 Reportes

- `GET /admin/reports/summary`.
- `GET /admin/reports/orders`.
- `GET /admin/reports/payments`.
- `GET /admin/reports/reservations`.
- `GET /admin/reports/customers`.

La API debe usar los mismos errores, paginación, ownership y idempotencia que las rutas cliente.

## 7. Funcionalidades completas requeridas

### 7.1 Catálogo

- Publicar atracciones.
- Filtrar, paginar y ordenar.
- Consultar disponibilidad.
- Administrar categorías y atributos.
- Gestionar imágenes y contenido.
- Desactivar recursos sin eliminarlos.

### 7.2 Reservas

- Crear y consultar.
- Asociar a un cliente y usuario.
- Validar stock y horario.
- Cancelar correctamente.
- Recuperar disponibilidad.
- Operar históricamente.

### 7.3 Carrito

- Carrito activo único.
- Items con precio guardado.
- Actualización de cantidades.
- Validación real en checkout.
- Persistencia y expiración.

### 7.4 Checkout

- Sesión activa.
- Bloqueos de disponibilidad.
- Pedido único por sesión.
- Comprobación de identidad y ownership.
- Pago simulado.
- Confirmación y historial.

### 7.5 Pedidos

- Crear.
- Consultar.
- Historial.
- Cancelar.
- Actualizar estado.
- Reembolsos parcial o total.
- Eventos inmutables.

### 7.6 Pagos simulado

- Estado inicial y transiciones.
- Intentos secuenciales.
- Eventos inmutables.
- Resultado sin datos reales.
- Reintento de pago.
- Manejo de rechazo y fallo.

### 7.7 Administracion

- Login administrativo.
- Roles y permisos.
- Dashboard.
- Administración de catálogo.
- Administración de inventario.
- Atención de reservas y pedidos.
- Revisión de pagos.
- Reportes.
- Auditoría de operaciones.

## 8. Contratos que faltan

### API

- `GET /admin/attractions`.
- `POST /admin/attractions`.
- `GET /admin/orders`.
- `PATCH /admin/orders/{orderId}/status`.
- `GET /admin/payments`.
- `GET /admin/reports/*`.
- `GET /admin/reservations`.
- `PATCH /admin/reservations/{reservationId}`.
- `GET /admin/customers`.
- `GET /admin/users`.
- `PATCH /admin/users/{userId}/status`.

### Business

- Servicios de administración.
- Validadores de roles y permisos.
- Casos de uso de catálogo administrativo.
- Casos de uso de reservas y pedidos administrativos.
- Casos de uso de reportes.
- Casos de pago y devolución.

### DataManagement

- Repositorios para usuarios administrativos.
- Repositorios para reportes.
- Repositorio de permisos y roles.
- Repositorio de auditoría.
- Repositorio de inventario.
- Repositorio de pagos y devoluciones.

### Frontend

- Dashboard administrativo.
- Gestión de catálogo.
- Gestión de usuarios, roles y permisos.
- Gestión de reservas y pedidos.
- Gestión de inventario.
- Dashboard y reportes.

## 9. Decisiones pendientes

1. Definir el modelo de cancelación y devolución.
2. Definir el tiempo de expiración de checkout, reserva y sesión.
3. Definir los estados exactos de pedido y pago.
4. Confirmar si una reserva debe crear un pedido automáticamente.
5. Definir la política de tarifa protegida y de precio congelado.
6. Definir el número de reintentos de pago.
7. Definir los permisos administrativos y su jerarquía.
8. Definir el modelo de usuarios administrativos.
9. Definir una política de auditoría de cambios.
10. Definir el alcance inicial de reportes.
11. Definir si los reportes serán API o agregados en el frontend.
12. Definir cómo se originarán los datos de catálogo para la primera versión.

## 10. Riesgos de implementación

- Crear pedidos sin checkout válido.
- Duplicar reservas o pedidos con idempotencia incompleta.
- Permitir que un usuario modifique un carrito ajeno.
- Permitir que un administrador altere atributos del sistema sin audit.
- Almacenar datos bancarios reales.
- Hacer que las rutas admin sean públicas por error.
- Generar el pedido antes de validar disponibilidad.
- Actualizar disponibilidad sin transacción.
- Emitir eventos de pago sin registrarlos en la misma unidad de trabajo.
- Inconsistencias entre la API, Application y base de datos.

## 11. Resultado de la auditoría

### Cumplido

- Se mantiene una separación clara de responsabilidades.
- La API tiene una intención de ser API-first.
- La idempotencia está incluida como requisito.
- El catálogo, disponibilidad, reservas y ecommerce básico están definidos.
- Los datos sensibles de pago se mantienen fuera del contrato de entrada.
- La separación de Application, DataManagement y DataAccess está planteada.

### No cumplido

- El commerce completo no puede afirmarse todavía.
- La administración no existe en ninguna capa.
- La dependencia de DataManagement respecto a Application es inconsistente.
- La idempotencia no está formalizada con política completa.
- El contrato no puede garantizar transacciones entre catálogo, checkout, pedidos y pagos.
- El flujo de devolución y reembolso está ausente.
- El frontend no contiene permisos ni panel administrativo.
- La API no define todavía una ruta administrativa ni sus scopes.

## 12. Recomendación final

Antes de implementar cualquier bloque, se deben cerrar estas reglas:

1. Publicar un OpenAPI único con los endpoints cliente y administrativo.
2. Definir scopes y roles OAuth2.
3. Formalizar la política completa de `Idempotency-Key`.
4. Definir estados y eventos de pago, pedido, reserva, checkout y disponibilidad.
5. Definir el modelo de cancelación, devolución y reembolso.
6. Definir una API administrativa completa.
7. Corregir las dependencias entre Application, DataManagement y DataAccess.
8. Añadir contratos y pruebas de verificaciones de ownership.
9. Definir una estrategia de administración segura y auditable.
10. Iniciar la implementación únicamente después de que el contrato sea consistente con las capas Business, DataManagement, DataAccess y frontend.

## 13. Nivel de riesgo

**Riesgo global:** alto.

La arquitectura base es apropiada, pero el alcance actual todavía está incompleto para afirmar que el commerce será completamente funcional. Los cambios principales deben centrarse en contratos unificados, roles administrativos, estados del negocio, idempotencia y transacciones atomicamente coordinadas.
