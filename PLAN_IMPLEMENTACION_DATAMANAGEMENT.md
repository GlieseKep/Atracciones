# Plan de implementación de DataManagement

> Este documento define la capa de contratos y abstracciones de persistencia para la solución. No modifica `CONTRATO.md` ni implementa código.

## 1. Objetivo

`AtraccionesService.DataManagment` debe proporcionar los contratos de persistencia que consume `AtraccionesService.Application` sin depender de Entity Framework, SQL Server ni ningún proveedor concreto.

## 2. Responsabilidades de DataManagement

La capa debe contener exclusivamente:

- Interfaces de repositorios.
- Contratos de consultas y operaciones de persistencia.
- Contratos de Unit of Work.
- DTOs propios de persistencia, si son necesarios.
- Contratos de concurrencia, idempotencia, auditoría y reportes de lectura.
- Contratos para eventos persistidos dentro de una unidad de trabajo.

La capa no debe contener:

- Implementaciones concretas de Entity Framework.
- Clases ORM.
- Consultas SQL o LINQ de infraestructura.
- Reglas de negocio.
- DTOs HTTP.
- Dependencias de ASP.NET Core.

## 3. Dependencias

```text
AtraccionesService.API
  ├── AtraccionesService.Application
  ├── AtraccionesService.Contracts
  └── AtraccionesService.DataAcess  (solo composition root)

AtraccionesService.Application
  ├── AtraccionesService.Domain
  └── AtraccionesService.DataManagment

AtraccionesService.DataAcess
  ├── AtraccionesService.DataManagment
  ├── AtraccionesService.Domain
  └── proveedor EF Core
```

`DataManagement` puede depender de Domain para tipos de dominio compartidos, pero nunca depende de Application, API ni DataAccess. Application consume sus interfaces y DataAccess las implementa. API compone las implementaciones en el arranque. No se duplican repositorios en Application.

## 4. Estructura propuesta

```text
AtraccionesService.DataManagment/
├── Contracts/
│   ├── Common/
│   │   ├── IPaginatedResult.cs
│   │   └── PaginationRequest.cs
│   ├── Catalog/
│   │   ├── IAttractionRepository.cs
│   │   ├── IAvailabilityRepository.cs
│   │   └── IReservationRepository.cs
│   ├── Identity/
│   │   ├── IUserRepository.cs
│   │   ├── ICustomerRepository.cs
│   │   ├── IRoleRepository.cs
│   │   └── IUserRoleRepository.cs
│   ├── Ecommerce/
│   │   ├── IPurchaseRepository.cs
│   │   ├── IOrderRepository.cs
│   │   ├── IPaymentSimulationRepository.cs
│   │   ├── IIdempotencyRepository.cs
│   │   └── IInventoryMovementRepository.cs
│   └── Events/
│       ├── IOrderEventRepository.cs
│       ├── IPaymentEventRepository.cs
│       └── IAuditEventRepository.cs
├── Administration/
│   ├── IAdminReportRepository.cs
│   └── IRefundSimulationRepository.cs
├── UnitOfWork/
│   ├── IUnitOfWork.cs
│   └── IUnitOfWorkFactory.cs
├── QueryModels/
│   ├── AttractionQueryResult.cs
│   ├── ReservationQueryResult.cs
│   ├── PurchaseQueryResult.cs
│   ├── OrderQueryResult.cs
│   └── PaymentSimulationQueryResult.cs
├── Specifications/
│   ├── CatalogSpecifications.cs
│   ├── ReservationSpecifications.cs
│   ├── PurchaseSpecifications.cs
│   └── OrderSpecifications.cs
├── Exceptions/
│   └── DataManagementException.cs
├── AtraccionesService.DataManagment.csproj
└── GlobalUsings.cs
```

La estructura puede ajustarse si el proyecto usa `Repositories` y `Interfaces` como carpetas de organización. No es necesario crear un repositorio genérico único para todos los agregados.

## 5. Principios de diseño

1. Cada agregado debe tener su propio repositorio.
2. Los repositorios deben exponer operaciones coherentes con el caso de uso de Application.
3. Los métodos deben aceptar `CancellationToken`.
4. Los repositorios no deben devolver entidades de Entity Framework.
5. Los repositorios no deben aceptar DTOs HTTP ni `HttpContext`.
6. Los métodos de actualización usan entidades de dominio o DTOs de persistencia explícitos.
7. Las interfaces deben ser pequeñas y cohesionadas por responsabilidad.
8. La variante genérica solo debe proporcionar operaciones comunes; no debe ocultar requisitos específicos de los agregados.
9. Los repositorios deben devolver objetos de dominio cuando Application pueda usarlos directamente.
10. DataManagement no debe depender de un proveedor concreto ni de EF Core.

## 6. Contratos comunes

### 6.1 `PaginationRequest`

Debe representar:

- `Page` o `Offset`.
- `PageSize` o `Limit`.
- `Sort`.
- `Filter` opcional.

Los valores deben tener límites configurables.

### 6.2 `IPaginatedResult<T>`

Debe proporcionar:

- Elementos de la página.
- Total de elementos.
- Número de página.
- Tamaño de página.
- Total de páginas.

La operación paginada puede devolver una lista y los metadatos asociados.

## 7. Repositorios del catálogo

### `IAttractionRepository`

Debe proporcionar:

- Obtener por identificador.
- Consultar catálogo por filtros.
- Consultar atracciones por categorías, países o ciudades.
- Consultar relaciones de imágenes, idiomas, ubicaciones y operadores.
- Crear, actualizar y eliminar una atracción.
- Guardar colecciones de relaciones asociadas.
- Determinar si una atracción existe.

### `IAvailabilityRepository`

Debe permitir:

- Consultar disponibilidad por fecha y hora.
- Consultar disponibilidad de una atracción.
- Guardar un registro de disponibilidad.
- Actualizar cupos disponibles.
- Registrar movimientos de inventario.

### `IReservationRepository`

Debe permitir:

- Crear y actualizar reservas.
- Obtener reservas por identificador.
- Consultar reservas por cliente.
- Consultar reservas por atracción.
- Filtrar por estado y fechas.
- Comprobar si una reserva pertenece al cliente.

## 8. Repositorios de identidad y clientes

### `IUserRepository`

Requisitos:

- Obtener usuario por identificador.
- Obtener usuario por `oauth_subject`.
- Obtener usuario por email.
- Crear usuario.
- Actualizar estado, email y datos de acceso no sensibles.
- Eliminar o deshabilitar únicamente bajo política específica.

### `ICustomerRepository`

Requisitos:

- Obtener cliente por identificador.
- Obtener cliente por usuario.
- Crear y actualizar datos de facturación.
- Consultar pedidos o carritos asociados.
- Confirmar que un cliente pertenece a un usuario.

No gestiona credenciales ni autenticación. Las lecturas administrativas se expresan mediante consultas especializadas; no se debe ampliar este repositorio con acceso indiscriminado a todos los clientes.

### Roles y permisos locales

`IRoleRepository` y `IUserRoleRepository` administran roles/permisos locales asociados al perfil provisionado. No emiten OAuth scopes ni reemplazan al issuer. Los contratos deben permitir consultar permisos efectivos y asignar/remover roles con actor y motivo para auditoría.

## 9. Repositorios de ecommerce

### `IPurchaseRepository`

Debe permitir:

- Obtener compra por identificador.
- Obtener compras de un cliente.
- Crear y actualizar compras.
- Agregar y actualizar elementos.
- Confirmar disponibilidad y precio.
- Marcar compras como pagadas, canceladas o expiradas.

### `IOrderRepository`

Debe permitir:

- Obtener pedido por identificador.
- Obtener pedidos por cliente.
- Obtener el historial de eventos de un pedido.
- Crear y actualizar órdenes.
- Agregar y actualizar elementos.
- Confirmar estado del pedido.
- Consultar ordenes relacionadas con una reserva.

### `IPaymentSimulationRepository`

Debe permitir:

- Obtener simulación por identificador.
- Obtener simulaciones de un pedido.
- Crear intento de pago.
- Registrar resultado del intento.
- Registrar eventos de pasarela.
- Actualizar estado de pago.

### `IIdempotencyRepository`

Debe permitir:

- Consultar una clave por operación.
- Consultar por solicitud o respuesta hash.
- Crear una nueva clave.
- Actualizar el estado de expiración.
- Eliminar claves vencidas.

Debe incluir una operación de concurrencia segura al crear la clave.

El identificador de operación es `subject + operation + key`. El contrato debe recibir hash canónico del payload, estado (`IN_PROGRESS`/`COMPLETED`), código HTTP y cuerpo serializado de respuesta para replay. Nunca almacena tokens ni secretos.

### `IInventoryMovementRepository`

Debe permitir:

- Registrar movimiento de disponibilidad.
- Consultar movimientos por atracción.
- Consultar movimientos asociados a una reserva.
- Consultar la cantidad disponible a partir del movimiento histórico.

## 10. Unit of Work

### `IUnitOfWork`

Debe agregar:

- `Task<int> SaveChangesAsync(CancellationToken)`.
- `Task BeginTransactionAsync(CancellationToken)`.
- `Task CommitAsync(CancellationToken)`.
- `Task RollbackAsync(CancellationToken)`.
- `bool HasActiveTransaction`.

También debe proporcionar acceso a cada repositorio por propiedad o método:

```csharp
public interface IUnitOfWork : IAsyncDisposable
{
    IAttractionRepository Attractions { get; }
    IAvailabilityRepository Availability { get; }
    IReservationRepository Reservations { get; }
    IUserRepository Users { get; }
    ICustomerRepository Customers { get; }
    IPurchaseRepository Purchases { get; }
    IOrderRepository Orders { get; }
    IPaymentSimulationRepository Payments { get; }
    IIdempotencyRepository Idempotency { get; }
    IInventoryMovementRepository Inventory { get; }
    IOrderEventRepository OrderEvents { get; }
    IPaymentEventRepository PaymentEvents { get; }
    IAuditEventRepository AuditEvents { get; }
    IRoleRepository Roles { get; }
    IUserRoleRepository UserRoles { get; }
    IRefundSimulationRepository Refunds { get; }
    IAdminReportRepository AdminReports { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

### `IUnitOfWorkFactory`

Puede ofrecer una instancia de Unit of Work para una operación o contexto específico.

## 11. Contratos y DTOs de persistencia

Los DTOs de persistencia deben ser usados únicamente para representar resultados de consulta o parámetros transferidos entre DataManagement y su implementación.

Objetivos:

- Separar consultas de escritura.
- Facilitar despliegue intenso de datos.
- Evitar pasar la estructura completa de EF Core.
- Controlar qué datos de una entidad se exponen a Application.

Ejemplos:

- `AttractionSummaryQueryResult`.
- `ReservationListItem`.
"PurchaseItemQueryResult".
- `OrderStatusHistoryResult`.
- `PaymentSimulationResult`.

No se deben definir DTOs GNU para cada campo; solo los necesarios para el caso de uso.

## 12. Criterios de consulta

Los repositorios deben preferir métodos específicos en vez de cualquier consulta genérica. Por ejemplo:

```text
GetByIdAsync
GetForCustomerAsync
GetActiveCartAsync
GetByStatusAsync
GetByPaymentIdAsync
GetPendingByExpirationAsync
```

Los métodos deben incluir criterios de filtros para:

- Fecha.
- Estado.
- Cliente.
- Operador.
- Pedido.
- Atracción.
- Compra.
- Moneda.
- Idempotencia.

## 13. Requisitos de concurrencia

DataManagement debe definir operaciones que tengan una semántica de concurrencia explícita:

- `AddWithConcurrencyCheckAsync`.
- `UpdateIfVersionMatchesAsync`.
- `TryReserveQuantityAsync`.
- `TryCreateIdempotencyRecordAsync`.

No debe asumir que los proveedores siempre ofrecen bloqueo de fila.

La implementación concreta debe usar:

- Versionado de filas si aplica.
- Transacciones de base de datos.
- Bloqueos de concurrencia si el motor lo admite.
- `SELECT` con mutex o estado de registro.

## 14. Especificaciones de consulta

Para reducir el número de métodos, se pueden definir especificaciones por agregado:

- `CatalogSpecifications`.
- `ReservationSpecifications`.
- `CartSpecifications`.
- `OrderSpecifications`.

Estas especificaciones deben encapsular:

- Filtros.
- Ordenamiento.
- Paginación.
- Reglas de acceso.
- Eager loading requerido.

No deben depender de una clase específica de Entity Framework.

## 15. Repositorios de agregados y operaciones transaccionales

### 15.1 Catálogo

- `GetByIdAsync`.
- `SearchAsync`.
- `GetDetailsAsync`.
- `AddAsync`.
- `UpdateAsync`.
- `DeleteAsync`.
- `ExistsAsync`.

### 15.2 Reservas

- `GetByIdAsync`.
- `GetByCustomerAsync`.
- `GetByAttractionAsync`.
- `AddAsync`.
- `CancelAsync`.
- `UpdateStatusAsync`.

### 15.3 Compra directa

- `GetByIdAsync`.
- `GetByCustomerAsync`.
- `CreateAsync`.
- `UpdateStatusAsync`.
- `AddItemAsync`.
- `UpdateItemAsync`.
- `RemoveItemAsync`.

### 15.4 Pedido

- `CreateAsync`.
- `GetByIdAsync`.
- `GetByCustomerAsync`.
- `AddEventAsync`.
- `UpdateStatusAsync`.

### 15.6 Pagos

- `CreateSimulationAsync`.
- `GetSimulationAsync`.
- `AddAttemptAsync`.
- `AddEventAsync`.
- `UpdateSimulationStatusAsync`.

### 15.7 Idempotencia

- `TryCreateInProgressAsync` con unicidad por issuer, subject, operation y key.
- `GetByIdentityAsync` para decidir replay, conflicto o solicitud en proceso.
- `CompleteAsync` guardando status y respuesta reproducible en la unidad de trabajo.
- `RemoveExpiredAsync`.

## 16. Contratos de eventos de persistencia

DataManagement debe proporcionar una interfaz para registrar eventos dentro de la transacción:

```text
IOrderEventStore
IPaymentEventStore
IInventoryEventStore
```

Estas interfaces deben permitir:

- Registrar eventos solo con datos de negocio.
- Hacerlos persistentes en la misma unidad de trabajo.
- Rechazar eventos duplicados.
- Conservar un historial inmutable.

Application crea los eventos de negocio y solicita su persistencia mediante estos contratos; DataAccess los guarda dentro de la unidad de trabajo. La publicación externa queda fuera de DataManagement y, si se incorpora posteriormente, se implementa con outbox.

## 17. Contratos de transacción

La transacción se controla exclusivamente mediante `IUnitOfWork`; no se expone el proveedor concreto ni se mantiene un `ITransactionManager` paralelo. Los métodos de Unit of Work reciben `CancellationToken`, implementan begin/commit/rollback y coordinan todos los repositorios de la operación.

## 18. Inyección de dependencias

DataManagement es una biblioteca de contratos y no registra implementaciones. `DataAccessServiceCollectionExtensions` en DataAccess registra implementaciones concretas; API invoca esa extensión desde el composition root.

## 19. Fases de implementación

### Fase 1: Contratos base

1. Crear `IUnitOfWork`.
2. Crear `IUnitOfWorkFactory`.
3. Crear `PaginationRequest` y `IPaginatedResult`.
4. Crear excepciones de DataManagement.
5. Verificar que DataManagement no contiene referencias de ASP.NET Core ni implementaciones concretas.

### Fase 2: Catalogo y reservas

1. Crear `IAttractionRepository`.
2. Crear `IAvailabilityRepository`.
3. Crear `IReservationRepository`.
4. Crear resultados de consulta asociados.
5. Validar métodos y parámetros con Application.

### Fase 3: Usuarios y clientes

1. Crear `IUserRepository`.
2. Crear `ICustomerRepository`.
3. Definir resultados de perfil y relación usuario-cliente.
4. Probar los contratos de actualización.

### Fase 4: Compras directas

1. Crear `IPurchaseRepository`.
2. Crear resultados de compra y compra-item.
3. Definir operaciones de actualización atómicas.

### Fase 5: Pedidos y pagos

1. Crear `IOrderRepository`.
2. Crear `IPaymentSimulationRepository`.
3. Crear `IInventoryMovementRepository`.
4. Crear `IOrderEventRepository`.
5. Crear contratos de roles, reportes, reembolsos simulados, auditoría y operaciones de eventos/estados.

### Fase 6: Idempotencia

1. Crear `IIdempotencyRepository`.
2. Crear contratos de respuesta original.
3. Definir creación atómica y manejo de clave duplicada.
4. Completar políticas de expiración.

### Fase 7: Validación y integración

1. Verificar que cada método corresponde a un caso de uso.
2. Verificar que Application no implementa lógica de persistencia.
3. Revisión de dependencias de DataManagement.
4. Validar con pruebas de compilación.

## 20. Pruebas de DataManagement

### Pruebas de contratos

- Todas las interfaces son implementables por DataAccess.
- Los métodos aceptan `CancellationToken`.
- Los repositorios no dependen de IHttp.
- Cada interfaz tiene una responsabilidad definida.
- Los parámetros de paginación y filtros usan tipos comunes.

### Pruebas de integración de contratos

- La implementación de DataAccess debe cumplir `IUnitOfWork`.
- Los repositorios deben implementar los métodos de su interfaz.
- El Unit of Work debe guardar cambios y confirmar transacciones.
- La operación debe revertirse si falla la transacción.
- Las consultas de carrito, pedido y pago deben usar una sola unidad de persistencia.

### Pruebas de compatibilidad

- Probar contra SQLite y SQL Server.
- Probar transacciones y campos de Decimal.
- Probar consultas paginadas.
- Probar concurrencia de idempotencia.
- Probar restricciones de ownership.

## 21. Criterios de aceptación

DataManagement estará completa cuando:

1. Application depende de interfaces de DataManagement, no de EF Core.
2. Cada agregado tiene un repositorio independiente.
3. Existen contratos de paginación, transacción y idempotencia.
4. `IUnitOfWork` coordina toda la operación transaccional.
5. Los repositorios no contienen reglas de negocio.
6. Los métodos de persistencia tienen `CancellationToken`.
7. Las operaciones de creación, actualización y consulta están definidas.
8. Los DTOs son de persistencia y no de HTTP.
9. DataAccess implementa todos los contratos sin modificar la interfaz.
10. Las pruebas verifican transacciones, concurrencia e idempotencia.

## 22. Riesgos y decisiones pendientes

1. Definir si `IUnitOfWork` debe incluir repositorios o métodos de acceso separado.
2. Confirmar qué entidades de dominio se devolverán directamente a Application.
3. Decidir el lugar exacto de los DTOs de consulta.
4. Confirmar la política de eliminación y anulación de entidades.
5. Confirmar extensibilidad de `Sort` y `Filter`.
7. Definir una política común para `CancellationToken`.
8. Definir duración de retención de idempotencia y auditoría.
9. Definir si las notificaciones de eventos se añadirán en una fase posterior; esta capa solo persiste eventos.

## 23. Estado actual

La carpeta `AtraccionesService.DataManagment` existe, pero actualmente contiene solamente un proyecto mínimo y no tiene contratos. El siguiente paso recomendado es:

1. Definir `IUnitOfWork` y las interfaces de repositorios del catálogo.
2. Crear `IUserRepository` y `ICustomerRepository`.
3. Crear los contratos de carrito, pedidos y pagos.
4. Crear contratos de idempotencia y transacciones.
5. Crear DTOs mínimos de consulta y paginación.
6. Validar que DataAccess puede implementar todas las interfaces sin cambios en Application.
