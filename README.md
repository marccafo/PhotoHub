# PhotoHub

Sistema de gestión de fotos y videos auto-hospedado. Indexa, organiza y visualiza tu biblioteca multimedia desde cualquier dispositivo.

## Características

- **Indexación automática** — Escanea directorios, extrae metadatos EXIF y genera miniaturas
- **Línea de tiempo** — Vista cronológica de toda la biblioteca
- **Álbumes y carpetas** — Organización manual y por estructura de directorios
- **Mapa** — Visualización geográfica por metadatos de ubicación
- **Miniaturas** — Generación paralela en tres tamaños (small, medium, large)
- **Etiquetas** — Tags automáticos por ML y tags manuales de usuario
- **Multi-usuario** — Roles, permisos por carpeta y álbum
- **Sincronización desde dispositivo** — Subida de assets desde navegador o app nativa
- **JWT + Refresh Token** — Autenticación con soporte multi-dispositivo
- **Multiplataforma** — Web (Blazor WASM), Android, iOS, Windows, macOS

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core 10, EF Core, PostgreSQL |
| Frontend Web | Blazor WebAssembly |
| App nativa | .NET MAUI |
| UI | MudBlazor 8 |
| Imágenes | ImageSharp, Magick.NET |
| Video | FFmpeg (vía Xabe.FFmpeg) |
| EXIF | MetadataExtractor |
| Auth | JWT Bearer + Refresh Tokens |
| Contenedores | Docker, Docker Compose |

## Estructura del proyecto

```
PhotoHub.sln
└── Src/
    ├── PhotoHub.Server.Api/        # API REST ASP.NET Core 10
    ├── PhotoHub.Client.Shared/     # Razor Class Library (páginas y servicios compartidos)
    ├── PhotoHub.Client.Web/        # Blazor WASM (cliente web)
    └── PhotoHub.Client.Native/     # .NET MAUI (Android, iOS, Windows, macOS)
```

### Responsabilidades por proyecto

**`Client.Shared`** — Todo lo platform-agnostic:
- Componentes: `AssetCard`, `ApiErrorDialog`, `EmptyState`
- Layout: `MainLayout`, `NavMenu`, `LoginLayout`
- Páginas: Albums, Timeline, Folders, Trash, AssetDetail, Login, etc.
- Servicios: interfaces + implementaciones compartidas

**`Client.Web`** — Solo específico de WASM:
- `AuthService` — usa `IJSRuntime`/`localStorage`
- `WebPendingAssetsProvider` — endpoint `/api/assets/device`
- `Device.razor` — página de sincronización desde navegador

**`Client.Native`** — Solo específico de MAUI:
- `MauiAuthService` — usa `SecureStorage`
- `MauiPendingAssetsProvider` — stub (pendiente de implementar)

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) y Docker Compose
- FFmpeg (descargado automáticamente si no está disponible)

## Inicio rápido

### 1. Clonar el repositorio

```bash
git clone https://github.com/tu-usuario/photohub.git
cd photohub
```

### 2. Levantar la infraestructura

```bash
docker compose up -d
```

Esto levanta:
- **PostgreSQL 16** en `localhost:5432`
- **PgAdmin 4** en `http://localhost:5050`
- **PhotoHub API** en `http://localhost:5000`

### 3. Ejecutar en desarrollo (sin Docker)

```bash
cd Src/PhotoHub.Server.Api
dotnet run
```

La API estará disponible en `https://localhost:5001`.
Documentación interactiva: `https://localhost:5001/scalar`

### 4. Cliente web

```bash
cd Src/PhotoHub.Client.Web
dotnet run
```

## Configuración

### Variables de entorno / appsettings

| Clave | Descripción | Default |
|---|---|---|
| `ConnectionStrings:Postgres` | Cadena de conexión PostgreSQL | `Host=localhost;Port=5432;Database=photohub;...` |
| `Jwt:Key` | Clave secreta para JWT (mín. 32 caracteres) | — |
| `Jwt:Issuer` | Emisor del token | `PhotoHub` |
| `Jwt:Audience` | Audiencia del token | `PhotoHub` |
| `ASSETS_PATH` | Ruta al directorio de assets | `C:\PhotoHubAssets\NAS\Assets` |
| `THUMBNAILS_PATH` | Ruta donde se guardan las miniaturas | `{WorkDir}/thumbnails` |

### Usuario administrador (desarrollo)

Configurado en `appsettings.Development.json`:

```json
{
  "AdminUser": {
    "Username": "admin",
    "Email": "admin@photohub.local",
    "Password": "admin123"
  }
}
```

### Docker Compose — variables personalizables

```yaml
# compose.yaml
POSTGRES_DB: photohub
POSTGRES_USER: photohub_user
POSTGRES_PASSWORD: photohub_password
ASSETS_PATH: /ruta/a/tus/fotos
```

## API

La API sigue una estructura por features. Endpoints principales:

| Área | Endpoints |
|---|---|
| Auth | `POST /api/login`, `POST /api/refresh-token` |
| Assets | `GET /api/assets`, `GET /api/assets/{id}`, `GET /api/assets/{id}/content` |
| Indexación | `GET /api/assets/index/stream` (SSE), `GET /api/assets/index` |
| Subida | `POST /api/assets/upload` |
| Miniaturas | `GET /api/assets/{id}/thumbnail/{size}` |
| Álbumes | `GET/POST /api/albums`, `GET /api/albums/{id}` |
| Carpetas | `GET /api/folders` |
| Línea de tiempo | `GET /api/timeline` |
| Mapa | `GET /api/map` |
| Configuración | `GET/PUT /api/settings` |
| Administración | `GET /api/admin/stats`, `GET /api/admin/users` |

Documentación interactiva disponible en `/scalar` (modo desarrollo).

## Indexación

El proceso de indexación es incremental y transmite progreso en tiempo real vía streaming:

1. **Descubrimiento** — Escaneo recursivo del directorio `ASSETS_PATH`
2. **Comparación** — Detección de archivos nuevos, modificados y huérfanos
3. **Extracción EXIF** — Metadatos de imagen/video (fecha, GPS, cámara, etc.)
4. **Detección de tags** — Clasificación automática por ML
5. **Miniaturas** — Generación paralela (small/medium/large)
6. **Base de datos** — Persistencia y limpieza de huérfanos

Accesible desde: `Admin > Colas > Indexar`

## Despliegue con Docker

```bash
# Construir la imagen
docker build -t photohub-api ./Src/PhotoHub.Server.Api

# O usar Docker Compose completo
docker compose up --build
```

La imagen base es `mcr.microsoft.com/dotnet/aspnet:10.0` con `ffmpeg` y `libgdiplus` instalados.

## Plataformas soportadas (MAUI)

| Plataforma | Versión mínima |
|---|---|
| Android | API 24 (Android 7.0) |
| iOS | 15.0 |
| macOS (Catalyst) | 15.0 |
| Windows | 10.0.19041.0 |

## Licencia

GNU Affero General Public License v3.0 — ver [LICENSE](LICENSE) para más detalles.
