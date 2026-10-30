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

### Comprobaciones de todos los grupos

*Completar esta tabla en el PR, indicando el componente, el servicio del proxy y los métodos utilizados por el grupo, y adjuntar las capturas indicadas.*

| Pantalla o API | Condición | Resultado esperado | Componente, servicio del proxy y métodos utilizados | Captura / Evidencia |
|---|---|---|---|---|
| **Navegación** | Acceso a la URL sin sesión | Redirección al login de ABP | `app.routes.ts` (`canActivate: [authGuard]`) | *[Adjuntar captura de redirección a /account/login]* |
| **Menú** | Usuario autenticado | Aparece “Mi despensa” y navega a la pantalla | `route.provider.ts` (`RoutesService`, `::Menu:MyPantry`) | *[Adjuntar captura del menú lateral y navegación]* |
| **Listado** | Respuesta pendiente | Estado de carga; sin mensaje de lista vacía | `PantryComponent` (`viewStatus = 'loading'`, spinner con `::Pantry:Loading`) | *[Adjuntar captura del estado de carga / spinner]* |
| **Listado** | BackEnd detenido | Estado de error con mensaje comprensible y opción de reintentar | `PantryComponent` (`viewStatus = 'error'`, botón reintentar con `retry()`, `{ skipHandleError: true }`) | *[Adjuntar captura del estado de error y botón reintentar]* |
| **Listado** | Usuario sin ítems | Lista vacía con mensaje localizado | `PantryComponent` (`viewStatus = 'empty'`, `::Pantry:Empty`) | *[Adjuntar captura de despensa vacía con mensaje]* |
| **Listado** | Usuario con ítems | Solo sus ítems; datos faltantes comunicados sin inventar valores | `PantryComponent` (`pantryService.getList(...)`), `::Pantry:NotSpecified` | *[Adjuntar captura de lista con ítems y textos "No especificado"]* |
| **Edición** | Cantidad negativa o fecha inválida | Mensaje de validación localizado; no se invoca al servidor | `PantryComponent` (`Validators.min(0)`, `calendarDateValidator`) | *[Adjuntar captura del modal con mensaje de validación]* |
| **Edición** | Datos válidos | Cambio visible en la lista y persistido | `PantryComponent` (`pantryService.update(id, input)`) | Cambiamos la cantidad de 1 a 3 y, al guardar, la lista mostró 3. <br> *[Adjuntar captura de la lista mostrando la cantidad actualizada]* |
| **Consumo** | Marcar un ítem | Estado actualizado según la decisión documentada | `PantryComponent` (`pantryService.consume(id)`) | Apareció como ‘Consumido’ y permaneció en la lista. <br> *[Adjuntar captura del producto con badge "Consumido" en la lista]* |
| **Eliminación** | Cancelar la confirmación | El ítem permanece; sin llamada al servidor | `PantryComponent` (`confirmation.warn(...)`, `Status.reject`) | Cancelamos el diálogo y el producto siguió en la lista. <br> *[Adjuntar captura del resultado después de cancelar]* |
| **Eliminación** | Confirmar | El ítem desaparece de la lista y de la base | `PantryComponent` (`pantryService.delete(id)`) | Confirmamos; desapareció de la lista y la consulta posterior no lo encontró. <br> *[Adjuntar captura del resultado después de confirmar]* |
| **API de despensa** | Ítem de otro usuario, con Bearer válido (Swagger) | No se lee ni se modifica; 404 o 403 según lo documentado | `PantryAppService` (`GetOwnedItemAsync`, lanza `AbpAuthorizationException`) | *[Adjuntar captura de Swagger con respuesta 403 Forbidden]* |
| **API de despensa** | Perfil sin sesión y sin token | 401 | `PantryAppService` anotado con `[Authorize]` | *[Adjuntar captura de Swagger o Postman con respuesta 401 Unauthorized]* |
| **Idioma** | Cambio entre español e inglés | Todos los textos de la pantalla cambian | `LocalizationPipe` (`abpLocalization`), recursos `es.json` y `en.json` | *[Adjuntar capturas comparativas en Español e Inglés]* |

> **Nota sobre evidencias de eliminación requeridas por la corrección docente:**
> 1. Captura del diálogo modal de confirmación abierto.
> 2. Captura del resultado inmediatamente **después de cancelar** (el producto continúa en la lista).
> 3. Captura del resultado inmediatamente **después de confirmar** (el producto desaparece de la lista).

> Compilar el BackEnd, ejecutar las pruebas existentes y las del componente, y verificar que el worker del TP08 continúa funcionando sin usuario conectado.

## Decisiones o dudas para la revisión
- **Historial de consumidos:** Se decidió no eliminar el registro de la base de datos al consumirse, sino cambiar su estado a `Consumed` y mostrarlo con un badge diferenciado en la misma grilla, cumpliendo estrictamente con la Regla de Negocio 6.
- **Manejo de errores en carga:** Se utilizó `{ skipHandleError: true }` en el listado de Angular para evitar alertas modales intrusivas globales del framework ABP y ofrecer una experiencia más amigable con el botón en línea de reintentar.
