# AGENTS.md — Sistema Operativo para Agentes y Desarrolladores (SmartPantry)

Este archivo constituye la brújula operativa obligatoria de **SmartPantry** (*ABP Framework 10.6.0 · .NET 10 · Entity Framework Core 10 · SQL Server · Angular 22*). Ningún cambio de código puede iniciarse fuera de estas directrices.

---

## 1. Comandos de Entorno y Verificación

### Backend (.NET 10 — Raíz del repositorio)
```bash
# Restaurar y compilar en modo estricto (CI Gate)
dotnet restore ./SmartPantry.slnx
dotnet build ./SmartPantry.slnx --configuration Release --no-restore

# Aplicar migraciones EF Core y semillas iniciales en SQL Server local
dotnet run --project ./src/SmartPantry.DbMigrator

# Levantar servidor API + OpenIddict + Worker (https://localhost:44359)
dotnet run --project ./src/SmartPantry.HttpApi.Host

# Ejecutar suite completa de pruebas (Domain, Application y EF Core SQLite)
dotnet test ./SmartPantry.slnx --configuration Release --no-build

# Medir cobertura ejecutable Domain + Application (meta obligatoria >= 70%)
dotnet test ./SmartPantry.slnx --settings CodeCoverage.runsettings --collect:"Code Coverage;Format=Cobertura"

# Crear nueva migración EF Core (desde la raíz)
dotnet ef migrations add <MigrationName> --project ./src/SmartPantry.EntityFrameworkCore --startup-project ./src/SmartPantry.HttpApi.Host
```

### Frontend (Angular 22 — Directorio `angular/`)
```bash
cd angular
yarn install --frozen-lockfile          # Instalar dependencias exactas
yarn start                              # Dev server en http://localhost:4200
yarn build                              # Build de producción y chequeo estricto de plantillas
yarn lint                               # Análisis estático ESLint (@angular-eslint + @typescript-eslint)
yarn test --watch=false                 # Suite unitaria en Vitest + jsdom
abp generate-proxy -t ng                # Regenerar @proxy/* (requiere HttpApi.Host corriendo)
```

> **Nota de Infraestructura / Docker:** El alcance oficial opera como monolito local sobre **SQL Server Developer / Express / LocalDB** sin contenedores Docker ni brokers externos. No introduzcas `docker-compose` salvo requerimiento explícito de despliegue.

---

## 2. Estándares Inmutables de Ingeniería

1. **Dirección de Dependencias e Inversión de Control:**
   - `SmartPantry.Domain` y `Domain.Shared` jamás referencian HTTP, ASP.NET Core, EF Core ni JSON de proveedores externos.
   - Open Food Facts se consume exclusivamente detrás de `IExternalProductCatalogClient` mediante `HttpClient` inyectado por `IHttpClientFactory` en `SmartPantry.HttpApi.Host`. Angular jamás llama directo a APIs externas.
2. **Tipado Estricto y Cero Código Muerto:**
   - En C# (`<Nullable>enable</Nullable>`): constructores de dominio protegen invariantes con setters privados; prohibido usar constantes mágicas fuera de `*Consts` (`Domain.Shared`).
   - En TypeScript/Angular: componentes `standalone: true`, inyección vía `inject()`, estado reactivo con Signals (`'loading' | 'error' | 'empty' | 'results'`), prohibido usar `any` en código propio y prohibido dejar código comentado, `TODO` o componentes muertos.
   - **Prohibido editar manualmente cualquier archivo dentro de `angular/src/app/proxy/`.**
3. **Manejo Canónico de Errores y Datos Ausentes:**
   - **Invariantes de dominio:** Lanzar `BusinessException("SmartPantry:<Aggregate>:<Reason>")` en agregados nuevos y `Pantry*` (preservando `ArgumentException` en `Product.cs` para no romper `ProductDomainTests`).
   - **Propiedad y Seguridad (Anti-IDOR):** Todo `AppService` lleva `[Authorize]`. La identidad proviene únicamente de `CurrentUser.GetId()`. Si un registro pertenece a otro usuario, lanzar `AbpAuthorizationException` (**HTTP 403 Forbidden**).
   - **Fallas esperadas del proveedor externo (`404`, `429`, `503`, timeout):** Encapsular en `ExternalProductResultDto` (`ExternalProductQueryStatus`), nunca propagar excepciones HTTP crudas ni stack traces a Angular.
   - **Datos incompletos y ética (`BR-001`, `BR-002`):** Nunca inventar valores por defecto; mostrar claves localizadas (`"No especificado"`). Si faltan ingredientes o alérgenos al evaluar preferencias, retornar `DietaryVerificationStatus.Unverifiable`, jamás asumir ausencia de riesgo.
4. **Internacionalización y Flujo Git:**
   - Todo texto visible en UI debe existir en `es.json` y `en.json` consumido con `abpLocalization`.
   - Ramas `feature/<issue>-<slug>` desde `main`. Commits bajo **Conventional Commits** (`feat:`, `fix:`, `test:`, `docs:`) **sin `Co-Authored-By` ni atribución de IA**.

---

## 3. Context Index (Mapa de Punteros a `docs/`)

Antes de diseñar o modificar cualquier archivo, abre obligatoriamente el documento rector correspondiente:

| Si vas a trabajar en... | Debes leer primero... |
|---|---|
| Alcance funcional (`RF-01`..`RF-15`), glosario ubicuo o dudas de producto | `docs/00_overview_and_domain.md` |
| Estructura de capas `.csproj`, librerías permitidas, DI o módulos Angular | `docs/01_architecture_and_tech_stack.md` |
| Entidades, tablas `App*`, columnas, tipos SQL, índices o migraciones EF Core | `docs/02_data_models_and_schemas.md` |
| Reglas `BR-001`..`BR-010`, estados de `PantryItem`, worker o cruce alimentario | `docs/03_business_rules_and_workflows.md` |
| Rutas `/api/app/*`, DTOs JSON, paginación o cliente Open Food Facts (v3 / POST) | `docs/04_api_contracts_and_integrations.md` |
| Árbol de carpetas, nombres, manejo de errores, snippets modelo o tests/cobertura | `docs/05_coding_standards_and_patterns.md` |
| Autenticación OpenIddict, permisos RBAC, `401`/`403`, CORS, Rate Limit o secretos | `docs/06_security_and_auth.md` |
| Estado de avance por bloques, dependencias entre tareas y próximos tickets | `TASK_ROADMAP.md` |

### Mapa Rápido de Aislamiento por Bloques (`TASK_ROADMAP.md`)
| Bloque | Alcance | Superficie Backend | Superficie Angular |
|---|---|---|---|
| **Bloque 1** | `RF-05`..`RF-09` (Catálogo, Search-a-licious, Detalle, Guardado) | `ExternalProductCatalog/`, `Products/` | `angular/src/app/catalog/` |
| **Bloque 2** | `RF-03`, `RF-12`, `RF-02` (Preferencias, Motor Alertas, Perfil) | `Preferences/` | `angular/src/app/preferences/` |
| **Bloque 3** | `RF-10`, `RF-13`, `RF-14` (Alta UI, Vencimientos UI, Notas) | `Pantry/` | `angular/src/app/pantry/` |
| **Bloque 4** | `HU-05`, Rate Limit y Cobertura $\ge 70\%$ | `IntegrationMetrics/`, `CodeCoverage.runsettings` | `angular/src/app/admin/integration-metrics/` |

---

## 4. Protocolo Obligatorio de Ejecución (Workflow del Agente)

Todo agente o desarrollador debe ejecutar este ciclo exacto en cada tarea:

1. **Sincronización de Contexto (Pre-Code):** Leer `TASK_ROADMAP.md` para identificar la tarea activa de su bloque y consultar los documentos indicados en el *Context Index* (`docs/`). Verificar que los nombres de entidades, columnas, DTOs y rutas coincidan 100% con la fuente de verdad.
2. **Implementación Vertical Acotada:** Desarrollar respetando las capas (`Domain.Shared` -> `Domain` -> `EntityFrameworkCore` -> `Application.Contracts` -> `Application` -> `HttpApi.Host` -> `Angular`), manteniendo los cambios restringidos al bloque asignado.
3. **Verificación Empírica Obligatoria:** Ejecutar `dotnet build`, `dotnet test`, `yarn build` y `yarn test --watch=false`. Ninguna tarea se da por terminada si falla un test existente o si la funcionalidad nueva carece de tests unitarios y de integración sin red.
4. **Actualización de Estado (Post-Code):** Al concluir y verificar una tarea, marcar su casilla `[x]` en `TASK_ROADMAP.md` registrando cualquier nota técnica relevante de integración antes de cerrar la entrega.
