# AtraccionesService.DataAcess

Implementación EF Core de los contratos de `AtraccionesService.DataManagment`. Depende solo de DataManagement, Domain y EF Core; no referencia Application ni API.

## Configuración

| Clave | Descripción |
|---|---|
| `Database:Provider` | `SqlServer` (producción) o `Sqlite` (desarrollo y pruebas). |
| `ConnectionStrings:Atracciones` | Cadena de conexión. En producción se entrega por variable de entorno (`ConnectionStrings__Atracciones`) o secretos, nunca en `appsettings.json`. |
| `Database:ApplyMigrations` | Migra al iniciar. Solo se respeta en Development. |
| `Database:Seed:Catalog` / `Database:Seed:Ecommerce` | Datos de ejemplo. Solo en Development; no crean roles ni administradores. |

El composition root de la API registra `AddDataAccess(configuration)` y llama a `InitializeDatabaseAsync(isDevelopment)`.

## Migraciones

Cada proveedor tiene su contexto y su carpeta de migraciones:

- `SqlServerAtraccionesDbContext` → `Migrations/SqlServer`
- `SqliteAtraccionesDbContext` → `Migrations/Sqlite`

La herramienta `dotnet-ef` está en el manifiesto local del repositorio (`dotnet tool restore`). Para un cambio de modelo, genere la migración en **ambos** proveedores con el mismo nombre:

```bash
dotnet ef migrations add <Nombre> --project AtraccionesService.DataAcess --context SqlServerAtraccionesDbContext --output-dir Migrations/SqlServer
dotnet ef migrations add <Nombre> --project AtraccionesService.DataAcess --context SqliteAtraccionesDbContext --output-dir Migrations/Sqlite
```

No modifique migraciones ya publicadas.

## Despliegue en producción (SQL Server)

La migración en producción es manual con scripts revisados (`Scripts/`):

```bash
# Script idempotente con todas las migraciones
dotnet ef migrations script --idempotent --project AtraccionesService.DataAcess --context SqlServerAtraccionesDbContext --output AtraccionesService.DataAcess/Scripts/sqlserver-migrations.sql
# Rollback de una migración concreta hacia la anterior
dotnet ef migrations script <Migracion> <Anterior|0> --project AtraccionesService.DataAcess --context SqlServerAtraccionesDbContext
```

## Decisiones de persistencia

- **Escritura inmediata dentro de la transacción:** cada operación de repositorio guarda en el acto, dentro de la transacción de `IUnitOfWork`, para que las lecturas posteriores del mismo caso de uso vean los cambios. La atomicidad la da la transacción: un `RollbackAsync` deshace todo.
- **Concurrencia de cupos:** `TryReserveQuantityAsync` es un `UPDATE` condicional (`Capacity - ReservedQuantity >= quantity`) que incrementa `Version`; no depende de bloqueos de fila.
- **Unicidad:** las violaciones de índices únicos y los conflictos de versión se exponen como `ConcurrencyException` (DataManagement).
- **Inmutabilidad:** eventos de pedido, eventos e intentos de pago, movimientos de inventario y auditoría rechazan actualizaciones y borrados.
- **Historia:** reservas, compras, elementos de pedido y movimientos referencian la atracción por id sin FK, para conservar el historial si se elimina una atracción sin reservas activas.
- **SQLite:** `decimal` se guarda como REAL e instantes `DateTimeOffset` como enteros binarios para poder filtrar y ordenar. Úselo solo en desarrollo y pruebas.
- **Pagos:** no existen columnas para números de tarjeta, CVV ni credenciales.
