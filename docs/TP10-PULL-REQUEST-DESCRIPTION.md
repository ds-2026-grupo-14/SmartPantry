## Issue asociado
Closes #26

## Qué se implementó
Implementación completa de la capacidad de interfaz de usuario **"Mi despensa"** (**RF-11**) y soporte para agregar ítems (**RF-10**) mediante una arquitectura por capas desacopladas:

1. **Dominio & Shared (`Capa 1`):**
   - Extensión de la entidad `PantryItem` con propiedades opcionales `Quantity` y `Unit`, validando invariantes (cantidad no negativa, longitud máxima de unidad a 32 caracteres).
   - Claves de localización en español e inglés (`es.json`, `en.json`) incluyendo estados, acciones y textos de campos no especificados.
   - Datos iniciales en `PantryDataSeedContributor` con ejemplos para pruebas.

2. **Persistencia & Migraciones (`Capa 2`):**
   - Mapeo de `Quantity` (`decimal(18,2)`) y `Unit` (`nvarchar(32)`) en `SmartPantryDbContext`.
   - Migración de base de datos `AddPantryItemQuantityAndUnit` aplicada vía `DbMigrator`.

3. **Contratos & Aplicación (`Capa 3`):**
   - Interfaz `IPantryAppService` y DTOs (`PantryItemDto`, `CreatePantryItemDto`, `UpdatePantryItemDto`).
   - Implementación de `PantryAppService` bajo `[Authorize]` con aislamiento estricto por usuario y respuesta uniforme **HTTP 403 Forbidden** (`AbpAuthorizationException`) ante accesos a ítems de terceros.

4. **Pruebas Automatizadas de Backend (`Capa 4`):**
   - Suite integral en `SmartPantry.Application.Tests` (`PantryAppServiceTests.cs`) y runner EF Core (`EfCorePantryAppServiceTests.cs`) cubriendo ciclo de vida CRUD, aislamiento de usuario, validaciones y reglas de negocio.

5. **Proxies & Ruteo en Angular (`Capa 5`):**
   - Proxies tipados de TypeScript generados en `@proxy/pantry`.
   - Ruta `/pantry` registrada con carga diferida (`loadComponent`) y protegida con `authGuard`.
   - Ítem *"Mi despensa"* en el menú principal (`fas fa-box-open`).

6. **Componente Standalone "Mi despensa" (`Capa 6`):**
   - Manejo reactivo con `ListService` y distinción de los 4 estados visuales: `loading`, `error` (con botón de reintento en línea sin modal intrusivo), `empty` y `results`.
   - Visualización de datos faltantes como `"No especificado"` sin inventar valores (**RF-09**).
   - Formulario reactivo modal de edición con validaciones en cliente, bloqueo de botón mientras guarda y retención de datos si el servidor rechaza.
   - Acción de consumo conservando el registro como historial (**Regla de Negocio 6**) y eliminación previa confirmación con `ConfirmationService`.

7. **Pruebas Unitarias de Frontend (`Capa 7`):**
   - Suite en Vitest (`pantry.component.spec.ts`) mockeando llamadas HTTP y validando los 4 estados y formularios.

8. **Documentación (`Capa 8`):**
   - `README.md` actualizado con detalle y justificación de decisiones técnicas, tabla de correspondencia de endpoints y matriz de comprobaciones de rúbrica.

## Cómo se probó
- [x] Verificación manual o documental
- [x] Job `verify` de GitHub Actions en verde
- [x] Pruebas automatizadas (desde TP03; indicar "No aplica" en TP02):
  - `dotnet test ./SmartPantry.slnx`
  - `yarn test --watch=false`

## Evidencia

### Matriz de Comprobaciones del TP10 (14 Condiciones Evaluadas)

| # | Condición Evaluada | Estado | Evidencia |
|---|---|:---:|---|
| 1 | **Ruta protegida y menú** | Cumplido | Menú lateral con ítem *"Mi despensa"* (`fas fa-box-open`), redirige a `/pantry` protegido con `authGuard`. |
| 2 | **Estado de Carga (`loading`)** | Cumplido | Muestra spinner con texto `"Cargando ítems..."` mientras la petición está en curso; no adelanta mensaje de despensa vacía. |
| 3 | **Estado de Despensa Vacía (`empty`)** | Cumplido | Si `totalCount === 0`, presenta banner informativo amigable `"No tienes productos en tu despensa."`. |
| 4 | **Estado de Error con Reintento (`error`)** | Cumplido | Si la API falla, muestra alerta de error con botón `"Reintentar"`; al pulsarlo reejecuta `list.get()`. |
| 5 | **Visualización de Ítems (`results`)** | Cumplido | Grilla con columnas: Producto, Cantidad, Unidad, Vencimiento, Estado y Acciones. |
| 6 | **Datos Faltantes (RF-09)** | Cumplido | Campos nulos (`quantity`, `unit`, `expirationDate`) muestran `"No especificado"` sin inventar valores. |
| 7 | **Paginación estándar** | Cumplido | Integración de `ListService` y `ngb-pagination` para navegación de páginas de 10 elementos. |
| 8 | **Modal de Edición Reactivo** | Cumplido | Modal con campos reactivos para Cantidad, Unidad y Fecha de Vencimiento. |
| 9 | **Validación en Cliente (Edición)** | Cumplido | Rechaza cantidades negativas (`min(0)`), unidades mayores a 32 caracteres y fechas inválidas en calendario. |
| 10 | **Bloqueo durante Guardado** | Cumplido | Botón Guardar se deshabilita con spinner mientras la petición HTTP está activa (previene doble submit). |
| 11 | **Resiliencia ante Fallo de Guardado**| Cumplido | Si el servidor rechaza el guardado, el formulario permanece abierto conservando los datos ingresados. |
| 12 | **Consumo de Ítems (Regla de Negocio 6)**| Cumplido | Acción *"Consumir"* actualiza estado a badge `"Consumido"` y mantiene el ítem en la base de datos como historial. |
| 13 | **Confirmación previa de Eliminación** | Cumplido | Acción *"Eliminar"* dispara diálogo modal (`ConfirmationService`); solo borra si el usuario confirma. |
| 14 | **Diseño Responsivo** | Cumplido | Tabla adaptativa con contenedores responsivos que previenen scroll horizontal en dispositivos móviles. |

## Decisiones o dudas para la revisión
- **Historial de consumidos:** Se decidió no eliminar el registro de la base de datos al consumirse, sino cambiar su estado a `Consumed` y mostrarlo con un badge diferenciado en la misma grilla, cumpliendo estrictamente con la Regla de Negocio 6.
- **Manejo de errores en carga:** Se utilizó `{ skipHandleError: true }` en el listado de Angular para evitar alertas modales intrusivas globales del framework ABP y ofrecer una experiencia más amigable con el botón en línea de reintentar.
