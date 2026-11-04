# 04. Contratos de API REST e Integraciones Externas — SmartPantry

> **Propósito de este documento:** Definir de manera taxativa las convenciones HTTP, los contratos JSON de entrada/salida de todos los endpoints expuestos por `SmartPantry.HttpApi.Host`, el formato canónico de errores de ABP Framework y la especificación de integración saliente con **Open Food Facts** (*Product Opener API v3* y *Search-a-licious*).

---

## 1. Convenciones Generales de la API

### 1.1 URL Base, Enrutamiento ABP y Versionado
- **URL Base Desarrollo Local:** `https://localhost:44359`
- **Especificación OpenAPI (Versionado `v1`):** `GET /swagger/v1/swagger.json` (Interfaz interactiva en `/swagger`).
- **Convención de Rutas (ABP Conventional Controllers):**
  - Servicios propios del dominio (`SmartPantry.Application`): `/api/app/{nombre-servicio-kebab-case}`
  - Autenticación OAuth2 / OpenIddict: `/connect/token`, `/connect/authorize`, `/connect/logout`
  - Gestión de Perfil Propio (ABP Account): `/api/account/my-profile`
  - Administración de Usuarios y Roles (ABP Identity): `/api/identity/users`, `/api/identity/roles`
- **Serialización JSON:** `camelCase` para todas las claves JSON; fechas ISO-8601 (`"2026-11-15T00:00:00"` para fechas de calendario y `"2026-11-04T22:15:30.124Z"` para timestamps UTC de auditoría).
- **Códigos HTTP Exitosos en ABP Auto API Controllers:**
  - `200 OK`: Operaciones `GET`, `POST` y `PUT` que retornan un DTO.
  - `204 No Content`: Operaciones `DELETE` o métodos `Task` (`void` asíncrono).

### 1.2 Headers Estándar Requeridos

| Header | Obligatoriedad | Valor / Ejemplo | Propósito |
|---|---|---|---|
| `Authorization` | Obligatorio en todos los endpoints bajo `/api/app/*`, `/api/account/*` y `/api/identity/*` | `Bearer eyJhbGciOiJSUzI1NiIs...` | Token de acceso JWT emitido por OpenIddict (`aud = SmartPantry`). |
| `Accept-Language` | Recomendado | `es` o `en` | Determina el idioma de los mensajes de error y textos localizados del servidor. |
| `Content-Type` | Obligatorio en `POST` y `PUT` con body | `application/json; charset=utf-8` | Codificación del cuerpo de la solicitud. |
| `Accept` | Recomendado | `application/json` | Negociación de formato de respuesta. |

### 1.3 Estándar de Paginación (`PagedAndSortedResultRequestDto`)
Todo listado paginado acepta los siguientes parámetros por Query String:
- `skipCount` (`int`, default `0`, mín. `0`): Cantidad de registros a omitir.
- `maxResultCount` (`int`, default `10`, mín. `1`, máx. `1000`): Tamaño de página.
- `sorting` (`string`, opcional, ej. `"CreationTime desc"` o `"ExpirationDate asc"`): Expresión de ordenamiento dinámico.

Y responde siempre con la estructura `PagedResultDto<T>`:
```json
{
  "totalCount": 42,
  "items": []
}
```

### 1.4 Formato Estándar de Errores (`RemoteServiceErrorResponse`)
Ante cualquier error de validación (`400`), negocio (`403` / `409`), autorización (`401` / `403`), inexistencia (`404`) o falla inesperada (`500`), ABP serializa la siguiente envolvente uniforme:

```json
{
  "error": {
    "code": "SmartPantry:PantryItem:NegativeQuantity",
    "message": "La cantidad no puede ser negativa.",
    "details": null,
    "data": {},
    "validationErrors": [
      {
        "message": "The field Quantity must be greater than or equal to 0.",
        "members": ["quantity"]
      }
    ]
  }
}
```

---

## 2. Endpoints Core del Sistema

---

### 2.1 Módulo de Catálogo Externo (`ExternalProductCatalogAppService`)

#### `GET /api/app/external-product-catalog/by-barcode`
Consulta un producto puntual en Open Food Facts por código de barras y evalúa su compatibilidad con las preferencias alimentarias del usuario autenticado (**RF-05, RF-07, RF-09, RF-12**).
- **Estado:** Implementado en backend (extensión pendiente de campos `imageUrl`, `nutriScore`, `novaGroup` y `dietaryEvaluation`).
- **Headers requeridos:** `Authorization: Bearer <token>`, `Accept-Language: es`
- **Query Parameters:**
  - `barcode` (`string`, requerido, `1..32` caracteres numéricos EAN-8 / UPC-A / EAN-13, ej. `7790895000997`).
- **Response `200 OK` (Producto hallado con advertencia alimentaria):**
```json
{
  "status": 0,
  "product": {
    "barcode": "7790895000997",
    "name": "Galletitas Dulces Rellenas Sabor Chocolate",
    "brand": "Bagley",
    "category": "Snacks dulces, Galletas y pasteles",
    "quantity": "118 g",
    "ingredients": "Harina de trigo enriquecida, azúcar, grasa bovina, cacao en polvo, leche descremada en polvo, lecitina de soja.",
    "allergens": "en:gluten, en:milk, en:soybeans",
    "imageUrl": "https://images.openfoodfacts.org/images/products/779/089/500/0997/front_es.jpg",
    "nutriScore": "E",
    "novaGroup": 4,
    "internalProductId": null,
    "dietaryEvaluation": {
      "verificationStatus": 1,
      "matchedTerms": ["gluten", "leche"],
      "informationalMessage": "Advertencia informativa: se detectaron coincidencias con sus preferencias configuradas (gluten, leche). Esta información proviene de Open Food Facts y no constituye diagnóstico médico."
    }
  }
}
```
> **Nota de diseño (`status`):** Las condiciones controladas del proveedor externo (`0 = Found`, `1 = NotFound`, `2 = RateLimitExceeded`, `3 = ServiceUnavailable`) retornan `200 OK` con `product: null` cuando `status != 0`, permitiendo que Angular distinga con precisión entre producto inexistente, exceso de cuota y proveedor caído sin confundirlo con una caída del propio backend de SmartPantry.

- **Errores documentados:**
  - `400 Bad Request`: `barcode` vacío o supera 32 caracteres (`validationErrors` sobre `barcode`).
  - `401 Unauthorized`: Petición sin sesión o token expirado.

---

#### `GET /api/app/external-product-catalog/search`
Realiza una búsqueda paginada por texto libre (nombre, marca o categoría) invocando internamente mediante `POST` a *Search-a-licious* y adjuntando la evaluación alimentaria de cada resultado (**RF-06, RF-07, RF-09, RF-12**).
- **Estado:** Por implementar.
- **Headers requeridos:** `Authorization: Bearer <token>`, `Accept-Language: es`
- **Query Parameters:**
  - `query` (`string`, requerido, `2..100` caracteres, ej. `"yogur natural"`).
  - `skipCount` (`int`, default `0`).
  - `maxResultCount` (`int`, default `10`, máx. `50`).
- **Response `200 OK`:**
```json
{
  "status": 0,
  "results": {
    "totalCount": 28,
    "items": [
      {
        "barcode": "7791337601015",
        "name": "Yogur Clásico Natural Sin Azúcar",
        "brand": "La Serenísima",
        "category": "Lácteos, Productos fermentados, Yogures",
        "quantity": "190 g",
        "ingredients": "Leche entera pasteurizada, fermentos lácticos seleccionados.",
        "allergens": null,
        "imageUrl": "https://images.openfoodfacts.org/images/products/779/133/760/1015/front_es.jpg",
        "nutriScore": "A",
        "novaGroup": 1,
        "internalProductId": "3a14f8c2-9012-4b5a-8811-09ab12cd34ef",
        "dietaryEvaluation": {
          "verificationStatus": 2,
          "matchedTerms": [],
          "informationalMessage": "No verificable: el proveedor externo no informa la totalidad de ingredientes o alérgenos para descartar coincidencias con sus preferencias."
        }
      }
    ]
  }
}
```
- **Errores documentados:**
  - `400 Bad Request`: `query` menor a 2 caracteres o `maxResultCount > 50`.
  - `401 Unauthorized`: Usuario no autenticado.

---

### 2.2 Módulo de Catálogo Interno de Productos (`ProductAppService`)

#### `POST /api/app/product/save-from-external`
Persiste de forma idempotente un producto proveniente de Open Food Facts en `AppProducts`. Si el `barcode` ya existe en la base propia, reutiliza el registro existente evitando duplicados (**RF-08, BR-004**).
- **Estado:** Por implementar sobre `ProductAppService`.
- **Headers requeridos:** `Authorization: Bearer <token>`, `Content-Type: application/json`
- **Request Body (`SaveExternalProductDto`):**
```json
{
  "barcode": "7790895000997",
  "name": "Galletitas Dulces Rellenas Sabor Chocolate",
  "category": 8,
  "brand": "Bagley",
  "quantity": "118 g",
  "ingredients": "Harina de trigo enriquecida, azúcar, grasa bovina, cacao en polvo, leche descremada en polvo, lecitina de soja.",
  "allergens": "en:gluten, en:milk, en:soybeans",
  "externalCategories": "Snacks dulces, Galletas y pasteles",
  "imageUrl": "https://images.openfoodfacts.org/images/products/779/089/500/0997/front_es.jpg",
  "nutriScore": "E",
  "novaGroup": 4
}
```
- **Response `200 OK` (`ProductDto`):**
```json
{
  "id": "8f4b2190-55d1-4e78-9c3a-112233445566",
  "barcode": "7790895000997",
  "name": "Galletitas Dulces Rellenas Sabor Chocolate",
  "category": 8,
  "brand": "Bagley",
  "quantity": "118 g",
  "ingredients": "Harina de trigo enriquecida, azúcar, grasa bovina, cacao en polvo, leche descremada en polvo, lecitina de soja.",
  "allergens": "en:gluten, en:milk, en:soybeans",
  "externalCategories": "Snacks dulces, Galletas y pasteles",
  "imageUrl": "https://images.openfoodfacts.org/images/products/779/089/500/0997/front_es.jpg",
  "nutriScore": "E",
  "novaGroup": 4
}
```
- **Errores documentados:**
  - `400 Bad Request`: `barcode` o `name` vacíos, `category == 0 (Undefined)`, `nutriScore` fuera de `A..E` o `novaGroup` fuera de `1..4`.
  - `401 Unauthorized`: Sin token válido.

#### Endpoints CRUD Complementarios en `/api/app/product` (Ya Implementados)
- `GET /api/app/product?skipCount=0&maxResultCount=10&sorting=Name asc`: Lista paginada de productos guardados internamente (`200 OK`).
- `GET /api/app/product/{id}`: Obtiene detalle de un producto interno por `Guid`, incluyendo `dietaryEvaluation` para el usuario actual (`200 OK`, `404 Not Found` si `id` no existe).
- `POST /api/app/product`: Alta directa con `CreateProductDto` (`200 OK`, `409 Conflict` / `403 BusinessException` si `barcode` está duplicado).
- `PUT /api/app/product/{id}`: Actualización de metadatos con `UpdateProductDto` (`200 OK`, `404 Not Found`).

---

### 2.3 Módulo de Preferencias Alimentarias (`DietaryPreferenceAppService`)

#### `GET /api/app/dietary-preference/my-preferences`
Obtiene el perfil alimentario del usuario autenticado (`CurrentUser.GetId()`). Si aún no fue guardado nunca, devuelve valores iniciales por defecto (**RF-03**).
- **Estado:** Por implementar.
- **Headers requeridos:** `Authorization: Bearer <token>`
- **Response `200 OK` (`DietaryPreferenceDto`):**
```json
{
  "userId": "2d9b8810-1234-4c99-aabb-ccddeeff0011",
  "watchedTerms": ["gluten", "lactosa", "mani"],
  "showAlertsOnScreen": true,
  "lastModificationTime": "2026-11-04T21:40:00Z"
}
```

#### `PUT /api/app/dietary-preference/my-preferences`
Actualiza la lista de términos vigilados y la opción de alertas en pantalla del usuario actual, recalculando las advertencias `"DietaryPreference"` de su despensa (**RF-03, RF-12, BR-010**).
- **Headers requeridos:** `Authorization: Bearer <token>`, `Content-Type: application/json`
- **Request Body (`UpdateDietaryPreferenceDto`):**
```json
{
  "watchedTerms": ["Gluten", "Lactosa", "Maní"],
  "showAlertsOnScreen": true
}
```
- **Response `200 OK` (`DietaryPreferenceDto`):** Retorna los términos normalizados (`["gluten", "lactosa", "mani"]`) sin duplicados.
- **Errores documentados:**
  - `400 Bad Request`: Más de 30 términos o algún término con menos de 2 o más de 64 caracteres.
  - `401 Unauthorized`: Sin sesión válida.

---

### 2.4 Módulo de Despensa Personal (`PantryAppService`)

#### `GET /api/app/pantry`
Lista de manera paginada exclusivamente los ítems pertenecientes a `CurrentUser.GetId()`, proyectando estado de vencimiento, notas privadas y advertencias activas (**RF-11, RF-13, RF-14**).
- **Estado:** Implementado en backend y frontend (extensión pendiente en DTO para `privateNotes`, `isExpired`, `isExpiringSoon` y `activeWarnings`).
- **Headers requeridos:** `Authorization: Bearer <token>`
- **Query Parameters:** `skipCount=0&maxResultCount=10&sorting=CreationTime desc`
- **Response `200 OK` (`PagedResultDto<PantryItemDto>`):**
```json
{
  "totalCount": 2,
  "items": [
    {
      "id": "91a0c3e4-7788-4123-9900-aabbccddeeff",
      "userId": "2d9b8810-1234-4c99-aabb-ccddeeff0011",
      "productId": "8f4b2190-55d1-4e78-9c3a-112233445566",
      "productName": "Galletitas Dulces Rellenas Sabor Chocolate",
      "quantity": 2.0,
      "unit": "paquetes",
      "expirationDate": "2026-11-06T00:00:00",
      "state": 0,
      "privateNotes": "Compradas en oferta para merienda del fin de semana.",
      "isExpired": false,
      "isExpiringSoon": true,
      "activeWarnings": [
        {
          "warningType": "Expiration",
          "message": "Próximo a vencer el 06/11/2026"
        },
        {
          "warningType": "DietaryPreference",
          "message": "Coincidencia detectada con sus preferencias: gluten, leche"
        }
      ],
      "creationTime": "2026-11-04T18:00:00Z",
      "creatorId": "2d9b8810-1234-4c99-aabb-ccddeeff0011",
      "lastModificationTime": null,
      "lastModifierId": null
    }
  ]
}
```

#### `POST /api/app/pantry`
Agrega un producto guardado a la despensa privada del usuario autenticado (**RF-10, RF-12, RF-14**).
- **Headers requeridos:** `Authorization: Bearer <token>`, `Content-Type: application/json`
- **Request Body (`CreatePantryItemDto`):**
```json
{
  "productId": "8f4b2190-55d1-4e78-9c3a-112233445566",
  "quantity": 2.0,
  "unit": "paquetes",
  "expirationDate": "2026-11-06",
  "privateNotes": "Compradas en oferta para merienda del fin de semana."
}
```
- **Response `200 OK`:** Retorna el `PantryItemDto` recién creado con sus `activeWarnings` evaluadas.
- **Errores documentados:**
  - `400 Bad Request`: `productId` vacío, `quantity < 0`, `unit > 32` chars o `privateNotes > 500` chars.
  - `401 Unauthorized`: Sin sesión válida.
  - `404 Not Found`: El `productId` no existe en `AppProducts`.

#### `PUT /api/app/pantry/{id}`
Actualiza cantidad, unidad, fecha de vencimiento y nota privada de un ítem propio (**RF-11, RF-14**).
- **Request Body (`UpdatePantryItemDto`):**
```json
{
  "quantity": 1.0,
  "unit": "paquetes",
  "expirationDate": "2026-11-20",
  "privateNotes": "Queda un solo paquete cerrado."
}
```
- **Response `200 OK`:** Retorna `PantryItemDto` actualizado.
- **Errores documentados:**
  - `400 Bad Request`: Violación de validaciones en cantidad, unidad, fecha o nota.
  - `401 Unauthorized`: Sin token válido.
  - `403 Forbidden`: El ítem pertenece a otro usuario (`AbpAuthorizationException`, **BR-003**).
  - `404 Not Found`: `id` inexistente en `AppPantryItems`.

#### `POST /api/app/pantry/{id}/consume`
Marca un ítem propio como `Consumed (1)` conservándolo en el historial y desactivando su alerta de vencimiento (**RF-11, BR-006**).
- **Request Body:** Vacío (`{}` o sin body).
- **Response `200 OK`:** Retorna `PantryItemDto` con `"state": 1`.
- **Errores:** `401 Unauthorized`, `403 Forbidden` (si es ajeno), `404 Not Found`.

#### `DELETE /api/app/pantry/{id}`
Elimina físicamente el ítem propio y sus advertencias asociadas (**RF-11**).
- **Response `204 No Content`**.
- **Errores:** `401 Unauthorized`, `403 Forbidden` (si es ajeno), `404 Not Found`.

---

### 2.5 Módulo Administrativo de Métricas de Integración (`IntegrationMetricsAppService`)

#### `GET /api/app/integration-metrics/summary`
Devuelve indicadores agregados de uso, tiempos de respuesta y errores de Open Food Facts en una ventana temporal (**HU-05, BR-005**).
- **Estado:** Por implementar.
- **Permiso requerido:** `[Authorize(SmartPantryPermissions.IntegrationMetrics.Default)]` (exclusivo rol `admin`).
- **Query Parameters:**
  - `fromUtc` (`DateTime?`, opcional, default `UtcNow - 7 days`).
  - `toUtc` (`DateTime?`, opcional, default `UtcNow`).
- **Response `200 OK` (`IntegrationMetricsSummaryDto`):**
```json
{
  "fromUtc": "2026-10-28T00:00:00Z",
  "toUtc": "2026-11-04T23:59:59Z",
  "totalCalls": 184,
  "successfulCalls": 169,
  "notFoundCalls": 21,
  "rateLimitedCalls": 3,
  "serviceUnavailableCalls": 12,
  "averageDurationMs": 412.6,
  "maxDurationMs": 4890,
  "byOperation": [
    {
      "operationType": "BarcodeLookup",
      "totalCalls": 74,
      "errorRatePercent": 5.4,
      "averageDurationMs": 318.2
    },
    {
      "operationType": "TextSearch",
      "totalCalls": 110,
      "errorRatePercent": 10.0,
      "averageDurationMs": 476.1
    }
  ]
}
```
- **Errores:** `401 Unauthorized`, `403 Forbidden` (si el usuario autenticado no posee permiso administrativo).

---

## 3. Especificación de Integraciones con Terceros (Open Food Facts)

Toda comunicación con Open Food Facts se encapsula exclusivamente dentro de `OpenFoodFactsProductCatalogClient` en `SmartPantry.HttpApi.Host`.

### 3.1 Proveedor 1: Product Opener API v3 (Consulta por Código — RF-05)
- **URL Base configurada:** `https://world.openfoodfacts.org/` (`OpenFoodFacts:BaseUrl`)
- **Método y Ruta:**
  `GET api/v3/product/{barcode}?fields=code,product_name,brands,categories,quantity,ingredients_text,allergens,image_front_url,nutriscore_grade,nova_group`
- **Headers salientes obligatorios:**
  - `User-Agent: SmartPantry/1.0 (ds-2026-grupo-14; ignacio.treppo@gmail.com)` (exigido por los términos de uso de Open Food Facts para evitar bloqueos WAF).
  - `Accept: application/json`

### 3.2 Proveedor 2: Search-a-licious API (Búsqueda por Texto Libre — RF-06)
- **URL Base configurada:** `https://search.openfoodfacts.org/` (`OpenFoodFacts:SearchBaseUrl`)
- **Método y Ruta:** `POST search`
- **Headers salientes obligatorios:**
  - `User-Agent: SmartPantry/1.0 (ds-2026-grupo-14; ignacio.treppo@gmail.com)`
  - `Content-Type: application/json`
- **Payload `POST` enviado por el Backend:**
```json
{
  "q": "yogur natural",
  "page": 1,
  "page_size": 10,
  "fields": [
    "code",
    "product_name",
    "brands",
    "categories",
    "quantity",
    "ingredients_text",
    "allergens",
    "image_front_url",
    "nutriscore_grade",
    "nova_group"
  ]
}
```
> **Conversión de Paginación:** Dado que ABP trabaja con `skipCount` y `maxResultCount` (base 0) y Search-a-licious utiliza `page` (base 1) y `page_size`, el adaptador calcula `page = (skipCount / maxResultCount) + 1` y `page_size = maxResultCount`.

### 3.3 Tabla de Mapeo de Atributos Externos -> Contrato Propio
| Campo JSON Open Food Facts | Propiedad `ExternalProductDto` | Regla de Transformación y Sanitización |
|---|---|---|
| `code` | `Barcode` | `.Trim()`, truncado defensivo a 32 chars. |
| `product_name` | `Name` | `.Trim()`; si viene vacío/nulo en un hit de búsqueda, se expone `null` para que la UI comunique `"Nombre no informado"` (RF-09). |
| `brands` | `Brand` | `.Trim()` o `null` si está en blanco. |
| `categories` | `Category` | `.Trim()` o `null` si está en blanco. |
| `quantity` | `Quantity` | `.Trim()` o `null` si está en blanco. |
| `ingredients_text` | `Ingredients` | `.Trim()`, truncado seguro a 1024 chars si excede límite de persistencia, o `null` si falta. |
| `allergens` | `Allergens` | `.Trim()` o `null` si está vacío (`""` en OFF se convierte obligatoriamente en `null` para disparar `Unverifiable` según `BR-002`). |
| `image_front_url` | `ImageUrl` | Validado como URI absoluta `https://`; `null` en caso contrario. |
| `nutriscore_grade` | `NutriScore` | Convertido a mayúscula (`"A"`..`"E"`); valores `"unknown"` o `"not-applicable"` de OFF se transforman en `null`. |
| `nova_group` | `NovaGroup` | Entero `1..4`; cualquier otro valor o ausencia se transforma en `null`. |

### 3.4 Políticas de Timeouts, Cuotas y Manejo de Fallas
1. **Timeout estricto de cliente HTTP:** `35 segundos` configurados en `IHttpClientFactory` (`OpenFoodFacts:TimeoutSeconds`). `TaskCanceledException` por timeout se traduce en `ServiceUnavailableException`.
2. **Prohibición de reintentos automáticos ante `429 Too Many Requests`:** Si Open Food Facts responde `HTTP 429`, `OpenFoodFactsProductCatalogClient` lanza inmediatamente `RateLimitExceededException` sin reintentar, respetando las políticas de *fair use* del proveedor comunitario.
3. **Aislamiento de fallas de red y `5xx`:** `HttpRequestException`, `JsonException` o códigos `>= 500` se encapsulan en `ServiceUnavailableException`, devolviendo al frontend un estado controlado (`ExternalProductQueryStatus.ServiceUnavailable`) sin exponer stack traces internos ni URLs de infraestructura.
