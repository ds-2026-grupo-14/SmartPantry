# 05. Estándares de Codificación, Patrones y Pruebas — SmartPantry

> **Propósito de este documento:** Establecer el árbol canónico del repositorio, las convenciones obligatorias de nomenclatura en C# y TypeScript, la estrategia unificada de manejo de errores entre capas, el patrón de implementación de referencia y los estándares de pruebas automatizadas y cobertura ($\ge 70\%$).

---

## 1. Estructura Canónica del Proyecto

Todo archivo nuevo debe ubicarse exactamente en la carpeta correspondiente a su contexto acotado (*Bounded Subdomain*) dentro de cada capa:

```text
SmartPantry/
├── SmartPantry.slnx
├── CodeCoverage.runsettings                  # Configuración oficial de cobertura >= 70% (Domain + Application)
├── docs/                                     # Suite Single Source of Truth (00..06)
├── src/
│   ├── SmartPantry.Domain.Shared/
│   │   ├── ExternalProductCatalog/           # ExternalApiConsts.cs
│   │   ├── Localization/SmartPantry/         # es.json, en.json (claves ::Pantry:*, ::Catalog:*, ::Preferences:*)
│   │   ├── Pantry/                           # PantryConsts.cs, PantryItemState.cs, PantryWarningConsts.cs
│   │   ├── Preferences/                      # PreferenceConsts.cs, DietaryVerificationStatus.cs
│   │   ├── Products/                         # ProductConsts.cs, ItemCategory.cs
│   │   └── SmartPantryDomainErrorCodes.cs
│   │
│   ├── SmartPantry.Domain/
│   │   ├── ExternalProductCatalog/           # ExternalApiCallLog.cs
│   │   ├── Identity/                         # TestUserDataSeedContributor.cs, PasswordPolicy...
│   │   ├── OpenIddict/                       # OpenIddictDataSeedContributor.cs
│   │   ├── Pantry/                           # PantryItem.cs, PantryItemWarning.cs, PantryExpirationManager.cs
│   │   ├── Preferences/                      # UserDietaryPreference.cs, DietaryWarningManager.cs
│   │   └── Products/                         # Product.cs
│   │
│   ├── SmartPantry.Application.Contracts/
│   │   ├── ExternalProductCatalog/           # IExternalProductCatalogAppService.cs, ExternalProduct*Dto.cs
│   │   ├── IntegrationMetrics/               # IIntegrationMetricsAppService.cs, IntegrationMetricsSummaryDto.cs
│   │   ├── Pantry/                           # IPantryAppService.cs, PantryItemDto.cs, Create/UpdatePantryItemDto.cs
│   │   ├── Permissions/                      # SmartPantryPermissions.cs, SmartPantryPermissionDefinitionProvider.cs
│   │   ├── Preferences/                      # IDietaryPreferenceAppService.cs, DietaryPreferenceDto.cs
│   │   └── Products/                         # IProductAppService.cs, ProductDto.cs, SaveExternalProductDto.cs
│   │
│   ├── SmartPantry.Application/
│   │   ├── ExternalProductCatalog/           # ExternalProductCatalogAppService.cs, IExternalProductCatalogClient.cs
│   │   ├── IntegrationMetrics/               # IntegrationMetricsAppService.cs
│   │   ├── Pantry/                           # PantryAppService.cs
│   │   ├── Preferences/                      # DietaryPreferenceAppService.cs
│   │   ├── Products/                         # ProductAppService.cs
│   │   ├── Workers/                          # ExpirationWarningWorker.cs
│   │   └── SmartPantryApplicationMappers.cs
│   │
│   ├── SmartPantry.EntityFrameworkCore/
│   │   ├── EntityFrameworkCore/              # SmartPantryDbContext.cs
│   │   └── Migrations/                       # Migraciones EF Core versionadas
│   │
│   ├── SmartPantry.HttpApi/                  # SmartPantryController.cs
│   ├── SmartPantry.HttpApi.Host/
│   │   ├── OpenFoodFacts/                    # OpenFoodFactsOptions.cs, OpenFoodFactsProductCatalogClient.cs
│   │   ├── HealthChecks/                     # SmartPantryDatabaseCheck.cs
│   │   ├── Program.cs
│   │   └── SmartPantryHttpApiHostModule.cs
│   │
│   └── SmartPantry.DbMigrator/               # Migrador CLI y sembrado inicial
│
├── angular/src/app/
│   ├── admin/integration-metrics/            # integration-metrics.component.{ts,html,scss,spec.ts}
│   ├── catalog/                              # catalog.component.{ts,html,scss,spec.ts} + product-detail-modal/
│   ├── home/                                 # home.component.{ts,html,scss}
│   ├── pantry/                               # pantry.component.{ts,html,scss,spec.ts}
│   ├── preferences/                          # preferences.component.{ts,html,scss,spec.ts}
│   ├── proxy/                                # Proxies autogenerados por ABP CLI (NO EDITAR A MANO)
│   ├── shared/components/medical-disclaimer/ # medical-disclaimer.component.ts
│   ├── app.routes.ts
│   └── route.provider.ts
│
└── test/
    ├── SmartPantry.TestBase/                 # Builders de prueba y FakeCurrentPrincipalAccessor
    ├── SmartPantry.Domain.Tests/             # Pruebas unitarias de agregados y DomainServices
    ├── SmartPantry.Application.Tests/        # Pruebas unitarias/integración de AppServices con NSubstitute
    └── SmartPantry.EntityFrameworkCore.Tests/# Pruebas de integración contra SQLite In-Memory
```

---

## 2. Convenciones Estrictas de Nomenclatura y Flujo Git

### 2.1 Backend (.NET 10 / C#)
| Elemento | Convención | Ejemplo Válido | Ejemplo Inválido |
|---|---|---|---|
| **Clases, Interfaces, Enums, Propiedades** | `PascalCase` en inglés | `UserDietaryPreference`, `IPantryAppService` | `PreferenciaUsuario`, `pantry_service` |
| **Campos privados de instancia** | `_camelCase` con `readonly` siempre que sea posible | `private readonly IRepository<Product, Guid> _productRepository;` | `private IRepository productRepo;` |
| **Métodos asíncronos** | Sufijo obligatorio `Async` y retorno `Task` / `Task<T>` | `SaveFromExternalAsync(...)` | `SaveFromExternal(...)` |
| **DTOs de Aplicación** | Sufijo obligatorio `Dto` y verbo explícito para mutaciones | `CreatePantryItemDto`, `UpdateDietaryPreferenceDto` | `PantryItemRequest`, `PreferenceModel` |
| **Constantes de validación** | Clase estática `*Consts` en `Domain.Shared` | `PantryConsts.MaxPrivateNotesLength` | Literales mágicos `500` sueltos en atributos |
| **Códigos de error de dominio** | `SmartPantry:<Aggregate>:<SpecificReason>` | `"SmartPantry:PantryItem:NotesTooLong"` | `"ERR_01"`, `"Invalid input"` |

### 2.2 Frontend (Angular 22 / TypeScript)
| Elemento | Convención | Ejemplo Válido |
|---|---|---|
| **Archivos de componente** | `kebab-case.component.{ts,html,scss,spec.ts}` | `dietary-preferences.component.ts` |
| **Componentes Angular** | `standalone: true` con `ChangeDetectionStrategy.OnPush` o Signals | `export class CatalogComponent` |
| **Estado de vista reactivo** | Signals tipados con unión discriminada explícita | `readonly viewStatus = signal<'idle' \| 'loading' \| 'error' \| 'empty' \| 'results'>('idle');` |
| **Inyección de dependencias** | Función `inject()` en inicialización de campos | `private readonly pantryService = inject(PantryService);` |
| **Textos visibles en HTML** | 100% localizados vía `abpLocalization` pipe (`es.json` + `en.json`) | `{{ '::Catalog:UnverifiableWarning' \| abpLocalization }}` |

### 2.3 Flujo Colaborativo Git y Commits
- **Ramas:** `feature/<numero-issue>-<descripcion-kebab-case>` creadas siempre desde `main` actualizado (ej. `feature/34-dietary-preferences-abm`).
- **Mensajes de Commit:** Estándar **Conventional Commits** (`feat(pantry): ...`, `fix(catalog): ...`, `test(domain): ...`, `docs(architecture): ...`). **Prohibido incluir líneas `Co-Authored-By` o atribuciones a herramientas de IA.**

---

## 3. Manejo Canónico de Errores por Capa

SmartPantry distingue rigurosamente entre **excepciones de invariantes/seguridad** y **estados esperados de integración externa**:

| Escenario | Mecanismo Canónico | Capa que lo Emite | Código HTTP Resultante | Tratamiento en Angular |
|---|---|---|---|---|
| **Formato de entrada inválido (DTO)** | `DataAnnotations` (`[Required]`, `[StringLength]`, `[Range]`) o `IValidatableObject` | `Application.Contracts` | `400 Bad Request` | Prevenido por `Validators` en `FormGroup`; si ocurre, se muestra alerta local en el formulario sin borrar lo ingresado. |
| **Violación de invariante de negocio** | `throw new BusinessException("SmartPantry:...")` en `PantryItem`, `PantryItemWarning` y agregados nuevos *(Nota: preservar `ArgumentException` en las validaciones ya probadas de `Product.cs` para mantener compatibilidad con `ProductDomainTests`)* | `Domain` | `403 Forbidden` / `400` | Mensaje localizado dentro del modal activo (`serverError.set(...)`). |
| **Petición sin token válido** | Atributo `[Authorize]` en clase `AppService` | `Application` / Middleware | `401 Unauthorized` | `authGuard` o interceptor OAuth redirige automáticamente a `/account/login`. |
| **Acceso a recurso de otro usuario (IDOR) o falta de rol admin** | `throw new AbpAuthorizationException(...)` o `[Authorize(Permission)]` | `Application` | `403 Forbidden` | Bloqueado en backend; pantalla de acceso denegado en Angular. |
| **Entidad interna inexistente por `Guid`** | `Repository.GetAsync(id)` lanza `EntityNotFoundException` | `Domain` / `EF Core` | `404 Not Found` | Mensaje comprensible de recurso no encontrado. |
| **Producto inexistente (`404`), límite de cuota (`429`) o caída (`503`/timeout) en Open Food Facts** | Adaptador lanza `RateLimitExceededException` / `ServiceUnavailableException`; `ExternalProductCatalogAppService` lo captura y retorna `ExternalProductResultDto` con `Status` tipado | `HttpApi.Host` -> `Application` | `200 OK` con `"status": 1 \| 2 \| 3` | El componente renderiza alerta contextual específica (ej. *"Límite temporal de consultas alcanzado, intente en unos segundos"*) con botón **Reintentar**. |
| **Caída de red entre Angular y el propio Backend** | Invocación proxy con `{ skipHandleError: true }` en cargas de pantalla | `Angular` | Error de red (`status 0` / `5xx`) | Transición a `viewStatus.set('error')` con botón en línea **Reintentar** (`retry()`), suprimiendo modales globales intrusivos. |

---

## 4. Patrón de Implementación de Referencia

El siguiente código ilustra el patrón canónico obligatorio: **Entidad/Servicio de Dominio puro -> Orquestación en `AppService` autenticado -> Consumo reactivo en Angular**.

### 4.1 Servicio de Dominio Puro (`src/SmartPantry.Domain/Preferences/DietaryWarningManager.cs`)
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Volo.Abp.Domain.Services;

namespace SmartPantry.Preferences;

public sealed record DietaryEvaluationResult(
    DietaryVerificationStatus Status,
    IReadOnlyList<string> MatchedTerms);

public class DietaryWarningManager : DomainService
{
    public DietaryEvaluationResult Evaluate(
        string? ingredients,
        string? allergens,
        UserDietaryPreference? preference)
    {
        if (preference is null || !preference.ShowAlertsOnScreen)
        {
            return new DietaryEvaluationResult(DietaryVerificationStatus.NotConfigured, Array.Empty<string>());
        }

        var watchedTerms = preference.GetWatchedTermsList();
        if (watchedTerms.Count == 0)
        {
            return new DietaryEvaluationResult(DietaryVerificationStatus.NotConfigured, Array.Empty<string>());
        }

        var normalizedIngredients = NormalizeForComparison(ingredients);
        var normalizedAllergens = NormalizeForComparison(allergens);

        var matches = watchedTerms
            .Where(term =>
                (!string.IsNullOrEmpty(normalizedAllergens) && normalizedAllergens.Contains(term, StringComparison.Ordinal)) ||
                (!string.IsNullOrEmpty(normalizedIngredients) && normalizedIngredients.Contains(term, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (matches.Count > 0)
        {
            return new DietaryEvaluationResult(DietaryVerificationStatus.Warning, matches);
        }

        // Business Rule BR-002: Missing ingredients OR missing allergens MUST NEVER be treated as risk-free
        if (string.IsNullOrEmpty(normalizedIngredients) || string.IsNullOrEmpty(normalizedAllergens))
        {
            return new DietaryEvaluationResult(DietaryVerificationStatus.Unverifiable, Array.Empty<string>());
        }

        return new DietaryEvaluationResult(DietaryVerificationStatus.Compatible, Array.Empty<string>());
    }

    public static string NormalizeForComparison(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
```

### 4.2 Caso de Uso en Capa de Aplicación (`src/SmartPantry.Application/Preferences/DietaryPreferenceAppService.cs`)
```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace SmartPantry.Preferences;

[Authorize]
public class DietaryPreferenceAppService : SmartPantryAppService, IDietaryPreferenceAppService
{
    private readonly IRepository<UserDietaryPreference, Guid> _preferenceRepository;

    public DietaryPreferenceAppService(IRepository<UserDietaryPreference, Guid> preferenceRepository)
    {
        _preferenceRepository = preferenceRepository;
    }

    public async Task<DietaryPreferenceDto> GetMyPreferencesAsync()
    {
        var currentUserId = CurrentUser.GetId();
        var entity = await _preferenceRepository.FindAsync(x => x.UserId == currentUserId);

        if (entity is null)
        {
            return new DietaryPreferenceDto
            {
                UserId = currentUserId,
                WatchedTerms = [],
                ShowAlertsOnScreen = true
            };
        }

        return MapToDto(entity);
    }

    public async Task<DietaryPreferenceDto> UpdateMyPreferencesAsync(UpdateDietaryPreferenceDto input)
    {
        var currentUserId = CurrentUser.GetId();
        var entity = await _preferenceRepository.FindAsync(x => x.UserId == currentUserId);

        if (entity is null)
        {
            entity = new UserDietaryPreference(
                GuidGenerator.Create(),
                currentUserId,
                input.WatchedTerms,
                input.ShowAlertsOnScreen);

            await _preferenceRepository.InsertAsync(entity, autoSave: true);
        }
        else
        {
            entity.SetWatchedTerms(input.WatchedTerms);
            entity.SetShowAlertsOnScreen(input.ShowAlertsOnScreen);
            await _preferenceRepository.UpdateAsync(entity, autoSave: true);
        }

        return MapToDto(entity);
    }

    private static DietaryPreferenceDto MapToDto(UserDietaryPreference entity) => new()
    {
        UserId = entity.UserId,
        WatchedTerms = entity.GetWatchedTermsList().ToArray(),
        ShowAlertsOnScreen = entity.ShowAlertsOnScreen,
        LastModificationTime = entity.LastModificationTime
    };
}
```

---

## 5. Estándares Obligatorios de Pruebas y Cobertura ($\ge 70\%$)

### 5.1 Matriz Mínima de Pruebas por Operación
Ningún Pull Request hacia `main` puede aprobarse si omite alguno de los siguientes niveles:
1. **Pruebas Unitarias de Dominio (`SmartPantry.Domain.Tests`):** Validar valores límite, entradas vacías/excesivas, transiciones de estado, normalización de tildes y cálculo temporal en `PantryExpirationManager` pasando `referenceLocalDate` fijo (sin `Thread.Sleep` ni dependencia del reloj real).
2. **Pruebas de Aplicación con Mocks (`SmartPantry.Application.Tests`):** Sustituir `IExternalProductCatalogClient` mediante `NSubstitute.Substitute.For<IExternalProductCatalogClient>()` para ejercitar `Found`, `NotFound`, `RateLimitExceeded` y `ServiceUnavailable` **sin acceso a Internet**.
3. **Pruebas de Integración obligatorias (`SmartPantry.EntityFrameworkCore.Tests`):**
   - **RF-08:** Persistir un `Product` y comprobar que un segundo intento con idéntico `Barcode` no duplica registros.
   - **RF-10, RF-11 y RF-14:** Persistir ítem/nota con Usuario A, cambiar contexto con `FakeCurrentPrincipalAccessor` a Usuario B y verificar que `GetAsync`, `UpdateAsync`, `ConsumeAsync` y `DeleteAsync` lanzan `AbpAuthorizationException`.
   - **RF-12:** Con preferencias guardadas, verificar que un producto con ingredientes ausentes devuelve `DietaryVerificationStatus.Unverifiable`.
   - **RF-15:** Ejecutar dos veces `ProcessExpirationsAsync` sobre ítems próximos a vencer y verificar `Count == 1` en `AppPantryItemWarnings`.
4. **Pruebas Unitarias de Componentes Angular (`angular/**/*.spec.ts` en Vitest):** Mockear el servicio `@proxy/*` y verificar los 4 estados (`loading`, `error` + `retry()`, `empty`, `results`), bloqueo de submit ante formulario inválido y cancelación de confirmación sin llamada al servidor.

### 5.2 Medición Reproducible de Cobertura (`CodeCoverage.runsettings`)
En la raíz del repositorio debe mantenerse `CodeCoverage.runsettings` incluyendo únicamente `SmartPantry.Domain.dll` y `SmartPantry.Application.dll` y excluyendo DTOs, migraciones, mappers generados y configuración de módulos (`*Module.cs`). Comando oficial de verificación:

```bash
dotnet test ./SmartPantry.slnx --settings CodeCoverage.runsettings --collect:"Code Coverage;Format=Cobertura"
```
Criterio de aprobación innegociable: **`100 * lines-covered / lines-valid >= 70.0%`** consolidado sobre el código ejecutable de `Domain` y `Application`.
