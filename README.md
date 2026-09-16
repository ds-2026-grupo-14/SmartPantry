
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
