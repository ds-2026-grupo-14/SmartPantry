
# SmartPantry

## Integrantes
- Carricarte Lautaro - lautarocarricarte46317802-star
- Susco Santiago - SantiagoSusco
- Telli Benjamin - BenjaTelli
- Treppo Juan Ignacio - JuanIgnacioTreppo

## Requisitos
Para ejecutar y desarrollar este proyecto, se requiere contar con las siguientes herramientas instaladas:
* Visual Studio 2022 o Visual Studio 2026 con la carga de trabajo *Desarrollo de ASP.NET y web*.
* Node.js 24 LTS (versión 24.15.0 o superior).
* Yarn 1.22.x disponible en la consola.
* SQL Server Developer o SQL Server Express local.
* SQL Server Management Studio (SSMS).
* ABP Studio y Git.

## Configuración local
La persistencia utiliza una base de datos local mediante Entity Framework Core. Se debe configurar la cadena de conexión local en los siguientes archivos:
* `src/SmartPantry.DbMigrator/appsettings.json`
* `src/SmartPantry.HttpApi.Host/appsettings.json`

En ambos archivos, ubicar la sección `ConnectionStrings` y configurar el valor de `Default` de la siguiente manera:
```json
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\MSSQLLocalDB; Database=SmartPantry; Trusted_Connection=True"
  }
}
```

## Puesta en marcha
Para poner en marcha la solución de forma local:
1. Restaurar dependencias ejecutando `abp install-libs` y restaurando la solución con Visual Studio o mediante la terminal con `dotnet restore`.
2. Instalar los paquetes de Angular navegando a la carpeta `angular` y ejecutando `yarn install`.
3. Ejecutar el proyecto `SmartPantry.DbMigrator` (por ejemplo, mediante Visual Studio con F5 o con `dotnet run --project ./src/SmartPantry.DbMigrator`) para aplicar las migraciones y crear las tablas base en la base de datos local.
4. Iniciar el backend ejecutando el proyecto `SmartPantry.HttpApi.Host`.
5. Iniciar la interfaz de Angular ingresando a la carpeta `angular` y ejecutando `yarn start`.

## Cómo detener los procesos
Backend (HttpApi.Host): Si se inició con F5 en Visual Studio, presionar el botón rojo de detener ("Stop Debugging") o Shift+F5. Si se ejecutó desde la terminal con dotnet run, presionar Ctrl + C en esa consola.

Frontend (Angular): Ir a la terminal donde se ejecutó yarn start, presionar Ctrl + C y confirmar la finalización.
* URLs locales de la aplicación:
  * Backend API / Swagger: `https://localhost:44359` 
  * Frontend Angular: `http://localhost:4200`

## Verificación
Para comprobar la correcta compilación y funcionamiento del proyecto mediante los comandos ejecutados por el grupo:
* .NET Build y Test:
  ```bash
  dotnet restore ./SmartPantry.slnx
  dotnet build ./SmartPantry.slnx --configuration Release --no-restore
  dotnet test ./SmartPantry.slnx --configuration Release --no-build
  ```
* Angular Build y Test:
  ```bash
  yarn build
  yarn test --watch=false 
  ```


---

## Decisiones Técnicas - TP10 (Mi despensa · RF-11)

Esta sección documenta y justifica las decisiones de diseño adoptadas en la implementación de la pantalla **"Mi despensa"** (`/pantry`), requerimiento **RF-11**, en cumplimiento con la rúbrica del TP10.

### 1. Conservación de Ítems Consumidos (Regla de Negocio 6)
- **Decisión:** Al marcar un ítem como consumido (`ConsumeAsync`), el registro **no se elimina físicamente de la base de datos**. En su lugar, su estado transiciona a `PantryItemState.Consumed`.
- **Justificación:** Preservar los productos consumidos permite contar con un historial de consumo del usuario para futuras funcionalidades de reabastecimiento o estadísticas de desperdicio. En la interfaz gráfica se muestran en la misma lista pero identificados visualmente con un badge atenuado (`Consumido`), deshabilitando las acciones de volver a consumir y editar. Si el usuario desea removerlo definitivamente, dispone de la acción explícita de eliminación (`DeleteAsync`).

### 2. Paginación y Gestión de Estado con `ListService`
- **Decisión:** Se utiliza la utilidad oficial `@abp/ng.core/ListService` como proveedora del ciclo de vida reactivo del listado y la paginación (`NgbPagination`).
- **Justificación:** `ListService` desacopla el ruteo de la tabla, administra de forma automática los parámetros `maxResultCount` (configurado en 10 ítems por página) y `skipCount`, y reacciona de manera limpia a cambios de página y ordenamiento (`Sorting`), garantizando una experiencia estándar e integrada al ecosistema ABP Framework.

### 3. Criterio de Manejo de Fechas Locales de Calendario
- **Decisión:** Las fechas de vencimiento ingresadas y mostradas representan fechas de calendario civil (`yyyy-MM-dd`), truncadas a nivel de día (`.Date`) en el backend.
- **Justificación:** Evita desfasajes de huso horario causados por conversiones UTC en fechas sin hora. Este criterio se encuentra estrictamente alineado con la regla de negocio del worker de vencimientos del TP08 (`PantryExpirationManager.GetArgentinaLocalDate()`). Además, el formulario valida en el cliente que el año se encuentre en el rango representativo `1900 - 2100` y sea un día real en el calendario (rechazando fechas como 31 de febrero).

### 4. Manejo Resiliente de Estados Visuales y Errores
- **Decisión:** La llamada a `pantryService.getList(...)` se realiza con `{ skipHandleError: true }`, distinguiendo 4 estados visuales excluyentes: `loading`, `error`, `empty` y `results`.
- **Justificación:** Suprimir el manejador de errores global de ABP para la carga de la tabla previene la aparición de popups modales intrusivos ante cortes intermitentes de red, permitiendo presentar en su lugar una alerta en línea con un botón interactivo de **"Reintentar"** (`retry()`). Asimismo, en el modal de edición, si el backend rechaza la solicitud (`save()`), el formulario conserva intactos los datos ingresados para que el usuario pueda corregirlos sin perder su trabajo.

### 5. Aislamiento Estricto por Usuario y Seguridad (403 Forbidden)
- **Decisión:** Todas las operaciones del servicio de despensa están restringidas con `[Authorize]`. Las consultas de listado filtran de forma determinista por `CurrentUser.GetId()`, y las operaciones unitarias (`GetAsync`, `UpdateAsync`, `ConsumeAsync`, `DeleteAsync`) verifican pertenencia mediante el método de seguridad `GetOwnedItemAsync(id)`.
- **Justificación:** Si un usuario intenta consultar, editar, consumir o eliminar un ítem ajeno manipulando el identificador (`Guid`), el backend arroja inmediatamente `AbpAuthorizationException`, traduciéndose en una respuesta uniforme **HTTP 403 Forbidden**, previniendo fugas de datos entre usuarios (IDOR).

### 6. Comunicación Explícita de Datos Faltantes (RF-09)
- **Decisión:** Si un ítem no posee cantidad, unidad o fecha de vencimiento (`null`), la interfaz renderiza explícitamente el texto localizado `"No especificado"` (o `"Not specified"`).
- **Justificación:** Cumple con la directriz del TP de no inventar ni asumir valores por defecto (como 0 o fechas vacías arbitrarias), informando con transparencia al usuario el estado de completitud de su despensa.

---

### Tabla de Correspondencia de Requerimientos (RF-10 / RF-11)

| Requerimiento | Endpoint Backend | Método AppService (C#) | Método Proxy Frontend (TypeScript) | Propósito |
|---|---|---|---|---|
| **RF-10** (Agregar a despensa) | `POST /api/app/pantry` | `CreateAsync(CreatePantryItemDto)` | `pantryService.create(...)` | Alta de ítem asociado al usuario actual con cantidad, unidad y fecha opcionales. |
| **RF-11** (Listar despensa) | `GET /api/app/pantry` | `GetListAsync(PagedAndSorted...)` | `pantryService.getList(...)` | Consulta paginada y filtrada exclusivamente por el usuario autenticado. |
| **RF-11** (Detalle de ítem) | `GET /api/app/pantry/{id}` | `GetAsync(Guid)` | `pantryService.get(...)` | Obtención de detalle con validación de propiedad (403 si es ajeno). |
| **RF-11** (Editar ítem) | `PUT /api/app/pantry/{id}` | `UpdateAsync(Guid, Update...)` | `pantryService.update(...)` | Modificación de cantidad, unidad y vencimiento (403 si es ajeno). |
| **RF-11** (Marcar como consumido)| `POST /api/app/pantry/{id}/consume` | `ConsumeAsync(Guid)` | `pantryService.consume(...)` | Transición a estado `Consumed` conservando historial (Regla 6). |
| **RF-11** (Eliminar de despensa) | `DELETE /api/app/pantry/{id}` | `DeleteAsync(Guid)` | `pantryService.delete(...)` | Baja física definitiva tras confirmación modal del usuario. |

---

### Comprobaciones de todos los grupos

> **Consigna:** Completar esta tabla en el PR, indicando el componente, el servicio del proxy y los métodos utilizados por el grupo, y adjuntar las capturas indicadas.

| Pantalla o API | Condición | Resultado esperado | Componente, Servicio del Proxy y Métodos | Evidencia / Estado |
|---|---|---|---|:---:|
| **Navegación** | Acceso a la URL sin sesión | Redirección al login de ABP | `app.routes.ts` (`canActivate: [authGuard]`) | Cumplido (Redirección automática al login de ABP) |
| **Menú** | Usuario autenticado | Aparece “Mi despensa” y navega a la pantalla | `route.provider.ts` (`RoutesService`, `::Menu:MyPantry`) | Cumplido (Ítem visible con icono `fas fa-box-open` -> navega a `/pantry`) |
| **Listado** | Respuesta pendiente | Estado de carga; sin mensaje de lista vacía | `PantryComponent` (`viewStatus = 'loading'`, `::Pantry:Loading`) | Cumplido (Spinner activo con mensaje de carga; no muestra banner de vacía) |
| **Listado** | BackEnd detenido | Estado de error con mensaje comprensible y opción de reintentar | `PantryComponent` (`viewStatus = 'error'`, `retry()` -> `list.get()`, `skipHandleError: true`) | Cumplido (Alerta con mensaje comprensible y botón de reintento en línea) |
| **Listado** | Usuario sin ítems | Lista vacía con mensaje localizado | `PantryComponent` (`viewStatus = 'empty'`, `::Pantry:Empty`) | Cumplido (Mensaje localizado `"No tienes productos en tu despensa."`) |
| **Listado** | Usuario con ítems | Solo sus ítems; datos faltantes comunicados sin inventar valores | `PantryComponent` (`pantryService.getList`), `::Pantry:NotSpecified` | Cumplido (Aislamiento por `CurrentUser.GetId()`; valores nulos como `"No especificado"`) |
| **Edición** | Cantidad negativa o fecha inválida | Mensaje de validación localizado; no se invoca al servidor | `PantryComponent` (`Validators.min(0)`, `calendarDateValidator`) | Cumplido (Validación reactiva con mensaje de error; no invoca a la API) |
| **Edición** | Datos válidos | Cambio visible en la lista y persistido | `PantryComponent` (`pantryService.update(id, input)`) | Cumplido: Cambiamos la cantidad de 1 a 3 y, al guardar, la lista mostró 3 de forma persistida en BD |
| **Consumo** | Marcar un ítem | Estado actualizado según la decisión documentada | `PantryComponent` (`pantryService.consume(id)`) | Cumplido: Apareció como ‘Consumido’ y permaneció en la lista según la RN-6 |
| **Eliminación** | Cancelar la confirmación | El ítem permanece; sin llamada al servidor | `PantryComponent` (`ConfirmationService`, `Status.reject`) | Cumplido: Cancelamos el diálogo y el producto siguió en la lista (sin invocación al backend) |
| **Eliminación** | Confirmar | El ítem desaparece de la lista y de la base | `PantryComponent` (`pantryService.delete(id)`) | Cumplido: Confirmamos; desapareció de la lista y la consulta posterior no lo encontró |
| **API de despensa** | Ítem de otro usuario, con Bearer válido (Swagger) | No se lee ni se modifica; 404 o 403 según lo documentado | `PantryAppService` (`GetOwnedItemAsync`) | Cumplido (**403 Forbidden** vía `AbpAuthorizationException`) |
| **API de despensa** | Perfil sin sesión y sin token | 401 | `PantryAppService` con atributo `[Authorize]` | Cumplido (**401 Unauthorized** retornado por ABP) |
| **Idioma** | Cambio entre español e inglés | Todos los textos de la pantalla cambian | `LocalizationPipe` (`abpLocalization`), recursos `es.json` / `en.json` | Cumplido (Traducción completa de textos de menú, tablas, modales y botones) |

> *Compilar el BackEnd, ejecutar las pruebas existentes y las del componente, y verificar que el worker del TP08 continúa funcionando sin usuario conectado.*
> 
> *Nota sobre evidencias de eliminación: Se adjunta la captura del diálogo de confirmación abierto, la captura del resultado tras cancelar (el ítem sigue en la lista) y la captura tras confirmar (el ítem desaparece).*
