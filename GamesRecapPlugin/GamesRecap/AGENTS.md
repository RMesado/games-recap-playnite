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
├── GamesRecap.cs              # Entry point, sidebar views (browser + calendar)
├── GamesRecap.csproj          # Target v4.8.1, LangVersion 12.0
├── GamesRecapSettings.cs      # Settings viewmodel + Loc helper
├── GamesRecapSettingsView.xaml/.cs  # UI de settings
├── extension.yaml             # Metadatos del plugin (Type: GenericPlugin)
├── icon.svg / icon.png        # Icono #ff506e
├── icon-calendar.svg / icon-calendar.png  # Icono calendario naranja
├── AGENTS.md                  # ← ESTE ARCHIVO
├── Models/
│   ├── InertiaModels.cs       # 20 DTOs con DataContract/DataMember
│   └── MetadataFieldConfig.cs # Configuración de fuentes de metadata
├── Services/
│   ├── GamesRecapApiClient.cs # Cliente HTTP Inertia con headers
│   ├── LocalDatabase.cs       # SQLite: UserGameState + AppMeta + PromotedGames + CalendarGames
│   └── PlayniteLibrarySync.cs # Sync con librería de Playnite (metadata por prioridad)
├── ViewModels/
│   ├── BrowserViewModel.cs    # Lógica browser: filtros, grid, wishlist, calendario
│   └── CalendarViewModel.cs   # Lógica calendario: 3 secciones, grid semanal
├── Views/
│   ├── BrowserView.xaml/.cs   # Vista principal (cards, filtros, paginación)
│   └── CalendarView.xaml/.cs  # Vista calendario (último mes, semana, próximos)
├── Resources/
│   ├── BadgeIcons.xaml        # Iconos SVG para badges
│   └── SharedResources.xaml   # Plantillas compartidas (tooltip estándar)
├── Localization/
│   ├── es_ES.xaml             # Localización español (primario)
│   └── en_US.xaml             # Localización inglés (fallback)
└── Data/schema.sql            # Schema de referencia
```

## Estado Actual

### ✅ Completado (Fases 0-8)
- Scaffolding del proyecto, compilación, Playnite carga el plugin
- 20 DTOs en `InertiaModels.cs` mapeando la respuesta Inertia completa
- `LocalDatabase.cs`: 4 tablas (UserGameState + AppMeta + PromotedGames + CalendarGames)
- `GamesRecapApiClient.cs`: HTTP client con headers Inertia, query builder, manejo de 409/429
- Icono actualizado a #ff506e
- `GenericPlugin` en vez de `LibraryPlugin`: no tiene `GetGames()`, `HasCustomizedGameImport` ni `LibraryClient`. El plugin añade juegos exclusivamente vía `ImportGame(GameMetadata)` + `Games.Update()` desde `AddToLibrary()`. Al no haber `PluginId`, otros plugins (HLTB, ProtonDB, etc.) se disparan en el mismo ciclo de importación al detectar juegos nuevos por timestamp.
- **Release Calendar**: vista sidebar con 3 secciones (último mes, última semana, próximos lanzamientos)
- **Localización**: español como primario, inglés como fallback, 63 claves traducidas
- **Settings**: DefaultWishlistAction (SqliteOnly/AddToLibrary) + ShowConfirmation
- **Metadata download**: por prioridad del usuario desde config.json, HTTP HEAD validation

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

### ✅ Fase 7 — Release Calendar (Sesiones 2026-07-03 al 2026-07-06)
1. **Tabla `CalendarGames`** en SQLite: GameId, Title, CoverUrl, ReleaseDate, AddedAt
2. **CRUD en `LocalDatabase.cs`**:
   - `AddToCalendar(int gameId, string title, string coverUrl, string releaseDate)`: INSERT OR IGNORE
   - `RemoveFromCalendar(int gameId)`: DELETE
   - `GetAllCalendarGames()`: SELECT con `CalendarGameEntry` DTO
   - `IsInCalendar(int gameId)`: COUNT check
   - `CalendarGameEntry` class (GameId, Title, CoverUrl, ReleaseDate, AddedAt)
3. **`CalendarViewModel.cs`** (nuevo):
   - `RefreshGames()`: carga todos los juegos del calendario, los clasifica en 3 colecciones observables
   - `LastMonthGames`: 30-7 días atrás, orden descendente
   - `LastWeekGames`: 7 días atrás hasta hoy, orden descendente
   - `UpcomingWeeks`: grid semanal con 6 columnas (lunes-sábado), 8 semanas hacia adelante
   - `DayHeaders`: nombres de día localizados (ej. "LUNES", "MARTES")
   - `RemoveFromCalendarCommand`: eliminación con confirmación + refresh
   - `TryParseDate(string, out DateTime)`: parsea `yyyy-MM-dd` estricto
   - `BuildWeeks()`: agrupa juegos por semana y día, genera `CalendarDay` con `DateLabel` localizado (formato "d 'de' MMMM" para español)
4. **`CalendarView.xaml`** (nuevo, 569 líneas):
   - Header con botón volver + título dinámico `{DynamicResource ReleaseCalendar}`
   - 3 secciones con `Visibility` condicional
   - Secciones 1 y 2: `ListBox` horizontal con `PreviewMouseWheel` forwarding al `ScrollViewer` padre
   - Section 3: grid de 6 columnas vía `UniformGrid`, cada día con borde + fecha + cards
   - Card grid: `ListBox` con estilo minimalista `CalendarGridStyle` (solo `ContentPresenter`)
   - Hover scale 1.04x con animación `QuadraticEase EaseOut` 250ms
   - Botón quitar con cross icon (IconCalendarClock + Line diagonal), opacity 0→1 en hover
   - Tooltips normalizados con `StandardTooltipTemplate`
   - Corner-radius clipping vía `RectangleGeometry` en `CalendarCard_OnLoaded`
5. **`CalendarView.xaml.cs`** (nuevo, 53 líneas):
   - `ListBox_PreviewMouseWheel`: forwards scroll al ScrollViewer padre
   - `FindParent<T>()`: generic visual tree walker
   - `CalendarCard_OnLoaded`: hook `SizeChanged` + `BeginInvoke(Loaded)` para clipping dinámico
   - `UpdateCardClip(Border)`: aplica `RectangleGeometry` con el `CornerRadius` actual del Border
6. **`icon-calendar.svg`** (nuevo): reloj con outline, stroke #D9D9D9
7. **`icon-calendar.png`**: PNG 128×128 exportado del SVG

### ✅ Fase 8 — Localización (Sesiones 2026-07-04 al 2026-07-06)
1. **`Localization/es_ES.xaml`** (nuevo, 93 líneas): 47+ claves traducidas al español
2. **`Localization/en_US.xaml`**: ampliado con ~15 nuevas claves (calendar, settings, card labels)
3. **`App.xaml`**: `es_ES.xaml` añadido como primer `MergedDictionary` (orden determina prioridad)
4. **Clase helper `Loc.Get(string key)`**: definida en `GamesRecapSettings.cs`, busca en `Application.Current.TryFindResource`, fallback al key name
5. **Todas las strings hardcodeadas reemplazadas**: ~30 lugares en XAML con `{DynamicResource}`, ~15 en C# con `Loc.Get()`
6. **Paginación localizada**: `PaginationText` y `CardCountText` como propiedades computadas con `string.Format`
7. **Tooltips normalizados**: ~20 tooltips migrados a `StandardTooltipTemplate` (SharedResources.xaml)
8. **SharedResources.xaml** (nuevo): `ControlTemplate x:Key="StandardTooltipTemplate"` con fondo #1a1a2e, borde #333, corner 4px, padding 12x6

### ✅ Integración Calendario ↔ Browser
1. **BrowserViewModel**:
   - `calendarIds` (HashSet<int>): precargado desde DB al abrir
   - `IsCalendarFilterActive` toggle: filtra cards mostrando solo juegos en calendario
   - `AddToCalendarCommand` + `ToggleCalendarFilterCommand`
   - `AddToCalendar(int gameId)`: confirmación → DB → notifica cambio → refresca si filtro activo
   - `RefreshCalendarIds()`: recarga desde DB
   - Filtro combinado wishlist+calendar: ambos sets se unen en `X-Wishlisted-Ids`
2. **CardViewModel**:
   - `IsInCalendar` property (computed desde parent)
   - `NotifyCalendarChanged()`: refresca `OnPropertyChanged(nameof(IsInCalendar))`
   - ReleaseWindows display: cambiado de `Kind`/`Label` a `Date`/`Platforms`
   - `TrailerTitle` property: primer media title o fallback localizado "Trailer"
   - `FormatReleaseDate()`: parsea fecha y devuelve formato "d MMM yyyy" en mayúsculas
   - Eliminado `ShowcaseBadgeWidth`: ya no se usa (badge auto-sizing)
3. **BrowserView.xaml**:
   - Botón calendario en header junto al de wishlist (Path IconCalendarClock, stroke #FF9800)
   - Badge "In Calendar" naranja en WrapPanel (junto a In Library)
   - Botón calendario en backface con toggle tooltip add/remove + cross line cuando ya está
   - `ClipToBounds="True"` eliminado del Grid de card (reemplazado por RectangleGeometry clipping)
   - Release windows: Date + Platforms en vez de Kind + Label
   - ScrollViewer añadido al backface para contenido largo
4. **`IsCompleteDateConverter`** (Converters.cs): retorna `Visibility.Visible` solo si la fecha es formato `yyyy-MM-dd` completo
5. **`YearToDisplayConverter`** (Converters.cs): ahora usa `Loc.Get("AllYears")` para localización

### ✅ Files modificados en Fase 7+8
- `GamesRecap.cs`:
  - Añadido `calendarView` / `calendarViewModel` fields
  - Segundo `SidebarItem` para Release Calendar con icono `icon-calendar.png`
  - `Opened` callback: crea lazy CalendarView, llama `calendarViewModel.RefreshGames()`
  - Constructor BrowserViewModel: pasa `GamesRecapSettings settings` en vez de `LibraryPlugin plugin`
  - Eliminados menús debug (`TestApiFetch`, "Ver estado de wishlist" — `GetMainMenuItems` vacío)
- `LocalDatabase.cs`:
  - Añadido `Description` column a `PromotedGames` (con ALTER TABLE migration)
  - Añadidos `AddToCalendar`, `RemoveFromCalendar`, `GetAllCalendarGames`, `IsInCalendar`
  - Añadido `CalendarGameEntry` class
  - `UpsertPromotedGame`: nuevo parámetro `description`
  - `GetAllPromotedGames`: incluye `Description` en SELECT
- `PlayniteLibrarySync.cs`:
  - `AddToLibrary()`: sin parámetro plugin (GenericPlugin)
  - `DownloadMetadataForGame()` reescrito: lee `config.json` → `MetadataSettings`, procesa campos por prioridad
  - `TryApplyField()`: procesa un campo de un provider, retorna bool
  - `IsImageUrlValid()`: HEAD request para validar URLs de cover/background
  - `GetLocalizedProgress()`: usa `Loc.Get("DownloadingMetadata")`
  - Eliminado `MapToGameMetadata()` (ya no se usa — no hay GetGames())
- `BrowserViewModel.cs`:
  - Constructor: `LibraryPlugin plugin` → `GamesRecapSettings settings`
  - Nuevos: `calendarIds`, `IsCalendarFilterActive`, `AddToCalendarCommand`, `ToggleCalendarFilterCommand`
  - `ToggleWishlist()`: chequea `settings.DefaultWishlistAction` para auto-add
  - `AddToLibrary()`: parámetro `silent`, chequea `settings.ShowConfirmation`
  - `AddToCalendar()` / `RemoveFromCalendar()` con diálogos de confirmación
  - Nuevas propiedades: `PaginationText`, `CardCountText`, `CalendarCount`, `TrailerTitle`
  - `ReleaseWindows` display: Date + Platforms, agrupado, ordenado por DisplayOrder
- `GamesRecapSettings.cs`: nuevo `WishlistAction` enum, `WishlistActionItem`, `Loc` helper
- `GamesRecapSettingsView.xaml`: ComboBox + CheckBox + nota condicional
- `GamesRecap.csproj`: referencias `System.Web.Extensions`, nuevos Compile/Page/None includes
- `Converters.cs`: `IsCompleteDateConverter`, localización en `YearToDisplayConverter`
- `InertiaModels.cs`: `ReleaseWindowDisplay` cambiado de `Kind`/`Label` a `Date`/`Platforms`
- `extension.yaml`: `Type: GameLibrary` → `Type: GenericPlugin`
- Eliminado `GamesRecapClient.cs`

### ⚠️ Pendiente
- Fase 9: Packaging (build .pext + README)

## API: gamesrecap.io

### Detalles de la API Inertia.js
- **Base URL**: `https://gamesrecap.io/`
- **Headers requeridos**: `X-Inertia: true`, `Accept: application/json`, `X-Inertia-Version: <hash>`
- **Versión actual**: `fe5cfce8e2cfdd6009a9f870a43fdbc1` (MD5 hash, cambia con cada deploy)
- **Versión por defecto** (código): `91c5bce49007757d62740bf9f1aacac6` (obsoleta)
- **409 Conflict**: ocurre cuando `X-Inertia-Version` no coincide con el servidor. La nueva versión se obtiene del campo `version` de la respuesta JSON, no de headers HTTP.
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

### CalendarGames
| Columna | Tipo | Notas |
|---------|------|-------|
| GameId | INTEGER PK | ID del juego en gamesrecap.io |
| Title | TEXT NOT NULL | Título del juego |
| CoverUrl | TEXT | URL de la cover (IGDB) |
| ReleaseDate | TEXT NOT NULL | Fecha yyyy-MM-dd (completa) |
| AddedAt | TEXT NOT NULL | UTC ISO8601 |

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
- **NO escribas código en el chat** al explicar qué vas a hacer. Describe la acción en texto breve. El código solo se escribe en los archivos al implementar.
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

## Localización

### Sistema de localización
El plugin usa `ResourceDictionary` XAML de WPF para localización. Los ficheros están en `Localization/*.xaml` y se cargan via `App.xaml` como `MergedDictionaries`. El orden determina la prioridad: el primer diccionario tiene preferencia, los siguientes actúan como fallback.

```
App.xaml:
  └─ Localization/es_ES.xaml  ← primario (se usa primero)
  └─ Localization/en_US.xaml  ← fallback (claves no encontradas en es_ES)
```

### Formato del fichero
Cada clave es un `sys:String` con `x:Key` único:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <sys:String x:Key="FilterPlatforms">Plataformas</sys:String>
    <sys:String x:Key="ConfirmAddMessage">¿Añadir "{0}" a la biblioteca de Playnite?</sys:String>
</ResourceDictionary>
```

### Convenciones de nombrado de claves
- **CamelCase** sin espacios
- Prefijo por contexto: `Filter*`, `Search*`, `Pagination*`, `Settings*`, `Error*`, etc.
- Las claves con `{0}`, `{1}` (placeholders para `string.Format`) lleván sufijo `*Format` o se documentan en el nombre (ej. `ConfirmAddMessage`, `ErrorAddMessage`)

### Tabla de claves (63 total)

| Clave | Contexto | Placeholders |
|-------|----------|-------------|
| `PluginName` | Título del sidebar y cabecera | — |
| `BackToLibrary` | Tooltip botón volver | — |
| `Refresh` | Tooltip botón actualizar | — |
| `WishlistedSuffix` | Sufijo contador wishlist (incluye espacio leading: `" en lista de deseos"`) | — |
| `SearchByGame` | Placeholder búsqueda principal | — |
| `SearchByPlatform` | Placeholder búsqueda de plataforma | — |
| `SearchByGenre` | Placeholder búsqueda de género | — |
| `SearchByTag` | Placeholder búsqueda de etiqueta | — |
| `SearchByShowcase` | Placeholder búsqueda de showcase | — |
| `FilterSort` | Etiqueta filtro Sort | — |
| `FilterPlatforms` | Etiqueta filtro Platforms | — |
| `FilterExclude` | Tooltip botón Exclude | — |
| `FilterGenres` | Etiqueta filtro Genres | — |
| `FilterTags` | Etiqueta filtro Tags | — |
| `FilterShowcases` | Etiqueta filtro Showcases | — |
| `FilterSelectAllInYear` | Checkbox "Select all in year" | — |
| `FilterReleaseDate` | Etiqueta Release Date + fallback `FormatReleaseKind` | — |
| `ClearDate` | Tooltip botón limpiar fecha | — |
| `FilterClearFilters` | Botón "Clear Filters" | — |
| `InPlayniteLibrary` | Tooltip badge de biblioteca | — |
| `AddToWishlist` | Tooltip botón wishlist | — |
| `RemoveFromWishlist` | Tooltip botón quitar wishlist | — |
| `FlipCard` | Tooltip botón girar tarjeta | — |
| `AddToCalendar` | Tooltip botón añadir calendario | — |
| `RemoveFromCalendar` | Tooltip botón quitar calendario | — |
| `InCalendar` | Tooltip badge "En calendario" | — |
| `Trailer` | Botón "Tráiler" | — |
| `AddToLibrary` | Botón "Añadir a la biblioteca" | — |
| `FlipBack` | Tooltip botón volver a girar | — |
| `PaginationPrevious` | Botón "◀ Anterior" | — |
| `PaginationNext` | Botón "Siguiente ▶" | — |
| `PaginationPageFormat` | Texto "Página {0} de {1}" | `{0}` = current page, `{1}` = total pages |
| `PaginationCardCount` | Texto "  ({0} tarjetas)" | `{0}` = total cards |
| `AllShowcasesInYear` | Chip "Todas las presentaciones de {0}" | `{0}` = year |
| `AllYears` | ComboBox año: "Todos los años" | — |
| `ErrorConnection` | Mensaje error de conexión | — |
| `ErrorGeneric` | Plantilla "Error: {0}" | `{0}` = ex.Message |
| `ConfirmAddTitle` | Título diálogo de confirmación | — |
| `SuccessAddTitle` | Título diálogo de éxito | — |
| `ErrorAddTitle` | Título diálogo de error | — |
| `ConfirmAddMessage` | Mensaje "¿Añadir "{0}" a la biblioteca de Playnite?" | `{0}` = título del juego |
| `SuccessAddMessage` | Mensaje ""{0}" añadido a la biblioteca de Playnite" | `{0}` = título del juego |
| `ErrorAddMessage` | Mensaje "Error al añadir "{0}"...:\n{1}" | `{0}` = título, `{1}` = ex.Message |
| `DownloadingMetadata` | Progreso "Descargando metadatos para "{0}"..." | `{0}` = título del juego |
| `SettingsDefaultAction` | Label settings | — |
| `SettingsActionSaveLocal` | Item ComboBox | — |
| `SettingsActionAddLibrary` | Item ComboBox | — |
| `SettingsNote` | Nota explicativa settings | — |
| `SettingsShowConfirmation` | CheckBox settings | — |
| `ReleaseCalendar` | Título vista calendario | — |
| `LastMonthReleases` | Sección "Lanzados último mes" | — |
| `LastWeekReleases` | Sección "Lanzados última semana" | — |
| `UpcomingReleases` | Sección "Próximos lanzamientos" | — |
| `CalendarToday` | Label "Hoy" | — |
| `NoGamesOnDay` | Placeholder día sin juegos | — |
| `CalendarAdded` | Mensaje ""{0}" añadido al calendario" | `{0}` = título del juego |
| `CalendarAlreadyAdded` | Mensaje ""{0}" ya está en el calendario" | `{0}` = título del juego |
| `CalendarRemoved` | Mensaje ""{0}" eliminado del calendario" | `{0}` = título del juego |
| `CalendarSuffix` | Sufijo contador calendario (incluye espacio leading: `" en calendario"`) | — |
| `ConfirmAddCalendarMessage` | Mensaje "¿Añadir "{0}" al calendario?" | `{0}` = título del juego |
| `ConfirmRemoveCalendarMessage` | Mensaje "¿Quitar "{0}" del calendario?" | `{0}` = título del juego |

### Uso en XAML: `{DynamicResource Key}`
```xml
<TextBlock Text="{DynamicResource FilterPlatforms}" ... />
<Button ToolTip="{DynamicResource AddToWishlist}" ... />
```

### Uso en C#: `Loc.Get("Key")`
La clase helper `Loc` está definida en `GamesRecapSettings.cs` (línea 31):
```csharp
internal static class Loc
{
    public static string Get(string key)
    {
        var resource = Application.Current?.TryFindResource(key);
        return resource as string ?? key;
    }
}
```
Si la clave no existe, devuelve el propio nombre de la clave como fallback. Ejemplos de uso:
```csharp
// Con string.Format para placeholders
string msg = string.Format(Loc.Get("ConfirmAddMessage"), cardVm.Title);

// Sin placeholders
ErrorMessage = Loc.Get("ErrorConnection");

// Formato con múltiples placeholders
string err = string.Format(Loc.Get("ErrorAddMessage"), cardVm.Title, ex.Message);
```

### Strings NO localizados (identificadores internos)
- `"Games Recap"` como `Source` name en `PlayniteLibrarySync.cs:68` — identificador de origen de metadatos en la DB de Playnite
- `"Wishlist"` como tag en `PlayniteLibrarySync.cs:71,84,255` — nombre de tag usado para lookup y persistencia en SQLite
- `"gr-"` prefix en `GameId` (`PlayniteLibrarySync.cs:66`) — prefijo interno de ID
- `"newest"` como valor por defecto de sort (`BrowserViewModel.cs:28`) — valor programático, no visible

### Cómo añadir un nuevo locale
1. Copiar `Localization/en_US.xaml` a `Localization/{código}.xaml` (ej. `fr_FR.xaml`)
2. Traducir los valores de cada `sys:String`
3. Añadir al `App.xaml` como primer `ResourceDictionary` en `MergedDictionaries`:
   ```xml
   <ResourceDictionary Source="Localization/fr_FR.xaml" />
   <ResourceDictionary Source="Localization/en_US.xaml" />
   ```
4. El `GamesRecap.csproj` ya incluye `Localization\*.xaml` con `CopyToOutputDirectory=PreserveNewest` (línea 89), no necesita cambios.

### Notas importantes
- Los placeholders de búsqueda (dentro de `VisualBrush`) también usan `{DynamicResource}` y funcionan correctamente porque las resources están a nivel de `Application`.
- Los strings con espacios leading (como `WishlistedSuffix`: `" en lista de deseos"`, y los search placeholders: `" Buscar por..."`) incluyen el espacio intencionadamente para mantener el espaciado visual sin modificar el Margin de cada XAML.
- `PaginationText` y `CardCountText` son propiedades computadas en `BrowserViewModel` (líneas 249-250) que se actualizan via `OnPropertyChanged` en los setters de `CurrentPage`, `TotalPages` y `TotalCards`.

### ✅ Fase 9 — Calendar Auto-Refresh + Release Notifications (Sesión 2026-07-06)

#### Timer de refresco (GamesRecap.cs)
- `System.Timers.Timer` con intervalo fijo de 24h (86400000ms), iniciado en constructor
- `CalendarRefreshElapsed()`: chequea `CalendarRefreshIntervalDays` vs `CalendarLastRefresh`; si no ha pasado el intervalo configurado, skipea (gate)
- Logging de errores con `logger.Error` try/catch alrededor de todo el refresh
- Llamadas a `PlayniteApi.Notifications` envueltas en `Dispatcher.BeginInvoke` (requerido porque ObservableCollection no es thread-safe)

#### API batch fetch (GamesRecapApiClient.cs)
- `FetchCardsByIdsAsync(List<int> ids)`: usa `ActiveFilters.WishlistedIds` + `WishlistedMode = "include"`, envía `X-Wishlisted-Ids` header
- Paginación completa Inertia (sigue `LastPage`, mergea todas las páginas en una lista)
- Manejo de versión Inertia (actualiza en DB si la versión cambia durante la paginación)

#### Base de datos (LocalDatabase.cs)
- Nueva tabla `CalendarNotifications` (GameId, Type, SentAt, PK compuesta)
- `UpdateCalendarGameDate(int gameId, string releaseDate, string title, string coverUrl)`: UPDATE con `ClearCalendarNotifications` previo para resetear estado de notificación
- `GetCalendarLastRefresh()` / `SetCalendarLastRefresh(DateTime)`: persiste timestamp en AppMeta
- `WasCalendarNotified(int gameId, string type)`: COUNT check en CalendarNotifications
- `MarkCalendarNotified(int gameId, string type)`: INSERT OR IGNORE
- `ClearCalendarNotifications(int gameId)`: DELETE por GameId
- `ClearPastCalendarNotifications()`: DELETE join con CalendarGames donde ReleaseDate < today (limpia notificaciones de juegos ya lanzados)
- `RemoveFromCalendar()` ahora llama a `ClearCalendarNotifications(gameId)` antes del DELETE

#### Settings (GamesRecapSettings.cs + GamesRecapSettingsView.xaml)
- `CalendarRefreshIntervalDays`: int, clamped 1-365, default 30
- Presets: Daily(1), Weekly(7), Monthly(30), Bimonthly(60), Quarterly(90) + Custom (TextBox manual)
- `CalendarNotifyMonthBefore` (≤30 días), `CalendarNotifyWeekBefore` (≤7 días), `CalendarNotifyDayBefore` (1 día), `CalendarNotifySameDay` (0 días) — todos default true
- `CalendarPresetItem` class con Id, Display, Days? (null para custom)
- `SelectedCalendarPreset` property con auto-match al cargar settings + `MatchCalendarPreset()` en setter de `CalendarRefreshIntervalDays`
- `IsCustomPreset` computed property para visibilidad del TextBox
- UI: ComboBox + conditional TextBox + 4 CheckBoxes

#### Localización
- 13 nuevas claves en `es_ES.xaml` y `en_US.xaml`: presets (6), días label (1), header notificaciones (1), checkboxes (4), textos de notificación (4)
