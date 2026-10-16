# TP09 – Login y Seguridad con ABP: Evidencias de Verificación (Issue #20)

## Resumen de Implementación
Se implementó el esquema de seguridad y autenticación para el sistema SmartPantry protegiendo los servicios de aplicación RF-05 y RF-08, habilitando la autenticación Bearer en Swagger UI e incorporando un usuario común de pruebas por seed de datos.

- **RF-05 (`ExternalProductCatalogAppService`):** Protegido con `[Authorize]`.
- **RF-08 (`ProductAppService`):** Protegido con `[Authorize]`.
- **Swagger UI (`SmartPantryHttpApiHostModule`):** Configurado con soporte Bearer JWT junto al esquema OIDC de ABP.
- **Usuario de pruebas común (`TestUserDataSeedContributor`):** Usuario `user` / `user@smartpantry.local` con contraseña por defecto `1q2w3E*` (sin rol `admin`).

---

## Matriz de Verificación de Endpoints HTTP

| # | Endpoint / RF | Autenticación / Credencial | HTTP Esperado | HTTP Obtenido | Resultado |
|---|---|---|:---:|:---:|:---:|
| 1 | RF-05 (`GET /api/app/external-product-catalog/by-barcode`) | Sin Header `Authorization` | `401 Unauthorized` | `401 Unauthorized` | PASS |
| 2 | RF-05 (`GET /api/app/external-product-catalog/by-barcode`) | `Authorization: Bearer <invalido>` | `401 Unauthorized` | `401 Unauthorized` | PASS |
| 3 | RF-05 (`GET /api/app/external-product-catalog/by-barcode?Barcode=7790895000997`) | `Authorization: Bearer <token_user>` | `200 OK` | `200 OK` | PASS |
| 4 | RF-08 (`GET /api/app/product`) | Sin Header `Authorization` | `401 Unauthorized` | `401 Unauthorized` | PASS |
| 5 | RF-08 (`GET /api/app/product`) | `Authorization: Bearer <token_user>` | `200 OK` | `200 OK` | PASS |
| 6 | RF-04 (`GET /api/identity/users`) | `Authorization: Bearer <token_user>` | `403 Forbidden` | `403 Forbidden` | PASS |
| 7 | RF-04 (`GET /api/identity/users`) | `Authorization: Bearer <token_admin>` | `200 OK` | `200 OK` | PASS |

---

## Detalle de Evidencias por Caso

### Caso 1: RF-05 sin token (Acceso anónimo bloqueado)
**Petición:**
```http
GET /api/app/external-product-catalog/by-barcode?Barcode=7790895000997 HTTP/1.1
Host: localhost:44359
X-Requested-With: XMLHttpRequest
```
**Respuesta:**
```http
HTTP/1.1 401 Unauthorized
Date: Fri, 16 Oct 2026 19:44:50 GMT
Server: Kestrel
X-Content-Type-Options: nosniff
X-Frame-Options: SAMEORIGIN
```

---

### Caso 2: RF-05 con token Bearer inválido
**Petición:**
```http
GET /api/app/external-product-catalog/by-barcode?Barcode=7790895000997 HTTP/1.1
Host: localhost:44359
Authorization: Bearer invalid_token_xyz
```
**Respuesta:**
```http
HTTP/1.1 401 Unauthorized
Cache-Control: no-store
WWW-Authenticate: Bearer error="invalid_token", error_description="The specified token is invalid.", error_uri="https://documentation.openiddict.com/errors/ID2004"
```

---

### Caso 3: RF-05 con usuario común autenticado (`user`)
**Petición:**
```http
GET /api/app/external-product-catalog/by-barcode?Barcode=7790895000997 HTTP/1.1
Host: localhost:44359
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...[TOKEN_USER_ANONIMIZADO]
```
**Respuesta:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{
  "status": 0,
  "product": {
    "barcode": "7790895000997",
    "name": "coca cola",
    "category": "Artificially sweetened beverages, Colas, Sweetened beverages",
    "brand": "Coca-Cola",
    "quantity": "2,25 L",
    "ingredients": "origin flavor\r\ns col 150 ,aro,ac 38, contains cafena...",
    "allergens": "es:Cafeína, es:Edulcorante"
  }
}
```

---

### Caso 4: RF-08 sin token (Acceso anónimo bloqueado)
**Petición:**
```http
GET /api/app/product HTTP/1.1
Host: localhost:44359
X-Requested-With: XMLHttpRequest
```
**Respuesta:**
```http
HTTP/1.1 401 Unauthorized
Date: Fri, 16 Oct 2026 19:45:00 GMT
Server: Kestrel
```

---

### Caso 5: RF-08 con usuario común autenticado (`user`)
**Petición:**
```http
GET /api/app/product HTTP/1.1
Host: localhost:44359
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...[TOKEN_USER_ANONIMIZADO]
```
**Respuesta:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{
  "totalCount": 3,
  "items": [
    {
      "barcode": "7791234567890",
      "name": "Leche Entera Clásica",
      "category": 1,
      "brand": "La Serenísima",
      "quantity": "1 L",
      "id": "c7c18896-4f8c-19ef-14eb-3a2434d4affc"
    },
    {
      "barcode": "111113",
      "name": "Manteca",
      "category": 1,
      "brand": "La serenisima",
      "quantity": "1Kg",
      "id": "adb8756a-01f5-b84b-3ec7-3a23a4b253f3"
    },
    {
      "barcode": "111112",
      "name": "Queso",
      "category": 1,
      "brand": "La Serenisima",
      "quantity": "1Kg",
      "id": "5be19d3f-8bed-abbc-639c-3a23a4af7580"
    }
  ]
}
```

---

### Caso 6: RF-04 con usuario común (Validación de rol / permiso RBAC)
**Petición:**
```http
GET /api/identity/users HTTP/1.1
Host: localhost:44359
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...[TOKEN_USER_ANONIMIZADO]
```
**Respuesta:**
```http
HTTP/1.1 403 Forbidden
Cache-Control: no-store
WWW-Authenticate: Bearer error="insufficient_access", error_description="The user represented by the token is not allowed to perform the requested action.", error_uri="https://documentation.openiddict.com/errors/ID2095"
```
*El usuario común carece del permiso `AbpIdentity.Users` y del rol `admin`, retornando 403 Forbidden tal como exige el requerimiento.*

---

### Caso 7: RF-04 con usuario administrador (`admin`)
**Petición:**
```http
GET /api/identity/users HTTP/1.1
Host: localhost:44359
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...[TOKEN_ADMIN_ANONIMIZADO]
```
**Respuesta:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{
  "totalCount": 2,
  "items": [
    {
      "userName": "user",
      "email": "user@smartpantry.local",
      "emailConfirmed": true,
      "isActive": true,
      "id": "c9762c89-a36c-4a0b-94ab-3a24586dcf23"
    },
    {
      "userName": "admin",
      "email": "admin@abp.io",
      "emailConfirmed": false,
      "isActive": true,
      "id": "6be90b5d-5408-3d7c-ceab-3a2376456180"
    }
  ]
}
```

---

## Verificación de Servicios en Segundo Plano

Se verificaron los logs de ejecución del worker [`ExpirationWarningWorker`](file:///C:/Users/santi/santiago/Facultad/Tercer%20a%C3%B1o/Segundo%20Cuatri/Desarrollo/SmartPantry/src/SmartPantry.HttpApi.Host/Workers/ExpirationWarningWorker.cs):

```text
[INF] Starting ExpirationWarningWorker to process pantry expirations...
[INF] ExpirationWarningWorker completed successfully. Processed warnings: 2
```

El worker continúa ejecutándose periódicamente sin arrojar excepciones ni verse afectado por las políticas de autorización HTTP.

---

## Verificación de Tests Automatizados

```powershell
dotnet test .\SmartPantry.slnx --configuration Release
```
- Total de pruebas: **33 pasadas** (0 con error, 0 omitidas).
- Gracias a `context.Services.AddAlwaysAllowAuthorization()`, la suite completa de tests de aplicación, dominio y persistencia permanece 100% verde.
