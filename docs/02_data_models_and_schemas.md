# 02. Modelos de Datos y Esquema Relacional — SmartPantry

> **Propósito de este documento:** Especificar el modelo entidad-relación completo en **Microsoft SQL Server** gestionado mediante **Entity Framework Core 10** (`SmartPantryDbContext`). Define con precisión milimétrica tablas, columnas, tipos SQL y C#, nulabilidad, restricciones de integridad, índices de rendimiento y políticas de auditoría, concurrencia optimista y borrado.

---

## 1. Convenciones de Nomenclatura de Base de Datos

SmartPantry adopta la convención estándar de **ABP Framework sobre SQL Server** definida en `SmartPantryConsts`:
- **Prefijo de tablas de negocio:** `App` (`SmartPantryConsts.DbTablePrefix = "App"`), con nombres pluralizados en `PascalCase` (`AppProducts`, `AppPantryItems`, `AppPantryItemWarnings`, `AppUserDietaryPreferences`, `AppExternalApiCallLogs`).
- **Prefijo de tablas de módulos base de ABP:** `Abp` (`AbpUsers`, `AbpRoles`, `AbpUserRoles`, `AbpAuditLogs`, `AbpBlobs`) y `OpenIddict` (`OpenIddictApplications`, `OpenIddictTokens`).
- **Esquema por defecto:** `dbo` (`SmartPantryConsts.DbSchema = null`).
- **Columnas:** `PascalCase` en SQL Server y entidades C#; serializadas automáticamente en `camelCase` en los payloads JSON de la API REST.
- **Claves primarias:** `uniqueidentifier` (`System.Guid`) generadas en aplicación mediante `IGuidGenerator` (`SequentialGuidGenerator` de ABP para minimizar fragmentación en índices clustered de SQL Server).

---

## 2. Diagrama Entidad-Relación (`erDiagram`)

```mermaid
erDiagram
    AbpUsers ||--o| AppUserDietaryPreferences : "configures (1:1)"
    AbpUsers ||--o{ AppPantryItems : "owns (1:N)"
    AbpUsers ||--o{ AppExternalApiCallLogs : "triggers (0..1:N)"
    AppProducts ||--o{ AppPantryItems : "referenced_by (1:N)"
    AppPantryItems ||--o{ AppPantryItemWarnings : "generates (1:N)"

    AbpUsers {
        uniqueidentifier Id PK
        nvarchar_256 UserName UK
        nvarchar_256 NormalizedEmail
        nvarchar_64 Name
        nvarchar_64 Surname
        bit IsActive
        bit IsDeleted
        datetime2 CreationTime
    }

    AppProducts {
        uniqueidentifier Id PK
        nvarchar_32 Barcode UK
        nvarchar_128 Name
        int Category
        nvarchar_64 Brand
        nvarchar_32 Quantity
        nvarchar_1024 Ingredients
        nvarchar_256 Allergens
        nvarchar_256 ExternalCategories
        nvarchar_512 ImageUrl
        nvarchar_1 NutriScore
        int NovaGroup
    }

    AppUserDietaryPreferences {
        uniqueidentifier Id PK
        uniqueidentifier UserId FK_UK
        nvarchar_1024 WatchedTerms
        bit ShowAlertsOnScreen
        datetime2 CreationTime
        datetime2 LastModificationTime
        nvarchar_40 ConcurrencyStamp
    }

    AppPantryItems {
        uniqueidentifier Id PK
        uniqueidentifier UserId FK
        uniqueidentifier ProductId FK
        int State
        datetime2 ExpirationDate
        decimal_18_2 Quantity
        nvarchar_32 Unit
        nvarchar_500 PrivateNotes
        datetime2 CreationTime
        datetime2 LastModificationTime
        nvarchar_40 ConcurrencyStamp
    }

    AppPantryItemWarnings {
        uniqueidentifier Id PK
        uniqueidentifier PantryItemId FK
        nvarchar_64 WarningType
        nvarchar_512 Message
        bit IsActive
        datetime2 CreationTime
        datetime2 LastModificationTime
        nvarchar_40 ConcurrencyStamp
    }

    AppExternalApiCallLogs {
        uniqueidentifier Id PK
        nvarchar_32 OperationType
        nvarchar_32 OutcomeStatus
        int HttpStatusCode
        int DurationMs
        int QueryTermLength
        datetime2 CreationTime
        uniqueidentifier CreatorId FK
    }
```

### Relaciones Clave Explicadas
1. **`AbpUsers` (1) -> (0..1) `AppUserDietaryPreferences`:** Cada usuario autenticado posee a lo sumo un perfil de preferencias alimentarias identificado unívocamente por `UserId`. Si el registro aún no existe en base de datos, el sistema asume lista vacía (`WatchedTerms = ""`) y `ShowAlertsOnScreen = true` hasta su primer guardado.
2. **`AppProducts` (1) -> (0..N) `AppPantryItems`:** `AppProducts` actúa como catálogo maestro compartido libre de datos privados de usuario. Múltiples usuarios pueden tener sus propios `AppPantryItems` apuntando al mismo `ProductId`.
3. **`AbpUsers` (1) -> (0..N) `AppPantryItems`:** Relación estricta de propiedad privada (`UserId`). Ninguna operación de lectura o escritura sobre `AppPantryItems` puede ejecutarse sin validar `item.UserId == CurrentUser.GetId()`.
4. **`AppPantryItems` (1) -> (0..N) `AppPantryItemWarnings`:** Un ítem de despensa puede poseer a lo sumo **una** advertencia por cada `WarningType` (`"Expiration"` o `"DietaryPreference"`), garantizado a nivel físico por el índice compuesto único `(PantryItemId, WarningType)`. Al eliminarse físicamente un `PantryItem`, sus advertencias asociadas se eliminan en cascada (`ON DELETE CASCADE`).

---

## 3. Definición Exhaustiva de Tablas y Entidades

> **Nota sobre trazabilidad de migraciones y compatibilidad 100% hacia atrás:**
> 1. En la columna *Estado Esquema*, `ACTIVO` indica tablas, columnas e índices ya creados exactamente así en las 4 migraciones vigentes (`20260902214828_Initial`, `20260911143729_AddProduct`, `20261002225523_AddPantryItemsAndWarnings`, `20261021224800_AddPantryItemQuantityAndUnit`). **Ninguna columna ni índice existente se renombra ni cambia de tipo.**
> 2. `MIGRACIÓN PENDIENTE` indica únicamente extensiones aditivas (`AddColumn` anulables en tablas existentes o `CreateTable` para tablas nuevas), garantizando cero pérdida de datos en bases SQL Server ya pobladas.
> 3. Las reglas de rango (`CHECK` lógicos en tabla) se aplican en constructores/métodos de dominio C# tal como en las migraciones actuales.
> 4. Las tablas de plantilla `AppAuthors` y `AppBooks` creadas en `20260902214828_Initial` permanecen intactas en `SmartPantryDbContext` para no romper el historial de migraciones ni los tests base de infraestructura.

---

### 3.1 Tabla `AppProducts`
- **Clase C#:** `SmartPantry.Products.Product : BasicAggregateRoot<Guid>`
- **Propósito:** Catálogo interno persistente de productos alimenticios envasados (RF-07, RF-08).

| Columna (SQL / C#) | Propiedad JSON | Tipo SQL Server | Tipo C# | Null | Default / Constraint | Estado Esquema |
|---|---|---|---|---|---|---|
| `Id` | `id` | `uniqueidentifier` | `Guid` | `NOT NULL` | `PK_AppProducts` | `ACTIVO` |
| `Barcode` | `barcode` | `nvarchar(32)` | `string` | `NOT NULL` | `UNIQUE`, Longitud `1..32` (`MaxBarcodeLength`) | `ACTIVO` |
| `Name` | `name` | `nvarchar(128)` | `string` | `NOT NULL` | Longitud `1..128` (`MaxNameLength`) | `ACTIVO` |
| `Category` | `category` | `int` | `ItemCategory` | `NOT NULL` | `CHECK (Category BETWEEN 1 AND 12)` (`Undefined = 0` prohibido) | `ACTIVO` |
| `Brand` | `brand` | `nvarchar(64)` | `string?` | `NULL` | Longitud máx. `64` (`MaxBrandLength`) | `ACTIVO` |
| `Quantity` | `quantity` | `nvarchar(32)` | `string?` | `NULL` | Presentación comercial (ej. `"500 g"`), máx. `32` | `ACTIVO` |
| `Ingredients` | `ingredients` | `nvarchar(1024)` | `string?` | `NULL` | Texto de ingredientes, máx. `1024` (`MaxIngredientsLength`) | `ACTIVO` |
| `Allergens` | `allergens` | `nvarchar(256)` | `string?` | `NULL` | Alérgenos declarados, máx. `256` (`MaxAllergensLength`) | `ACTIVO` |
| `ExternalCategories` | `externalCategories` | `nvarchar(256)` | `string?` | `NULL` | Etiquetas textuales de categorías Open Food Facts, máx. `256` | `MIGRACIÓN PENDIENTE` |
| `ImageUrl` | `imageUrl` | `nvarchar(512)` | `string?` | `NULL` | URL HTTPS de imagen frontal del envase, máx. `512` | `MIGRACIÓN PENDIENTE` |
| `NutriScore` | `nutriScore` | `nvarchar(1)` | `string?` | `NULL` | `CHECK (NutriScore IN ('A','B','C','D','E') OR NutriScore IS NULL)` | `MIGRACIÓN PENDIENTE` |
| `NovaGroup` | `novaGroup` | `int` | `int?` | `NULL` | `CHECK (NovaGroup BETWEEN 1 AND 4 OR NovaGroup IS NULL)` | `MIGRACIÓN PENDIENTE` |

**Índices de `AppProducts`:**
- `PK_AppProducts (Id)` — Clustered Index primario.
- `IX_AppProducts_Barcode (Barcode) UNIQUE` (`ACTIVO`) — Garantiza la Regla de Negocio 4 y RF-08 a nivel motor de base de datos contra condiciones de carrera concurrentes, permitiendo búsquedas `O(log N)` por código de barras al guardar productos externos.

---

### 3.2 Tabla `AppPantryItems`
- **Clase C#:** `SmartPantry.Pantry.PantryItem : AuditedAggregateRoot<Guid>`
- **Propósito:** Inventario doméstico privado de cada usuario (RF-10, RF-11, RF-13, RF-14).

| Columna (SQL / C#) | Propiedad JSON | Tipo SQL Server | Tipo C# | Null | Default / Constraint | Estado Esquema |
|---|---|---|---|---|---|---|
| `Id` | `id` | `uniqueidentifier` | `Guid` | `NOT NULL` | `PK_AppPantryItems` | `ACTIVO` |
| `UserId` | `userId` | `uniqueidentifier` | `Guid` | `NOT NULL` | `FK` lógica a `AbpUsers(Id)`, `UserId <> Guid.Empty` | `ACTIVO` |
| `ProductId` | `productId` | `uniqueidentifier` | `Guid` | `NOT NULL` | `FK_AppPantryItems_AppProducts_ProductId` (`ON DELETE CASCADE`) | `ACTIVO` |
| `State` | `state` | `int` | `PantryItemState` | `NOT NULL` | Default `0` (`Active=0`, `Consumed=1`, `Discarded=2`) | `ACTIVO` |
| `ExpirationDate` | `expirationDate` | `datetime2` | `DateTime?` | `NULL` | Fecha civil truncada a `00:00:00` (`1900-01-01` a `2100-12-31`) | `ACTIVO` |
| `Quantity` | `quantity` | `decimal(18,2)` | `decimal?` | `NULL` | `CHECK (Quantity >= 0 OR Quantity IS NULL)` | `ACTIVO` |
| `Unit` | `unit` | `nvarchar(32)` | `string?` | `NULL` | Máx. `32` caracteres (`MaxUnitLength`) | `ACTIVO` |
| `PrivateNotes` | `privateNotes` | `nvarchar(500)` | `string?` | `NULL` | Máx. `500` caracteres (`MaxPrivateNotesLength = 500`) | `MIGRACIÓN PENDIENTE` |
| `CreationTime` | `creationTime` | `datetime2` | `DateTime` | `NOT NULL` | Poblado automáticamente por `IAuditPropertySetter` | `ACTIVO` |
| `CreatorId` | `creatorId` | `uniqueidentifier` | `Guid?` | `NULL` | Poblado automáticamente desde `ICurrentUser.Id` | `ACTIVO` |
| `LastModificationTime` | `lastModificationTime` | `datetime2` | `DateTime?` | `NULL` | Actualizado automáticamente en cada `UpdateAsync` | `ACTIVO` |
| `LastModifierId` | `lastModifierId` | `uniqueidentifier` | `Guid?` | `NULL` | Usuario que ejecutó la última modificación | `ACTIVO` |
| `ExtraProperties` | `extraProperties` | `nvarchar(max)` | `ExtraPropertyDictionary` | `NOT NULL` | JSON extensible de ABP (`"{}"`) | `ACTIVO` |
| `ConcurrencyStamp` | `concurrencyStamp` | `nvarchar(40)` | `string` | `NOT NULL` | Token de concurrencia optimista (`[ConcurrencyCheck]`) | `ACTIVO` |

**Índices de `AppPantryItems`:**
- `PK_AppPantryItems (Id)` — Clustered Index primario.
- `IX_AppPantryItems_ProductId (ProductId)` (`ACTIVO`) — Optimiza joins e integridad referencial con `AppProducts`.
- `IX_AppPantryItems_UserId_CreationTime (UserId, CreationTime DESC)` (`MIGRACIÓN PENDIENTE`) — Acelera directamente `PantryAppService.GetListAsync` (que filtra por `UserId == currentUserId` y ordena por `CreationTime desc` por defecto), evitando *Full Table Scans* a medida que crece la tabla multi-usuario.
- `IX_AppPantryItems_State_ExpirationDate (State, ExpirationDate)` (`MIGRACIÓN PENDIENTE`) — Permite que `PantryExpirationManager` localice rápidamente ítems activos con fecha de vencimiento definida.

---

### 3.3 Tabla `AppPantryItemWarnings`
- **Clase C#:** `SmartPantry.Pantry.PantryItemWarning : AuditedAggregateRoot<Guid>`
- **Propósito:** Almacenamiento idempotente de advertencias por vencimiento o preferencia alimentaria asociadas a un ítem de despensa (RF-12, RF-13, RF-15).

| Columna (SQL / C#) | Propiedad JSON | Tipo SQL Server | Tipo C# | Null | Default / Constraint | Estado Esquema |
|---|---|---|---|---|---|---|
| `Id` | `id` | `uniqueidentifier` | `Guid` | `NOT NULL` | `PK_AppPantryItemWarnings` | `ACTIVO` |
| `PantryItemId` | `pantryItemId` | `uniqueidentifier` | `Guid` | `NOT NULL` | `FK_AppPantryItemWarnings_AppPantryItems_PantryItemId` (`ON DELETE CASCADE`) | `ACTIVO` |
| `WarningType` | `warningType` | `nvarchar(64)` | `string` | `NOT NULL` | Valores canónicos: `"Expiration"`, `"DietaryPreference"` | `ACTIVO` |
| `Message` | `message` | `nvarchar(512)` | `string` | `NOT NULL` | Descripción informativa localizada/estructurada, máx. `512` | `ACTIVO` |
| `IsActive` | `isActive` | `bit` | `bool` | `NOT NULL` | Default `1` (`true` = vigente, `false` = desactivada/resuelta) | `ACTIVO` |
| `CreationTime` | `creationTime` | `datetime2` | `DateTime` | `NOT NULL` | Timestamp de primera detección | `ACTIVO` |
| `CreatorId` | `creatorId` | `uniqueidentifier` | `Guid?` | `NULL` | `NULL` cuando la genera `ExpirationWarningWorker` | `ACTIVO` |
| `LastModificationTime` | `lastModificationTime` | `datetime2` | `DateTime?` | `NULL` | Timestamp de última actualización/reactivación/desactivación | `ACTIVO` |
| `LastModifierId` | `lastModifierId` | `uniqueidentifier` | `Guid?` | `NULL` | `NULL` en ejecución de worker sin usuario | `ACTIVO` |
| `ExtraProperties` | `extraProperties` | `nvarchar(max)` | `ExtraPropertyDictionary` | `NOT NULL` | JSON extensible de ABP | `ACTIVO` |
| `ConcurrencyStamp` | `concurrencyStamp` | `nvarchar(40)` | `string` | `NOT NULL` | Token de concurrencia optimista | `ACTIVO` |

**Índices de `AppPantryItemWarnings`:**
- `PK_AppPantryItemWarnings (Id)` — Clustered Index primario.
- `IX_AppPantryItemWarnings_PantryItemId_WarningType (PantryItemId, WarningType) UNIQUE` (`ACTIVO`) — **Índice crítico de idempotencia (RF-15):** impide estructuralmente que dos corridas del worker o llamadas concurrentes inserten más de una advertencia del mismo tipo para un mismo ítem de despensa.

---

### 3.4 Tabla `AppUserDietaryPreferences`
- **Clase C#:** `SmartPantry.Preferences.UserDietaryPreference : AuditedAggregateRoot<Guid>`
- **Propósito:** Persistencia de términos alimentarios/alérgenos vigilados y preferencia visual de alertas por usuario (RF-03, RF-12).

| Columna (SQL / C#) | Propiedad JSON | Tipo SQL Server | Tipo C# | Null | Default / Constraint | Estado Esquema |
|---|---|---|---|---|---|---|
| `Id` | `id` | `uniqueidentifier` | `Guid` | `NOT NULL` | `PK_AppUserDietaryPreferences` | `MIGRACIÓN PENDIENTE` |
| `UserId` | `userId` | `uniqueidentifier` | `Guid` | `NOT NULL` | `UNIQUE`, `FK` a `AbpUsers(Id)` (`ON DELETE CASCADE`) | `MIGRACIÓN PENDIENTE` |
| `WatchedTerms` | `watchedTerms` (array en DTO) | `nvarchar(1024)` | `string` | `NOT NULL` | Términos normalizados en minúsculas sin acentos separados por `;` (máx. 30 ítems, `2..64` chars c/u) | `MIGRACIÓN PENDIENTE` |
| `ShowAlertsOnScreen` | `showAlertsOnScreen` | `bit` | `bool` | `NOT NULL` | Default `1` (`true`) | `MIGRACIÓN PENDIENTE` |
| `CreationTime` | `creationTime` | `datetime2` | `DateTime` | `NOT NULL` | Timestamp de creación | `MIGRACIÓN PENDIENTE` |
| `CreatorId` | `creatorId` | `uniqueidentifier` | `Guid?` | `NULL` | Propietario autenticado | `MIGRACIÓN PENDIENTE` |
| `LastModificationTime` | `lastModificationTime` | `datetime2` | `DateTime?` | `NULL` | Última edición de preferencias | `MIGRACIÓN PENDIENTE` |
| `LastModifierId` | `lastModifierId` | `uniqueidentifier` | `Guid?` | `NULL` | Propietario autenticado | `MIGRACIÓN PENDIENTE` |
| `ExtraProperties` | `extraProperties` | `nvarchar(max)` | `ExtraPropertyDictionary` | `NOT NULL` | JSON extensible de ABP | `MIGRACIÓN PENDIENTE` |
| `ConcurrencyStamp` | `concurrencyStamp` | `nvarchar(40)` | `string` | `NOT NULL` | Token de concurrencia optimista | `MIGRACIÓN PENDIENTE` |

**Índices de `AppUserDietaryPreferences`:**
- `PK_AppUserDietaryPreferences (Id)` — Clustered Index primario.
- `IX_AppUserDietaryPreferences_UserId (UserId) UNIQUE` — Garantiza cardinalidad exacta `1:1` por usuario y permite resolución instantánea (`Index Seek`) en cada consulta o guardado de producto.

---

### 3.5 Tabla `AppExternalApiCallLogs`
- **Clase C#:** `SmartPantry.ExternalProductCatalog.ExternalApiCallLog : CreationAuditedAggregateRoot<Guid>`
- **Propósito:** Registro inmutable (*append-only*) de telemetría de llamadas hacia Open Food Facts para métricas administrativas y observabilidad (HU-05, Regla de Negocio 5).

| Columna (SQL / C#) | Propiedad JSON | Tipo SQL Server | Tipo C# | Null | Default / Constraint | Estado Esquema |
|---|---|---|---|---|---|---|
| `Id` | `id` | `uniqueidentifier` | `Guid` | `NOT NULL` | `PK_AppExternalApiCallLogs` | `MIGRACIÓN PENDIENTE` |
| `OperationType` | `operationType` | `nvarchar(32)` | `string` | `NOT NULL` | `CHECK (OperationType IN ('BarcodeLookup', 'TextSearch'))` | `MIGRACIÓN PENDIENTE` |
| `OutcomeStatus` | `outcomeStatus` | `nvarchar(32)` | `string` | `NOT NULL` | `'Found'`, `'NotFound'`, `'RateLimitExceeded'`, `'ServiceUnavailable'` | `MIGRACIÓN PENDIENTE` |
| `HttpStatusCode` | `httpStatusCode` | `int` | `int?` | `NULL` | Código HTTP devuelto por Open Food Facts (`200`, `404`, `429`, `503` o `NULL` en falla de red/timeout) | `MIGRACIÓN PENDIENTE` |
| `DurationMs` | `durationMs` | `int` | `int` | `NOT NULL` | `CHECK (DurationMs >= 0)` — Tiempo medido con `Stopwatch` | `MIGRACIÓN PENDIENTE` |
| `QueryTermLength` | `queryTermLength` | `int` | `int` | `NOT NULL` | `CHECK (QueryTermLength >= 0)` — Longitud de la consulta (sin guardar secretos ni payload completo) | `MIGRACIÓN PENDIENTE` |
| `CreationTime` | `creationTime` | `datetime2` | `DateTime` | `NOT NULL` | Timestamp UTC exacto del evento | `MIGRACIÓN PENDIENTE` |
| `CreatorId` | `creatorId` | `uniqueidentifier` | `Guid?` | `NULL` | Usuario autenticado que originó la búsqueda | `MIGRACIÓN PENDIENTE` |
| `ExtraProperties` | `extraProperties` | `nvarchar(max)` | `ExtraPropertyDictionary` | `NOT NULL` | JSON extensible de ABP | `MIGRACIÓN PENDIENTE` |
| `ConcurrencyStamp` | `concurrencyStamp` | `nvarchar(40)` | `string` | `NOT NULL` | Token estándar de agregado ABP | `MIGRACIÓN PENDIENTE` |

**Índices de `AppExternalApiCallLogs`:**
- `PK_AppExternalApiCallLogs (Id)` — Clustered Index primario.
- `IX_AppExternalApiCallLogs_CreationTime_OperationType_OutcomeStatus (CreationTime DESC, OperationType, OutcomeStatus)` — Índice cubriente (*covering index*) diseñado para que las agregaciones `COUNT`, `AVG(DurationMs)` y desgloses por estado en el panel administrativo se resuelvan mediante *Index Range Scan* sobre rangos de fechas sin leer páginas innecesarias.

---

## 4. Diccionario de Enumeraciones Persistidas y de Dominio

| Enumeración (`Domain.Shared`) | Valores Enteros Exactos | Uso |
|---|---|---|
| `ItemCategory` | `0 = Undefined` *(inválido para persistir en `Product`)*, `1 = Dairy`, `2 = Bakery`, `3 = Beverages`, `4 = CannedGoods`, `5 = DryGoods`, `6 = FruitsAndVegetables`, `7 = MeatAndFish`, `8 = Snacks`, `9 = Condiments`, `10 = Frozen`, `11 = Cleaning`, `12 = Other` | Clasificación en `AppProducts.Category` y cálculo de umbral en `PantryExpirationManager`. |
| `PantryItemState` | `0 = Active`, `1 = Consumed`, `2 = Discarded` | Ciclo de vida en `AppPantryItems.State`. |
| `ExternalProductQueryStatus` | `0 = Found`, `1 = NotFound`, `2 = RateLimitExceeded`, `3 = ServiceUnavailable` | Resultado tipado de invocaciones al catálogo externo. |
| `DietaryVerificationStatus` | `0 = Compatible`, `1 = Warning`, `2 = Unverifiable`, `3 = NotConfigured` | Resultado del motor `DietaryWarningManager` expuesto en DTOs de catálogo y despensa. |

---

## 5. Estrategia de Auditoría, Concurrencia Optimista y Borrado (Soft vs Hard Delete)

### 5.1 Trazabilidad Temporal y de Autoría (Auditing)
ABP Framework inyecta automáticamente las marcas de tiempo en UTC (`Clock.Now`) y el identificador del usuario autenticado (`ICurrentUser.Id`) al invocar `SaveChangesAsync` en `SmartPantryDbContext`:
- **`AuditedAggregateRoot<Guid>` (`AppPantryItems`, `AppPantryItemWarnings`, `AppUserDietaryPreferences`):** Mantiene `CreationTime`, `CreatorId`, `LastModificationTime` y `LastModifierId`. Cuando `ExpirationWarningWorker` actualiza `AppPantryItemWarnings` fuera de un contexto HTTP, `LastModificationTime` se actualiza con la hora UTC del servidor y `LastModifierId` queda en `NULL`.
- **`CreationAuditedAggregateRoot<Guid>` (`AppExternalApiCallLogs`):** Solo registra `CreationTime` y `CreatorId`, garantizando semántica inmutable de bitácora de eventos.

### 5.2 Control de Concurrencia Optimista
Todos los agregados heredados de `AggregateRoot<Guid>` incluyen la columna `ConcurrencyStamp nvarchar(40) NOT NULL` configurada con `.IsConcurrencyToken()`. Si dos peticiones simultáneas intentan mutar el mismo `PantryItem` o `UserDietaryPreference`, EF Core detecta la discrepancia de `ConcurrencyStamp` y lanza `AbpDbConcurrencyException` (`HTTP 409 Conflict`), evitando sobreescrituras silenciosas (*Lost Updates*).

### 5.3 Política Explícita de Soft Delete vs Borrado Físico
El sistema distingue intencionalmente el ciclo de baja según la naturaleza legal y funcional de cada entidad:
1. **Cuentas de Usuario (`AbpUsers` — Soft Delete activo):** Implementa `ISoftDelete` (`FullAuditedAggregateRoot<Guid>`). Las columnas `IsDeleted (bit)`, `DeleterId` y `DeletionTime` aseguran que deshabilitar o dar de baja un usuario nunca rompa referencias históricas de auditoría; el filtro global de EF Core en ABP excluye automáticamente filas con `IsDeleted = 1`.
2. **Ítems de Despensa (`AppPantryItems` — Conservación por Estado + Hard Delete Explícito):** Conforme a la **Regla de Negocio 6** documentada en el proyecto:
   - El consumo cotidiano **no borra** el registro: ejecuta transición de estado a `PantryItemState.Consumed (1)`, preservándolo visible como historial.
   - Cuando el propietario confirma explícitamente la acción **"Eliminar"** (`DELETE /api/app/pantry/{id}`), se aplica **borrado físico definitivo** (`Hard Delete`) en `AppPantryItems` y sus `AppPantryItemWarnings` dependientes, respetando el derecho del usuario a suprimir de forma permanente sus registros y notas privadas.
3. **Advertencias (`AppPantryItemWarnings` — Desactivación Lógica Idempotente):** Nunca se eliminan durante la operación normal del worker; transicionan entre `IsActive = 1` e `IsActive = 0` preservando la fila única `(PantryItemId, WarningType)`.
