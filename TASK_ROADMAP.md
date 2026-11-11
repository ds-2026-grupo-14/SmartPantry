# TASK_ROADMAP.md — Plan Maestro de Implementación en 4 Bloques Independientes

> **Fuente Única de Verdad:** Todas las tareas de este roadmap derivan directamente de la suite técnica en `docs/00_overview_and_domain.md` .. `docs/06_security_and_auth.md`.  
> **Regla Operativa:** Al finalizar y verificar cada tarea con sus pruebas automatizadas en verde, actualizar su estado de `[ ]` a `[x]`.

---

## 0. Línea Base Auditada (Completado en `main` tras TP10)

Los siguientes requerimientos ya están cerrados y probados en el repositorio base; sirven como cimiento inmutable para los 4 bloques:
- [x] **RF-01 (Autenticación):** Login/Logout OIDC con OpenIddict, `[Authorize]` en AppServices y `authGuard` en Angular.
- [x] **RF-04 (Administración de usuarios):** Gestión de cuentas, roles y bloqueo vía `@abp/ng.identity` y `Volo.Abp.Identity`.
- [x] **RF-11 (Administrar despensa):** Listado paginado aislado por usuario (`403` ante terceros), edición, consumo con historial (`BR-006`) y baja física con confirmación en `PantryComponent`.
- [x] **RF-15 (Tarea periódica de vencimientos):** `ExpirationWarningWorker` y `PantryExpirationManager` idempotentes operando sin sesión de usuario.

---

## Matriz de Partición en 4 Bloques Independientes

Los requerimientos faltantes y parciales se dividen en **4 bloques verticales autónomos** (Dominio + EF Core + AppService + Tests Backend + Angular + Tests Vitest), diseñados para que 4 desarrolladores o agentes trabajen en paralelo minimizando conflictos de merge:

| Bloque | Nombre del Módulo Vertical | RFs / HUs Cubiertos | Namespace Backend Principal | Ruta Angular Principal |
|---|---|---|---|---|
| **Bloque 1** | Catálogo Externo, Búsqueda Dual, Detalle y Guardado Interno | **RF-05, RF-06, RF-07, RF-08, RF-09** *(HU-02, HU-03)* | `SmartPantry.ExternalProductCatalog` & `SmartPantry.Products` | `/catalog` (`angular/src/app/catalog/`) |
| **Bloque 2** | Perfil Alimentario, Motor de Advertencias y Cuenta | **RF-03, RF-12, RF-02** *(HU-01, HU-04)* | `SmartPantry.Preferences` | `/preferences` (`angular/src/app/preferences/`) |
| **Bloque 3** | Evolución de Mi Despensa: Alta UI, Vencimientos y Notas | **RF-10, RF-13, RF-14** *(HU-03, HU-04, HU-06)* | `SmartPantry.Pantry` | `/pantry` (`angular/src/app/pantry/`) |
| **Bloque 4** | Observabilidad Externa, Panel Admin, Rate Limit y Cobertura | **HU-05 (RN-5), RNF Seguridad y Calidad $\ge 70\%$** | `SmartPantry.IntegrationMetrics` | `/admin/integration-metrics` (`angular/src/app/admin/`) |

### Contrato de Independencia entre Bloques (Zero-Collision Rules)
1. **Aislamiento de carpetas:** Ningún bloque modifica componentes Angular ni `AppServices` de otro bloque durante su desarrollo principal.
2. **Desacoplamiento mediante contratos fijos (`docs/04`):**
   - El **Bloque 1** define `DietaryEvaluationDto` en `Application.Contracts` (con valor por defecto `NotConfigured` si la tabla de preferencias aún no fue mergeada). Cuando ambos bloques convergen en `main`, `ExternalProductCatalogAppService` delega el cálculo en `DietaryWarningManager` del **Bloque 2**.
   - El **Bloque 3** consume `IProductAppService.GetListAsync` (ya existente en `main`) para el selector del modal *"Agregar a mi despensa"*, sin bloquearse esperando la pantalla `/catalog` del **Bloque 1**.
   - El **Bloque 4** instrumenta `OpenFoodFactsProductCatalogClient` mediante una interfaz liviana `IExternalApiCallLogger` registrada por DI, evitando acoplar el dominio de métricas con la lógica de negocio del catálogo.
3. **Puntos compartidos al integrar en `main`:** En `SmartPantryDbContext.cs`, `es.json`/`en.json`, `app.routes.ts` y `route.provider.ts`, cada bloque agrega sus bloques al final de la sección correspondiente y regenera `abp generate-proxy -t ng` sobre `main` actualizado.

---

## BLOQUE 1: Catálogo Externo, Búsqueda Dual, Detalle Nutricional y Guardado Interno
**Alcance:** `RF-05`, `RF-06`, `RF-07`, `RF-08`, `RF-09`  
**Documentos rectores:** `docs/02_data_models_and_schemas.md` (§3.1), `docs/03_business_rules_and_workflows.md` (`BR-001`, `BR-002`, `BR-004`), `docs/04_api_contracts_and_integrations.md` (§2.1, §2.2, §3).

### Backlog Secuencial del Bloque 1
- [x] **B1-T01 — Ampliación del Dominio `Product` para Ficha Nutricional Completa (`RF-07`)**
  - **Acción:** Extender `ProductConsts` (`MaxExternalCategoriesLength = 256`, `MaxImageUrlLength = 512`, `MaxNutriScoreLength = 1`) y `Product.cs` con las propiedades opcionales `ExternalCategories`, `ImageUrl`, `NutriScore` (`A`..`E`) y `NovaGroup` (`1`..`4`), protegiendo sus invariantes en métodos `Set*` y `Update`.
  - **Criterio de cierre:** Pruebas unitarias en `ProductDomainTests.cs` cubriendo valores válidos, nulos y rechazos fuera de rango (`NutriScore = "Z"`, `NovaGroup = 5`).

- [x] **B1-T02 — Migración EF Core de `AppProducts` (`RF-07`, `RF-08`)**
  - **Acción:** Configurar las nuevas columnas y restricciones en `SmartPantryDbContext.cs`, generar migración `AddProductNutritionalDetails` y verificar su aplicación limpia con `SmartPantry.DbMigrator`.
  - **Criterio de cierre:** Esquema actualizado en SQL Server manteniendo intacto el índice único `IX_AppProducts_Barcode`.

- [ ] **B1-T03 — Persistencia Interna Idempotente desde Catálogo Externo (`RF-08`, `BR-004`)**
  - **Acción:** Definir `SaveExternalProductDto` e implementar `ProductAppService.SaveFromExternalAsync` (`POST /api/app/product/save-from-external`): si el `Barcode` no existe en `AppProducts`, crea el `Product`; si ya existe, completa metadatos faltantes y retorna el `ProductDto` existente sin duplicar filas.
  - **Criterio de cierre:** Test de integración obligatorio en `EfCoreProductAppServiceTests.cs` verificando que guardar dos veces el mismo código de barras deja exactamente 1 registro en `AppProducts`.

- [ ] **B1-T04 — Cliente HTTP Search-a-licious (`POST`) y Ampliación v3 (`RF-05`, `RF-06`, `RF-07`)**
  - **Acción:** Agregar `SearchBaseUrl` (`https://search.openfoodfacts.org/`) en `OpenFoodFactsOptions` y `appsettings.json`. Ampliar `OpenFoodFactsProductCatalogClient.GetByBarcodeAsync` con `image_front_url`, `nutriscore_grade` y `nova_group` (convirtiendo `"unknown"`/`""` en `null`). Implementar `SearchByTextAsync` enviando `POST search` con paginación (`page`, `page_size`, `q`, `fields`) y manejo controlado de `429` y `5xx`/timeout.
  - **Criterio de cierre:** `ExternalProductDto` poblado sin inventar datos ausentes (`BR-002`).

- [ ] **B1-T05 — Endpoint de Búsqueda Paginada y Enriquecimiento de Estado Interno (`RF-06`, `RF-08`)**
  - **Acción:** Exponer `GET /api/app/external-product-catalog/search` en `ExternalProductCatalogAppService`, resolviendo `internalProductId` si alguno de los códigos retornados ya se encuentra guardado en `AppProducts` y preparando el campo `DietaryEvaluationDto`.
  - **Criterio de cierre:** Suite en `ExternalProductCatalogAppServiceTests.cs` con mocks `NSubstitute` cubriendo resultados paginados, lista vacía, `RateLimitExceeded` y `ServiceUnavailable` sin depender de Internet.

- [ ] **B1-T06 — Localización (`es`/`en`), Proxies y Ruteo `/catalog` en Angular (`RF-05`, `RF-06`)**
  - **Acción:** Incorporar claves `::Menu:Catalog` y `::Catalog:*` (incluyendo leyenda ética `BR-001` y textos `"No especificado"` `RF-09`) en `es.json` y `en.json`. Regenerar proxies `@proxy/external-product-catalog` y `@proxy/products`. Registrar ruta `/catalog` con `authGuard` y entrada en el menú lateral.
  - **Criterio de cierre:** Navegación autenticada hacia `/catalog` y redirección a login sin sesión.

- [ ] **B1-T07 — Pantalla `CatalogComponent` y Modal de Detalle Nutricional (`RF-05` a `RF-09`)**
  - **Acción:** Construir `CatalogComponent` standalone con pestañas/modo dual (búsqueda por código de barras y búsqueda por texto paginada con `ngb-pagination`), los 4 estados visuales (`loading`, `error` con `retry()`, `empty`, `results`), componente `<app-medical-disclaimer>`, vista/modal de detalle mostrando imagen, marca, categorías, ingredientes, alérgenos, badges Nutri-Score/NOVA, textos explícitos ante datos ausentes (`RF-09`) y botón *"Guardar en catálogo interno"* (`saveFromExternal`).
  - **Criterio de cierre:** Recorrido real verificado desde Angular hacia backend y SQL Server.

- [ ] **B1-T08 — Pruebas Unitarias Frontend de `CatalogComponent` en Vitest**
  - **Acción:** Crear `catalog.component.spec.ts` simulando `ExternalProductCatalogService` y `ProductService` para validar búsqueda por código, búsqueda paginada, producto no encontrado (`NotFound`), error de cuota (`RateLimitExceeded`), datos incompletos (`null`) y guardado interno.
  - **Criterio de cierre:** `yarn test --watch=false` y `yarn build` en verde.

---

## BLOQUE 2: Perfil Alimentario, Motor de Advertencias Informativas y Cuenta
**Alcance:** `RF-03`, `RF-12`, cierre documental/funcional de `RF-02`  
**Documentos rectores:** `docs/02_data_models_and_schemas.md` (§3.4), `docs/03_business_rules_and_workflows.md` (`BR-001`, `BR-002`, `BR-003`, `BR-010`), `docs/04_api_contracts_and_integrations.md` (§2.3), `docs/05_coding_standards_and_patterns.md` (§4).

### Backlog Secuencial del Bloque 2
- [ ] **B2-T01 — Agregado `UserDietaryPreference` y Enumeración `DietaryVerificationStatus` (`RF-03`, `RF-12`)**
  - **Acción:** Crear `PreferenceConsts` (`MaxWatchedTermsCount = 30`, `MinTermLength = 2`, `MaxTermLength = 64`), `DietaryVerificationStatus` (`Compatible`, `Warning`, `Unverifiable`, `NotConfigured`) en `Domain.Shared`, y el agregado `UserDietaryPreference` en `Domain/Preferences` con normalización en minúsculas sin acentos y deduplicación automática de términos.
  - **Criterio de cierre:** Pruebas unitarias en `UserDietaryPreferenceTests.cs` verificando normalización (`"Maní"` -> `"mani"`), límites de cantidad/longitud e invariante `UserId != Guid.Empty`.

- [ ] **B2-T02 — Servicio de Dominio `DietaryWarningManager` (`RF-12`, `BR-002`)**
  - **Acción:** Implementar `DietaryWarningManager` en `Domain/Preferences` aplicando estrictamente `BR-002` (coincidencia positiva -> `Warning`; ausencia de `Ingredients` o `Allergens` con preferencias activas -> `Unverifiable`; ambos presentes sin coincidencia -> `Compatible`; alertas desactivadas o lista vacía -> `NotConfigured`) y método `SyncItemDietaryWarningAsync` para persistir/desactivar `PantryItemWarning` (`WarningType = "DietaryPreference"`).
  - **Criterio de cierre:** Suite unitaria exhaustiva `DietaryWarningManagerTests.cs` cubriendo todos los cuadrantes de completitud de datos y tildes/mayúsculas.

- [ ] **B2-T03 — Persistencia EF Core de `AppUserDietaryPreferences` (`RF-03`)**
  - **Acción:** Registrar `DbSet<UserDietaryPreference>` en `SmartPantryDbContext.cs` con índice único `IX_AppUserDietaryPreferences_UserId`, generar migración `AddUserDietaryPreferences` y aplicarla con `DbMigrator`.
  - **Criterio de cierre:** Restricción `1:1` por `UserId` verificada en base de datos.

- [ ] **B2-T04 — `DietaryPreferenceAppService` y Recálculo de Alertas (`RF-03`, `RF-12`, `BR-010`)**
  - **Acción:** Implementar `IDietaryPreferenceAppService` y `DietaryPreferenceAppService` (`GET` y `PUT /api/app/dietary-preference/my-preferences`) bajo `[Authorize]` usando `CurrentUser.GetId()`. Al actualizar preferencias, sincronizar de forma idempotente las advertencias `"DietaryPreference"` de los ítems activos del usuario en `AppPantryItemWarnings` y exponer helper de evaluación para catálogo/producto.
  - **Criterio de cierre:** Tests de integración obligatorios (`EfCoreDietaryPreferenceAppServiceTests.cs`) comprobando aislamiento entre usuarios, evaluación `Warning` vs `Unverifiable` e idempotencia en `AppPantryItemWarnings`.

- [ ] **B2-T05 — Verificación y Extensión Opcional de Perfil de Usuario (`RF-02`)**
  - **Acción:** Verificar localización completa (`es`/`en`) y funcionamiento del flujo nativo `/account/manage-profile` (nombre visible y correo). Implementar endpoint liviano de avatar opcional sobre `Volo.Abp.BlobStoring` (`ProfilePictureContainer` -> `GET/PUT /api/app/profile-picture`) o documentar formalmente en `docs/` la cobertura acordada con el equipo, sin duplicar las pantallas nativas de ABP Account.
  - **Criterio de cierre:** Actualización de perfil comprobada de punta a punta con sesión real.

- [ ] **B2-T06 — Pantalla `PreferencesComponent` (`/preferences`) y Componente `<app-dietary-alert>` (`RF-03`, `RF-12`)**
  - **Acción:** Agregar claves `::Menu:Preferences` y `::Preferences:*` en `es.json`/`en.json`, generar proxies `@proxy/preferences`, registrar ruta `/preferences` con `authGuard` y construir `PreferencesComponent` (alta/baja interactiva de chips de alérgenos/ingredientes vigilados, switch `showAlertsOnScreen`, guardado reactivo y leyenda ética `BR-001`) más componente compartido `<app-dietary-alert>` reutilizable en Catálogo y Despensa.
  - **Criterio de cierre:** Configuración persistida correctamente entre recargas y cambio de idioma español/inglés.

- [ ] **B2-T07 — Pruebas Unitarias Frontend de `PreferencesComponent` en Vitest**
  - **Acción:** Crear `preferences.component.spec.ts` validando estados de carga/error, validación de longitud de términos, prevención de chips duplicados, toggle de alertas y guardado contra proxy simulado.
  - **Criterio de cierre:** `yarn test --watch=false` en verde.

---

## BLOQUE 3: Evolución de Mi Despensa — Alta en UI, Vencimientos Visuales y Notas Privadas
**Alcance:** `RF-10` (UI), `RF-13` (Proyección API + UI), `RF-14` (Full-Stack)  
**Documentos rectores:** `docs/02_data_models_and_schemas.md` (§3.2, §3.3), `docs/03_business_rules_and_workflows.md` (`BR-003`, `BR-006`, `BR-007`, `BR-009`), `docs/04_api_contracts_and_integrations.md` (§2.4).

### Backlog Secuencial del Bloque 3
- [ ] **B3-T01 — Extensión de `PantryItem` con `PrivateNotes` (`RF-14`, `BR-009`)**
  - **Acción:** Agregar `MaxPrivateNotesLength = 500` en `PantryConsts.cs` y la propiedad `PrivateNotes` con método `SetPrivateNotes(string? notes)` en `PantryItem.cs` (trim automático, blanco -> `null`, excepción `SmartPantry:PantryItem:NotesTooLong` si supera 500 caracteres).
  - **Criterio de cierre:** Pruebas unitarias en `PantryItemDomainTests.cs` validando normalización y límite de 500 caracteres.

- [ ] **B3-T02 — Migración EF Core de Notas Privadas e Índices de `AppPantryItems` (`RF-14`)**
  - **Acción:** Mapear `PrivateNotes` (`nvarchar(500)`) y los índices `IX_AppPantryItems_UserId_CreationTime` e `IX_AppPantryItems_State_ExpirationDate` en `SmartPantryDbContext.cs`. Generar migración `AddPantryItemPrivateNotesAndIndexes` y ejecutar `DbMigrator`.
  - **Criterio de cierre:** Migración aplicada limpia en SQL Server y tests EF Core SQLite.

- [ ] **B3-T03 — Ampliación de `PantryAppService`: Notas, Alertas Activas y Sincronización Inmediata (`RF-13`, `RF-14`)**
  - **Acción:** Extender `CreatePantryItemDto` y `UpdatePantryItemDto` con `PrivateNotes`, y `PantryItemDto` con `PrivateNotes`, `IsExpired`, `IsExpiringSoon` y `List<PantryWarningDto> ActiveWarnings`. En `GetListAsync` y `GetAsync`, cruzar en lote con `AppPantryItemWarnings` (`IsActive == true`) y calcular banderas de vencimiento usando `PantryExpirationManager`. Sincronizar inmediatamente la alerta de vencimiento al ejecutar `CreateAsync`, `UpdateAsync` y `ConsumeAsync`.
  - **Criterio de cierre:** Tests unitarios y de integración en `PantryAppServiceTests.cs` y `EfCorePantryAppServiceTests.cs` comprobando persistencia de nota privada, rechazo `403 Forbidden` desde otro usuario (`BR-003`) y actualización inmediata de `ActiveWarnings`.

- [ ] **B3-T04 — Regeneración de Proxies `@proxy/pantry` y Claves de Localización (`RF-10`, `RF-13`, `RF-14`)**
  - **Acción:** Incorporar en `es.json` y `en.json` los textos para alta de ítem (`::Pantry:AddItem`, `::Pantry:SelectProduct`), estados de vencimiento (`::Pantry:Expired`, `::Pantry:ExpiringSoon`) y notas privadas (`::Pantry:PrivateNotes`, validaciones). Regenerar `@proxy/pantry`.
  - **Criterio de cierre:** Contratos TypeScript sincronizados sin ediciones manuales.

- [ ] **B3-T05 — Modal de Alta en `PantryComponent` ("Agregar a mi despensa" — `RF-10`, `RF-14`)**
  - **Acción:** Incorporar botón primario *"Agregar producto"* en el encabezado de `PantryComponent` que abra modal reactivo permitiendo seleccionar un producto guardado (`ProductService.getList`) e ingresar cantidad, unidad, fecha de vencimiento y nota privada opcionales (`pantryService.create`), soportando además preselección por query param `?addProductId=<guid>` para conectar fluidamente desde `/catalog`.
  - **Criterio de cierre:** Creación completa desde Angular persistida en base de datos y reflejada en la tabla/tarjetas.

- [ ] **B3-T06 — Visualización de Vencimientos Próximos, Alertas y Notas Privadas en `PantryComponent` (`RF-13`, `RF-14`)**
  - **Acción:** Actualizar tabla desktop y tarjetas mobile de `PantryComponent` para destacar ítems vencidos (`bg-danger`) y próximos a vencer (`bg-warning text-dark`), renderizar sus `activeWarnings` informativas, mostrar `privateNotes` (o `"Sin notas"` / `"No especificado"`) e incluir el `textarea` validado (`maxLength(500)`) en el modal de edición.
  - **Criterio de cierre:** Distinción visual clara entre vigente, próximo a vencer, vencido y consumido tanto en desktop como en mobile.

- [ ] **B3-T07 — Ampliación de Suite Vitest `pantry.component.spec.ts`**
  - **Acción:** Agregar pruebas unitarias en `pantry.component.spec.ts` cubriendo apertura y envío del modal de creación (`RF-10`), renderizado de badges `isExpiringSoon` / `isExpired` (`RF-13`) y validación/visualización de `privateNotes` (`RF-14`).
  - **Criterio de cierre:** `yarn test --watch=false` y `yarn build` en verde.

---

## BLOQUE 4: Observabilidad Externa, Panel Administrativo, Rate Limiting y Cobertura Global
**Alcance:** `HU-05` (`RN-5`), `RNF Observabilidad`, `RNF Seguridad (Rate Limiting)` y `RNF Calidad (CodeCoverage >= 70% + Integración Final)`  
**Documentos rectores:** `docs/02_data_models_and_schemas.md` (§3.5), `docs/03_business_rules_and_workflows.md` (`BR-005`), `docs/04_api_contracts_and_integrations.md` (§2.5), `docs/05_coding_standards_and_patterns.md` (§5.2), `docs/06_security_and_auth.md` (§2, §4.2).

### Backlog Secuencial del Bloque 4
- [ ] **B4-T01 — Agregado `ExternalApiCallLog` y Migración EF Core (`HU-05`, `BR-005`)**
  - **Acción:** Crear `ExternalApiConsts` y la entidad append-only `ExternalApiCallLog` (`OperationType`, `OutcomeStatus`, `HttpStatusCode`, `DurationMs`, `QueryTermLength`). Configurar `AppExternalApiCallLogs` y su índice cubriente `(CreationTime DESC, OperationType, OutcomeStatus)` en `SmartPantryDbContext.cs`, generando la migración `AddExternalApiCallLogs`.
  - **Criterio de cierre:** Entidad y migración probadas verificando que jamás almacena tokens ni respuestas JSON completas.

- [ ] **B4-T02 — Instrumentación Transaccional de `OpenFoodFactsProductCatalogClient` (`BR-005`)**
  - **Acción:** Implementar `IExternalApiCallLogger` utilizando un `UnitOfWork` independiente (`requiresNew: true`) y `Stopwatch` en las llamadas externas para registrar duración, código HTTP y resultado incluso cuando la invocación arroja `RateLimitExceededException` o `ServiceUnavailableException`.
  - **Criterio de cierre:** Pruebas unitarias comprobando registro de telemetría tanto en respuestas exitosas (`200`/`404`) como en fallas (`429`/`503`/timeout).

- [ ] **B4-T03 — Permiso RBAC `SmartPantry.IntegrationMetrics` e `IntegrationMetricsAppService` (`HU-05`)**
  - **Acción:** Registrar el permiso `SmartPantryPermissions.IntegrationMetrics.Default` en `SmartPantryPermissionDefinitionProvider` (asignado por defecto al rol `admin`). Implementar `IntegrationMetricsAppService.GetSummaryAsync` (`GET /api/app/integration-metrics/summary`) calculando totales, tasa de error, excesos de cuota, latencia media/máxima y desglose por `OperationType`.
  - **Criterio de cierre:** Tests en `IntegrationMetricsAppServiceTests.cs` verificando agregaciones exactas, rechazo `401` sin sesión y rechazo `403 Forbidden` para un usuario estándar sin rol `admin`.

- [ ] **B4-T04 — Rate Limiting en `SmartPantryHttpApiHostModule` (`RNF Seguridad`)**
  - **Acción:** Configurar `Microsoft.AspNetCore.RateLimiting` (`ExternalCatalogPolicy` particionada por `CurrentUser.Id`: máx. 30 req/min código, 10 req/min búsqueda textual) protegiendo los endpoints de consulta externa contra abuso accidental o automatizado.
  - **Criterio de cierre:** Respuesta controlada `429` verificada al superar el umbral configurado.

- [ ] **B4-T05 — Tablero Administrativo `IntegrationMetricsComponent` en Angular (`HU-05`)**
  - **Acción:** Agregar claves `::Menu:IntegrationMetrics` y `::Metrics:*` en `es.json`/`en.json`, generar `@proxy/integration-metrics`, configurar ruta `/admin/integration-metrics` protegida con `authGuard` + `permissionGuard` (`requiredPolicy: 'SmartPantry.IntegrationMetrics'`) y construir el tablero con tarjetas KPI, filtro de rango de fechas, tabla por operación y estados `loading`/`error`/`empty`/`results` + tests en `integration-metrics.component.spec.ts`.
  - **Criterio de cierre:** Ítem de menú visible únicamente para administradores y bloqueado para usuarios estándar.

- [ ] **B4-T06 — Configuración de `CodeCoverage.runsettings` y Auditoría de Meta $\ge 70\%$ (`RNF Calidad`)**
  - **Acción:** Crear `CodeCoverage.runsettings` en la raíz incluyendo `SmartPantry.Domain` y `SmartPantry.Application` y excluyendo DTOs, migraciones, mappers generados, workers de infraestructura pura y clases `*Module.cs`. Ejecutar recolección Cobertura XML y reforzar tests unitarios donde sea necesario hasta superar holgadamente el **70% de líneas ejecutables**.
  - **Criterio de cierre:** Informe Cobertura XML verificado con `100 * lines-covered / lines-valid >= 70%`.

- [ ] **B4-T07 — Costura Final entre Bloques y Actualización de `README.md` de Entrega**
  - **Acción:** Con los Bloques 1, 2 y 3 integrados en `main`, conectar el enlace directo *"Agregar a mi despensa"* desde el detalle de `/catalog` hacia `/pantry?addProductId=<id>`, verificar la visualización de `<app-dietary-alert>` en `/catalog` y actualizar `README.md` con instrucciones reales de configuración, variables `OpenFoodFacts`, migraciones, ejecución y comando de cobertura.
  - **Criterio de cierre:** Suite completa backend + frontend en verde en CI y recorridos E2E manuales de las 6 Historias de Usuario (`HU-01` a `HU-06`) validados.
