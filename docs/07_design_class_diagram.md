# 07. Diagrama de Clases de Diseño (DCD) — SmartPantry

> **Propósito de este documento:** Presentar el Diagrama de Clases de Diseño completo y formal en sintaxis Mermaid (`classDiagram`) bajo el estándar **UML 2.5**, organizando las entidades y componentes en sus capas físicas de arquitectura e identificando visibilidad, tipos de datos, métodos, cardinalidades y relaciones orientadas a objetos.

---

## 1. Decisiones de Diseño y Criterios UML 2.5

1. **Aislamiento en Cajas por Capa Física (`namespace`):** Cada clase, interfaz, agregado y enumeración se encuentra agrupada en el paquete o namespace correspondiente a la solución ABP:
   - `Capa_1_Domain_Shared`: Enumeraciones y tipos compartidos (`ItemCategory`, `PantryItemState`, `DietaryVerificationStatus`).
   - `Capa_2_Domain`: Entidades del modelo de dominio (`Product`, `PantryItem`, `PantryItemWarning`, `UserDietaryPreference`, `ExternalApiCallLog`) y servicios de dominio (`PantryExpirationManager`, `DietaryWarningManager`).
   - `Capa_3_Application_Contracts`: DTOs de transferencia, enumeraciones de estado de consulta e interfaces de servicios de aplicación (`IPantryAppService`, `IProductAppService`, `IExternalProductCatalogAppService`, `IDietaryPreferenceAppService`, `IIntegrationMetricsAppService`).
   - `Capa_4_Application`: Clases coordinadoras de casos de uso (`AppService`), puerto de salida desacoplado (`IExternalProductCatalogClient`) y workers en segundo plano (`ExpirationWarningWorker`).
   - `Capa_5_EntityFrameworkCore`: Contexto de base de datos (`SmartPantryDbContext`).
   - `Capa_6_HttpApi_Host`: Adaptador HTTP para Open Food Facts (`OpenFoodFactsProductCatalogClient`) y opciones de configuración.
   - `ABP_Framework_Base`: Clases base abstractas provistas por ABP Framework (`BasicAggregateRoot`, `AuditedAggregateRoot`, etc.).

2. **Inversión de Dependencias (Puerto y Adaptador):**
   - La capa de aplicación define el contrato de salida `IExternalProductCatalogClient`.
   - La capa de host implementa dicho contrato mediante `OpenFoodFactsProductCatalogClient`, consumiendo `HttpClient` administrado por `IHttpClientFactory`. De este modo, la lógica de negocio y aplicación se aísla de librerías HTTP externas y se testea con mocks (`NSubstitute`).

3. **Distinción entre Composición y Asociación:**
   - **Composición (`*--`):** `PantryItem "1" *-- "0..2" PantryItemWarning` refleja que las advertencias no tienen sentido sin el ítem de despensa que las origina y se eliminan en cascada (`ON DELETE CASCADE`). De igual manera, `IdentityUser "1" *-- "0..1" UserDietaryPreference` vincula de forma privada y unívoca el perfil de preferencias con el usuario.
   - **Asociación navegable (`-->`):** `PantryItem "0..*" --> "1" Product` modela que múltiples ítems de despensa privados de distintos usuarios referencian al mismo producto maestro del catálogo interno sin dependencia de ciclo de vida existencial.

4. **Encapsulamiento y Protección de Invariantes (DDD Táctico):**
   - Atributos privados (`-`) con métodos explícitos de mutación (`+Set*`, `+Update`, `+Consume`, `+Reactivate`, `+Deactivate`).
   - Constructor protegido sin parámetros (`#`) para hidratación de Entity Framework Core.
   - Constructor público con validación de invariantes no nulas y rangos permitidos.

---

## 2. Diagrama de Clases en Sintaxis Mermaid

```mermaid
classDiagram
    direction TB

    namespace ABP_Framework_Base {
        class BasicAggregateRoot_Guid {
            <<abstract>>
            +Id : Guid
        }

        class CreationAuditedAggregateRoot_Guid {
            <<abstract>>
            +Id : Guid
            +CreationTime : DateTime
            +CreatorId : Nullable~Guid~
            +ConcurrencyStamp : String
        }

        class AuditedAggregateRoot_Guid {
            <<abstract>>
            +LastModificationTime : Nullable~DateTime~
            +LastModifierId : Nullable~Guid~
        }

        class IdentityUser {
            -userName : String
            -email : String
            -name : Nullable~String~
            -surname : Nullable~String~
            -isActive : Boolean
            -isDeleted : Boolean
        }

        class DomainService {
            <<abstract>>
        }

        class SmartPantryAppService {
            <<abstract>>
            #CurrentUser : ICurrentUser
            #GuidGenerator : IGuidGenerator
        }

        class AsyncPeriodicBackgroundWorkerBase {
            <<abstract>>
            #Timer : AbpAsyncTimer
            #ServiceScopeFactory : IServiceScopeFactory
            #DoWorkAsync(workerContext : PeriodicBackgroundWorkerContext) Task
        }
    }

    namespace Capa_1_Domain_Shared {
        class ItemCategory {
            <<enumeration>>
            Undefined
            Dairy
            Bakery
            Beverages
            CannedGoods
            DryGoods
            FruitsAndVegetables
            MeatAndFish
            Snacks
            Condiments
            Frozen
            Cleaning
            Other
        }

        class PantryItemState {
            <<enumeration>>
            Active
            Consumed
            Discarded
        }

        class DietaryVerificationStatus {
            <<enumeration>>
            Compatible
            Warning
            Unverifiable
            NotConfigured
        }
    }

    namespace Capa_2_Domain {
        class Product {
            -barcode : String
            -name : String
            -category : ItemCategory
            -brand : Nullable~String~
            -quantity : Nullable~String~
            -ingredients : Nullable~String~
            -allergens : Nullable~String~
            -externalCategories : Nullable~String~
            -imageUrl : Nullable~String~
            -nutriScore : Nullable~String~
            -novaGroup : Nullable~Int32~
            #Product()
            +Product(id : Guid, barcode : String, name : String, category : ItemCategory, brand : Nullable~String~, quantity : Nullable~String~, ingredients : Nullable~String~, allergens : Nullable~String~)
            +Update(name : String, category : ItemCategory, brand : Nullable~String~, quantity : Nullable~String~, ingredients : Nullable~String~, allergens : Nullable~String~) void
            +SetBarcode(barcode : String) void
            +SetName(name : String) void
            +SetCategory(category : ItemCategory) void
            +SetBrand(brand : Nullable~String~) void
            +SetQuantity(quantity : Nullable~String~) void
            +SetIngredients(ingredients : Nullable~String~) void
            +SetAllergens(allergens : Nullable~String~) void
            +SetNutritionalMetadata(imageUrl : Nullable~String~, nutriScore : Nullable~String~, novaGroup : Nullable~Int32~, externalCategories : Nullable~String~) void
        }

        class PantryItem {
            -userId : Guid
            -productId : Guid
            -state : PantryItemState
            -expirationDate : Nullable~DateTime~
            -quantity : Nullable~Decimal~
            -unit : Nullable~String~
            -privateNotes : Nullable~String~
            #PantryItem()
            +PantryItem(id : Guid, userId : Guid, productId : Guid, expirationDate : Nullable~DateTime~, state : PantryItemState, quantity : Nullable~Decimal~, unit : Nullable~String~, privateNotes : Nullable~String~)
            +SetExpirationDate(expirationDate : Nullable~DateTime~) void
            +SetQuantity(quantity : Nullable~Decimal~) void
            +SetUnit(unit : Nullable~String~) void
            +SetPrivateNotes(notes : Nullable~String~) void
            +Consume() void
            +Discard() void
            +SetActive() void
        }

        class PantryItemWarning {
            -pantryItemId : Guid
            -warningType : String
            -message : String
            -isActive : Boolean
            #PantryItemWarning()
            +PantryItemWarning(id : Guid, pantryItemId : Guid, warningType : String, message : String, isActive : Boolean)
            +UpdateMessage(message : String) void
            +Deactivate() void
            +Reactivate(message : String) void
        }

        class UserDietaryPreference {
            -userId : Guid
            -watchedTerms : String
            -showAlertsOnScreen : Boolean
            #UserDietaryPreference()
            +UserDietaryPreference(id : Guid, userId : Guid, watchedTerms : IEnumerable~String~, showAlertsOnScreen : Boolean)
            +GetWatchedTermsList() IReadOnlyList~String~
            +SetWatchedTerms(terms : IEnumerable~String~) void
            +SetShowAlertsOnScreen(show : Boolean) void
        }

        class ExternalApiCallLog {
            -operationType : String
            -outcomeStatus : String
            -httpStatusCode : Nullable~Int32~
            -durationMs : Int32
            -queryTermLength : Int32
            #ExternalApiCallLog()
            +ExternalApiCallLog(id : Guid, operationType : String, outcomeStatus : String, httpStatusCode : Nullable~Int32~, durationMs : Int32, queryTermLength : Int32)
        }

        class PantryExpirationManager {
            -_pantryItemRepository : IRepository~PantryItem~
            -_productRepository : IRepository~Product~
            -_warningRepository : IRepository~PantryItemWarning~
            -_guidGenerator : IGuidGenerator
            +GetArgentinaLocalDate() DateTime
            +GetThresholdDays(category : ItemCategory) Int32
            +ProcessExpirationsAsync(referenceLocalDate : Nullable~DateTime~, cancellationToken : CancellationToken) Task~Int32~
            -UpsertWarningAsync(pantryItemId : Guid, existingWarning : Nullable~PantryItemWarning~, message : String, cancellationToken : CancellationToken) Task
        }

        class DietaryWarningManager {
            -_warningRepository : IRepository~PantryItemWarning~
            -_guidGenerator : IGuidGenerator
            +Evaluate(ingredients : Nullable~String~, allergens : Nullable~String~, preference : Nullable~UserDietaryPreference~) DietaryEvaluationResult
            +SyncItemDietaryWarningAsync(item : PantryItem, product : Product, preference : Nullable~UserDietaryPreference~) Task
            +NormalizeForComparison(value : Nullable~String~) String
        }
    }

    namespace Capa_3_Application_Contracts {
        class ExternalProductQueryStatus {
            <<enumeration>>
            Found
            NotFound
            RateLimitExceeded
            ServiceUnavailable
        }

        class DietaryEvaluationDto {
            +VerificationStatus : DietaryVerificationStatus
            +MatchedTerms : List~String~
            +InformationalMessage : Nullable~String~
        }

        class ExternalProductDto {
            +Barcode : String
            +Name : Nullable~String~
            +Brand : Nullable~String~
            +Category : Nullable~String~
            +Quantity : Nullable~String~
            +Ingredients : Nullable~String~
            +Allergens : Nullable~String~
            +ImageUrl : Nullable~String~
            +NutriScore : Nullable~String~
            +NovaGroup : Nullable~Int32~
            +InternalProductId : Nullable~Guid~
            +DietaryEvaluation : DietaryEvaluationDto
        }

        class ExternalProductResultDto {
            +Status : ExternalProductQueryStatus
            +Product : Nullable~ExternalProductDto~
        }

        class ProductDto {
            +Id : Guid
            +Barcode : String
            +Name : String
            +Category : ItemCategory
            +Brand : Nullable~String~
            +Quantity : Nullable~String~
            +Ingredients : Nullable~String~
            +Allergens : Nullable~String~
            +ExternalCategories : Nullable~String~
            +ImageUrl : Nullable~String~
            +NutriScore : Nullable~String~
            +NovaGroup : Nullable~Int32~
        }

        class PantryItemDto {
            +Id : Guid
            +UserId : Guid
            +ProductId : Guid
            +ProductName : String
            +Quantity : Nullable~Decimal~
            +Unit : Nullable~String~
            +ExpirationDate : Nullable~DateTime~
            +State : PantryItemState
            +PrivateNotes : Nullable~String~
            +IsExpired : Boolean
            +IsExpiringSoon : Boolean
            +ActiveWarnings : List~PantryWarningDto~
        }

        class DietaryPreferenceDto {
            +UserId : Guid
            +WatchedTerms : List~String~
            +ShowAlertsOnScreen : Boolean
            +LastModificationTime : Nullable~DateTime~
        }

        class IntegrationMetricsSummaryDto {
            +FromUtc : DateTime
            +ToUtc : DateTime
            +TotalCalls : Int32
            +SuccessfulCalls : Int32
            +NotFoundCalls : Int32
            +RateLimitedCalls : Int32
            +ServiceUnavailableCalls : Int32
            +AverageDurationMs : Double
            +MaxDurationMs : Int32
        }

        class IExternalProductCatalogAppService {
            <<interface>>
            +GetByBarcodeAsync(input : GetExternalProductByBarcodeDto) Task~ExternalProductResultDto~
            +SearchAsync(input : SearchExternalProductsDto) Task~ExternalProductSearchResultDto~
        }

        class IProductAppService {
            <<interface>>
            +GetAsync(id : Guid) Task~ProductDto~
            +GetListAsync(input : PagedAndSortedResultRequestDto) Task~PagedResultDto_ProductDto~
            +CreateAsync(input : CreateProductDto) Task~ProductDto~
            +UpdateAsync(id : Guid, input : UpdateProductDto) Task~ProductDto~
            +DeleteAsync(id : Guid) Task
            +SaveFromExternalAsync(input : SaveExternalProductDto) Task~ProductDto~
        }

        class IPantryAppService {
            <<interface>>
            +GetAsync(id : Guid) Task~PantryItemDto~
            +GetListAsync(input : PagedAndSortedResultRequestDto) Task~PagedResultDto_PantryItemDto~
            +CreateAsync(input : CreatePantryItemDto) Task~PantryItemDto~
            +UpdateAsync(id : Guid, input : UpdatePantryItemDto) Task~PantryItemDto~
            +ConsumeAsync(id : Guid) Task~PantryItemDto~
            +DeleteAsync(id : Guid) Task
        }

        class IDietaryPreferenceAppService {
            <<interface>>
            +GetMyPreferencesAsync() Task~DietaryPreferenceDto~
            +UpdateMyPreferencesAsync(input : UpdateDietaryPreferenceDto) Task~DietaryPreferenceDto~
        }

        class IIntegrationMetricsAppService {
            <<interface>>
            +GetSummaryAsync(fromUtc : Nullable~DateTime~, toUtc : Nullable~DateTime~) Task~IntegrationMetricsSummaryDto~
        }
    }

    namespace Capa_4_Application {
        class IExternalProductCatalogClient {
            <<interface>>
            +GetByBarcodeAsync(barcode : String) Task~ExternalProductDto~
            +SearchByTextAsync(query : String, skipCount : Int32, maxResultCount : Int32) Task~PagedExternalProducts~
        }

        class ExternalProductCatalogAppService {
            -_externalClient : IExternalProductCatalogClient
            -_preferenceRepository : IRepository~UserDietaryPreference~
            -_productRepository : IRepository~Product~
            -_dietaryWarningManager : DietaryWarningManager
            +GetByBarcodeAsync(input : GetExternalProductByBarcodeDto) Task~ExternalProductResultDto~
            +SearchAsync(input : SearchExternalProductsDto) Task~ExternalProductSearchResultDto~
        }

        class ProductAppService {
            -_productRepository : IRepository~Product~
            +CreateAsync(input : CreateProductDto) Task~ProductDto~
            +UpdateAsync(id : Guid, input : UpdateProductDto) Task~ProductDto~
            +SaveFromExternalAsync(input : SaveExternalProductDto) Task~ProductDto~
        }

        class PantryAppService {
            -_pantryItemRepository : IRepository~PantryItem~
            -_productRepository : IRepository~Product~
            -_warningRepository : IRepository~PantryItemWarning~
            -_preferenceRepository : IRepository~UserDietaryPreference~
            -_expirationManager : PantryExpirationManager
            -_dietaryWarningManager : DietaryWarningManager
            +GetAsync(id : Guid) Task~PantryItemDto~
            +GetListAsync(input : PagedAndSortedResultRequestDto) Task~PagedResultDto_PantryItemDto~
            +CreateAsync(input : CreatePantryItemDto) Task~PantryItemDto~
            +UpdateAsync(id : Guid, input : UpdatePantryItemDto) Task~PantryItemDto~
            +ConsumeAsync(id : Guid) Task~PantryItemDto~
            +DeleteAsync(id : Guid) Task
            -GetOwnedItemAsync(id : Guid) Task~PantryItem~
            -MapToDtoAsync(item : PantryItem) Task~PantryItemDto~
        }

        class DietaryPreferenceAppService {
            -_preferenceRepository : IRepository~UserDietaryPreference~
            -_pantryItemRepository : IRepository~PantryItem~
            -_productRepository : IRepository~Product~
            -_dietaryWarningManager : DietaryWarningManager
            +GetMyPreferencesAsync() Task~DietaryPreferenceDto~
            +UpdateMyPreferencesAsync(input : UpdateDietaryPreferenceDto) Task~DietaryPreferenceDto~
        }

        class IntegrationMetricsAppService {
            -_callLogRepository : IRepository~ExternalApiCallLog~
            +GetSummaryAsync(fromUtc : Nullable~DateTime~, toUtc : Nullable~DateTime~) Task~IntegrationMetricsSummaryDto~
        }

        class ExpirationWarningWorker {
            #DoWorkAsync(workerContext : PeriodicBackgroundWorkerContext) Task
        }
    }

    namespace Capa_5_EntityFrameworkCore {
        class SmartPantryDbContext {
            +Products : DbSet~Product~
            +PantryItems : DbSet~PantryItem~
            +PantryItemWarnings : DbSet~PantryItemWarning~
            +UserDietaryPreferences : DbSet~UserDietaryPreference~
            +ExternalApiCallLogs : DbSet~ExternalApiCallLog~
            +Users : DbSet~IdentityUser~
            #OnModelCreating(builder : ModelBuilder) void
        }
    }

    namespace Capa_6_HttpApi_Host {
        class OpenFoodFactsOptions {
            +BaseUrl : String
            +SearchBaseUrl : String
            +UserAgent : String
            +TimeoutSeconds : Int32
        }

        class OpenFoodFactsProductCatalogClient {
            -_httpClient : HttpClient
            -_callLogRepository : IRepository~ExternalApiCallLog~
            +GetByBarcodeAsync(barcode : String) Task~ExternalProductDto~
            +SearchByTextAsync(query : String, skipCount : Int32, maxResultCount : Int32) Task~PagedExternalProducts~
        }
    }

    %% Herencias de Framework y Dominio
    CreationAuditedAggregateRoot_Guid <|-- AuditedAggregateRoot_Guid
    BasicAggregateRoot_Guid <|-- Product
    AuditedAggregateRoot_Guid <|-- PantryItem
    AuditedAggregateRoot_Guid <|-- PantryItemWarning
    AuditedAggregateRoot_Guid <|-- UserDietaryPreference
    CreationAuditedAggregateRoot_Guid <|-- ExternalApiCallLog
    DomainService <|-- PantryExpirationManager
    DomainService <|-- DietaryWarningManager
    SmartPantryAppService <|-- ExternalProductCatalogAppService
    SmartPantryAppService <|-- ProductAppService
    SmartPantryAppService <|-- PantryAppService
    SmartPantryAppService <|-- DietaryPreferenceAppService
    SmartPantryAppService <|-- IntegrationMetricsAppService
    AsyncPeriodicBackgroundWorkerBase <|-- ExpirationWarningWorker

    %% Realización de Contratos e Inversión de Dependencias
    IExternalProductCatalogAppService <|.. ExternalProductCatalogAppService
    IProductAppService <|.. ProductAppService
    IPantryAppService <|.. PantryAppService
    IDietaryPreferenceAppService <|.. DietaryPreferenceAppService
    IIntegrationMetricsAppService <|.. IntegrationMetricsAppService
    IExternalProductCatalogClient <|.. OpenFoodFactsProductCatalogClient

    %% Composiciones, Asociaciones y Multiplicidades del Modelo de Dominio
    IdentityUser "1" *-- "0..1" UserDietaryPreference : dietaryPreference
    IdentityUser "1" --> "0..*" PantryItem : ownedItems
    IdentityUser "0..1" --> "0..*" ExternalApiCallLog : triggeredLogs
    PantryItem "0..*" --> "1" Product : referencedProduct
    PantryItem "1" *-- "0..2" PantryItemWarning : warnings
    Product --> "1" ItemCategory : category
    PantryItem --> "1" PantryItemState : state

    %% Relaciones entre Servicios de Dominio y Agregados
    PantryExpirationManager ..> PantryItem : evaluates
    PantryExpirationManager ..> Product : readsCategory
    PantryExpirationManager ..> PantryItemWarning : upsertsIdempotently
    DietaryWarningManager ..> UserDietaryPreference : contrastsTerms
    DietaryWarningManager ..> PantryItemWarning : syncsDietaryWarning
    DietaryWarningManager ..> DietaryVerificationStatus : computes

    %% Composición y Dependencias en Contratos DTO
    ExternalProductResultDto "1" --> "1" ExternalProductQueryStatus : status
    ExternalProductResultDto "1" o-- "0..1" ExternalProductDto : product
    ExternalProductDto "1" *-- "1" DietaryEvaluationDto : dietaryEvaluation
    DietaryEvaluationDto --> "1" DietaryVerificationStatus : verificationStatus

    %% Colaboración e Inyección en Capa de Aplicación y Worker
    ExternalProductCatalogAppService o-- "1" IExternalProductCatalogClient : externalClient
    ExternalProductCatalogAppService o-- "1" DietaryWarningManager : dietaryWarningManager
    ExternalProductCatalogAppService ..> ExternalProductResultDto : returns
    ProductAppService ..> Product : persistsUniqueBarcode
    ProductAppService ..> ProductDto : returns
    PantryAppService o-- "1" PantryExpirationManager : expirationManager
    PantryAppService o-- "1" DietaryWarningManager : dietaryWarningManager
    PantryAppService ..> PantryItem : enforcesOwnerIsolation
    PantryAppService ..> PantryItemDto : returns
    DietaryPreferenceAppService o-- "1" DietaryWarningManager : dietaryWarningManager
    DietaryPreferenceAppService ..> UserDietaryPreference : managesOwnProfile
    IntegrationMetricsAppService ..> ExternalApiCallLog : aggregatesMetrics
    IntegrationMetricsAppService ..> IntegrationMetricsSummaryDto : returns
    ExpirationWarningWorker ..> PantryExpirationManager : executesPeriodically

    %% Infraestructura de Persistencia y Cliente HTTP Externo
    OpenFoodFactsProductCatalogClient --> "1" OpenFoodFactsOptions : options
    OpenFoodFactsProductCatalogClient ..> ExternalApiCallLog : logsTelemetry
    SmartPantryDbContext o-- "0..*" Product : AppProducts
    SmartPantryDbContext o-- "0..*" PantryItem : AppPantryItems
    SmartPantryDbContext o-- "0..*" PantryItemWarning : AppPantryItemWarnings
    SmartPantryDbContext o-- "0..*" UserDietaryPreference : AppUserDietaryPreferences
    SmartPantryDbContext o-- "0..*" ExternalApiCallLog : AppExternalApiCallLogs
```

---

## 3. Matriz de Trazabilidad: Clases vs. Requerimientos Funcionales

| Clase / Componente | Capa | Requerimientos Satisfechos | Reglas de Negocio Asociadas |
|---|---|---|---|
| `Product` | `Domain` | RF-07, RF-08, RF-09 | BR-004 (Unicidad por Barcode) |
| `PantryItem` | `Domain` | RF-10, RF-11, RF-13, RF-14 | BR-003 (Owner Isolation), BR-006 (Consumo), BR-009 (Invariantes) |
| `PantryItemWarning` | `Domain` | RF-12, RF-13, RF-15 | BR-007 (Vencimientos), BR-008 (Idempotencia), BR-010 (Preferencia) |
| `UserDietaryPreference` | `Domain` | RF-03 | BR-002 (Cruce de alérgenos), BR-003 (Owner Isolation) |
| `ExternalApiCallLog` | `Domain` | HU-05 | BR-005 (Métricas sanitizadas) |
| `PantryExpirationManager` | `Domain` | RF-13, RF-15 | BR-007 (Cálculo temporal), BR-008 (Idempotencia worker) |
| `DietaryWarningManager` | `Domain` | RF-12 | BR-001 (Ética), BR-002 (Datos ausentes = Unverifiable) |
| `ExternalProductCatalogAppService` | `Application` | RF-05, RF-06, RF-07, RF-09, RF-12 | BR-001, BR-002 |
| `ProductAppService` | `Application` | RF-08 | BR-004 |
| `PantryAppService` | `Application` | RF-10, RF-11, RF-13, RF-14 | BR-003, BR-006, BR-007, BR-009, BR-010 |
| `DietaryPreferenceAppService` | `Application` | RF-03, RF-12 | BR-002, BR-003, BR-010 |
| `IntegrationMetricsAppService` | `Application` | HU-05 | BR-005 |
| `ExpirationWarningWorker` | `Application` | RF-15 | BR-008 |
| `OpenFoodFactsProductCatalogClient` | `HttpApi.Host` | RF-05, RF-06 | BR-005 |
| `SmartPantryDbContext` | `EntityFrameworkCore` | Todos (Persistencia) | Integridad referencial y restricciones UNIQUE |
