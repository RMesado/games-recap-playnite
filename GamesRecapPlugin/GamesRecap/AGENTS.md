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
├── GamesRecap.cs              # Entry point, menús, Test API
├── GamesRecap.csproj          # Target v4.8.1, LangVersion 12.0
├── GamesRecapSettings.cs      # Settings viewmodel
├── GamesRecapSettingsView.xaml/.cs  # UI de settings
├── extension.yaml             # Metadatos del plugin
├── icon.svg / icon.png        # Icono #ff506e
├── AGENTS.md                  # ← ESTE ARCHIVO
├── Models/
│   ├── InertiaModels.cs       # 20 DTOs con DataContract/DataMember
│   └── MetadataFieldConfig.cs # Configuración de fuentes de metadata
├── Services/
│   ├── GamesRecapApiClient.cs # Cliente HTTP Inertia con headers
│   ├── LocalDatabase.cs       # SQLite: UserGameState + AppMeta + PromotedGames
│   └── PlayniteLibrarySync.cs # Sync con librería de Playnite
└── Data/schema.sql            # Schema de referencia
```

## Estado Actual

### ✅ Completado (Fase 0 y 1)
- Scaffolding del proyecto, compilación, Playnite carga el plugin
- 20 DTOs en `InertiaModels.cs` mapeando la respuesta Inertia completa
- `LocalDatabase.cs`: 3 tablas (UserGameState + AppMeta + PromotedGames)
- `GamesRecapApiClient.cs`: HTTP client con headers Inertia, query builder, manejo de 409/429
- Menús de prueba: "Test API", "Ver estado de wishlist"
- Icono actualizado a #ff506e
- `GenericPlugin` en vez de `LibraryPlugin`: no tiene `GetGames()`, `HasCustomizedGameImport` ni `LibraryClient`. El plugin añade juegos exclusivamente vía `ImportGame(GameMetadata)` + `Games.Update()` desde `AddToLibrary()`. Al no haber `PluginId`, otros plugins (HLTB, ProtonDB, etc.) se disparan en el mismo ciclo de importación al detectar juegos nuevos por timestamp.

### 🛠️ Changelog Completo — Fase 3 (Sesiones 2026-06-11 al 2026-06-30)

#### Base de datos y API
1. Schema SQLite simplificado: eliminadas 12 tablas de caché local (Platforms, Genres, Tags, Companies, Games, etc.) — solo quedan UserGameState y AppMeta (Posteriormente se añadió PromotedGames en Fase 4)
2. Dead code masivo removido de `LocalDatabase.cs`: `UpsertFromApiResponse`, `UpsertTaxonomy`, `UpsertPlatform/Genre/Tag/Showcase/Company/Game/Card`, `GetCachedCardCount`, `GetCachedGameCount`, `GetLibraryGames`, `LogSync`, `LibraryGameEntry`
3. Fix: `ShowSlug` columna cambiada de `NOT NULL UNIQUE` a nullable con fallback de slug generado
4. Fix: `UpsertShowcase` ahora se llama dentro de `UpsertCard` para asegurar foreign key
5. Fix: `SetInertiaVersion` movido dentro de la transacción en `UpsertFromApiResponse`
6. Fix: Scraping HTML (`ScrapeVersionFromHtmlAsync`) eliminado — `fullResponse.Version` se lee directo del JSON
7. Fix: `db.UpsertFromApiResponse` eliminado de `FetchCardsAsync` — ya no persiste datos localmente
8. Fix: Wishlist movido de query params a HTTP headers (`X-Wishlisted-Ids`, `X-Wishlisted-Mode`) como espera la API
9. Fix: endpoint de API corregido — wishlist se envía como headers en vez de query params

#### ViewModel y lógica de filtros
10. Fix: Race condition de paginación corregida con contador `requestGeneration` + stale-response detection
11. Fix: `CommandManager.InvalidateRequerySuggested()` en setters de paginación para botones habilitados correctamente
12. Fix: `IsLoading` cambiado a `SetValue(ref isLoading, value)` con notificación correcta (ShowLoadingSpinner eliminado)
13. Fix: Carga de datos diferida — `LoadCardsAsync(1)` movido del constructor al `Opened` delegate del sidebar
14. Fix: Search ahora se dispara desde setter de `SearchText` (PropertyChanged → LoadCardsAsync) sin key handler
15. Añadido: `ExcludePlatforms`/`ExcludeGenres`/`ExcludeTags` con `IsExcluded` en `FilterItem` + query params
16. Añadido: `ReleaseDateFrom`/`ReleaseDateTo` con DatePickers + ClearCommands
17. Añadido: `IsWishlistFilterActive` toggle + `WishlistCount` en header
18. Añadido: `SelectedShowcaseYear` con agrupación por año y auto-selección del año más cercano
19. Añadido: `ShowcaseYearChips` + `ShowcaseIndividualChips` + `DeselectAllInYearCommand`
20. Añadido: `PlatformFilterSearch`/`GenreFilterSearch`/`TagFilterSearch`/`ShowcaseFilterSearch` con `ICollectionView` live filter
21. Añadido: `ClearAllFiltersCommand`, `SelectAll*Command`, `ToggleSelectAllShowcasesCommand`, `GoBackToLibraryCommand`, `RefreshCommand`
22. Añadido: `DisplayImageUrl` (prefiere ScreenshotUrl sobre CoverUrl)
23. Añadido: `ShowcaseDate` formateado, `HasShowcase`, `HasTags` en `CardViewModel`

#### UI/XAML
24. Fix: Dropdown filters reemplazados — eliminados custom controls (`FilterDropdown.xaml`, `ShowcaseFilterDropdown.xaml`), migrado a ToggleButton+Popup inline
25. Fix: Card grid migrado de ItemsControl a ListBox con WrapPanel + ItemContainerStyle + corner-radius clipping
26. Fix: Placeholder de búsqueda con StackPanel overlay + code-behind (reemplazado VisualBrush que no funcionaba)
27. Fix: Placeholder icons cambiados a `&#xed11;` con `FontFamily="{DynamicResource FontIcoFont}"` (icono lupa nativo)
28. Fix: Filter search TextBoxes: `Trigger` → `MultiTrigger` con `IsKeyboardFocused=False`
29. Fix: `WindowChrome.IsHitTestVisibleInChrome="True"` en botones del sidebar para clickeabilidad en title bar
30. Fix: `BrowserView` Background `"Transparent"` → `"{x:Null}"` para heredar fondo del tema
31. Fix: Wishlist/flip buttons con `Opacity="0"` + DataTrigger `IsMouseOver` para hover reveal
32. Icono sidebar corregido de `#38BDF8` a `#FF506E`
33. Icono faltante en "Clientes de terceros" corregido — `override string Icon` agregado en `GamesRecapClient`

#### Code-behind (BrowserView.xaml.cs)
34. Fix: Progreso — `Action<bool>` delegate reemplazado por PropertyChanged subscription (más confiable)
35. Fix: Timer con `DispatcherTimer` + chunks aleatorios 100-1800ms, cap 75%, fade out 400ms
36. Fix: Dual hook (`DataContextChanged` + `Loaded`) + cleanup en `Unloaded`
37. Fix: `CompleteProgress` diferido a `DispatcherPriority.Background` para evitar stuttering
38. Añadido: Card flip animation con `ScaleTransform` + `CubicEase` 400ms + midpoint visibility swap
39. Añadido: `ResetAllCards()` en `IsVisibleChanged` para restaurar front face al volver a la vista
40. Añadido: Corner-radius clipping dinámico vía `RectangleGeometry` + `border.SizeChanged`

#### Badge de Showcase (Sesión actual — última)
41. Fix: Badge con `Width="{Binding ShowcaseBadgeWidth}"` basado en `FormattedText` descartado — el binding de ancho anula `HorizontalAlignment="Left"`
42. Fix: Badge reposicionado como sibling directo del título en `FrontFace Grid` (sin StackPanel/Grid/DockPanel contenedor) — el título ya no estira el badge
43. Fix: `HorizontalAlignment="Left"` + `Padding="4,2"` en el Border, sin `Width`/`MaxWidth` bindeados
44. Fix: `VerticalAlignment="Bottom"` + `Margin="8,0,8,32"` para que quede encima del título
45. Fix: `ShowcaseName?.Trim()` en ViewModel para evitar espacios invisibles
46. Fix: Eliminados `FontWeight="SemiBold"` y `TextTrimming` del ShowcaseName

### ✅ Fase 4 — Wishlist + Library Sync (Completada)
1. **Tabla `PromotedGames`** en SQLite: GameId, Title, CoverUrl, PlatformsJson, GenresJson, TagsJson, ReleaseDate, PlayniteId
2. **PlayniteLibrarySync.cs**:
   - `AddToLibrary(int gameId, string title, int? igdbId, IPlayniteAPI api, LocalDatabase db)`: crea `GameMetadata` con GameId `gr-{gameId}`, Source "Games Recap", tags (Wishlist); llama a `api.Database.ImportGame()`; persiste en PromotedGames
   - `MapToGameMetadata(PromotedGameEntry)`: reconstruye `GameMetadata` desde DB (anteriormente usado por `GetGames()`, ahora solo referencia)
3. **Botón "Add to Library"** en backface de cada card (IconGamepad2, tooltip "Add to Playnite library")
4. **Badge "In Library"** con icono verde en WrapPanel junto a tags, tooltip "In Playnite library"
5. **libraryGameIds HashSet** en BrowserViewModel: precargado desde DB al abrir la vista, sin acceso a SQLite durante data binding
6. **Feedback con diálogo**: `Dialogs.ShowMessage` en éxito ("X added to Playnite library"), `ShowErrorMessage` en error
7. **Eliminado GetGames()**: ya no existe. Plugin tipo GenericPlugin — solo añade juegos vía ImportGame() manual.
8. **Eliminado OnGamesUpdated**: causaba deadlock al llamar `Games.Any()` durante `ItemUpdated` de importación
9. **Inverse sync**: `CleanupOrphanedPromotedGames()` se ejecuta cada vez que se abre el sidebar (vía `Opened` delegate), limpiando huérfanos; `RefreshLibraryGameIds()` actualiza el HashSet en memoria. Sin `ItemUpdated` porque no se dispara para borrados en Playnite.

### ✅ Fase 5 — Metadata download por prioridad del usuario (Sesión 2026-07-01, refactorizada 2026-07-01)
1. `PlayniteLibrarySync.AddToLibrary()` simplificado: parámetros cambiados de `Card` a `(int gameId, string title, int? igdbId, ...)`
2. `GameMetadata` mínimo: solo Name, Source ("Games Recap"), Tags (["Wishlist"]), GameId="gr-{id}", IsInstalled=false
3. `ImportGame(metadata)` se llama una sola vez (no más duplicados)
4. La descarga de metadata usa `ActivateGlobalProgress` — popup nativo "Downloading metadata..." sin congelar UI
5. **Las fuentes de metadata se leen de `config.json` (MetadataSettings)**, respetando exactamente el orden de prioridad que el usuario configuró en **Configuración → Metadata** para cada campo
6. Por cada campo, se iteran las fuentes en su orden de prioridad; la primera que devuelve datos válidos gana (por campo, no por plugin)
7. Se omite `Guid.Empty` (Tienda oficial / Official Store) — los juegos no vienen de una tienda
8. `GameId` del request se pasa como `igdbId?.ToString()` para lookup directo en IGDB
9. `Games.Update(game)` reemplaza `ImportGame()` para actualizar metadata — patrón nativo de Playnite (`MetadataDownloader`)
10. `BrowserViewModel.AddToLibrary()` pasa `igdbId` desde `cardVm.SourceCard.Game?.IgdbId`
11. `PromotedGames` persiste solo GameId, Title, TagsJson, PlayniteId

### ✅ Fase 6 — Settings (Sesión 2026-07-01)
1. `DefaultWishlistAction` (enum: `SqliteOnly`/`AddToLibrary`): cuando es `AddToLibrary`, el toggle de wishlist (corazón) también añade a la biblioteca de Playnite automáticamente, sin confirmación
2. `ShowConfirmation` (bool): muestra diálogo de confirmación antes de añadir a biblioteca vía botón manual; se omite (silent) cuando viene del toggle de wishlist
3. `WishlistActionItem` class para mostrar texto legible en ComboBox ("Save to local database only" / "Add to Playnite library")
4. UI de Settings: ComboBox con `DisplayMemberPath` + `SelectedValuePath`, CheckBox deshabilitado via DataTrigger cuando `IsAddToLibraryAction`, nota explicativa con `BooleanToVisibilityConverter`
5. `IsAddToLibraryAction` computed property en ViewModel, actualizada via PropertyChanged en Settings
6. `AutoSyncWishlist` — documentado en AGENTS.md como idea futura, no implementado (pendiente de decisión)

### ✅ Fase 5b — Validación de URLs de metadata (Sesión 2026-07-02)
1. **Problema**: Cuando un juego no tiene `library_hero` en Steam, la URL `https://steamcdn-a.akamaihd.net/steam/apps/{appid}/library_hero.jpg` devuelve 404. Playnite guarda esta URL como background y luego falla al cargarla.
2. **Solución**: Validación HTTP HEAD antes de guardar URLs de cover y background en `TryApplyField()`.
3. `HttpClient validationHttp` estático con timeout 8s para no bloquear la descarga de metadata.
4. `IsImageUrlValid(string path)`: retorna `true` para paths locales (ya descargados), hace HEAD request para URLs HTTP. Si falla, retorna `false`.
5. Casos `CoverImage` y `BackgroundImage` en `TryApplyField()` validan la URL antes de asignar `game.CoverImage` / `game.BackgroundImage`.
6. Si la validación falla, se loggea un warning con la URL y se retorna `false`, permitiendo que el siguiente provider en la lista de prioridades intente con su propia imagen.
7. Archivos modificados: `PlayniteLibrarySync.cs` (lines 10, 20-23, 267-287, 427-444).

### ⚠️ Pendiente
- Fase 7: Packaging (build .pext + README)

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
- `hidden_ids` / `seen_ids` - IDs separados por coma
- `seen_mode` - string
- `wishlisted_ids` / `wishlisted_mode` - **NO se envían como query params**. Se envían como headers HTTP: `X-Wishlisted-Ids` y `X-Wishlisted-Mode` (ver `GamesRecapApiClient.FetchCardsAsync`)
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

## Base de Datos (3 tablas)

Schema mínimo: solo persiste estado de usuario, metadatos del sistema y juegos promovidos a la librería de Playnite.
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

### PromotedGames
| Columna | Tipo | Notas |
|---------|------|-------|
| GameId | INTEGER PK | ID del juego en gamesrecap.io |
| Title | TEXT NOT NULL | Título del juego |
| CoverUrl | TEXT | URL de la cover (IGDB) |
| PlatformsJson | TEXT | JSON array de plataformas |
| GenresJson | TEXT | JSON array de géneros |
| TagsJson | TEXT | JSON array de tags |
| ReleaseDate | TEXT | Fecha de lanzamiento |
| Description | TEXT | Descripción del juego |
| PlayniteId | TEXT UNIQUE | GUID del juego en librería Playnite (NULL si no ha sido añadido) |

## Reglas de Compilación y Deploy

### Compilación (con Playnite cerrado)
```powershell
# 1. Cerrar Playnite (libera los DLLs)
Stop-Process -Name "Playnite.DesktopApp" -Force -ErrorAction SilentlyContinue; Start-Sleep 2

# 2. Compilar (desde GamesRecap\)
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GamesRecap.csproj /t:Clean,Rebuild /p:Configuration=Debug /p:Platform=AnyCPU /v:m

# 3. Opcional: solo Build rápido (sin Clean)
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GamesRecap.csproj /t:Build /p:Configuration=Debug /p:Platform=AnyCPU /v:m /nologo
```

### Deploy
- No se necesita copiar archivos — Playnite cargó el plugin desde `bin\Debug\GamesRecap.dll`
- Al abrir Playnite, recarga extensiones automáticamente
- No es necesario reiniciar Windows ni recargar tema

### Ciclo típico de prueba
1. Cerrar Playnite (Stop-Process)
2. Compilar (MSBuild)
3. Abrir Playnite
4. Abrir Games Recap (sidebar)
5. Realizar acciones a probar
6. Si hay logs, leer en `<PlayniteDir>\extensions.log` (ruta según instalación)
7. Repetir

### Notas
- Playnite bloquea `GamesRecap.dll` mientras está abierto, por eso hay que cerrarlo primero
- `MSBuild.exe` se encuentra en `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\`
- El flag `/v:m` muestra solo warnings y errores; `/nologo` omite el banner inicial
- Usar `Clean,Rebuild` cuando se cambian recursos (XAML, imágenes); `Build` rápido para cambios solo en código .cs

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

### Card Grid (ListBox + WrapPanel, corner-radius clipping)
- `ListBox` con `WrapPanel` horizontal (`ItemWidth="300"`)
- `ItemContainerStyle="{StaticResource CardItemStyle}"`:
  - `OverridesDefaultStyle="True"`, `FocusVisualStyle="{x:Null}"`
  - Template: `Grid` (ItemGrid, `Margin="5"`) → `Border` (`CornerRadius="8"`, sin `Background`) → `ContentPresenter`
  - Clip aplicado al `Border` vía `RectangleGeometry` con las esquinas redondeadas (`CardRoot_OnLoaded`)
  - Triggers: `IsMouseOver` → scale 1.04x con `QuadraticEase EaseOut` 250ms, `Canvas.ZIndex = 90`
- Card DataTemplate (`Grid` `Height="173"`, `Margin="0"`, `ClipToBounds="True"`):
  - `Image Stretch="UniformToFill"` — ocupa todo el card y se recorta con las esquinas redondeadas del Border padre
  - Front face: gradient overlay, tag badges, showcase+title, wishlist/flip buttons
  - Back face: release info, platforms, genres, trailer button
  - Flip: `ScaleTransform.ScaleX` 1→-1 con `CubicEase EaseInOut` 400ms
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

## Próximas fases

### Fase 7 — Packaging
- Build .pext + README

### Files Modified (Sesión 2026-07-01 — Fase 6: Settings + auto-add-to-library)

- `GamesRecapSettings.cs`:
  - Added `WishlistAction` enum (`SqliteOnly`, `AddToLibrary`)
  - Replaced placeholder properties with `DefaultWishlistAction` + `ShowConfirmation`
  - Added `WishlistActionItem` class (Value + Display) for ComboBox display
  - Added `WishlistActions` list (`List<WishlistActionItem>`) and `IsAddToLibraryAction` computed property
  - `Settings` setter subscribes to `PropertyChanged` for `DefaultWishlistAction` → notifies `IsAddToLibraryAction`
- `GamesRecapSettingsView.xaml`:
  - ComboBox with `DisplayMemberPath="Display"` + `SelectedValuePath="Value"`
  - Conditional note text with `BooleanToVisibilityConverter`
  - CheckBox disabled via `DataTrigger` when `IsAddToLibraryAction`
- `ViewModels/BrowserViewModel.cs`:
  - Added `settings` field, constructor param `GamesRecapSettings`
  - `AddToLibrary(int gameId, bool silent = false)`: checks `settings.ShowConfirmation` unless silent
  - `ToggleWishlist()`: when `DefaultWishlistAction == AddToLibrary`, calls `AddToLibrary(gameId, silent: true)`
- `GamesRecap.cs`:
  - Passes `settings.Settings` to BrowserViewModel constructor

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

### Files Modified (Sesión 2026-06-15 — search icons, placeholders, AGENTS.md)
- `Views/BrowserView.xaml`:
  - Main search: revertido a `Style="{DynamicResource {x:Type TextBox}}"` (sin inline Style), placeholder vía StackPanel overlay + code-behind (IsHitTestVisible=False) → clickability fix
  - Todos los placeholders: emoji 🔍 reemplazado por icono `&#xed11;` con `FontFamily="{DynamicResource FontIcoFont}"` (icono lupa nativo de Playnite)
  - Filter search TextBoxes (platform, genre, tag, showcase): `Trigger` → `MultiTrigger` con `IsKeyboardFocused=False` (placeholder desaparece al focus)
- `Views/BrowserView.xaml.cs`:
  - Added `MainSearch_TextChanged/GotFocus/LostFocus` + `UpdateMainSearchPlaceholder()` (toggle visibility según Text vacío + focus)

### Files Modified (Sesión 2026-06-26 — Progress Bar)
- `Views/BrowserView.xaml.cs`: Implementación completa de la barra de progreso
  - `BrowserView.xaml`:260 — `<Rectangle>` en Grid.Row="0", VerticalAlignment="Bottom", Height="2", Fill="#ff506e", ScaleTransform con ScaleX=0
- Reemplazado `Action<bool>` delegate por suscripción a `PropertyChanged` del ViewModel en `OnDataContextChanged` + `OnViewLoaded`
- Añadido `Unloaded` handler para cleanup del evento
- `CompleteProgress` diferido a `DispatcherPriority.Background` para evitar stuttering con el layout de cards
- Timer `DispatcherTimer` para chunks aleatorios: primer tick a 100-300ms, siguientes a 400-1800ms, cap 75%
- `StartProgress`: nudge inicial a ScaleX=0.08 para visibilidad inmediata
- `CompleteProgress`: fill current→1 en 600ms (QuadraticEase), fade 1→0 en 400ms tras fill

### Files Modified (Sesión 2026-06-30 — Fase 4 final + inverse sync + auto-refresh)
- `GamesRecap.cs`:
  - Eliminado `OnGameItemUpdated` (ItemUpdated no se dispara para borrados en Playnite)
  - Añadido `CleanupOrphanedPromotedGames()`: recorre PromotedGames, elimina entradas cuyo PlayniteId ya no existe en la DB de Playnite
  - `SidebarItem.Opened` ahora siempre llama a `CleanupOrphanedPromotedGames()` + `browserViewModel.RefreshLibraryGameIds()` + `LoadCardsAsync(1)` en cada apertura
- `LocalDatabase.cs`:
  - Añadido `GetGameIdByPlayniteId(Guid)`: lookup inverso para inverse sync
- `BrowserViewModel.cs`:
  - Añadido `RefreshLibraryGameIds()`: recarga el HashSet desde DB
  - `PopulateFilters` ahora se llama siempre (no solo en primer load): usa merge strategy que preserva selecciones existentes, agrega items nuevos, remueve items que ya no existen
  - Auto-selección de año showcase solo en primer load; en refrescos mantiene selección del usuario
- `BrowserView.xaml.cs`:
  - Fix: `OnViewLoaded` ahora resuscribe `PropertyChanged` (previo -= y +=) para evitar pérdida del handler entre ciclos mostrar/esconder que dejaba la barra de progreso atascada

### Files Modified (Sesión 2026-07-01 — Fase 5 refactor: metadata por prioridad del usuario)
- `Models/MetadataFieldConfig.cs` (nuevo): clases `PlayniteConfigRoot` + `MetadataFieldSetting` para parsear MetadataSettings del `config.json` via `JavaScriptSerializer`
- `Services/PlayniteLibrarySync.cs`:
  - Eliminado `ProcessPlugin()` (hardcodeaba fields para un solo plugin)
  - Reemplazado `DownloadMetadataForGame()` con nuevo algoritmo:
    1. Lee `api.Paths.ConfigurationPath + "config.json"`
    2. Parsea `MetadataSettings` sección con `JavaScriptSerializer`
    3. Para cada campo definido en `FieldProcessingOrder` (Name, Genre, ..., InstallSize):
       - Lee su `Import` flag y `Sources` array (GUIDs en orden de prioridad)
       - Omite `Guid.Empty` (Tienda oficial)
       - Busca el `MetadataPlugin` instalado por GUID
       - Cachea providers por GUID (no recrea para cada campo)
       - Si el provider soporta el campo → `TryApplyField()` → primer resultado válido gana, break
   - `TryApplyField()` (nuevo): procesa un solo campo de un provider; retorna `true` si obtuvo datos válidos
   - Fix en `TryApplyField` para `MetadataField.Tags`: preserva el tag "Wishlist" concatenándolo (`tagIds.Insert(0, wishlistTag.Id)`) si el provider no lo incluye
   - `JsonFieldMapping`: mapea nombres JSON ("Genre", "Developer", "Tag", etc.) a `MetadataField` enum
  - `FieldProcessingOrder`: orden determinista de procesamiento de campos
  - Añadido `using System.Web.Script.Serialization` + `using GamesRecap.Models`
- `GamesRecap.csproj`:
  - Añadido `Compile Include="Models\MetadataFieldConfig.cs"`
  - Añadido `Reference Include="System.Web.Extensions"` (para `JavaScriptSerializer`)
- `ViewModels/BrowserViewModel.cs`:
  - `AddToLibrary()` pasa `igdbId = cardVm.SourceCard.Game?.IgdbId` a `sync.AddToLibrary()`

## Playnite Theme Resources
- **Default theme path**: `<PlayniteDir>\Themes\Desktop\Default\`
- **Search icon**: `Media.xaml:44` — `SearchTextIconTemplate` con `&#xed11;` en `FontIcoFont`
- **IcoFont**: `FontFamily="{DynamicResource FontIcoFont}"`
- **Theme XAML dir**: `<PlayniteDir>\Themes\Desktop\Default\CustomControls\SearchBox.xaml`
- Para usar el icono de búsqueda nativo en placeholders: `<TextBlock Text="&#xed11;" FontFamily="{DynamicResource FontIcoFont}" />`
