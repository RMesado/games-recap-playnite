# Games Recap Plugin — Contexto del Proyecto

## Objetivo
Plugin de biblioteca para Playnite que integra gamesrecap.io vía API Inertia.js, con caché local SQLite, navegador de juegos, wishlist y sincronización con la biblioteca de Playnite.

## Stack
- **Runtime**: .NET Framework 4.8.1, C# 12.0, WPF
- **Serialización**: `DataContractJsonSerializer` (System.Runtime.Serialization) — NO Newtonsoft.Json (evita conflictos de versión con Playnite)
- **Convención JSON**: `[DataMember(Name = "snake_case")]`
- **Base de datos**: System.Data.SQLite (System.Data.SQLite.Core.NetFramework 1.0.119.0, Stub para xcopy)
- **SDK**: PlayniteSDK 6.15.0
- **Compilación**: MSBuild 17.14 (Build Tools VS 2022), sin Visual Studio IDE
- **Icono**: SVG → PNG 128×128, color #ff506e (Svg.Skia para conversión)

## Estructura del Proyecto
```
GamesRecapPlugin/GamesRecap/
├── GamesRecap.cs              # Entry point, menús, Test API, GetGames
├── GamesRecap.csproj          # Target v4.8.1, LangVersion 12.0
├── GamesRecapClient.cs        # Stub de LibraryClient
├── GamesRecapSettings.cs      # Settings viewmodel
├── GamesRecapSettingsView.xaml/.cs  # UI de settings
├── extension.yaml             # Metadatos del plugin
├── icon.svg / icon.png        # Icono #ff506e
├── AGENTS.md                  # ← ESTE ARCHIVO
├── Models/
│   └── InertiaModels.cs       # 20 DTOs con DataContract/DataMember
├── Services/
│   ├── GamesRecapApiClient.cs # Cliente HTTP Inertia con scrape de versión
│   └── LocalDatabase.cs       # SQLite: 14 tablas, CRUD, upsert desde API
└── Data/schema.sql            # Schema de referencia
```

## Estado Actual

### ✅ Completado (Fase 0 y 1)
- Scaffolding del proyecto, compilación, Playnite carga el plugin
- 20 DTOs en `InertiaModels.cs` mapeando la respuesta Inertia completa
- `LocalDatabase.cs`: 14 tablas (Platforms, Genres, Tags, Showcases, Companies, Games, GamePlatforms, GameGenres, GameTags, GameDevelopers, ReleaseWindows, Cards, CardMedia, UserGameState, SyncLog, AppMeta)
- `GamesRecapApiClient.cs`: HTTP client con headers Inertia, query builder, manejo de 409/429
- Menús de prueba: "Test API", "Ver estado de caché local"
- Icono actualizado a #ff506e

### 🛠️ Últimos Cambios (Sesión actual)
1. **Fix 409 Conflict** (`GamesRecapApiClient.cs:108-128`):
   - Nuevo `ScrapeVersionFromHtmlAsync`: en 409, hace GET HTML sin headers Inertia, extrae MD5 hash con regex `[0-9a-f]{32}`, lo guarda en DB y reintenta
   - `htmlHttp` separado (sin headers Inertia) para el scrape
   - El servidor NO devuelve `X-Inertia-Version` en headers. La versión solo está en el HTML.

2. **Fix NOT NULL Slug** (`LocalDatabase.cs:70-81, 277-298, 404-406`):
   - Schema: `Slug TEXT NOT NULL UNIQUE` → `Slug TEXT` (nullable) en Showcases
   - `UpsertShowcase`: genera slug desde el nombre cuando es null
   - `UpsertCard`: ahora upserta `card.Showcase` para garantizar integridad referencial
   - Los showcases en `options.showcases` (filtros) NO incluyen campo `slug` en la API

### ⚠️ Pendiente / Bloqueado
- Nada bloqueado actualmente. API responde 200 OK con versión `fe5cfce8e2cfdd6009a9f870a43fdbc1`.
- Siguiente: Fase 3 — BrowserView WPF con filtros, grid de cards, paginación.

## API: gamesrecap.io

### Detalles de la API Inertia.js
- **Base URL**: `https://gamesrecap.io/`
- **Headers requeridos**: `X-Inertia: true`, `Accept: application/json`, `X-Inertia-Version: <hash>`
- **Versión actual**: `fe5cfce8e2cfdd6009a9f870a43fdbc1` (MD5 hash, cambia con cada deploy)
- **Versión por defecto** (código): `91c5bce49007757d62740bf9f1aacac6` (obsoleta)
- **409 Conflict**: ocurre cuando `X-Inertia-Version` no coincide con el servidor. El servidor NO devuelve la versión nueva en ningún header. Solo se puede obtener haciendo scraping del HTML.
- **429 Too Many Requests**: rate limiting, esperar 5s y reintentar.
- **Almacenamiento de versión**: tabla `AppMeta` clave `inertia_version`.

### Formato de la respuesta (Inertia JSON)
```json
{
  "component": "home",
  "props": {
    "features": { "userAccounts": false },
    "cards": { "current_page": 1, "data": [...], "total": N, "last_page": N, "per_page": 24 },
    "filters": { "q": "", "platforms": [], "genres": [], ... },
    "options": {
      "platforms": [{ "id": 1, "name": "Windows", "slug": "windows" }, ...],
      "genres": [{ "id": 1, "name": "Action", "slug": "action" }, ...],
      "tags": [{ "id": 1, "name": "Single Player", "slug": "single-player", "icon": "User", "color": "text-sky-400" }, ...],
      "showcases": [{ "id": 309, "name": "Nintendo Direct", "event_name": "Summer Game Fest", "start_at": "...", "series_key": "nintendo" }, ...],
      "sorts": [{ "value": "newest", "label": "Newest" }, ...]
    },
    "upcomingShowcases": [...],
    "topSpotlights": [...]
  },
  "version": "...",
  "url": "..."
}
```

### Parámetros de query (BuildQuery)
- `q` - búsqueda por título
- `platforms` / `exclude_platforms` - IDs separados por coma
- `genres` / `exclude_genres` - IDs separados por coma
- `tags` / `exclude_tags` - IDs separados por coma
- `showcases` - IDs separados por coma
- `hidden_ids` / `seen_ids` / `wishlisted_ids` - IDs separados por coma
- `seen_mode` / `wishlisted_mode` - string
- `release_from` / `release_to` - fechas
- `sort` - string (default "newest")
- `view` - string (default "cards")

### Estructura de Card (cada item en cards.data)
```json
{
  "id": 123,
  "showcase_id": 300,
  "game_id": 456,
  "sort_at": "2026-01-15T10:00:00Z",
  "is_draft": false,
  "game": { "id": 456, "title": "...", "slug": "...", "release_date": "...", ... },
  "showcase": { "id": 300, "name": "PC Gaming Show", "slug": "pc-gaming-show", ... },
  "media": [{ "id": 1, "type": "youtube", "title": "...", "url": "...", "is_unavailable": false }],
  "tags": [...]
}
```

### Nota sobre Showcases
Los showcases aparecen en DOS contextos con fields diferentes:
1. **`options.showcases`** (filtros): `id`, `name`, `event_name`, `start_at`, `series_key`, `series_label` — NO tienen `slug`
2. **`card.showcase`** (asociado a cada card): `id`, `name`, `slug`, `series_key`, `start_at`, `end_at`, `url`, `event_name`, `event_id`

## Base de Datos (14 tablas)

### Showcases (esquema actual)
| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Name | TEXT NOT NULL | |
| Slug | TEXT | nullable: los filtros no tienen slug |
| SeriesKey | TEXT | |
| EventName | TEXT | |
| EventId | INTEGER | |
| StartAt | TEXT | |
| EndAt | TEXT | |
| StreamUrl | TEXT | |
| CachedAt | TEXT NOT NULL | UTC ISO8601 |

### Games
| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Title | TEXT NOT NULL | |
| Slug | TEXT NOT NULL UNIQUE | |
| ReleaseDate | TEXT | |
| CoverUrl | TEXT | cover_image_url |
| ScreenshotUrl | TEXT | screenshot_url |
| IgdbId | INTEGER | |
| Kind | TEXT DEFAULT 'game' | |
| PublisherId | INTEGER FK→Companies | |
| CachedAt | TEXT NOT NULL | |

## Reglas de Compilación
```powershell
# Compilar (desde GamesRecap\)
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GamesRecap.csproj /t:Clean,Rebuild /p:Configuration=Debug /p:Platform=AnyCPU /v:m

# Si Playnite bloquea los DLLs
Stop-Process -Name "Playnite.DesktopApp" -Force; Start-Sleep 2
```

## Dependencias NuGet
- `PlayniteSDK.6.15.0` — SDK de Playnite
- `Stub.System.Data.SQLite.Core.NetFramework.1.0.119.0` — SQLite para .NET Framework (incluye SQLite.Interop.dll nativo para x86/x64)

## Convenciones de Código
- Sin `using Newtonsoft.Json` — usar `DataContractJsonSerializer`
- `[DataMember(Name = "snake_case")]` para mapear JSON
- C# 12.0 features permitidos (ej. `using var`, pattern matching)
- Sin comentarios en código a menos que sea necesario
- Logger via `Playnite.SDK.ILogger` (`LogManager.GetLogger()`)
- `UpsertFromApiResponse` usa transacción SQLite con rollback en error
