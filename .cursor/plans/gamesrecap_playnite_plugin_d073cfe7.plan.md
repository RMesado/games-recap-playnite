---
name: GamesRecap Playnite Plugin
overview: Plan de implementación desde cero de un Library Plugin para Playnite (.NET Framework 4.6.2) que consume la API Inertia.js de gamesrecap.io, persiste estado local en SQLite y permite descubrir, wishlistear y promover juegos a la librería de Playnite.
todos:
  - id: phase-0-scaffold
    content: "Fase 0: Generar proyecto Library Plugin con Toolbox, extension.yaml, verificar carga en Playnite"
    status: completed
  - id: phase-1-sqlite
    content: "Fase 1: Implementar LocalDatabase + schema SQLite + DTOs Inertia desde respuesta-filtros-ex.json"
    status: pending
  - id: phase-2-client
    content: "Fase 2: GamesRecapClient con headers Inertia, query builder de filtros, cache de versión y upsert"
    status: pending
  - id: phase-3-browser
    content: "Fase 3: BrowserView WPF + BrowserViewModel con filtros, grid, paginación y menú principal"
    status: pending
  - id: phase-4-wishlist
    content: "Fase 4: Botón 'Add to Library' en card, PlayniteLibrarySync, tabla PromotedGames, GetGames() real e inverse sync opcional"
    status: pending
  - id: phase-5-metadata
    content: "Fase 5: (APLAZADA) Metadata Provider — sin caché local de games, no hay datos que servir sin HTTP. Se omite por ahora."
    status: pending
  - id: phase-6-settings
    content: "Fase 6: PluginSettings (default wishlist action, auto-sync, notificaciones), botón limpiar estado"
    status: pending
  - id: phase-7-package
    content: "Fase 7: Build .pext, README y checklist de pruebas manuales"
    status: pending
isProject: false
---

# Plan de implementación: GamesRecap Plugin para Playnite

## Contexto

El repositorio actual solo contiene documentación de diseño ([gamesrecap-playnite-plugin.md](gamesrecap-playnite-plugin.md)) y una respuesta real de la API ([respuesta-filtros-ex.json](respuesta-filtros-ex.json)). **No hay código C# todavía** — es un proyecto greenfield.

La consulta de ejemplo `?q=Control%20Resonant&platforms=1&showcases=300` devuelve 1 card (Control Resonant, PC Gaming Show #300) con paginación Laravel (`per_page: 60`, `total: 1`) y versión Inertia `91c5bce49007757d62740bf9f1aacac6`.

## Referencias Playnite

Seguir las guías oficiales:

- [Plugins Introduction](https://api.playnite.link/docs/tutorials/extensions/plugins.html): .NET Framework 4.6.2, solo referenciar `Playnite.SDK`, generar proyecto con Toolbox, cargar desde *Settings → For developers → External extensions*
- [Library Plugins](https://api.playnite.link/docs/tutorials/extensions/libraryPlugins.html): heredar `LibraryPlugin`, implementar `GetGames(LibraryGetGamesArgs)` devolviendo `GameMetadata`
- [Plugin settings](https://api.playnite.link/docs/tutorials/extensions/pluginSettings.html): `GetSettings` + `GetSettingsView`
- [Creating custom windows](https://api.playnite.link/docs/tutorials/extensions/windows.html): `PlayniteApi.Dialogs.CreateWindow()` para el browser WPF con tema Playnite
- SDK no es thread-safe: operaciones UI vía `PlayniteApi.MainView.UIDispatcher`

## Decisiones de arquitectura

| Decisión | Elección | Motivo |
|---|---|---|
| Tipo de plugin | **Library Plugin** | Añade fuente `Games Recap`, expone juegos promovidos vía `GetGames()`, metadata opcional |
| Vista browser | **Menú principal** (`GetMainMenuItems`) + ventana modal | Library plugins no tienen vista de descubrimiento nativa; patrón documentado en TestPlugin |
| Estado usuario | **SQLite local** en `GetPluginUserDataPath()` | Wishlist/vistos/ocultos propios; incluido en backups Playnite |
| Catálogo | **API Inertia** `GET https://gamesrecap.io/` | Un solo endpoint; filtros como query params |
| Caché | SQLite + TTL configurable | Evitar rate limiting y acelerar filtros repetidos |

## Flujo de datos

```mermaid
flowchart TB
    subgraph playnite [Playnite]
        Menu[GetMainMenuItems]
        Browser[BrowserView WPF]
        GetGames[GetGames]
        DB_Playnite[Playnite Database]
    end

    subgraph plugin [GamesRecap Plugin]
        VM[BrowserViewModel]
        Client[GamesRecapClient]
        LocalDB[LocalDatabase SQLite]
        Sync[PlayniteLibrarySync]
    end

    subgraph external [gamesrecap.io]
        API["GET / con headers Inertia"]
    end

    Menu --> Browser
    Browser --> VM
    VM --> Client
    VM --> LocalDB
    Client --> API
    API --> Client
    Client --> LocalDB
    VM --> Sync
    Sync --> DB_Playnite
    GetGames --> LocalDB
```

## Ajustes al diseño según JSON real

El archivo [respuesta-filtros-ex.json](respuesta-filtros-ex.json) confirma la spec pero añade campos que los DTOs deben contemplar:

- **`filters.hidden_ids`** y **`filters.view`** (`"cards"`) — no están en la tabla de la doc; incluirlos en `ActiveFilters`
- **`options.showcases`**: lista histórica enorme (~200+ entradas) con `series_key`, `series_label`, `event_name`, `start_at` — agrupar en UI por `series_key` + año
- **`options.sorts`**: array `{ value, label }` — poblar combo de ordenación dinámicamente
- Campos extra en cards: `custom_order`, `created_at`, `updated_at`; `tags` a nivel card (distinto de `game.tags`)
- Género real: `"Role-Playing (RPG)"` (id 23), no abreviado como en la doc

## Estructura del proyecto

Generar con Toolbox:

```bash
Toolbox.exe new LibraryPlugin "Games Recap" "c:\Users\rafae\PycharmProjects\games-recap-playnite\GamesRecapPlugin"
```

Estructura objetivo (según [gamesrecap-playnite-plugin.md](gamesrecap-playnite-plugin.md)):

```
GamesRecapPlugin/
├── extension.yaml
├── GamesRecapPlugin.cs          # LibraryPlugin principal
├── Services/
│   ├── GamesRecapClient.cs
│   ├── InertiaVersionCache.cs
│   ├── LocalDatabase.cs
│   └── PlayniteLibrarySync.cs
├── Models/                      # DTOs Inertia + entidades locales
├── ViewModels/
├── Views/
├── Settings/
└── Data/schema.sql
```

**Dependencias NuGet** (verificar versiones compatibles con Playnite antes de añadir):
- `System.Data.SQLite` — persistencia local
- Usar serialización del SDK (`SerializationPropertyName`) donde sea posible en lugar de Newtonsoft con versión distinta

## Fases de implementación

### Fase 0 — Scaffolding Playnite (1-2 días)

1. Generar proyecto Library Plugin con Toolbox
2. Configurar `.csproj`: Target `net462`, referencia solo `Playnite.SDK`
3. Crear `extension.yaml` con GUID fijo, nombre `Games Recap`, versión `0.1.0`
4. Implementar clase mínima:

```csharp
public class GamesRecapPlugin : LibraryPlugin
{
    public override LibraryPluginProperties Properties => new()
    {
        HasSettings = true,
        HasCustomizedGameImport = true
    };

    public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        => Enumerable.Empty<GameMetadata>(); // stub
}
```

5. Registrar carpeta `bin\Debug\` en *External extensions* y verificar carga en Playnite
6. Configurar debug: *Start external program* → ejecutable de Playnite ([doc plugins](https://api.playnite.link/docs/tutorials/extensions/plugins.html))

**Criterio de éxito:** plugin aparece en lista de extensiones sin errores.

---

### Fase 1 — SQLite y modelos (2-3 días)

1. Implementar [schema SQL completo](gamesrecap-playnite-plugin.md) en `LocalDatabase.cs`:
   - Creación idempotente al primer arranque
   - Tabla `AppMeta` para `inertia_version`
2. CRUD esencial:
   - `UserGameState`: wishlist / seen / hidden / `PlayniteId`
   - Upsert de taxonomía (`Platforms`, `Genres`, `Tags`, `Showcases`, `Companies`)
   - Upsert de `Games`, relaciones y `Cards`/`CardMedia` desde respuestas API
   - `GetWishlistedIds()`, `GetSeenIds()`, `GetHiddenIds()` para construir query params
   - `GetLibraryGames()` — solo registros con `PlayniteId != null`
3. Crear DTOs Inertia en `Models/` mapeando [respuesta-filtros-ex.json](respuesta-filtros-ex.json):
   - `InertiaResponse` → `component`, `props`, `version`, `url`
   - `HomeProps` → `cards`, `filters`, `options`, `upcomingShowcases`, `hiddenMatchingCount`
   - `PaginatedCards`, `Card`, `GrGame`, `Showcase`, `MediaItem`
   - Usar `[SerializationPropertyName("snake_case")]` del SDK
   - Propiedades nullable y listas vacías por defecto (tolerancia a cambios API)

**Criterio de éxito:** deserializar `respuesta-filtros-ex.json` en tests manuales y persistir 1 card completa en SQLite.

---

### Fase 2 — Cliente HTTP Inertia (2-3 días)

Implementar `GamesRecapClient.cs`:

**Headers obligatorios en cada petición:**
```http
X-Inertia: true
X-Inertia-Version: {cached}
Accept: application/json
```

**Query builder** — mapear filtros UI → URL (validado con ejemplo real):

| UI | Query param | Ejemplo real |
|---|---|---|
| Búsqueda | `q` | `Contro` |
| Plataformas incluir | `platforms` | `1` |
| Showcase | `showcases` | `300` |
| Wishlist local | `wishlisted_ids` + `wishlisted_mode` | desde SQLite |
| Vistos | `seen_ids` + `seen_mode` | desde SQLite |
| Ocultos | `hidden_ids` | desde SQLite |
| Fechas | `release_from`, `release_to` | ISO date |
| Orden | `sort` | `newest` (default) |
| Página | `page` | `1` |

**Lógica de versión (`InertiaVersionCache`):**
1. Leer versión de `AppMeta` o usar valor hardcoded inicial del JSON de ejemplo
2. Tras cada respuesta exitosa, persistir `response.version`
3. Si respuesta es 409 con header `X-Inertia-Location` → reintentar con nueva versión (manejo estándar Inertia)

**Resiliencia:**
- Timeout configurable (30s default)
- Backoff exponencial en 429/5xx
- No bloquear `GetGames()` — solo lectura SQLite

**Post-fetch:** `LocalDatabase.UpsertFromApiResponse(props)` sincroniza taxonomía + cards recibidas.

**Criterio de éxito:** desde consola/debug, fetch con filtros del ejemplo devuelve Control Resonant y persiste en DB.

---

### Fase 3 — Browser UI (4-5 días)

**Entrada:** `GetMainMenuItems` → item "Explorar Games Recap" abre `BrowserView` en ventana `CreateWindow` (1024×768, `CenterOwner`).

**Layout WPF** (`BrowserView.xaml`):
- Panel izquierdo: filtros
- Panel derecho: grid de cards + paginación + estados loading/error

**Filtros** (alimentados por `props.options` de última respuesta):
- TextBox búsqueda (debounce 400ms)
- Showcase: `TreeView`/`ComboBox` agrupado por `series_label ?? series_key`, sub-items por año (`start_at.Year`)
- Plataformas / Géneros / Tags: checkboxes incluir + toggle excluir
- Rango fechas (`DatePicker`)
- Ordenación: `ComboBox` desde `options.sorts`
- Estado: Todos | Solo wishlist | Excluir wishlist | Solo vistos | Ocultos

**Card template:**
- Cover IGDB (`cover_image_url`)
- Título + badge `kind` (DLC, update...)
- Fecha release (priorizar `release_windows` por plataforma filtrada)
- Showcase origen + `series_key`
- Plataformas (chips)
- Tags con color
- Botón trailer → `Process.Start` URL YouTube
- Botón wishlist (estrella) → abre diálogo

**Paginación:** botones Anterior/Siguiente + indicador `page X de Y` usando `cards.current_page`, `cards.last_page`, `cards.total`.

**MVVM:** `BrowserViewModel` con `RelayCommand`, propiedades observables, llamadas async al client en background thread, actualización UI en `UIDispatcher`.

**Indicador `hiddenMatchingCount`:** mostrar aviso si > 0 ("X juegos ocultos coinciden con el filtro").

**Criterio de éxito:** usuario puede filtrar, paginar, ver trailer y marcar wishlist sin errores UI.

---

### Fase 4 — Wishlist + Library Sync (3-4 días)

**NOTA:** La Opción A (wishlist toggle en SQLite) ya existe desde Fase 3 — botón corazón en cada card con `ToggleWishlistCommand`. Esta fase añade la promoción a biblioteca de Playnite.

**1. Botón "Add to Library" en el backface de la card**
- En el backface (visible al hacer flip), junto al botón de trailer
- Llama a `AddToLibraryCommand(int gameId)` en el ViewModel
- Pasa el objeto `Card` completo para tener title, cover, platforms, genres, etc.

**2. `PlayniteLibrarySync.cs` — nuevo servicio**
```csharp
public class PlayniteLibrarySync
{
    // Añadir juego a la biblioteca de Playnite
    void AddToLibrary(Card card, IPlayniteAPI api, LocalDatabase db)

    // Devuelve GameMetadata para GetGames() desde PromotedGames
    List<GameMetadata> GetPromotedGames(LocalDatabase db)
}
```
- `AddToLibrary`: crea `GameMetadata`, llama a `api.Database.Games.Add()`, guarda `PlayniteId` en `UserGameState` y datos en `PromotedGames`
- Mapeo: `GameId = $"gr-{gameId}"`, Name, Source("Games Recap"), Platforms (`MetadataNameProperty`), Tags (Wishlist + showcase + géneros), CoverImage (`MetadataFile`), Links, ReleaseDate
- Si ya existe (`PlayniteId` no null): actualizar metadata, no duplicar

**3. Tabla `PromotedGames` en SQLite**
```sql
CREATE TABLE IF NOT EXISTS PromotedGames (
    GameId INTEGER PRIMARY KEY,
    Title TEXT NOT NULL,
    CoverUrl TEXT,
    PlatformsJson TEXT,
    GenresJson TEXT,
    TagsJson TEXT,
    ReleaseDate TEXT,
    PlayniteId TEXT UNIQUE
);
```
- Se llena al hacer "Add to Library" con datos mínimos
- `GetGames()` lee de aquí — **síncrono, sin HTTP**
- Se elimina el registro si el juego se quita de la librería Playnite

**4. `GetGames()` en `GamesRecap.cs`**
```csharp
public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
{
    var sync = new PlayniteLibrarySync();
    return sync.GetPromotedGames(localDb);
}
```
- Solo devuelve juegos con `PlayniteId` en `PromotedGames`
- Síncrono, solo lectura SQLite

**5. Inverse sync (opcional)**
- Suscribirse a `PlayniteApi.Database.Games.ItemUpdated`
- Si un juego con source "Games Recap" se elimina, limpiar `PlayniteId` en `UserGameState` y borrar de `PromotedGames`

**Criterio de éxito:** juego promovido aparece en Playnite con fuente "Games Recap", filtrable por tag Wishlist; `GetGames()` lo devuelve al reiniciar.

---

### Fase 5 — Metadata Provider (APLAZADA)

**Decisión:** Omitir `LibraryMetadataProvider` por ahora.

En Fase 3 se eliminaron las 12 tablas de caché local (Games, Platforms, Genres, Tags, etc.). Sin caché local de games, no hay datos que servir sin una llamada HTTP, lo que viola la naturaleza síncrona de metadata download.

Si se necesita en el futuro, se puede implementar con:
- Fetch bajo demanda a la API (lento, pero funcional)
- O guardar metadata completa en `PromotedGames` al promover

**Criterio de éxito:** No implementado. Marcado como `TODO` en el código.

---

### Fase 6 — Settings y pulido (2-3 días)

**`PluginSettings`** (`GetSettings` / `GetSettingsView`):

| Setting | Tipo | Default | Descripción |
|---------|------|---------|-------------|
| `DefaultWishlistAction` | Enum (`SqliteOnly`, `AddToLibrary`) | `SqliteOnly` | Comportamiento al hacer clic en corazón |
| `AutoSyncWishlist` | bool | `false` | Añadir automáticamente juegos wishlisteados a la librería |
| `ShowConfirmation` | bool | `true` | Mostrar confirmación antes de añadir a la biblioteca |
| (acción) | Button | — | Limpiar `UserGameState` + `PromotedGames` |

**Notas:**
- TTL de caché ya no aplica (no hay taxonomy cache desde Fase 3)
- Timeout HTTP se mantiene hardcoded (no necesario como setting)

**UX:**
- Notificaciones `PlayniteApi.Notifications` en errores de red y al añadir juego a la biblioteca
- Respetar tema Playnite (evitar colores hardcoded; usar recursos dinámicos)

**Criterio de éxito:** settings persisten entre sesiones; UI coherente con tema oscuro/claro de Playnite.

---

### Fase 7 — Empaquetado y documentación (1 día)

1. Build Release → empaquetar como `.pext` (zip con `extension.yaml` + DLLs)
2. README: requisitos (Playnite 10+, .NET 4.6.2), instalación, uso del browser, limitaciones API
3. Checklist manual de pruebas

## Mapeo Playnite ↔ GamesRecap

| Campo Playnite | Origen |
|---|---|
| `Name` | `game.title` |
| `GameId` | `gr-{game.id}` |
| `Source` | `"Games Recap"` |
| `Platforms` | `game.platforms[].name` |
| `Genres` | `game.genres[].name` |
| `Tags` | Wishlist + showcase + tags seleccionados |
| `CoverImage` | `game.cover_image_url` |
| `BackgroundImage` | `game.screenshot_url` |
| `ReleaseDate` | `release_windows` o `release_date` |
| `Links` | `media[].url` (trailers) |
| `IsInstalled` | `false` |

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Cambio `X-Inertia-Version` | Cachear en SQLite; detectar 409; reintentar automático |
| JSON no documentado cambia | DTOs tolerantes; logging de deserialización fallida |
| Conflictos NuGet con Playnite | Solo SDK + verificar versiones de dependencias en [deps Playnite](https://api.playnite.link/docs/tutorials/extensions/plugins.html) |
| `GetGames()` lento | Solo SQLite; índices en `UserGameState.PlayniteId` |
| Rate limiting | TTL caché + debounce búsqueda + backoff |
| UI thread crashes | Todo binding/update vía `UIDispatcher` |
| `options.showcases` enorme | Cachear en SQLite; UI con virtualización y agrupación |

## Estimación

| Fase | Días |
|---|---|---|
| 0 Scaffolding | 1-2 |
| 1 SQLite + modelos | 2-3 |
| 2 Cliente Inertia | 2-3 |
| 3 Browser UI | 4-5 |
| 4 Wishlist + Library Sync | 3-4 |
| 5 Metadata Provider | APLAZADA |
| 6 Settings + pulido | 2-3 |
| 7 Empaquetado | 1 |
| **Total** | **14-20 días** |

## MVP recomendado (entregable intermedio ~8-10 días)

Para validar valor antes del producto completo:

1. Fases 0-2 completas
2. Fase 3 reducida: grid + filtros básicos (búsqueda, plataforma, showcase, sort) sin excluir
3. Fase 4 reducida: wishlist SQLite + promover a librería (ya sin diálogo, toggle directo implementado en Fase 3)
4. Posponer: metadata provider, filtros exclude, estado seen/hidden, empaquetado

Esto permite explorar showcases y wishlistear desde Playnite con el menor riesgo.

## Archivos clave a crear primero

1. [GamesRecapPlugin/GamesRecapPlugin.cs](GamesRecapPlugin/GamesRecapPlugin.cs) — punto de entrada
2. [GamesRecapPlugin/Services/GamesRecapClient.cs](GamesRecapPlugin/Services/GamesRecapClient.cs) — integración API
3. [GamesRecapPlugin/Services/LocalDatabase.cs](GamesRecapPlugin/Services/LocalDatabase.cs) — persistencia
4. [GamesRecapPlugin/Models/InertiaModels.cs](GamesRecapPlugin/Models/InertiaModels.cs) — DTOs basados en [respuesta-filtros-ex.json](respuesta-filtros-ex.json)
5. [GamesRecapPlugin/Views/BrowserView.xaml](GamesRecapPlugin/Views/BrowserView.xaml) — UI principal
