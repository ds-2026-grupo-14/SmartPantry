
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

### Matriz de Comprobaciones de Rúbrica — TP10 (Checklist para PR)

Esta tabla resume las 14 condiciones funcionales y no funcionales evaluadas en el TP10, lista para ser utilizada como referencia en la descripción del Pull Request:

| # | Condición Evaluada | Estado | Criterio de Verificación / Evidencia |
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
