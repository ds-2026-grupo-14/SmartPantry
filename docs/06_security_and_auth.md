# 06. Seguridad, Autenticación y Control de Accesos — SmartPantry

> **Propósito de este documento:** Definir la arquitectura de seguridad de punta a punta de SmartPantry: flujos OAuth 2.0 / OpenID Connect sobre **OpenIddict**, matriz de control de acceso basado en roles (**RBAC**) y propiedad de recursos (**ABAC / Owner Isolation**), criptografía de credenciales, gestión de secretos fuera del repositorio y políticas de CORS y Rate Limiting.

---

## 1. Mecanismo de Autenticación (OAuth 2.0 / OpenID Connect + JWT)

### 1.1 Arquitectura con OpenIddict y ABP Account
SmartPantry delega la emisión y validación criptográfica de tokens en **OpenIddict 10.6.0**, hospedado dentro de `SmartPantry.HttpApi.Host` y respaldado en las tablas `OpenIddict*` de SQL Server:

- **Cliente SPA (`SmartPantry_App`):** Cliente público (`ClientTypes.Public`) que utiliza **Authorization Code Flow con PKCE (`S256`)** mediante `@abp/ng.oauth`. Angular jamás manipula credenciales crudas de base de datos ni secretos de cliente.
- **Cliente de Pruebas Interactivas (`SmartPantry_Swagger`):** Cliente público registrado para autorización OIDC desde `/swagger`.
- **Scopes Autorizados:** `openid`, `profile`, `email`, `phone`, `address`, `roles`, `offline_access` y el scope de audiencia propia **`SmartPantry`**.
- **Ciclo de Vida y Expiración de Tokens:**
  - `access_token` (Formato JWT firmado asimétricamente): **3.600 segundos (1 hora)** por defecto en OpenIddict. Contiene los claims `sub` (`UserId`), `unique_name`, `email`, `role` y `aud = "SmartPantry"`.
  - `refresh_token` (Cuando se solicita `offline_access`): **14 días**, rotado y revocable en servidor (`OpenIddictTokens`).
- **Revalidación Dinámica de Claims (`Dynamic Claims`):** `SmartPantryHttpApiHostModule` habilita explícitamente `options.IsDynamicClaimsEnabled = true`. Si un administrador deshabilita una cuenta o modifica sus roles (RF-04), el cambio surte efecto de inmediato en la siguiente petición HTTP sin esperar a que expire el JWT de 1 hora.
- **Certificados de Firma y Cifrado:**
  - **Desarrollo (`IsDevelopment() == true`):** Certificados efímeros de desarrollo de OpenIddict.
  - **Producción (`IsDevelopment() == false`):** Certificado X.509 dedicado (`openiddict.pfx`) protegido por frase de paso inyectada fuera del código fuente (`AuthServer:CertificatePassPhrase`) y transporte TLS obligatorio (`RequireHttpsMetadata = true`).

### 1.2 Principio Inquebrantable: La Autorización Reside en el Backend
Los guards de rutas en Angular (`authGuard`, `permissionGuard`) y el ocultamiento condicional de botones existen exclusivamente por **usabilidad**. Todo `ApplicationService` propio valida autenticación (`[Authorize]`), permisos (`[Authorize("Permiso")]`) y pertenencia de datos (`ICurrentUser`) en el servidor antes de ejecutar cualquier consulta o mutación.

---

## 2. Matriz RBAC y Control de Pertenencia de Datos

### 2.1 Permisos Definidos en `SmartPantryPermissions`
Además de los permisos nativos de `Volo.Abp.Identity` (`AbpIdentity.Users`, `AbpIdentity.Roles`), el grupo de permisos `SmartPantry` registra:
- `SmartPantry.IntegrationMetrics` (`Default`): Consulta de métricas agregadas y salud de la integración con Open Food Facts (otorgado por semilla exclusivamente al rol `admin`).

### 2.2 Matriz Exhaustiva de Accesos por Actor y Recurso

| Recurso / Operación | Endpoint / Componente | Visitante Anónimo | Usuario Estándar (Dato Propio) | Usuario Estándar (Dato Ajeno) | Administrador (`admin`) | Worker Interno (`ExpirationWarningWorker`) |
|---|---|:---:|:---:|:---:|:---:|:---:|
| **Portada pública e inicio de sesión** | `/`, `/account/login`, `/connect/token` | Permitido (`200`) | Permitido (`200`) | — | Permitido (`200`) | No aplica |
| **Perfil propio (RF-02)** | `GET/PUT /api/account/my-profile` | Denegado (`401`) | Permitido (`200`) | Denegado (opera sobre `CurrentUser`) | Permitido (sobre su propio perfil) | No aplica |
| **Preferencias alimentarias (RF-03)** | `GET/PUT /api/app/dietary-preference/my-preferences` | Denegado (`401`) | Permitido (`200`) | Denegado (aislado por `CurrentUser.GetId()`) | Permitido (solo sus propias preferencias) | Solo lectura indirecta si aplica |
| **Búsqueda externa código / texto (RF-05, RF-06)** | `GET /api/app/external-product-catalog/*` | Denegado (`401`) | Permitido (`200`) | — | Permitido (`200`) | Prohibido (no usa red) |
| **Guardar y consultar catálogo interno (RF-07, RF-08)** | `GET/POST /api/app/product*` | Denegado (`401`) | Permitido (`200`) | Recurso maestro compartido (solo lectura/alta sin dueño privado) | Permitido (`200` + edición/baja administrativa) | Solo lectura (`AppProducts`) |
| **Listar y crear ítems en despensa (RF-10, RF-11)** | `GET/POST /api/app/pantry` | Denegado (`401`) | Permitido (`200`) | Filtrado estricto `WHERE UserId == CurrentUser.Id` | Permitido (solo ve su propia despensa) | Lectura global interna por UoW |
| **Ver, editar, consumir, eliminar ítem o nota privada (RF-11, RF-14)** | `GET/PUT/POST/DELETE /api/app/pantry/{id}*` | Denegado (`401`) | Permitido (`200` / `204`) | **Denegado (`403 Forbidden` vía `AbpAuthorizationException`)** | **Denegado (`403 Forbidden` si el ítem pertenece a otro usuario)** | No modifica ítems; solo gestiona `AppPantryItemWarnings` |
| **Administración de usuarios y roles (RF-04)** | `/api/identity/users*`, `/api/identity/roles*` | Denegado (`401`) | **Denegado (`403 Forbidden`)** | **Denegado (`403 Forbidden`)** | Permitido (`200`, requiere `AbpIdentity.*`) | No aplica |
| **Métricas de integración externa (HU-05)** | `GET /api/app/integration-metrics/summary` | Denegado (`401`) | **Denegado (`403 Forbidden`)** | **Denegado (`403 Forbidden`)** | Permitido (`200`, requiere `SmartPantry.IntegrationMetrics`) | No aplica |

---

## 3. Seguridad en Datos, Credenciales y Variables Sensibles

### 3.1 Hasheo de Contraseñas y Políticas de Identidad
- **Algoritmo criptográfico:** Gestionado por `Microsoft.AspNetCore.Identity.PasswordHasher<IdentityUser>` (estándar **PBKDF2 con HMAC-SHA512**, salt criptográfico aleatorio de 128 bits por usuario y 100.000 iteraciones en .NET 10). Jamás se almacenan ni registran contraseñas reversibles o en texto plano.
- **Política de complejidad:** En entorno local de pruebas académicas, `ChangeIdentityPasswordPolicySettingDefinitionProvider` relaja las exigencias de mayúsculas/símbolos para facilitar semillas de test, manteniendo longitud mínima; en despliegue productivo las políticas se administran dinámicamente desde `AbpSettings` (`Identity.Password.*`) exigiendo longitud mínima de 8 caracteres, dígito y mayúscula.

### 3.2 Sanitización de Entradas y Prevención de Vulnerabilidades Web (OWASP Top 10)
1. **Prevención de SQL Injection:** Todas las consultas a SQL Server se ejecutan mediante expresiones tipadas LINQ sobre EF Core parametrizadas automáticamente. En `System.Linq.Dynamic.Core` (`OrderBy`), solo se admiten nombres de propiedades válidas del agregado.
2. **Prevención de XSS (Cross-Site Scripting):** Angular 22 escapa por defecto toda interpolación `{{ ... }}` en plantillas HTML (`PrivateNotes`, `Ingredients`, `Name` proveniente de Open Food Facts). **Queda prohibido usar `[innerHTML]` o `DomSanitizer.bypassSecurityTrustHtml`** con textos provenientes de la API externa o notas de usuario.
3. **Prevención de SSRF en Imágenes Externas:** `ImageUrl` proveniente de Open Food Facts solo se acepta si cumple esquema `https://` válido hacia dominios públicos y se renderiza exclusivamente en etiquetas `<img [src]="...">` del navegador del cliente (el backend jamás descarga binarios desde URLs arbitrarias provistas por usuarios).
4. **Protección de Secretos y Logs (Regla `BR-005`):**
   - Angular (`environment.ts` / `environment.prod.ts`) contiene únicamente URLs públicas e identificadores públicos OAuth (`clientId: 'SmartPantry_App'`), **nunca** claves privadas ni cadenas de conexión.
   - En producción, `App:DisablePII` debe establecerse en `true` para impedir que trazas OIDC detallen artefactos de seguridad en los logs.

### 3.3 Variables de Entorno Sensibles Requeridas (Plantilla `.env.example` / User Secrets)
En desarrollo local se emplea `dotnet user-secrets` o cadenas Trusted Connection locales sin contraseñas personales; en CI/CD y producción las configuraciones sensibles se inyectan mediante variables de entorno jerárquicas de ASP.NET Core (`__` reemplaza a `:`):

```dotenv
# ============================================================================
# SmartPantry - Variables de Entorno Requeridas en Producción (.env.example)
# NUNCA versionar valores reales de producción en el repositorio Git
# ============================================================================

ASPNETCORE_ENVIRONMENT=Production

# URLs Públicas y Orígenes Permitidos
App__SelfUrl=https://api.smartpantry.local
App__AngularUrl=https://app.smartpantry.local
App__CorsOrigins=https://app.smartpantry.local
App__RedirectAllowedUrls=https://app.smartpantry.local
App__DisablePII=true

# Base de Datos Relacional (SQL Server)
ConnectionStrings__Default=Server=tcp:sql-smartpantry.database.windows.net,1433;Initial Catalog=SmartPantry;User ID=<DB_USER>;Password=<DB_STRONG_PASSWORD>;Encrypt=True;TrustServerCertificate=False;

# Servidor OIDC / OpenIddict
AuthServer__Authority=https://api.smartpantry.local
AuthServer__RequireHttpsMetadata=true
AuthServer__SwaggerClientId=SmartPantry_Swagger
AuthServer__CertificatePassPhrase=<OPENIDDICT_PFX_PASSPHRASE>

# Cifrado Interno de Settings de ABP (16 caracteres exactos AES)
StringEncryption__DefaultPassPhrase=<16_CHAR_PASSPHRASE>

# Integración Externa Open Food Facts
OpenFoodFacts__BaseUrl=https://world.openfoodfacts.org/
OpenFoodFacts__SearchBaseUrl=https://search.openfoodfacts.org/
OpenFoodFacts__UserAgent=SmartPantry/1.0 (ds-2026-grupo-14; contacto-equipo@utn.edu.ar)
OpenFoodFacts__TimeoutSeconds=35
```

---

## 4. Políticas de CORS, Cabeceras HTTP y Rate Limiting

### 4.1 Política Estricta de CORS
Configurada en `SmartPantryHttpApiHostModule.ConfigureCors`:
- **Orígenes habilitados:** Lista blanca estricta leída desde `App:CorsOrigins` (`http://localhost:4200` en desarrollo). **Prohibido usar `AllowAnyOrigin()` junto con `AllowCredentials()`.**
- **Headers expuestos:** `WithAbpExposedHeaders()` (`_AbpErrorFormat`, correlación y paginación).
- **Seguridad de Cabeceras HTTP:** El pipeline invoca `app.UseAbpSecurityHeaders()`, inyectando cabeceras defensivas (`X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `X-XSS-Protection`).

### 4.2 Políticas de Rate Limiting y Protección del Proveedor Externo
Open Food Facts es una infraestructura pública comunitaria con límites estrictos por dirección IP y `User-Agent` (aprox. 100 req/min en lectura de producto y ~10 req/min en búsquedas de texto pesadas):

1. **Restricción de acceso anónimo:** Al exigir `[Authorize]` en `ExternalProductCatalogAppService`, ningún bot anónimo de Internet puede utilizar a SmartPantry como proxy abierto hacia Open Food Facts.
2. **Rate Limiting por Usuario en Backend (`Microsoft.AspNetCore.RateLimiting`):**
   - **Política `ExternalCatalogPolicy` (Fixed Window / Token Bucket por `CurrentUser.Id`):** Máximo **30 consultas por minuto** en búsqueda por código de barras y **10 consultas por minuto** en búsqueda por texto libre por usuario autenticado; exceso local responde `HTTP 429 Too Many Requests` antes de golpear a Open Food Facts.
3. **Debounce y Control en Frontend:** El formulario de búsqueda por texto libre en Angular requiere acción explícita de envío (`(ngSubmit)`) o `debounceTime(400)` + `distinctUntilChanged()` con longitud mínima de 2 caracteres, bloqueando el botón mientras una consulta está en vuelo (`viewStatus() === 'loading'`).
