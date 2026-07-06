# OpenGameHUB

[English](README.md)

Meta-lanzador ligero para Windows que unifica tus bibliotecas de juegos de varias tiendas en un solo sitio. Explora juegos instalados y en la nube, lánzalos o instálalos, y mantén portadas y tiempo de juego en una única biblioteca.

> **Plataforma:** solo Windows 10/11 por ahora. Linux aún no está soportado.

Los instaladores precompilados se publican en [GitHub Releases](https://github.com/Davidjc13/OpenGameHUB/releases).

## Características

- **Biblioteca multiplataforma** — Detecta juegos de Steam, Epic, GOG, Ubisoft Connect, EA App, Battle.net, Rockstar, Riot Games y **Xbox PC / Game Pass** (instalados) mediante [GameLib.NET](https://github.com/Phalkion/GameLib.NET) y [GameFinder](https://github.com/erri120/GameFinder).
- **Bibliotecas en la nube** — Los juegos de Steam (Web API o cuenta local) y Epic (vía [legendary](https://github.com/derrod/legendary)) aparecen aunque no estén instalados. Los catálogos en la nube de Ubisoft y EA están soportados cuando están disponibles.
- **Instalar desde la app** — Los juegos de Steam no instalados abren el flujo de instalación de Steam. Los juegos Epic en la nube activan la instalación en el **Epic Games Launcher** (la app no espera a que termine la descarga).
- **Cuenta Epic** — Conecta o desconecta tu cuenta Epic desde Ajustes para sincronizar la biblioteca en la nube.
- **Portadas** — Steam CDN, Wikipedia, IGDB, SteamGridDB y mapeos integrados para títulos de Riot. Las portadas se guardan en caché local.
- **Tiempo de juego** — Sincronización de tiempo de juego de Steam cuando hay credenciales de API configuradas.
- **Favoritos y filtros** — Búsqueda, filtro por plataforma, orden por nombre/tiempo de juego, solo favoritos o solo instalados.
- **Actualizaciones integradas** — Las builds instaladas pueden comprobar GitHub Releases y descargar el último instalador desde Ajustes.
- **Localización** — Interfaz en inglés y español.

## Requisitos

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (para desarrollo)
- Launchers instalados de las plataformas que uses (Steam, Epic Games Launcher, etc.)

### Opcional

| Integración | Propósito |
|-------------|-----------|
| Epic Games Launcher + legendary | Biblioteca Epic en la nube, inicio de sesión e instalaciones |
| Steam Web API key + SteamID64 | Biblioteca Steam en la nube y tiempo de juego (sin API, se usan datos locales de Steam si está instalado) |
| IGDB Client ID + Secret | Mejores portadas para juegos que no son de Steam |
| SteamGridDB API key | Fuentes alternativas de portadas |

## Descarga

Descarga el último `OpenGameHUB-Setup-*.exe` desde [Releases](https://github.com/Davidjc13/OpenGameHUB/releases) y ejecútalo. No requiere permisos de administrador.

Ubicación tras la instalación:

```
%LocalAppData%\Programs\OpenGameHUB\
```

## Primeros pasos (desarrollo)

```powershell
git clone https://github.com/Davidjc13/OpenGameHUB.git
cd OpenGameHUB
dotnet run
```

### Publicar un ejecutable independiente

```powershell
.\publish.ps1
```

Salida: `publish/win-x64/OpenGameHUB.exe` (autocontenido, un solo archivo).

### Crear el instalador de Windows (`setup.exe`)

Requiere [Inno Setup 6](https://jrsoftware.org/isinfo.php) (se instala automáticamente con winget si no está presente).

```powershell
.\build-installer.ps1
```

Con versión explícita (recomendado para releases):

```powershell
.\build-installer.ps1 -AppVersion "alpha-0.0.7"
```

Salida: `dist/OpenGameHUB-Setup-<version>.exe`

El instalador:
- Instala en `%LocalAppData%\Programs\OpenGameHUB` (por usuario, sin admin)
- Crea acceso directo en el menú Inicio y opcionalmente en el escritorio
- Registra un desinstalador en Configuración de Windows
- Incluye el paquete de idioma español (`es/`)
- Muestra la licencia GPL-3.0 durante la instalación

### Flujo de release (CI)

Al subir un tag que coincida con `alpha-*`, `beta-*` o `x.y.z`, se ejecuta [`.github/workflows/build-installer.yml`](.github/workflows/build-installer.yml), que compila el instalador y lo adjunta a la GitHub Release.

Ejemplo:

```powershell
git tag alpha-0.0.8
git push origin alpha-0.0.8
```

## Configuración

Abre **Ajustes** en la app para configurar:

- **Idioma** — Inglés o español
- **Steam Web API** — [API key](https://steamcommunity.com/dev/apikey) y [SteamID64](https://steamid.io)
- **Cuenta Epic** — Conectar o desconectar Epic para sincronizar la biblioteca en la nube
- **Visualización** — Activar o desactivar portadas en la rejilla de juegos
- **IGDB / SteamGridDB** — Proveedores opcionales de portadas
- **Actualizaciones** — Buscar actualizaciones e instalar la última release de GitHub

Los ajustes se guardan en:

```
%LocalAppData%\OpenGameHUB\settings.json
```

## Documentación

Documentación técnica (en inglés): **[docs/](docs/README.md)**.

## Almacenamiento de datos

| Ruta | Contenido |
|------|-----------|
| `%LocalAppData%\OpenGameHUB\library.db` | Biblioteca de juegos (SQLite) |
| `%LocalAppData%\OpenGameHUB\covers\` | Portadas en caché |
| `%LocalAppData%\OpenGameHUB\tools\` | Ayudante legendary descargado (si hace falta) |
| `%USERPROFILE%\.config\legendary\` | Credenciales Epic gestionadas por legendary |

## Modo desarrollador

Al compilar en **Debug**, o con `OPENGAMEHUB_DEV=1`, Ajustes muestra la sección **Desarrollador**:

- **Restablecer conexiones** — Borra Steam API, auth Epic y avisos de onboarding
- **Restablecer y relanzar** — Lo mismo, borra la caché local de biblioteca y reinicia la app
- **Borrar base de datos local** — Elimina `library.db` y la caché de portadas, y actualiza la biblioteca

```powershell
$env:OPENGAMEHUB_DEV = "1"
dotnet run
```

## Estructura del proyecto

```
OpenGameHUB/
├── Data/              Persistencia SQLite
├── Models/            Tipos de dominio (UnifiedGame, Platform, …)
├── Services/          Escaneo, metadatos, lanzamiento, actualizador
│   ├── Epic/          Manifiestos Epic, bootstrap de legendary, cliente del launcher
│   ├── Ea/            Ayudantes del catálogo EA Desktop
│   └── LibraryProviders/  Proveedores de biblioteca en la nube (Steam, Epic, Ubisoft, EA)
├── ViewModels/        Capa MVVM
├── Views/             UI Avalonia (MainWindow, Ajustes, avisos de onboarding)
├── Resources/         Cadenas de localización (en / es)
├── Localization/      Bindings de textos de la UI
├── Converters/        Convertidores de Avalonia
├── installer/         Script de Inno Setup
└── tools/             Utilidades de diagnóstico (no forman parte del build principal)
```

## Stack tecnológico

- **.NET 8** (`net8.0-windows`)
- **Avalonia UI 12** — Interfaz de escritorio
- **CommunityToolkit.Mvvm** — Helpers MVVM
- **Dapper + Microsoft.Data.Sqlite** — Base de datos local
- **GameLib.NET + GameFinder** — Detección de launchers y juegos
- **legendary** — Biblioteca Epic en la nube y autenticación

## Aviso legal

OpenGameHUB es un proyecto independiente. **No está afiliado ni respaldado** por Steam, Epic Games, Ubisoft, EA, Microsoft ni ninguna otra plataforma. Se requieren los launchers oficiales para instalar y jugar. El software se proporciona **sin garantías**; consulta [LICENSE](LICENSE).

## Licencia

Este proyecto está bajo la [GNU General Public License v3.0](LICENSE).
