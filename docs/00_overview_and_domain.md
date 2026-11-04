# 00. Visión General y Modelo de Dominio — SmartPantry

> **Propósito de este documento:** Establecer el contexto de negocio, la propuesta de valor, el lenguaje ubicuo (*Ubiquitous Language*), los actores, la trazabilidad de requerimientos funcionales y los límites estrictos de alcance de **SmartPantry** (*Despensa Inteligente y Perfil Nutricional*). Este archivo actúa como punto de entrada de la Fuente Única de Verdad (*Single Source of Truth*) del proyecto.

---

## 1. Problema y Solución

### 1.1 El Problema
Las personas que administran alimentos envasados en su hogar enfrentan tres fricciones cotidianas:
1. **Opacidad e dispersión de la información nutricional:** Los rótulos físicos de ingredientes, trazas y alérgenos suelen ser difíciles de leer o interpretar rápidamente al momento de decidir el consumo o almacenamiento.
2. **Riesgo de omisión frente a restricciones personales:** Quienes vigilan determinados ingredientes o alérgenos (por ejemplo, gluten, lactosa, maní, soja o derivados lácteos) deben revisar manualmente cada envase, con alto margen de error humano cuando los datos están incompletos o expresados con distinta terminología.
3. **Desperdicio doméstico por vencimientos inadvertidos:** La falta de un inventario personal estructurado con alertas tempranas según la perecibilidad de cada categoría de alimento provoca que productos almacenados superen su fecha de vencimiento sin ser advertidos a tiempo.

### 1.2 La Solución y Propuesta de Valor
**SmartPantry** es una aplicación web monolítica en capas construida sobre **ABP Framework (.NET 10 + Angular 22 + SQL Server)** que centraliza la consulta de alimentos envasados, la gestión del inventario doméstico personal y la detección temprana de incompatibilidades alimentarias y vencimientos próximos:

- **Integración desacoplada con catálogos abiertos:** Consulta información pública de productos por código de barras (**Product Opener API v3**) y texto libre (**Search-a-licious**) del ecosistema **Open Food Facts**, traduciendo respuestas heterogéneas o incompletas a contratos internos controlados sin inventar datos ausentes.
- **Contraste informativo contra preferencias del usuario:** Permite a cada usuario declarar una lista personalizada de alérgenos o ingredientes a vigilar y contrasta automáticamente dichos términos al abrir, guardar o agregar un producto, distinguiendo explícitamente entre producto compatible, producto con coincidencia detectada y producto **no verificable** por falta de datos del proveedor.
- **Gestión integral de despensa y alertas idempotentes:** Registra existencias privadas por usuario (cantidad, unidad, fecha de vencimiento, estado de consumo y notas privadas) y ejecuta un proceso periódico autónomo en segundo plano que identifica vencimientos próximos o pasados según umbrales por categoría sin generar advertencias duplicadas.
- **Ética y transparencia de datos:** Comunica explícitamente el origen externo de los datos y exhibe un aviso permanente aclarando que las alertas son estrictamente informativas y **no constituyen diagnóstico ni consejo médico o nutricional**.

---

## 2. Ubiquitous Language (Glosario Operativo del Dominio)

Todo el código fuente, contratos DTO, pruebas automatizadas y documentación técnica respetan de manera estricta las siguientes definiciones operativas:

| Término (Español) | Identificador Canónico en Código | Definición Operativa Estricta |
|---|---|---|
| **Producto Externo** | `ExternalProductDto` | Representación transitoria en memoria de un alimento envasado obtenida desde Open Food Facts (vía código de barras o texto libre). No posee identidad persistente interna (`Guid`) hasta que un usuario solicita explícitamente guardarlo en el catálogo propio. |
| **Producto Interno** | `Product` (`AppProducts`) | Agregado raíz persistido en la base de datos propia que representa un alimento envasado identificado de manera única e inmutable por su código de barras (`Barcode`). Sirve como referencia maestra compartida para los ítems de despensa de múltiples usuarios. |
| **Ítem de Despensa** | `PantryItem` (`AppPantryItems`) | Agregado raíz privado perteneciente exclusivamente a un único `UserId`. Vincula un `ProductId` interno con atributos domésticos propios del usuario: `Quantity`, `Unit`, `ExpirationDate`, `State` (`Active`, `Consumed`, `Discarded`) y `PrivateNotes`. |
| **Estado de Ítem de Despensa** | `PantryItemState` | Ciclo de vida operativo de un `PantryItem`: `Active (0)` cuando está disponible en la alacena; `Consumed (1)` cuando fue utilizado y se conserva como historial sin borrado físico automático; `Discarded (2)` cuando se descarta sin consumo. |
| **Preferencia Alimentaria** | `UserDietaryPreference` (`AppUserDietaryPreferences`) | Configuración privada por usuario (`1:1` con `UserId`) que almacena el conjunto normalizado de términos de alérgenos o ingredientes a vigilar (`WatchedTerms`) y el interruptor booleano para visualizar alertas en pantalla (`ShowAlertsOnScreen`). |
| **Advertencia de Despensa** | `PantryItemWarning` (`AppPantryItemWarnings`) | Registro persistente e idempotente asociado a un `PantryItemId` y tipificado por `WarningType` (`"Expiration"` o `"DietaryPreference"`). Posee estado booleano `IsActive` para activarse, actualizar su mensaje o desactivarse sin crear filas duplicadas. |
| **Evaluación Alimentaria** | `DietaryVerificationStatus` | Resultado determinista de cruzar un producto contra `UserDietaryPreference`: `Compatible` (datos completos sin coincidencias), `Warning` (coincidencia hallada en ingredientes o alérgenos), `Unverifiable` (faltan ingredientes o alérgenos en el producto para descartar riesgo) o `NotConfigured` (usuario sin términos activos). |
| **Producto No Verificable** | `DietaryVerificationStatus.Unverifiable` | Condición crítica de seguridad informativa que ocurre cuando el usuario vigila al menos un término pero el producto carece de `Ingredients` o `Allergens` informados por el proveedor. El sistema prohíbe tratar datos ausentes como ausencia de riesgo. |
| **Umbral de Vencimiento** | `ThresholdDays` | Cantidad de días calendario previos o iguales a `ExpirationDate` a partir de los cuales un `PantryItem` en estado `Active` se considera próximo a vencer. Se determina de forma determinista según `ItemCategory` (2 días para lácteos/carnes, 3 días para panadería/frutas/default, 7 días para secos/conservas). |
| **Fecha Local de Referencia** | `ReferenceLocalDate` | Fecha civil truncada a día (`yyyy-MM-dd` sin componente horario) calculada en la zona horaria `America/Argentina/Buenos_Aires` para evaluar vencimientos sin desfasajes por conversión UTC. |
| **Nota Privada** | `PrivateNotes` | Anotación textual libre (máximo 500 caracteres) registrada por el propietario dentro de su `PantryItem` para recordar decisiones de compra, lote, sabor o consumo. Es estrictamente inaccesible para terceros. |
| **Registro de Llamada Externa** | `ExternalApiCallLog` (`AppExternalApiCallLogs`) | Evento de observabilidad persistido por cada invocación saliente hacia Open Food Facts que almacena tipo de operación (`BarcodeLookup` / `TextSearch`), duración en milisegundos (`DurationMs`) y resultado (`OutcomeStatus`), omitiendo tokens de sesión y respuestas crudas completas. |

---

## 3. Actores y Casos de Uso Core

### 3.1 Matriz de Actores del Sistema

| Actor | Identificador / Rol Técnico | Responsabilidades y Capacidades Primordiales |
|---|---|---|
| **Visitante Anónimo** | `Anonymous` (Sin token JWT) | Acceder a la portada pública (`HomeComponent`), visualizar avisos éticos generales e iniciar el flujo de autenticación OAuth2/OIDC hacia ABP Account (`/account/login`). No tiene acceso a catálogo externo, catálogo interno, preferencias ni despensa (`HTTP 401`). |
| **Usuario Autenticado** | Rol autenticado estándar (`ICurrentUser.IsAuthenticated == true`) | Administrar su perfil básico (RF-02) y preferencias alimentarias (RF-03); buscar productos externos por código (RF-05) o texto libre (RF-06); inspeccionar detalles y advertencias informativas (RF-07, RF-09, RF-12); persistir productos en el catálogo interno evitando duplicados (RF-08); administrar su propia despensa, consumos, vencimientos y notas privadas con aislamiento total respecto de otros usuarios (RF-10, RF-11, RF-13, RF-14). |
| **Administrador** | Rol `admin` + Permisos `AbpIdentity.*` y `SmartPantry.IntegrationMetrics.*` | Posee todas las capacidades de un Usuario Autenticado. Adicionalmente, administra cuentas de usuario, asignación de roles y habilitación/bloqueo de accesos mediante ABP Identity (RF-04), y consulta métricas agregadas de uso, latencia y errores de integración con Open Food Facts (HU-05). |
| **Worker de Vencimientos** | `ExpirationWarningWorker` (Proceso interno `AsyncPeriodicBackgroundWorkerBase`) | Ejecuta cada 12 horas (y al inicio del host) el barrido transaccional e idempotente sobre `AppPantryItems` sin depender de una sesión HTTP ni de disponibilidad de Open Food Facts, creando, actualizando o desactivando alertas en `AppPantryItemWarnings` (RF-15). |
| **Ecosistema Open Food Facts** | Actor externo pasivo (`Product Opener API v3` y `Search-a-licious`) | Provee datos públicos de productos alimenticios envasados mediante HTTP REST. No interviene en la autenticación del sistema ni es modificado por SmartPantry. |

### 3.2 Trazabilidad de Requerimientos Funcionales (Estado de Implementación Base vs Objetivo Final)

Esta matriz refleja el estado real auditado en el repositorio tras el cierre del TP10 y guía la construcción de los módulos restantes hasta alcanzar el 100% del alcance obligatorio:

| ID | Requerimiento Funcional | Historia Asociada | Estado Actual en Repositorio | Trabajo Técnico Definido en esta Suite |
|---|---|---|---|---|
| **RF-01** | Autenticación de sesión | Transversal | **Completo** | Mantenido sobre OpenIddict + `[Authorize]` + `authGuard`. |
| **RF-02** | Perfil de usuario | Transversal | **Parcial (Base ABP)** | Nombre, apellido y correo cubiertos por `/account/manage-profile`; soporte opcional de avatar vía `BlobStoring` documentado. |
| **RF-03** | Preferencias alimentarias | HU-01 | **Pendiente** | Entidad `UserDietaryPreference`, `DietaryPreferenceAppService` y pantalla `/preferences` en Angular. |
| **RF-04** | Administración de usuarios | Transversal | **Completo** | Cubierto nativamente por `@abp/ng.identity` y `Volo.Abp.Identity`. |
| **RF-05** | Búsqueda externa por código | HU-02 | **Parcial (Solo Backend)** | Backend operativo (`GetByBarcodeAsync`); se especifica vista `/catalog` en Angular conectada al proxy. |
| **RF-06** | Búsqueda externa por texto | HU-02 | **Pendiente** | Integración `POST` contra Search-a-licious en `OpenFoodFactsProductCatalogClient` + endpoint paginado + vista Angular. |
| **RF-07** | Detalle completo de producto | HU-02 | **Pendiente** | Ampliación de `Product` y `ExternalProductDto` con `ImageUrl`, `NutriScore`, `NovaGroup` y `ExternalCategories` + vista/modal de detalle. |
| **RF-08** | Persistencia interna sin duplicados | HU-03 | **Parcial** | Índice único `Barcode` ya activo; se especifica operación idempotente `SaveFromExternalAsync` y acción desde Angular. |
| **RF-09** | Comunicación de datos incompletos | HU-02, HU-04 | **Parcial (Solo Despensa)** | Resuelto en `PantryComponent`; extendido obligatoriamente a tarjetas de búsqueda y detalle de producto. |
| **RF-10** | Agregar producto a despensa | HU-03 | **Parcial (Solo Backend)** | `PantryAppService.CreateAsync` operativo; se especifica modal de alta en Angular desde Catálogo y desde Mi Despensa. |
| **RF-11** | Administrar despensa propia | HU-03 | **Completo** | Listado paginado, edición, consumo con historial y eliminación confirmada operativos en backend y Angular. |
| **RF-12** | Advertencias por cruce de preferencias | HU-01, HU-04 | **Pendiente** | Servicio de dominio `DietaryWarningManager` evaluando `Compatible`, `Warning` y `Unverifiable` al consultar, guardar y agregar. |
| **RF-13** | Identificación visual de vencimientos | HU-04 | **Parcial (Solo Dominio)** | Proyección de estado de vencimiento y alertas activas en `PantryItemDto` y badges visuales en `PantryComponent`. |
| **RF-14** | Notas privadas en despensa | HU-06 | **Pendiente** | Propiedad `PrivateNotes` en `PantryItem`, migración EF Core, contratos DTO y campo en modal/listado de `PantryComponent`. |
| **RF-15** | Tarea periódica de vencimientos | HU-04 | **Completo** | `ExpirationWarningWorker` y `PantryExpirationManager` operativos con idempotencia garantizada por índice único compuesto. |
| **OBS** | Métricas de integración externa | HU-05 | **Pendiente** | Entidad `ExternalApiCallLog`, registro instrumentado en cliente HTTP y panel administrativo `/admin/integration-metrics`. |

---

## 4. Límites de Alcance (In Scope vs Out of Scope)

Para prevenir desviaciones de alcance (*scope creep*) y alucinaciones funcionales durante el desarrollo, se establecen fronteras taxativas:

### 4.1 Incluido en el Alcance (In Scope — Obligatorio)
1. **Autenticación, perfil y autorización basada en roles/propietario** utilizando ABP Account, ABP Identity y OpenIddict.
2. **Consulta desacoplada a Open Food Facts:** Búsqueda puntual por código de barras vía *Product Opener API v3* (`GET`) y búsqueda paginada por texto libre vía *Search-a-licious* (`POST`).
3. **Visualización transparente de detalle nutricional público:** Nombre, marca, imagen frontal, categorías, lista de ingredientes, alérgenos declarados, grado Nutri-Score (`A`–`E`) y clasificación NOVA (`1`–`4`), indicando explícitamente cualquier atributo no provisto por la fuente externa.
4. **Catálogo interno de productos (`AppProducts`):** Guardado local de productos externos con prevención determinista de duplicados por código de barras.
5. **Perfil alimentario por usuario (`AppUserDietaryPreferences`):** ABM de términos de alérgenos/ingredientes vigilados y switch de visualización de alertas.
6. **Evaluación de advertencias informativas:** Contraste automático en apertura de detalle, guardado interno e incorporación a despensa, reportando coincidencias o estado `Unverifiable` junto con la leyenda ética de no diagnóstico médico.
7. **Gestión completa de despensa personal (`AppPantryItems`):** Alta, listado paginado, edición de cantidad/unidad/vencimiento/nota privada, marcado como consumido conservando historial y baja física con confirmación explícita.
8. **Detección periódica e idempotente de vencimientos:** Worker autónomo en segundo plano (`ExpirationWarningWorker`) y señalización visual de ítems vencidos o próximos a vencer en la interfaz de despensa.
9. **Observabilidad administrativa (`AppExternalApiCallLogs`):** Registro de latencia, operación y estado de llamadas externas y consulta agregada exclusiva para administradores.
10. **Internacionalización completa (`es` / `en`) y suite de pruebas automatizadas** (unitarias de dominio/aplicación con cobertura $\ge 70\%$, integración con base de pruebas y unitarias de componentes Angular en Vitest).

### 4.2 Excluido del Alcance (Out of Scope — Prohibido implementar en el núcleo)
1. **Diagnóstico médico, prescripción dietoterápica o cálculo calórico clínico:** El sistema jamás recomienda tratamientos, dietas personalizadas ni interpreta niveles de severidad clínica (anafilaxia vs intolerancia leve).
2. **Comercio electrónico, carrito de compras, precios o pasarelas de pago:** SmartPantry gestiona inventario doméstico; no cotiza productos ni ejecuta compras reales.
3. **Escritura o contribución hacia Open Food Facts:** La integración con el ecosistema externo es estrictamente de **solo lectura**. No se suben fotos ni se editan productos en servidores de terceros.
4. **Arquitectura de microservicios, mensajería distribuida (Kafka/RabbitMQ) o contenedores multi-cluster:** El sistema es por diseño un monolito modular/en capas sobre ABP Framework y SQL Server.
5. **Multi-tenancy comercial activo o compartición familiar de despensas:** Cada ítem de despensa pertenece a un único `UserId`; no existen despensas grupales ni permisos cruzados entre usuarios estándar.
6. **Extensiones opcionales fuera del núcleo obligatorio:** Envío de correos electrónicos por vencimiento, lectura de códigos mediante cámara web (`MediaDevices`), motores de recomendación por IA, exportación a PDF/Excel de despensa o comparadores lado a lado no deben implementarse mientras exista cualquier ítem pendiente de la tabla 3.2.
