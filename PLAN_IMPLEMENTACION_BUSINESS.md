# Plan de implementación de la capa Business (Application)

> Este documento define la estructura, responsabilidades, contratos, casos de uso y pruebas de la capa Business. No modifica `CONTRATO.md` ni implementa código.

## 1. Objetivo

La capa Business coordina la lógica de aplicación y la regla del negocio de atracciones y ecommerce. Debe actuar como una capa independiente entre:

- `AtraccionesService.API`: transporte HTTP, autenticación, autorización y respuestas.
- `AtraccionesService.Domain`: entidades, reglas de dominio y excepciones propias del negocio.
- `AtraccionesService.DataManagment`: contratos de repositorio y transacciones usados por Application.
- `AtraccionesService.DataAcess`: implementación concreta de persistencia; solo la API la registra en el composition root.
- `AtraccionesService.Contracts`: contratos HTTP compartidos por API; API los mapea a comandos y consultas propios de Application.

## 2. Criterios arquitectónicos

1. La capa Application no debe depender directamente de ASP.NET Core.
2. La capa Application no debe contener SQL, consultas ORM ni detalles de infraestructura.
3. La capa Domain debe contener reglas que no dependan de HTTP, Persistence ni proveedores externos.
4. `DataManagement` define los contratos de persistencia; `DataAccess` los implementa y puede depender de Domain, pero Application no depende de DataAccess.
5. Los controladores de API deben transformar HTTP en comandos y consultas; no implementar reglas de negocio.
6. Los casos de uso aceptan comandos/consultas propios de Application y devuelven resultados tipados. API mapea los DTOs de Contracts, evitando que Application dependa de Contracts.
7. Cada operación debe registrar los efectos de negocio mediante un resultado explícito.
8. Los cambios de estado deben ser atómicos dentro de una unidad de trabajo y deben protegerse contra duplicados o condiciones de carrera.
9. Los datos financieros y de pago deben mantenerse simulados; no deben almacenarse ni exponerse números de tarjeta completos.
10. Las reglas de ownership se deben evaluar con la identidad autenticada y el identificador de cliente, no con un valor recibido sin validar.

## 3. Estructura propuesta

```text
AtraccionesService.Application/
├── Abstractions/
│   ├── Ecommerce/
│   │   ├── IPaymentSimulationPolicy.cs
│   │   └── IReportQueryService.cs
│   ├── Idempotency/
│   │   └── IIdempotencyService.cs
│   └── Authorization/
│       └── ICurrentUserService.cs
├── Commands/
│   ├── Attractions/
│   │   ├── CreateAttractionCommand.cs
│   │   ├── UpdateAttractionCommand.cs
│   │   └── DeleteAttractionCommand.cs
│   ├── Reservations/
│   │   ├── CreateReservationCommand.cs
│   │   └── CancelReservationCommand.cs
│   ├── Customers/
│   │   └── UpdateCustomerCommand.cs
│   ├── Purchases/
│   │   └── CreatePurchaseCommand.cs
│   ├── Orders/
│   │   ├── CreateOrderCommand.cs
│   │   └── CancelOrderCommand.cs
│   ├── Payments/
│       └── SimulatePaymentCommand.cs
│   └── Administration/
│   │   ├── AdjustAvailabilityCommand.cs
│   │   ├── ChangeOrderStatusCommand.cs
│   │   ├── AssignUserRoleCommand.cs
│   │   └── SimulateRefundCommand.cs
├── Queries/
│   ├── Attractions/
│   │   ├── SearchAttractionsQuery.cs
│   │   ├── GetAttractionQuery.cs
│   │   └── GetAttractionAvailabilityQuery.cs
│   ├── Reservations/
│   │   ├── GetReservationsQuery.cs
│   │   └── GetReservationQuery.cs
│   ├── Customers/
│   │   └── GetCustomerQuery.cs
│   ├── Purchases/
│   │   └── GetPurchaseQuery.cs
│   ├── Orders/
│   │   ├── GetOrderQuery.cs
│   │   └── GetOrderEventsQuery.cs
│   ├── Payments/
│       └── GetPaymentSimulationQuery.cs
│   └── Administration/
│       ├── GetAdminDashboardQuery.cs
│       ├── SearchAdminOrdersQuery.cs
│       ├── SearchAdminCustomersQuery.cs
│       └── GetAdminReportsQuery.cs
├── Services/
│   ├── Catalog/
│   │   ├── AttractionService.cs
│   │   ├── AvailabilityService.cs
│   │   └── ReservationService.cs
│   ├── Identity/
│   │   ├── UserProfileService.cs
│   │   └── CustomerService.cs
│   ├── Ecommerce/
│   │   ├── PurchaseService.cs
│   │   ├── OrderService.cs
│   │   └── PaymentSimulationService.cs
│   ├── Administration/
│   │   ├── AdminCatalogService.cs
│   │   ├── AdminOrderService.cs
│   │   ├── AdminUserService.cs
│   │   ├── AdminInventoryService.cs
│   │   └── AdminReportService.cs
│   └── Shared/
│       ├── IdempotencyService.cs
│       ├── PriceCalculator.cs
│       └── AvailabilityPolicyService.cs
├── Validators/
│   ├── AttractionValidator.cs
│   ├── ReservationValidator.cs
│   ├── PurchaseValidator.cs
│   ├── OrderValidator.cs
│   └── PaymentSimulationValidator.cs
├── Mappers/
│   ├── AttractionMapper.cs
│   ├── ReservationMapper.cs
│   ├── PurchaseMapper.cs
│   ├── OrderMapper.cs
│   └── PaymentMapper.cs
├── Exceptions/
│   ├── BusinessException.cs
│   ├── NotFoundException.cs
│   ├── ValidationException.cs
│   ├── ConflictException.cs
│   ├── UnauthorizedException.cs
│   ├── ForbiddenException.cs
│   └── PaymentSimulationException.cs
├── ResultModels/
│   ├── OperationResult.cs
│   └── PaginationResult.cs
├── DependencyInjection/
│   └── ApplicationServiceCollectionExtensions.cs
└── AtraccionesService.Application.csproj
```

### Recomendación de nombres

El proyecto actual se llama `AtraccionesService.Application`, pero la solución existente usa `Atracciones.Application`. Para evitar un cambio prematuro, se conservarán ambos nombres únicamente si forman parte de la misma solución; si la estructura actual será la implementación definitiva, se debe unificar el proyecto y el namespace antes de crear servicios concretos.

## 4. Dependencias de la capa

```text
API
  -> Application
            -> Domain
            -> DataManagement

DataAccess
    -> DataManagement
    -> Domain
    -> Entity Framework / proveedor de persistencia
```

`Contracts` es consumido por API y no es una dependencia de Application. Application no registra ni referencia DataAccess. El composition root de API registra Application y DataAccess. Los contratos de repositorio y Unit of Work viven en DataManagement, no duplicados en Application.

## 5. Casos de uso y responsabilidades

### 5.1 Catálogo

#### SearchAttractions

- Recibe un query de Application; API convierte `SearchAtraccionesRequest` a ese query.
- Valida idioma, moneda, fechas, límites y ordenamiento.
- Consulta ofertas disponibles mediante un repositorio de catálogo.
- Optimiza la relación entre catálogo, ubicaciones, imagenes y atributos.
- Devuelve una colección paginada con el DTO de respuesta.
- No debe realizar conversión HTTP ni preparar QueryString.

#### GetAttraction

- Busca una atracción por identificador.
- Valida que el recurso exista.
- Obtiene los datos relacionados necesarios para crear el DTO.
- Aplica filtrado de idiomas y propiedades opcionales.
- Devuelve el recurso completo o un error `NotFoundException`.

#### CreateAttraction

- Valida los campos obligatorios.
- Verifica que el tipo de producto, moneda y precio sean compatibles.
- Asegura que categorías, ubicaciones y idiomas sean válidos.
- Crea la entidad de dominio y delega la persistencia a la unidad de trabajo.
- Retorna el identificador y el recurso creado.

#### UpdateAttraction y PatchAttraction

- Aplican validación para campos requeridos y valores nulos.
- Actualizan únicamente los campos permitidos en el contrato.
- Reemplazan relaciones de catálogo cuando es necesario.
- Evitan cambios parciales inconsistentes.

#### DeleteAttraction

- Verifica que la atracción exista.
- Elimina sus dependencias mediante una transacción.
- No debe eliminar datos de reservas que hagan referencia al recurso si la política de negocio así lo exige.
- Define explícitamente el comportamiento de eliminación: lógica física, lógica borrado o bloqueado.

### 5.2 Disponibilidad

#### GetAvailability

- Valida fecha, zona horaria y parámetro de atracción.
- Consulta disponibilidad con su política de capacidad.
- Calcula `availableSpots` y `times`.
- Indicará fechas ya reservadas o fuera de operación únicamente mediante los datos autorizados por el contrato.

#### ReserveSpot

- Valida que la fecha, hora y cantidad sean correctos.
- Comprueba la disponibilidad en el mismo contexto transaccional.
- Crea una reserva y conserva el total calculado.
- Registra la operación bajo una clave de idempotencia.
- Evita condiciones de carrera mediante bloqueo o versión optimista.

#### CancelReservation

- Valida propiedad y estado de la reserva.
- Garantiza que la reserva no quede cancelable dos veces.
- Registra la razón de cancelación y el evento asociado.
- Actualiza la disponibilidad o libera el bloque temporal requerido por el negocio.

### 5.3 Usuarios y clientes

#### RegisterAuthenticatedUser

- Recibe `sub`, `email` y otros claims verificados por API.
- Comprueba si el usuario ya existe.
- Crea el usuario y el cliente asociado en una transacción.
- Evita crear un cliente duplicado.
- No debe almacenar contraseña, token bearer ni credenciales OAuth2.

#### GetCurrentUser

- Obtiene la identidad resoluble desde el contexto actual.
- Devuelve el perfil público y sus relaciones con el cliente.

#### GetCurrentCustomer

- Consulta el cliente asociado al usuario autenticado.
- Aplica ownership y evita la entrada de un `customerId` ajeno.

#### UpdateCustomer

- valida nombre, documento, correo y dirección de facturación.
- Escribe únicamente los campos permitidos.
- No permite modificar campos de factura no definidos.

### 5.4 Compra directa

#### CreatePurchase

- Valida que el usuario autenticado pertenece al cliente y que la atracción existe.
- Comprueba la disponibilidad concreta de la fecha, horario y cantidad solicitadas.
- Recalcula y confirma el precio en el servidor antes de crear el pedido.
- Recibe el método de pago simulado sin aceptar datos bancarios.
- Crea un único pedido `PENDING_PAYMENT` o `PAID` y el registro correspondiente de pago.
- Usa una sola transacción local para validar disponibilidad, generar el pedido y registrar el resultado de pago.
- No mantiene una retención independiente del pedido ni una sesión de checkout.

#### GetPurchase

- Verifica la pertenencia del pedido y devuelve su estado, items, pago y eventos.
- No expone eventos o estados internos de pasarela a clientes no autorizados.

### 5.5 Pedidos

#### CreateOrder

- Verifica que la compra directa sea válida y que el precio se haya confirmado.
- Crea un único pedido `PENDING_PAYMENT` con snapshot de precios y slots seleccionados.
- Asigna estado inicial, moneda, total y la reserva asociada cuando exista.
- Garantiza que no existan pedidos duplicados mediante restricción única y la misma clave de idempotencia.

#### GetOrder

- Verifica pertenencia del pedido
- Devuelve el pedido completo con sus items y estado.

#### GetOrderEvents

- Consulta los eventos internos del pedido.
- Limita resultados mediante paginación.
- No es un endpoint público de aplicación.

#### CancelOrder

- Valida estado de cancelación.
- Carga la política de reembolso y disponibilidad.
- Registra la cancelación en el pedido y sus eventos.

### 5.7 Pagos simulados

#### SimulatePayment

- Recibe una operación de pago y un `Idempotency-Key`.
- Valida el pedido, el cliente, el importe y el método de pago.
- Genera el intento de pago interno usando un determinista simulador.
- Registra el resultado y el evento correspondiente.
- Actualiza el estado del pedido como resultado de la simulación.
- Actualiza el estado del pedido como resultado de la simulación. El rechazo/fallo deja el pedido `PENDING_PAYMENT` mientras haya reintentos y libera cupos solo al expirar/cancelar.
- No acepta ni devuelve datos bancarios reales.
- Debe ser un caso de uso de Application que API expone de forma segura.
- El primer request crea la simulación y procesa un intento determinista. Reintentos posteriores son nuevos requests idempotentes, nunca alteran intentos anteriores.
- El fallo de intento no termina el pedido mientras queden reintentos; al éxito `SETTLED` actualiza el pedido y confirma disponibilidad.

### 5.9 Estados y transacciones

- Compra directa: una solicitud valida disponibilidad, confirma precio y crea pedido/pago en una sola operación transaccional. No existe un estado intermedio de checkout.
- Pedido: `PENDING_PAYMENT -> PAID | CANCELLED`; `PAID -> FULFILLED | CANCELLED | PARTIALLY_REFUNDED | REFUNDED`; `PARTIALLY_REFUNDED -> PARTIALLY_REFUNDED | REFUNDED`.
- Pago: `PENDING -> AUTHORIZED | REJECTED | FAILED`; `AUTHORIZED -> SETTLED | CANCELLED | FAILED`; `SETTLED -> PARTIALLY_REFUNDED | REFUNDED`. Solo se permiten reintentos dentro de la política configurada.
- Cada request transaccional confirma o revierte sus propios cambios en una unidad de trabajo; no se mantiene una transacción de base de datos abierta entre requests HTTP.
- La validación de disponibilidad, el snapshot de precios y la creación del pedido deben ocurrir en una sola transacción. Simular pago es otra transacción atómica que actualiza pago, pedido y disponibilidad.
- Cancelación y reembolso simulado son casos de uso distintos; el reembolso requiere permiso financiero/admin, motivo, idempotencia y auditoría.
- El `POST /atracciones/{id}/reservations` existente se conserva como operación de reserva independiente y no se convierte automáticamente en compra.

### 5.8 Administración

Los casos administrativos solo se ejecutan para una identidad autenticada con permisos locales vigentes y scopes OAuth2 acordes. La autenticación y la emisión de tokens siguen perteneciendo al issuer. Los roles locales no sustituyen la validación de issuer, audiencia, expiración ni scopes.

- `AdminCatalogService`: alta, edición, publicación/desactivación y consulta del catálogo.
- `AdminInventoryService`: ajustes auditados de capacidad y disponibilidad.
- `AdminOrderService`: búsqueda, cambios de estado permitidos, cancelación y simulación de reembolsos.
- `AdminUserService`: activar/desactivar perfiles locales y asignar/quitar roles locales; no administra contraseñas ni cuentas del issuer.
- `AdminReportService`: métricas agregadas y paginadas; no expone datos de pago sensibles.
- Cada cambio administrativo verifica permiso en Application, aplica ownership cuando corresponde y registra actor, acción, recurso, motivo y fecha en auditoría.
- Ningún request permite establecer libremente estados finales: Application valida cada transición.

## 6. Contratos de Application

### 6.1 Commands

Los comandos deben contener solamente datos mínimos para ejecutar una operación y ser tipos inmutables propios de Application. No dependen de contratos HTTP de `Contracts`.

Ejemplo conceptual:

```csharp
public sealed record CreateReservationCommand(
    Guid AttractionId,
    DateOnly Date,
    string Time,
    int TicketCount,
    string IdempotencyKey,
    string Issuer,
    string? UserSubject);
```

### 6.2 Queries

Las consultas deben recibir solo los parámetros requeridos por la operación, por ejemplo:

```csharp
public sealed record GetOrderQuery(
    Guid OrderId,
    string Issuer,
    string UserSubject);
```

### 6.3 Resultado de operación

El resultado debe no depender de HTTP. Puede incluir:

- recurso creado o actualizado;
- estado de la operación;
- código o identificador de transacción interno;
- evento generado;
- metadatos de idempotencia;
- indicadores de consulta o paginación.

## 7. Interfaces de Infrastructure

Application consume contratos definidos en `DataManagement` para:

- Repositorios de catálogo, disponibilidad, reservas, clientes, usuarios, compras directas, pedidos, pagos e idempotencia.
- Operaciones transaccionales mediante `IUnitOfWork` de DataManagement.
- Registro de eventos de dominio.
- Simulación de pagos.
- Autorización de aplicación a través de un contexto de usuario ya validado y pasado por la API; Application no accede a `HttpContext` ni valida JWT.

Ejemplo de interfaz:

```csharp
public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken);
    Task<Reservation?> GetByIdForCustomerAsync(Guid reservationId, Guid customerId, CancellationToken cancellationToken);
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);
    Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken);
}
```

Se debe definir una interfaz por agregación de responsabilidad, no un repositorio genérico que contenga todos los casos de uso.

## 8. Idempotencia

La regla obligatoria se aplicará en Application.

### Identidad de la operación

Cada mutación transaccional recibirá `Idempotency-Key` y una operación definida, por ejemplo:

- `create-reservation`
- `cancel-reservation`
- `create-order`
- `simulate-payment`

### Comportamiento

1. La identidad compuesta es `issuer + subject + operation + key`; issuer/subject proceden del token validado y nunca del body.
2. Application calcula un hash del payload canónico y solicita el registro atómico a `IIdempotencyRepository` de DataManagement.
3. La primera solicitud registra `IN_PROGRESS`; la respuesta y sus metadatos se completan en la misma transacción de negocio.
4. Una repetición completada con la misma identidad y hash devuelve el mismo resultado lógico y código HTTP original, sin repetir efectos.
5. La misma clave con hash distinto produce `409 Conflict` con tipo estable `IDEMPOTENCY_KEY_REUSED`; una repetición aún en proceso produce un conflicto estable `IDEMPOTENCY_IN_PROGRESS`.
6. El TTL es configurable y común a API, Application y DataAccess; una clave expirada puede volver a utilizarse únicamente después de su limpieza segura.
7. Se aplica a toda mutación con efectos: reservas, carrito, checkout, pedidos, pagos, cancelaciones y mutaciones administrativas. GET no requiere clave.
8. La capa API valida presencia/formato y Application/DataAccess garantizan semántica, concurrencia y replay.

## 9. Reglas de ownership

La lógica de ownership deberá ejecutarse con los identificadores aceptados del contexto autenticado:

- `UserSubject` para identidad OAuth2.
- `CustomerId` obtenido de la relación usuario-cliente.
- `ReservationId`, `OrderId`, `PurchaseId`, `PaymentId` como recursos individuales.

Nunca se aceptará un `userId`, `customerId` o `reservationId` como fuente de autorización sin comprobar su relación con la identidad actual.

## 10. Excepciones de Application

| Excepción | Código HTTP sugerido | Uso |
|---|---:|---|
| `ValidationException` | 400 | Datos inválidos o faltantes |
| `NotFoundException` | 404 | Recurso inexistente |
| `ForbiddenException` | 403 | Usuario autenticado sin permiso |
| `UnauthorizedException` | 401 | Identidad ausente o inválida |
| `ConflictException` | 409 | Estado, operación o idempotencia incompatible |
| `PaymentSimulationException` | 422 o 409 | Pago rechazado o inválido |
| `BusinessException` | 500 o 409 | Error general de dominio |

La clase `BusinessException` debe ser la base; no conviene que las excepciones que no son de negocio se propaguen a la API.

## 11. Mapeo entre DTOs y entidades

API mapea sus DTOs de `Contracts` a Commands/Queries de Application. Application mapea Commands/Queries a Domain; DataAccess mapea Domain y modelos de lectura. El mapeo HTTP nunca se coloca en Application. El mapeo de dominio incluye:

- `CreateAttractionRequest` y `Attraction`.
- `CreateAttractionCommand` / `UpdateAttractionCommand` y el agregado `Attraction`.
- `CreateReservationCommand` y el agregado `Reservation`, resolviendo el cliente a partir del contexto autenticado.
- `CreatePurchaseCommand` y el agregado `Purchase`.
- `CreateOrderCommand` y los agregados `Order` y `OrderItem`.
- `SimulatePaymentCommand` y el agregado `PaymentSimulation`.

API es responsable del mapeo entre requests/responses HTTP y Commands/Queries. DataAccess es responsable del mapeo entre Domain y entidades EF Core.

No se deben devolver entidades de Infrastructure directamente a la API. Tampoco se deben usar DTOs HTTP dentro de entidades de dominio.

## 12. Mecanismo de validación

La validación puede implementarse mediante:

1. Validadores dedicados por dominio.
2. Regla de dominio en entidades.
3. Validación de contexto o caso de uso.
4. Validación de contratos HTTP en API para requisitos de transporte.

Debe existir una separación clara:

- API: tipos, campos, headers y formato HTTP.
- Application: reglas de negocio y estados.
- Domain: invariantes del agregado.
- Infrastructure: validación de persistencia y proveedores externos.

## 13. Patrones sugeridos

### 13.1 Handler

Cada caso de uso puede implementarse como un servicio `Handler`, por ejemplo:

```text
CreateReservationCommandHandler
GetAttractionQueryHandler
SimulatePaymentCommandHandler
```

Esto permite separar casos de uso y facilitar pruebas unitarias.

### 13.2 Service

Los servicios coordinan operaciones complejas y pueden tener una interfaz dedicada, por ejemplo:

```text
IAvailabilityService
IPriceCalculator
IIdempotencyService
IPaymentSimulationService
```

### 13.3 Repository

Cada agregación puede tener sus propios repositorios y métodos de consulta. El repositorio debe devolver entidades o DTOs de dominio, no objetos de infraestructura.

### 13.4 Unit of Work

La unidad de trabajo agrupará:

- guardado de cambios;
- confirmación o reversión de la transacción;
- persistencia de eventos y respuesta idempotente en la misma unidad de trabajo.

La publicación externa de mensajes queda fuera de esta unidad local; si se incorpora, se usará outbox.

## 14. Fases de implementación

### Fase 1: Fundamentos y estructura

1. Crear las carpetas de `Abstractions`, `Commands`, `Queries`, `Services`, `Validators`, `Mappers`, `Exceptions`, `ResultModels` y `DependencyInjection`.
2. Crear el proyecto `AtraccionesService.Application` y establecer referencias únicamente a Domain y DataManagement.
3. Definir `ApplicationServiceCollectionExtensions`; DataAccess se registra únicamente desde el composition root de API.
4. Crear excepciones base y un resultado común.
5. Añadir pruebas iniciales para carga de DI y casos de uso simples.

### Fase 2: Dominio y catalogo

1. Crear las entidades de catálogo y sus reglas.
2. Implementar validador de nombre, precio, categoría, ubicación y tipo de producto.
3. Implementar servicio de búsqueda.
4. Implementar servicio de detalle y disponibilidad.
5. Definir interfaces de repositorio para catálogo y disponibilidad.
6. Implementar pruebas unitarias de validaciones y cálculo de disponibilidad.

### Fase 3: Reservas y disponibilidad

1. Crear la entidad de reserva y su estado.
2. Implementar reglas de creación, cancelación y propiedad.
3. Integrar validación de disponibilidad y capacidad.
4. Implementar idempotencia para las reservas y cancelación.
5. Asegurar ejecución transaccional de reserva y disponibilidad.
6. Probar reservas duplicadas, cupos insuficientes y estados inválidos.

### Fase 4: Usuarios y clientes

1. Crear el servicio de registro autenticado.
2. Definir el servicio de consulta de perfil.
3. Implementar la relación usuario-cliente.
4. Definir las operaciones de actualización de facturación.
5. Probar creación, actualización y acceso no autorizado.

### Fase 5: Compra directa

1. Crear el caso de uso `CreatePurchaseCommand` y su validador.
2. Validar disponibilidad, cantidad, precio y método de pago.
3. Generar el pedido y sus items desde la selección directa.
4. Completar pago simulado y actualizar disponibilidad en una transacción.
5. Probar que solo el cliente propietario pueda solicitar y consultar la compra.

### Fase 6: Pedidos y pagos simulados

1. Implementar generación de pedidos desde la compra directa.
2. Crear el servicio de pagos simulados.
3. Registrar eventos internos del pedido.
4. Coordinar actualización de estado y saldo disponible.
5. Implementar idempotencia de pagos.
6. Probar estados exitosos, rechazados y repetidos.

### Fase 7: Integración y calidad

1. Completar el registro de servicios en ApplicationServiceCollectionExtensions.
2. Asegurar que los servicios no dependan de HTTP.
3. Ejecutar pruebas unitarias de Application y pruebas de integración con la implementación DataAccess.
4. Revisar cobertura de casos de negocio y de idempotencia.
5. Verificar que la API delega correctamente en Application.

## 15. Pruebas de la capa Business

### Pruebas unitarias

- Validación de catálogo y precios.
- Cambio de estados de reserva.
- Cancelación de reservas en estados inválidos.
- Cálculo de cantidades y totales.
- Ownership del cliente y usuario.
- Idempotencia con misma y distinta clave.
- Pago simulado con éxito y rechazo.
- Disponibilidad y límites de capacidad.

### Pruebas de integración

- Crear una reserva y persistirla en una base de datos en memoria o test container.
- Repetir una operación con la misma idempotencia y verificar una sola persistencia.
- Crear una compra directa con fecha, horario, cantidad y precio válidos.
- Crear un pedido desde la compra directa y generar eventos.
- Simular pago y comprobar el cambio de estado del pedido.
- Intentar acceder a una compra o pedido de otro cliente.

### Requisitos de pruebas

- No se deben usar mocks para probar la lógica interna de un servicio.
- Los repositorios y la unidad de trabajo deben probarse con almacenamiento real o infraestructura de prueba equivalente.
- Los tests de API deben validar solo el resultado HTTP observable, mientras que los tests de Application validan la regla de negocio.
- Al ejecutar pruebas seleccionadas, se debe registrar el resultado esperado y cualquier condición especial de entorno.

## 16. Criterios de aceptación de la capa Business

La capa Business estará lista cuando:

1. Todos los casos de uso esperados están definidos en commands y queries.
2. No existe lógica HTTP en Application.
3. No existe lógica SQL o ORM en Application.
4. Las reglas de negocio están en Domain o Services de Application.
5. Los repositorios se consumen mediante interfaces de DataManagement y se implementan en DataAccess.
6. Las mutaciones transaccionales se ejecutan con el `IUnitOfWork` definido por DataManagement.
7. La idempotencia controla duplicados y payload distinto.
8. La propiedad/ownership se aplica con la identidad validada entregada por API y el cliente resuelto en Application.
9. Los pagos permanecen simulados y no almacenan datos bancarios sensibles.
10. Las excepciones se traducen a respuestas HTTP coherentes.
11. El código se prueba sin depender de la API ni de infraestructura de producción.
12. La cobertura cubre criterios normales, inválidos, duplicados, propiedad y errores de disponibilidad.
13. Los casos administrativos comprueban scopes, permisos locales, transición permitida y auditoría.

## 17. Orden recomendado de desarrollo

1. Creación de estructura y excepciones base.
2. Contratos de Commands/Queries y servicios de identidad.
3. Reglas de catálogo y disponibilidad.
4. Reglas de reservas.
5. Reglas de usuarios y clientes.
6. Reglas de compra directa.
7. Reglas de pedidos y pagos.
8. Implementación de repositorios e idempotencia en DataAccess.
9. Pruebas unitarias y de integración de cada agregado.
10. Integración con API y validación final.

## 18. Decisiones pendientes

Antes de implementar exactamente los servicios definitivos, se deben confirmar:

1. Política de eliminación de atracciones y reservas.
2. Política de disponibilidad, retención de cupos y liberación.
3. Moneda soportada y reglas de precio/tasa de cambio.
4. Tiempo de retención de idempotencia.
5. Uso de `404` o `403` para recursos ajenos.
6. Número máximo y política de reintentos de pago simulado.
7. Contrato para asociar fechas/horarios y cantidades a items del carrito/pedido; sin esos datos el checkout no puede generar reservas temporales por horario.
8. Aprobación de endpoints, schemas y scopes de administración en el documento de correcciones.

## 19. Estado actual

El proyecto `AtraccionesService.Application` existe, pero contiene únicamente `Class1.cs` y una referencia mínima. La capa Business aún no tiene casos de uso, servicios, repositorios, DTOs de aplicación, excepciones ni pruebas.

El trabajo siguiente recomendado es:

1. Definir la clase base de excepciones.
2. Crear la primera suite de tests: catálogo y validación.
3. Acordar primero los contratos faltantes de disponibilidad y administración.
4. Implementar casos de uso consumiendo repositorios de DataManagement.
5. Conectar Application con DataAccess desde el composition root de API.
