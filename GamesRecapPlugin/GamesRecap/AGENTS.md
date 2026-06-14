# Games Recap Plugin — Contexto del Proyecto

## Objetivo
Plugin de biblioteca para Playnite que integra gamesrecap.io vía API Inertia.js, con SQLite local para estado de usuario, navegador de juegos, wishlist y sincronización con la biblioteca de Playnite.

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
│   └── LocalDatabase.cs       # SQLite: UserGameState + AppMeta
└── Data/schema.sql            # Schema de referencia
```

## Estado Actual

### ✅ Completado (Fase 0 y 1)
- Scaffolding del proyecto, compilación, Playnite carga el plugin
- 20 DTOs en `InertiaModels.cs` mapeando la respuesta Inertia completa
- `LocalDatabase.cs`: 2 tablas (UserGameState + AppMeta), solo persiste estado de usuario e inertia_version
- `GamesRecapApiClient.cs`: HTTP client con headers Inertia, query builder, manejo de 409/429
- Menús de prueba: "Test API", "Ver estado de wishlist"
- Icono actualizado a #ff506e

### 🛠️ Últimos Cambios (Sesión actual)
1. **Eliminada caché redundante de API** (`LocalDatabase.cs`, `GamesRecapApiClient.cs`):
   - Schema reducido de 14 a 2 tablas (solo `UserGameState` + `AppMeta`)
   - Eliminado `UpsertFromApiResponse` y todos los métodos de caché de datos de API
   - Las tablas antiguas se eliminan automáticamente en `Initialize()` con `DROP TABLE IF EXISTS`
   - Ahora `GetGames()` devuelve lista vacía (sin implementar aún)
   - Menú "Ver estado de caché local" → "Ver estado de wishlist"

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

## Base de Datos (2 tablas)

Schema mínimo: solo persiste estado de usuario y metadatos del sistema.
Los datos de juegos y catálogo vienen exclusivamente de la respuesta HTTP de gamesrecap.io.

### UserGameState
| Columna | Tipo | Notas |
|---------|------|-------|
| GameId | INTEGER PK | ID del juego en gamesrecap.io |
| Wishlisted | INTEGER DEFAULT 0 | 0/1 |
| WishlistedAt | TEXT | UTC ISO8601 |
| Seen | INTEGER DEFAULT 0 | 0/1 |
| SeenAt | TEXT | UTC ISO8601 |
| Hidden | INTEGER DEFAULT 0 | 0/1 |
| HiddenAt | TEXT | UTC ISO8601 |
| PlayniteId | TEXT | GUID del juego en librería Playnite |

### AppMeta
| Columna | Tipo | Notas |
|---------|------|-------|
| Key | TEXT PK | `inertia_version` |
| Value | TEXT NOT NULL | MD5 hash de la versión Inertia |

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

## UI Architecture (Sesión 2026-06-11)
### Sidebar View Integration
- `GetSidebarItems()` retorna `SidebarItem` con `Type = SiderbarItemType.View`
- `Opened` callback retorna un `UserControl` (lazy), mostrado en `PART_ContentView` del MainWindow
- El UserControl tiene `Background="Transparent"` para heredar el fondo del tema de Playnite

### Filter Dropdowns (Inline ToggleButton+Popup, NO custom controls)
- Cada filtro (Platforms, Genres, Tags, Showcases) usa esta estructura inline:
  - `Border` con `Style="{DynamicResource HighlightBorder}"` para el look de FilterSelectionBox
  - `TextBlock` muestra header (e.g., "Platforms (3)")
  - Marlett arrow "6" como indicador de dropdown
  - `ToggleButton` invisible sobrepuesto maneja el toggle
  - `Popup` con `PopupBackgroundBrush`/`PopupBorderBrush`/`PopupBorderThickness`
  - Dentro del Popup: `TextBox` (search) + `Button` "Select All Filtered" + `ListBox`
- Search filtrado via `ICollectionView` establecido en ViewModel (propiedades `PlatformFilterSearch`, etc.)
- Search se aplica como text filter en el ICollectionView local, no recarga la API
- "Select All Filtered" llama a `SelectAll*Command` que marca `IsSelected=true` en todos
- Exclude toggle (⊕) presente en Platforms, Genres, Tags

### Showcase Year Filtering
- `AvailableYears` se extrae de `ShowcaseFilters` (año desde `start_at`)
- `SelectedShowcaseYear` ComboBox → establece `showcaseView.Filter = item => item.Year == year` (solo cambia visibilidad en dropdown)
- Al seleccionar año NO se auto-seleccionan/deseleccionan showcases (solo en carga inicial vía `PopulateFilters`)
- Carga inicial: selecciona el año más cercano al actual y llama `SelectShowcasesForYear(closest)` + `UpdateShowcaseChips()`
- Chips multi-año via dos `ObservableCollection`:
  - `ShowcaseYearChips` (ObservableCollection<YearChipItem>): un chip por año donde TODOS los showcases de ese año están seleccionados → texto "All showcases in YEAR" + botón X
  - `ShowcaseIndividualChips` (ObservableCollection<FilterItem>): chips individuales para showcases seleccionados de años NO completamente seleccionados
- `UpdateShowcaseChips()` agrupa `ShowcaseFilters.Where(f.IsSelected)` por año, decide año-chip vs individual
- `DeselectAllInYearCommand` (RelayCommand<int>) deselecciona todos los showcases de un año específico

### Card Grid (Playnite-Native Style via ListBox)
- `ListBox` con `WrapPanel` horizontal (reemplazó `ItemsControl` para que `ListBoxItem` contenedor exista)
- `ItemContainerStyle="{StaticResource CardItemStyle}"` replica `GridViewItemStyle`:
  - `OverridesDefaultStyle="True"`, `FocusVisualStyle="{x:Null}"`
  - Template: `Grid` con `GridItemBackgroundBrush`, `Border "SelectionBorder"` (border 3px, margin -3), `ContentPresenter`
  - Triggers: `IsMouseOver`/`IsSelected` → `BorderBrush = GlyphBrush`, `Canvas.ZIndex = 90`
- Card DataTemplate (200×267 cover = 3:4 ratio):
  - `Grid Height="267"` con `Image Stretch="Uniform" Fant` que llena el área
  - Hover overlay: `Border#99000000 Opacity="0→1"` via `DataTrigger IsMouseOver ListBoxItem`
  - Botones: Trailer + Wishlist (♥, #ff506e cuando wishlisted)
  - Nombre, showcase, plataformas debajo con `BaseTextBlockStyle`
- `ListBox` con `SelectionMode="Single"`, `Background="Transparent"`, `ScrollViewer.HorizontalScrollBarVisibility="Disabled"`

### Release Date Range
- Dos `DatePicker` bound a `ReleaseDateFrom`/`ReleaseDateTo` (DateTime?)
- Playnite tema ya tiene estilo implícito para DatePicker (PopupBackgroundBrush, NormalBorderBrush, etc.)
- Al cambiar fecha → `LoadCardsAsync(1)` vía PropertyChanged

### Theme Resources Used
- Todos via `{DynamicResource ...}`: TextBrush, TextBrushDarker, GlyphBrush, HoverBrush, NormalBrush, NormalBrushDark, NormalBorderBrush, ControlBackgroundBrush, ControlBorderThickness, ControlCornerRadius, GridItemBackgroundBrush, PanelSeparatorBrush, PopupBackgroundBrush, PopupBorderBrush, PopupBorderThickness, HighlightBorder, FontFamily, FontSize, FontSizeSmall, BaseTextBlockStyle, SimpleButton, WarningBrush
- `{StaticResource BaseTextBlockStyle}` para TextBlocks
- `{StaticResource SimpleButton}` para botones de acción
- `{StaticResource {x:Type CheckBox}}` y `{StaticResource {x:Type ToggleButton}}` para heredar estilos del tema

### Files Removed (Sesión 2026-06-11)
- `Controls/FilterDropdown.xaml` + `.cs`
- `Controls/ShowcaseFilterDropdown.xaml` + `.cs`

### Files Modified (Sesión 2026-06-11)
- `ViewModels/BrowserViewModel.cs` → rewrite: year-filtered showcases, select-all, date range, search per filter, chip logic
- `Views/BrowserView.xaml` → rewrite: inline ToggleButton+Popup, Playnite-style card grid, transparent bg, date pickers
- `Views/BrowserView.xaml.cs` → simplified (no event handlers needed)
- `GamesRecap.csproj` → removed deleted control references

## Planned Features (próximas sesiones)
- **Game Calendar**: Calendario de lanzamientos/próximos juegos. Pendiente de especificación detallada.
- **Wishlist Sync**: Sincronización bidireccional con la biblioteca de Playnite.
- **Trailer Popup**: Reproductor embebido de YouTube para trailers desde la card grid.
- **Card Filter Presets**: Guardar/cargar combinaciones de filtros.

### Files Modified (Sesión 2026-06-11, segunda ronda — ListBox + hover fix + multi-year chips)
- `ViewModels/BrowserViewModel.cs`:
  - `SelectedShowcaseYear` setter: removed `SelectShowcasesForYear(value)` (year ComboBox ya no auto-selecciona)
  - Added `ShowcaseYearChips`/`ShowcaseIndividualChips` ObservableCollections
  - Added `YearChipItem` class (Year, ChipText, DeselectAllInYearCommand)
  - Added `UpdateShowcaseChips()` → agrupa items seleccionados por año, decide year-chip vs individual
  - Added `DeselectAllInYear(int year)` → deselecciona todos los showcases de un año
  - `PopulateFilters`: llamada inicial a `SelectShowcasesForYear(closest)` + `UpdateShowcaseChips()` (solo al cargar)
  - `OnFilterChanged`: llama a `UpdateShowcaseChips()` en vez de notificar viejas props computadas
  - Removed old computed properties `ShowAllShowcasesChip`, `ShowcaseChipText`, `ShowcaseIndividualChips` (bool)
- `Views/BrowserView.xaml`:
  - Card grid: `ItemsControl` → `ListBox` con `WrapPanel`, `ItemContainerStyle="{StaticResource CardItemStyle}"`
  - Card DataTemplate redesigned: cover 200×267 (3:4 ratio), Image fill sin max height, texto debajo sin Grid.RowDefinitions
  - Hover overlay binding now works because `ListBoxItem` ancestor exists
  - Showcase chips: reemplazado `ShowAllShowcasesChip` Border + `ShowcaseFilters` ItemsControl por `ShowcaseYearChips` ItemsControl + `ShowcaseIndividualChips` ItemsControl
