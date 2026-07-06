# Changelog

All notable changes to this project will be documented in this file.

## 0.1.2 - 2026-07-06

### Changed
- Release notes ahora se extraen automáticamente de `docs/changelog.md` en lugar de generarse de commits
- Formato del changelog reorganizado con secciones por versión

## 0.1.3 - 2026-07-06

### Fixed
- `extension.yaml` ahora se parchea con la versión correcta del tag durante el build, solucionando que Playnite mostrara siempre `0.1.0` como versión del plugin

## 0.1.5 - 2026-07-06

### Fixed
- Restaurados `SQLite.Interop.dll` (x86/x64) en el `.pext` — son requeridos por System.Data.SQLite.dll para funcionar

### Changed
- README actualizado con advertencia: cerrar Playnite antes de instalar/actualizar el plugin

## 0.1.4 - 2026-07-06

### Fixed (DEPRECATED — no usar)
- Eliminados `SQLite.Interop.dll` (x86/x64) del `.pext` — causó que el plugin no cargara. Solucionado en v0.1.5.

## 0.1.1 - 2026-07-06

### Fixed
- Localización ahora se carga programáticamente en el constructor del plugin, solucionando textos vacíos y calendario sin renderizar en instalaciones via `.pext`
- Archivos `.xaml` de localización ya no se pierden en build Release (removido `<Generator>` conflictivo del `.csproj`)

## 0.1.0 - 2026-07-06

### Added
- Release inicial del plugin
- Navegador de juegos con cuadrícula de cards, paginación y flip animation
- Filtros avanzados: plataforma (incluir/excluir), género, etiqueta, showcase (con chips multi-año), fecha de lanzamiento, ordenación
- Wishlist local en SQLite
- Integración con biblioteca de Playnite: añadir juegos con descarga de metadatos por prioridad del usuario
- Calendario de lanzamientos: 3 secciones (último mes, última semana, próximos)
- Notificaciones de lanzamiento con intervalo configurable
- Localización completa español/inglés
- Settings: DefaultWishlistAction, ShowConfirmation, calendario
- Validación HTTP HEAD para URLs de metadata (evita covers 404)
- Cliente HTTP Inertia.js con manejo de 409/429
- Barra de progreso animada, tooltips normalizados
- Icono #ff506e y soporte para temas de Playnite

## Phase 9 — Calendar Auto-Refresh + Release Notifications (2026-07-06)

- **Timer**: `System.Timers.Timer` with 24h interval, checks `CalendarRefreshIntervalDays` vs `CalendarLastRefresh` before refreshing
- **API batch fetch**: `FetchCardsByIdsAsync(List<int> ids)` with full Inertia pagination
- **CalendarNotifications table**: tracks sent notifications per game per type
- **Settings**: Calendar refresh interval presets (Daily/Weekly/Monthly/Bimonthly/Quarterly) + custom, 4 notification checkboxes (month/week/day/same-day)
- **13 new localization keys** for calendar notification settings

## Phase 8 — Localization (2026-07-04 to 2026-07-06)

- `Localization/es_ES.xaml`: 63+ keys translated to Spanish
- `Localization/en_US.xaml`: expanded with ~15 new keys (calendar, settings, card labels)
- `App.xaml`: Spanish as primary, English as fallback
- `Loc.Get(string key)` helper class in `GamesRecapSettings.cs`
- All hardcoded strings replaced: ~30 XAML locations with `{DynamicResource}`, ~15 C# with `Loc.Get()`
- Pagination localized via computed properties
- ~20 tooltips migrated to `StandardTooltipTemplate` in `SharedResources.xaml`

## Phase 7 — Release Calendar (2026-07-03 to 2026-07-06)

- `CalendarGames` table in SQLite with CRUD operations
- `CalendarViewModel.cs`: 3 sections (last month, last week, upcoming), 6-column weekly grid
- `CalendarView.xaml`: horizontal scroll for recent sections, UniformGrid for upcoming, hover scale 1.04x
- Calendar ↔ Browser integration: filter toggle, badges, combined wishlist+calendar filtering
- `IsCompleteDateConverter`: only allows complete `yyyy-MM-dd` dates
- `icon-calendar.svg` + `icon-calendar.png`

## Phase 6 — Settings (2026-07-01)

- `DefaultWishlistAction` enum: `SqliteOnly` / `AddToLibrary`
- `ShowConfirmation` bool for confirmation dialog before adding to library
- Settings UI: ComboBox + conditional CheckBox + explanatory note

## Phase 5b — Metadata URL Validation (2026-07-02)

- HTTP HEAD validation for cover and background URLs before saving
- `IsImageUrlValid()`: returns false for 404 URLs, allowing next provider to try
- 8s timeout per HEAD request

## Phase 5 — Priority-based Metadata Download (2026-07-01)

- Metadata sources read from `config.json` → `MetadataSettings`, respecting user's priority order
- `TryApplyField()`: processes one field from one provider, returns bool
- `Games.Update()` replaces `ImportGame()` for metadata updates
- Skips `Guid.Empty` (Official Store)
- Passes `igdbId` for direct IGDB lookup

## Phase 4 — Wishlist + Library Sync (2026-06-30)

- `PromotedGames` table with GameId, Title, CoverUrl, PlatformsJson, GenresJson, TagsJson, ReleaseDate, PlayniteId
- `AddToLibrary()`: creates GameMetadata with `gr-{gameId}`, Source "Games Recap", tags (Wishlist)
- "In Library" badge with green icon
- Inverse sync: `CleanupOrphanedPromotedGames()` on sidebar open
- Removed `GetGames()` — plugin is GenericPlugin

## Phase 3 — Browser + Filters + UI (2026-06-11 to 2026-06-30)

- Simplified SQLite schema (removed 12 cache tables)
- Dead code removal from `LocalDatabase.cs`
- Fix: Wishlist moved from query params to HTTP headers (`X-Wishlisted-Ids`, `X-Wishlisted-Mode`)
- Card grid migrated from ItemsControl to ListBox + WrapPanel
- Inline ToggleButton+Popup filters (removed custom controls)
- Showcase year filtering with chips (year chips + individual chips)
- Progress bar with random chunks, cap 75%, fade out
- Card flip animation with ScaleTransform + CubicEase 400ms
- Corner-radius clipping via RectangleGeometry

## Phase 0-2 — Setup, Database, API Client

- C# Class Library targeting .NET Framework 4.8.1
- PlayniteSDK 6.15.0 reference
- `extension.yaml` with GUID, name, version
- `LocalDatabase.cs` with UserGameState, AppMeta tables
- `GamesRecapApiClient.cs` with Inertia headers, version caching, 409/429 handling
- 20 DTOs in `InertiaModels.cs`
