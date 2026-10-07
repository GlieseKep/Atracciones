# Plan de implementación de DataAccess

> Este documento define la implementación de persistencia de `AtraccionesService.DataAcess`, que implementa los contratos de `AtraccionesService.DataManagment`. No modifica `CONTRATO.md` ni implementa código.

## 1. Objetivo

La capa DataAccess debe proporcionar una implementación persistente, versionable y testeable del modelo de negocio definido en `PLAN_IMPLEMENTACION_BASE_DE_DATOS.md`.

La referencia del repositorio externo se utiliza únicamente para orientar la arquitectura de:

- Entity Framework Core.
- Contexto de base de datos.
- Entidades de persistencia.
- Repositorios.
- Unit of Work.
- Seed de datos.
- Organización de proyectos.

No se copiarán sus entidades, reglas de negocio, datos de vuelos, entidades de pasarela ni valores específicos de Aerocache.

## 2. Arquitectura propuesta

```text
AtraccionesService.DataAcess/
├── Context/
│   └── AtraccionesDbContext.cs
├── Entities/
│   ├── Catalog/
│   │   ├── Attraction.cs
│   │   ├── Category.cs
│   │   ├── Badge.cs
│   │   ├── Inclusion.cs
│   │   ├── Language.cs
│   │   ├── Location.cs
│   │   ├── Operator.cs
│   │   ├── Photo.cs
│   │   ├── Price.cs
│   │   ├── Rating.cs
│   │   └── UrlInfo.cs
│   ├── Availability/
│   │   └── AttractionAvailability.cs
│   ├── Reservations/
│   │   └── Reservation.cs
│   ├── Identity/
│   │   ├── User.cs
│   │   ├── Customer.cs
│   │   ├── Role.cs
│   │   └── UserRole.cs
│   ├── Ecommerce/
│   │   ├── Purchase.cs
│   │   ├── Order.cs
│   │   ├── OrderItem.cs
│   │   ├── OrderEvent.cs
│   │   ├── PaymentSimulation.cs
│   │   ├── PaymentAttempt.cs
│   │   ├── PaymentEvent.cs
│   │   ├── RefundSimulation.cs
│   │   ├── AuditEvent.cs
│   │   └── InventoryMovement.cs
│   └── Technical/
│       └── IdempotencyKey.cs
├── Configurations/
│   ├── Catalog/
│   ├── Availability/
│   ├── Reservations/
│   ├── Identity/
│   ├── Administration/
│   └── Ecommerce/
├── Migrations/
├── Seed/
│   ├── CatalogSeeder.cs
│   ├── EcommerceSeeder.cs
│   └── DatabaseSeeder.cs
├── Repositories/
│   ├── Catalog/
│   │   ├── AttractionRepository.cs
│   │   ├── AvailabilityRepository.cs
│   │   └── ReservationRepository.cs
│   ├── Identity/
│   │   ├── UserRepository.cs
│   │   └── CustomerRepository.cs
│   ├── Ecommerce/
│   │   ├── PurchaseRepository.cs
│   │   ├── OrderRepository.cs
│   │   ├── PaymentRepository.cs
│   │   └── IdempotencyRepository.cs
│   └── Generic/
│       └── GenericRepository.cs
├── UnitOfWork/
│   └── UnitOfWork.cs
├── Exceptions/
│   └── DataAccessException.cs
├── Extensions/
│   └── DataAccessServiceCollectionExtensions.cs
├── AtraccionesService.DataAcess.csproj
└── README.md
```

DataManagement define los contratos de persistencia y depende de Domain para tipos compartidos. Application consume esas interfaces, mientras DataAccess implementa las interfaces y depende de Domain y EF Core. DataAccess no debe depender de Application.

## 3. Separación entre DataAccess y DataManagement

### 3.1 `AtraccionesService.DataAcess`

Representa la capa de acceso a datos física y contiene:

- Contexto EF Core.
- Entidades persistentes.
- Configuraciones de entidad.
- Migraciones.
- Seeder.
- Implementaciones concretas.

### 3.2 `AtraccionesService.DataManagment`

Representa la capa de contratos y abstracción de persistencia:

- Interfaces de repositorios.
- Contratos de Unit of Work.
- DTOs utilizados exclusivamente para acceso a datos.
- Operaciones requeridas por Application.

El proyecto de gestión debe depender de `AtraccionesService.Domain`; no debe depender de Application, API ni DataAccess. La implementación concreta debe residir en DataAccess.

## 4. Tecnologías

- .NET 10.
- Entity Framework Core.
- SQL Server como base de datos principal para producción.
- SQLite como opción ligera para desarrollo y pruebas locales.
- `Microsoft.EntityFrameworkCore.Design` para migraciones.
- `Microsoft.EntityFrameworkCore.SqlServer` y `Microsoft.EntityFrameworkCore.Sqlite` como paquetes opcionales.
- `Microsoft.EntityFrameworkCore.InMemory` solamente para pruebas aisladas.
- `Microsoft.EntityFrameworkCore.Proxies` no debe utilizarse por defecto.

## 5. Modelo de persistencia

### 5.1 Catálogo

Las entidades de catálogo deben mantener una relación estructurada basada en los campos del contrato.

| Entidad | Propósito |
|---|---|
| `Attraction` | Representa el recurso principal. |
| `Category` | Categoría catalogada. |
| `Badge` | Insignia o etiqueta. |
| `Inclusion` | Servicio incluido. |
| `Language` | Idioma soportado. |
| `Location` | Ubicación asociada. |
| `Operator` | Operador responsable. |
| `Photo` | Foto referenciada por la atracción. |
| `Price` | Precio de la atracción. |
| `Rating` | Calificación agregada. |
| `UrlInfo` | URLs web y aplicación. |

#### Reglas

- `Attraction.Id` debe ser `Guid`.
- `ProductType` se persistirá con un valor de enumeración o un texto normalizado.
- Las relaciones de catálogo deben usar claves foráneas y tablas de asociación.
- `Price` puede almacenarse como objeto embebido o entidad separada, según se defina la persistencia.
- `_links` no se persistirá como dato de negocio.
- No deben agregarse campos de auditoría si la persistencia no los requiere.

### 5.2 Disponibilidad

`AttractionAvailability` debe representar una franja específica:

```text
AttractionAvailability
- Id
- AttractionId
- Date
- Time
- Capacity
- ReservedQuantity
- Version
```

- Se utilizará `Version` para controlar condiciones de carrera.
- La disponibilidad debe consultarse y actualizarse dentro de una transacción.
- Las fechas deben conservarse como `DateOnly` y las horas como `TimeOnly`, si el proveedor lo permite.
- En el caso de una capacidad total, el valor debe calcularse a partir de entradas confirmadas y movimientos de inventario.
- `AvailableSpots` se calcula como `Capacity - ReservedQuantity`; una restricción única cubre `AttractionId + Date + Time`.
- `AvailableSpots` es un valor calculado (`Capacity - ReservedQuantity`), no una segunda columna persistida; una restricción única cubre `AttractionId + Date + Time`.

### 5.3 Reservas

`Reservation` debe conservar:

- Reserva identificada por `Guid`.
- Estado (`CONFIRMED`, `PENDING`, `CANCELLED`).
- Fecha y hora.
- Cantidad de tickets.
- Nombre y email del cliente.
- Precio total.
- Id de atracción.
- Relación opcional con un pedido.
- Razón de cancelación interna, cuando corresponda; no se expone si no está en el contrato aprobado.

La entidad debe incorporar una restricción para evitar estados incorrectos y registrar historial mediante un evento de dominio, no mediante una entidad adicional en la base de datos.

### 5.4 Usuarios y clientes

#### `User`

- `Id` UUID.
- `OauthIssuer` y `OauthSubject`, únicos en conjunto.
- `Email` único.
- Estado activo, bloqueado o deshabilitado.
- Fecha de creación y actualización.

#### `Customer`

- `Id` UUID.
- `UserId` único.
- Datos de facturación.
- Referencia del método de pago simulado.
- Fecha de creación y actualización.

El cliente debe tener una relación uno a uno con el usuario y un único carrito activo.

### 5.5 Compra directa y pedidos

#### `Purchase`

- `Id` UUID.
- `CustomerId` UUID.
- `AttractionId` UUID.
- `ServiceDate` y `ServiceTime` confirmados.
- `Quantity` y cálculo de precio.
- `Status` de la compra.
- Moneda y total calculado por el servidor.
- Fechas de creación y actualización.

#### `PurchaseItem`

- `Id` UUID.
- `PurchaseId` UUID.
- `AttractionId` UUID.
- `ServiceDate` y `ServiceTime`.
- Cantidad y precio unitario.
- Estado y fecha de creación.

### 5.6 Pedidos y pagos

#### `Order`

- `Id` UUID.
- `CustomerId` UUID.
- `PurchaseId` UUID con restricción única.
- `ReservationId` opcional.
- `Status`.
- Moneda y total.
- Fechas de creación y actualización.

#### `OrderItem`

- `Id` UUID.
- `OrderId` UUID.
- `AttractionId` UUID.
- `ServiceDate` y `ServiceTime` confirmados.
- Cantidad.
- Moneda y precio unitario.
- Estado.

#### `PaymentSimulation`

- `Id` UUID.
- `OrderId` UUID.
- Método de pago.
- Estado.
- Monto y moneda.
- Referencia de pasarela.
- Fechas de creación y procesamiento.
- Motivo de error.

#### `PaymentAttempt`

- `Id` UUID.
- `PaymentSimulationId` UUID.
- Número de intento.
- Estado.
- Código y mensaje de respuesta.
- Fecha de creación.

#### `PaymentEvent`

- `Id` UUID.
- `PaymentSimulationId` UUID.
- Tipo de evento.
- Payload JSON.
- Fecha de creación.

#### `RefundSimulation`

- `Id`, `PaymentSimulationId`, `Amount`, `Currency`, `Status`, `Reason`, `CreatedByUserId`, `CreatedAt`, `ProcessedAt`.
- Representa un reembolso simulado; no invoca un proveedor externo.

#### `Role` y `UserRole`

- `Role`, `RolePermission` y `UserRole` persisten permisos locales asociados a perfiles provisionados; no almacenan scopes ni credenciales del issuer.
- Asignaciones registran actor, fecha y motivo.

### 5.7 Inventario y eventos

#### `InventoryMovement`

- `Id` UUID.
- `AttractionId` UUID.
- `AvailabilityId` UUID.
- Cantidad.
- Tipo de movimiento (`HELD`, `CONFIRMED`, `RELEASED`, `CANCELLED`).
- `PurchaseId` opcional.
- `ReservationId` opcional.
- Fecha de creación.

#### `OrderEvent`

- `Id` UUID.
- `OrderId` UUID.
- Tipo de evento.
- Estado anterior y nuevo.
- Fecha de creación.

#### `IdempotencyKey`

- `Issuer`, `Subject`, `Operation`, `Key`, índice único compuesto.
- Recurso afectado.
- Hash de request canónico.
- Estado `IN_PROGRESS` o `COMPLETED`.
- Status HTTP y cuerpo de respuesta reproducible.
- Fecha de creación y expiración.

#### `AuditEvent`

- `Id`, `ActorUserId`, `Action`, `ResourceType`, `ResourceId`, `Reason`, metadata sanitizada y `CreatedAt`.
- Inmutable; no almacena tokens, secretos ni datos financieros sensibles.

## 6. Configuración de Entity Framework

`AtraccionesDbContext` debe:

- Heredar de `DbContext`.
- Configurar todas las entidades mediante `OnModelCreating`.
- Configurar relaciones muchos a muchos mediante tablas de asociación.
- Configurar claves, índices y restricciones del DTO.
- Configurar valores por defecto para fechas y estados.
- Configurar `DecimalPrecision` para cantidad y moneda.
- Configurar `Guid` como clave primaria para recursos distribuidos.
- No incluir secretos ni cadenas de conexión en el código fuente.

### Configuraciones recomendadas

#### Constraints

- `oauth_subject` único por issuer.
- `oauth_issuer + oauth_subject` único.
- `email` único por usuario.
- `customer.user_id` único.
- `purchase` única para `customer_id + attraction_id + service_date + service_time` dentro de un pedido activo.
- `purchase_item` compuesto por `purchase_id` y `attraction_id`.
- `order` y `order_item` con claves foráneas restrictivas.
- `payment_simulation.order_id` único si una simulación corresponde a un pedido.
- `idempotency_key` compuesta por `key` y `operation`.
- `idempotency_key` única por `issuer + subject + operation + key`.
- `purchase_id` único y nullable en pedidos para impedir pedidos duplicados por compra.
- `AttractionId + Date + Time` único para disponibilidad.

#### Índices

- `Attraction.Id`.
- `Attraction.ProductType`.
- `Location.City`.
- `Reservation.ReservationId`.
- `Reservation.AttractionId`.
- `Reservation.Status`.
- `Reservation.CustomerEmail`.
- `Order.CustomerId`.
- `Order.Status`.
- `Purchase.CustomerId`.
- `Purchase.AttractionId` y `ServiceDate`.
- `PaymentSimulation.OrderId`.
- `PaymentSimulation.Status`.
- `IdempotencyKey.Key` y `ExpiresAt`.

## 7. Repositorios

### 7.1 Repositorio genérico

`GenericRepository<T>` debe implementar operaciones comunes:

- `GetByIdAsync`.
- `GetAllAsync`.
- `AddAsync`.
- `UpdateAsync`.
- `DeleteAsync`.
- `ExistsAsync`.
- `SaveChangesAsync`.

Debe permanecer desacoplado de la lógica de negocio y no incluir validaciones de dominio.

### 7.2 Repositorios especializados

| Repositorio | Métodos principales |
|---|---|
| `AttractionRepository` | Buscar, obtener por id, paginar, filtrar y obtener relaciones. |
| `AvailabilityRepository` | Consultar cupos y registrar movimientos. |
| `ReservationRepository` | Crear, consultar, actualizar y validar propiedad. |
| `UserRepository` | Crear, consultar por OAuth subject y correo. |
| `CustomerRepository` | Consultar cliente por usuario y actualizar datos. |
| `PurchaseRepository` | Gestionar compras activas, items, estado y totales. |
| `OrderRepository` | Crear, consultar y registrar eventos. |
| `PaymentRepository` | Crear simulación, registrar intentos y eventos. |
| `IdempotencyRepository` | Consultar, guardar y revocar claves. |
| `RefundSimulationRepository` | Registrar y consultar reembolsos simulados. |
| `RoleRepository` | Consultar roles y permisos locales efectivos. |
| `AuditEventRepository` | Registrar y consultar auditoría inmutable. |
| `AdminReportRepository` | Consultas agregadas y paginadas. |

Los métodos deben depender de las entidades de dominio o DTOs de persistencia, no retornar `DbSet` ni entidades de EF Core a Application.

## 8. Unit of Work

`IUnitOfWork` debe definir:

- `Task<int> SaveChangesAsync(CancellationToken)`.
- `Task BeginTransactionAsync()`.
- `Task CommitAsync()`.
- `Task RollbackAsync()`.
- Acceso a los repositorios definidos por DataManagement.

La interfaz pública vive en DataManagement y no expone `IDbContextTransaction`; esa implementación EF Core permanece interna a DataAccess. Begin/commit/rollback reciben `CancellationToken`.

`UnitOfWork` debe coordinar:

1. Inicio de transacción.
2. Persistencia de entidades.
3. Eventos de dominio.
4. Confirmación o reversión.
5. Persistencia de idempotencia.

Para operaciones transaccionales, el guardado debe ejecutarse una sola vez al finalizar la operación.

## 9. Idempotencia en DataAccess

La tabla `idempotency_keys` debe persistir:

- Clave recibida.
- Operación que la define.
- Hash del payload de entrada.
- Hash de la respuesta generada.
- Usuario o cliente asociado.
- Fecha de expiración.

La operación debe ser atómica:

1. Encontrar o crear el registro de idempotencia.
2. Comparar operación y hash.
3. Si coincide, devolver el resultado original.
4. Si es distinta, lanzar conflicto.
5. Si no existe, almacenar la clave y ejecutar la operación.

La clave compuesta es `issuer + subject + operation + key`. El registro pasa de `IN_PROGRESS` a `COMPLETED`; status HTTP y respuesta reproducible se guardan en la transacción con los efectos de negocio. La base impone unicidad para carreras concurrentes. Mismo hash devuelve replay; hash distinto produce conflicto. El TTL es configuración compartida.

La implementación debe protegerse contra condiciones de carrera mediante índices únicos y una transacción.

## 10. Eventos y pagos simulados

### 10.1 Almacenamiento

`PaymentSimulation` guarda el estado general, y sus subentidades guardan:

- `PaymentAttempt` para cada intento.
- `PaymentEvent` para cada evento generado por la pasarela simulada.

Los eventos deben ser inmutables. No deben existir operaciones de eliminación o actualización de eventos pasados.

### 10.2 Orden de relación

```text
PaymentSimulation
  ├── N PaymentAttempt
  └── N PaymentEvent

Order
  └── N PaymentSimulation
```

### 10.3 Estado de pago

Los valores definidos deben representarse mediante una enumeración de Application o una entidad de dominio; la persistencia debe usar valores constantes y evitar cadenas abiertas.

Estados sugeridos:

- `PENDING`
- `AUTHORIZED`
- `SETTLED`
- `REJECTED`
- `FAILED`
- `CANCELLED`
- `PARTIALLY_REFUNDED`
- `REFUNDED`

Los pedidos usan estados `PENDING_PAYMENT`, `PAID`, `FULFILLED`, `CANCELLED`, `PARTIALLY_REFUNDED` y `REFUNDED`. Los intentos fallidos no dejan el pedido en estado terminal mientras se permitan reintentos. Los reembolsos generan eventos de pago y pedido dentro de la unidad de trabajo.

## 11. Relación con categorías y valores de catálogo

Los enumerables de lista deben tener una tabla propia:

- `categories`.
- `badges`.
- `includes`.
- `supported_languages`.

Cada tabla de asociación debe conectarse por identidad de referencia:

```text
Attraction 1 ─── N AttractionCategory
Category 1 ─── N AttractionCategory
```

El seeder debe insertar únicamente los valores definidos por el contrato y evitar duplicados mediante índices únicos.

## 12. Migraciones

### 12.1 Convenciones

- Usar migraciones de EF Core con nombres descriptivos.
- Mantener migraciones incrementalmente.
- No modificar migraciones históricas una vez publicadas.
- Generar scripts SQL para bases de datos de producción.
- Usar `ApplyMigrations` en desarrollo, y control manual de migración en producción.

### 12.2 Orden de migraciones

1. Catálogo y operator.
2. Disponibilidad y reservas.
3. Usuarios y clientes.
4. Compras directas y elementos.
5. Pedidos y elementos.
6. Simulación y eventos de pago.
7. Inventario y movimientos.
8. Idempotencia y eventos de pedido.
9. Roles/permisos, reembolsos simulados y auditoría administrativa.
10. Índices y restricciones de integridad.

### 12.3 Datos iniciales

El seeder debe incluir:

- Operadores de prueba.
- Categorías permitidas.
- Insignias permitidas.
- Idiomas del contrato.
- Servicios incluidos.
- Ubicaciones locales de atracciones.
- Al menos una atracción de prueba para desarrollo.

No se deben incluir:

- Contraseñas.
- Tokens OAuth2.
- Secretos de pagos.
- Datos de producción reales.
- Usuarios ni roles administrativos con accesos auto-generados. El primer usuario `admin` se provisiona mediante procedimiento operativo controlado.

## 13. Seed y carga de datos iniciales

### 13.1 `CatalogSeeder`

Debe inicializar:

- Operadores.
- Categorías.
- Insignias.
- Incluidos.
- Idiomas.
- Ubicaciones.
- Atracciones de ejemplo.
- Disponibilidad inicial.

### 13.2 `EcommerceSeeder`

Debe inicializar:

- Usuarios de prueba anónimos o de entorno local.
- Clientes asociados.
- Compra directa de ejemplo.
- Pedido de prueba.
- Simulación de pago.
- Eventos.

El seeder debe habilitarse solo con una opción de entorno seguro y no debe ejecutarse automáticamente en producción.

## 14. Mapeo entre entidades y tablas

Cada entidad debe tener un `EntityConfiguration` separado:

```text
AttractionConfiguration
AvailabilityConfiguration
ReservationConfiguration
UserConfiguration
CustomerConfiguration
PurchaseConfiguration
PurchaseItemConfiguration
OrderConfiguration
OrderItemConfiguration
PaymentSimulationConfiguration
PaymentAttemptConfiguration
PaymentEventConfiguration
IdempotencyKeyConfiguration
InventoryMovementConfiguration
OrderEventConfiguration
RoleConfiguration
RolePermissionConfiguration
UserRoleConfiguration
RefundSimulationConfiguration
AuditEventConfiguration
```

El objetivo es evitar que `OnModelCreating` sea una lista larga y mantener el esquema ordenado.

## 15. Dependencias de DataAcess

```text
AtraccionesService.DataAcess
  ├── AtraccionesService.DataManagment
  ├── AtraccionesService.Domain
  └── EF Core / proveedor de persistencia
```

El proyecto `DataAccess` implementa las interfaces de DataManagement. No referencia Application.

## 16. Inyección de dependencias

`DataAccessServiceCollectionExtensions` debe registrar:

- `AtraccionesDbContext`.
- `IUnitOfWork`.
- `GenericRepository<T>`.
- Repositorios especializados.
- Repositorios de reportes, roles, reembolsos simulados y auditoría.
- Migración automática solo en desarrollo.

La configuración debe recibir:

- Cadena de conexión.
- Proveedor de base de datos.
- Opciones de aplicación.
- Tiempo de expiración de idempotencia.

No se debe guardar en `appsettings.json` una conexión con credenciales de producción.

## 17. Transporte y persistencia

DataAccess no debe depender de ASP.NET Core ni de controllers. La capa API debe únicamente:

- Inyectar los servicios de Application.
- Proporcionar identidad y permisos.
- Traducir errores de Application a HTTP.

Los repositorios no deben recibir `HttpContext`, DTOs HTTP ni respuesta visual.

## 18. Transacciones y consistencia

Las operaciones transaccionales deben mantener una unidad lógica:

1. Crear/actualizar carrito.
2. Crear checkout session.
3. Confirmar pedido.
4. Crear reserva.
5. Actualizar disponibilidad.
6. Registrar resultado del pago simulado.
7. Registrar eventos.
8. Confirmar cambios.

No se realizan llamadas externas dentro de la transacción local. Si posteriormente se integra un proveedor externo, se requerirá outbox/saga y no se intentará una transacción distribuida mediante EF Core.

En caso de fallo:

- Revertir la transacción.
- No cambiar el estado del pedido a pagado.
- No registrar eventos de pago como confirmados.
- No crear servicios externos o pasarelas de pago reales.

## 19. Fases de implementación

### Fase 1: Preparar la base de datos

1. Crear el proyecto DataAccess.
2. Definir `AtraccionesDbContext`.
3. Enumerar entidades y configuraciones.
4. Crear la conexión según el ambiente.
5. Ejecutar la primera migración de catálogo.

### Fase 2: Catálogo y reservas

1. Crear entidades y relaciones del catálogo.
2. Crear configuración de ubicación, operator, precio, rating y urls.
3. Crear repositorio de atracciones y disponibilidad.
4. Crear repositorio de reservas.
5. Crear migración y pruebas de integridad referencial.

### Fase 3: Usuarios y clientes

1. Crear `User` y `Customer`.
2. Crear repositorios y restricciones únicas.
3. Crear migración y pruebas de relaciones uno a uno.
4. Comprobar que un usuario no repite un `oauth_subject`.

### Fase 4: Compras directas

1. Crear `Purchase` y `PurchaseItem`.
2. Crear repositorios y relaciones con cliente y atracción.
3. Crear pruebas de cantidad, propiedad, fechas y disponibilidad.

### Fase 5: Pedidos y eventos

1. Crear `Order` y `OrderItem`.
2. Crear `OrderEvent`.
3. Crear repositorios y unidades de persistencia.
4. Probar cambio de estado y historial.

### Fase 6: Pagos y idempotencia

1. Crear `PaymentSimulation`, `PaymentAttempt` y `PaymentEvent`.
2. Crear `IdempotencyKey`.
3. Implementar repositorio de pagos.
4. Probar duplicados, intentos y respuesta original.

### Fase 7: Seed y despliegue

1. Crear seed de catálogo.
2. Crear seed de ecommerce para desarrollo.
3. Validar migraciones para SQL Server y SQLite.
4. Probar campañas de diseño sin datos sensibles.
5. Crear scripts de despliegue y rollback.

## 20. Pruebas de persistencia

### Pruebas unitarias

- Validación de configuración de entidades.
- Relaciones de catálogo.
- Restricciones de claves únicas.
- Conversión de estados.
- Configuración de decimal y timestamps.
- Operaciones de GenericRepository.

### Pruebas de integración

- Crear una atracción y sus relaciones.
- Crear una reserva y asociarla a una atracción.
- Crear usuario y cliente evocando el `oauth_subject` único.
- Crear una compra directa con varios items y validar total.
- Convertir una compra en pedido.
- Crear una simulación de pago con varios intentos.
- Generar eventos de pedido.
- Repetir una operación con la misma clave idempotente.
- Repetir con una clave distinta y comprobar conflicto.
- Reiniciar una compra en estado inválido.
- Revertir una operación de transacción.

### Infraestructura de pruebas

- SQLite en memoria para pruebas rápidas.
- SQL Server local o container para pruebas de compatibilidad.
- Testcontainers para escenarios de integración real.
- No usar mocks para comprobar persistencia.

## 21. Criterios de aceptación

La implementación de DataAccess se considera completa cuando:

1. El contexto crea todas las tablas definidas en el modelo.
2. Las relaciones de catálogo, reservas, usuarios, clientes, pedido y pagos son correctas.
3. Las claves únicas y los índices cumplen las reglas del negocio.
4. Cada operación de persistencia está implementada con repositorio especializado.
5. `UnitOfWork` controla transacciones y reversión.
6. `IdempotencyKey` protege operaciones duplicadas.
7. Los registros de pago y eventos siguen siendo inmutables.
8. `PaymentSimulation` no guarda información privada de tarjeta.
9. Los datos de catálogo y ecommerce se cargan mediante seed seguro.
10. Las migraciones se ejecutan correctamente en entorno de desarrollo y pruebas.
11. Los repositorios devuelven entidades o DTOs de dominio, no DTOs HTTP.
12. Application implementa sus interfaces sin depender de EF Core.
13. Las pruebas invalidan condiciones de integridad y concurrencia.

## 22. Riesgos y decisiones pendientes

1. Definir la base principal de datos de producción.
2. Confirmar si las fechas y horas usarán `DateOnly` y `TimeOnly`.
3. Confirmar el tipo de longitud de `Decimal` para precio y pago.
4. Decidir si `Price` será una entidad separada o una entidad embebida.
5. Definir si la compra de una reserva se realiza a partir de stock o disponibilidad calculada.
6. Confirmar el comportamiento de eliminación física o lógica.
7. Definir la política de retención de `IdempotencyKey` y `PaymentEvent`.
8. Seleccionar un proveedor de persistencia compatible con SQL Server y SQLite.
9. Determinar el número máximo de intentos de pago.
10. Confirmar si `inventory_movements` se generará también para carrito y checkout.

## 23. Estado actual

Los proyectos `AtraccionesService.DataAcess` y `AtraccionesService.DataManagment` existen, pero no contienen implementación. El siguiente desarrollo recomendado es:

1. Crear `AtraccionesDbContext` y entidades principales.
2. Implementar las configuraciones de catálogo y reservas.
3. Definir la interfaz `IUnitOfWork`.
4. Crear el repositorio base y los repositorios especializados.
5. Añadir migraciones iniciales y pruebas de integridad.
6. Integrar los repositorios con Application y ejecutar pruebas de persistencia.
