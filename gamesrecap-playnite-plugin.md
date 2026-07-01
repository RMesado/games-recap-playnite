# GamesRecap Plugin para Playnite — Documentación Técnica

> Documentación de diseño y arquitectura para un plugin de Playnite que integra [gamesrecap.io](https://gamesrecap.io) como fuente de descubrimiento de juegos, con wishlist propia y sincronización con la librería de Playnite.

---

## Índice

1. [Concepto](#concepto)
2. [Autenticación y cuentas](#autenticación-y-cuentas)
3. [La API: Inertia.js](#la-api-inertiajs)
4. [Estructura de datos real](#estructura-de-datos-real)
5. [Parámetros de filtrado](#parámetros-de-filtrado)
6. [Flujo de usuario](#flujo-de-usuario)
7. [Tipo de plugin](#tipo-de-plugin)
8. [Arquitectura del proyecto](#arquitectura-del-proyecto)
9. [Almacenamiento: SQLite](#almacenamiento-sqlite)
10. [Integración con la librería de Playnite](#integración-con-la-librería-de-playnite)
11. [Plan de implementación](#plan-de-implementación)
12. [Riesgos](#riesgos)

---

## Concepto

Plugin de tipo **Library Plugin** que integra gamesrecap.io dentro de Playnite como un browser de juegos anunciados en showcases y conferencias gaming. El usuario descubre juegos desde el historial de showcases (Summer Game Fest, Xbox, State of Play, Nintendo Direct, etc.), los wishlistea, y opcionalmente los promueve a su librería principal de Playnite.

El estado del usuario (wishlist, vistos, ocultos) se almacena íntegramente en un **SQLite local** dentro de la carpeta de datos del plugin, incluido en los backups automáticos de Playnite. La web gamesrecap.io actúa únicamente como fuente de datos de catálogo — nunca como almacén del estado del usuario.

---

## Autenticación y cuentas

**No hay autenticación.** El campo `features.userAccounts: false` presente en todas las respuestas de la API confirma que las cuentas de usuario públicas están desactivadas de forma intencional.

```json
"features": {
    "userAccounts": false
}
```

La wishlist en la web se almacena en `localStorage` del navegador con claves como `gr.wishlist`:

```json
[
  {
    "gameId": 4874,
    "title": "Spyro: A Realm Beyond",
    "slug": "spyro-a-realm-beyond",
    "releaseDate": null,
    "addedAt": "2026-06-07T18:50:59.979Z"
  }
]
```

El plugin no necesita leer este localStorage ni interactuar con él. Gestiona su propia wishlist en SQLite desde el primer uso.

---

## La API: Inertia.js

GamesRecap usa **Inertia.js**, no una API REST convencional. El servidor devuelve HTML o JSON dependiendo de los headers de la petición.

### Headers obligatorios

```http
GET https://gamesrecap.io/
X-Inertia: true
X-Inertia-Version: 91c5bce49007757d62740bf9f1aacac6
Accept: application/json
```

- Sin `X-Inertia: true` → el servidor devuelve HTML
- Sin `X-Inertia-Version` → el servidor puede forzar un reload completo
- El valor de `X-Inertia-Version` corresponde al campo `version` de cualquier respuesta previa. Debe cachearse y actualizarse si el servidor devuelve una versión diferente.

### Endpoint único

Toda la funcionalidad del browser se obtiene desde un único endpoint:

```
GET https://gamesrecap.io/
```

Con query parameters para filtrar, paginar y buscar. No hay endpoints separados para juegos, showcases o filtros — todo llega en la misma respuesta.

### Estructura de la respuesta

```json
{
  "component": "home",
  "props": {
    "features": { "userAccounts": false },
    "cards": { ...paginación + datos },
    "filters": { ...filtros activos },
    "options": { ...opciones disponibles },
    "upcomingShowcases": [ ... ],
    "hiddenMatchingCount": 0
  },
  "version": "91c5bce49007757d62740bf9f1aacac6"
}
```

---

## Estructura de datos real

### Card (unidad de contenido)

Cada entrada en `props.cards.data` representa un juego presentado en un showcase concreto:

```json
{
  "id": 6268,
  "showcase_id": 300,
  "game_id": 4360,
  "sort_at": "2026-06-07T19:00:00Z",
  "is_draft": false,
  "game": { ... },
  "showcase": { ... },
  "media": [ ... ],
  "tags": [ ... ]
}
```

### Game

```json
{
  "id": 4360,
  "title": "Control Resonant",
  "slug": "control-resonant",
  "release_date": "2026-09-22",
  "cover_image_url": "https://images.igdb.com/igdb/image/upload/t_cover_big_2x/coc64f.jpg",
  "screenshot_url": "https://images.igdb.com/igdb/image/upload/t_1080p/scuxut.jpg",
  "igdb_id": 225582,
  "kind": "game",
  "is_draft": false,
  "publisher": { "id": 61, "name": "Remedy Entertainment", "slug": "..." },
  "developers": [ { "id": 135, "name": "Remedy Entertainment" } ],
  "platforms": [
    {
      "id": 1, "name": "Windows", "slug": "windows",
      "pivot": { "release_date": "2026-09-24" }
    }
  ],
  "genres": [ { "id": 1, "name": "Action", "slug": "action" } ],
  "tags": [
    {
      "id": 1, "name": "Single Player", "slug": "single-player",
      "icon": "User", "color": "text-sky-400",
      "auto_apply_rule": "single-player", "scope": "game"
    }
  ],
  "release_windows": [
    {
      "id": 2928, "kind": "release",
      "date": "2026-09-22T00:00:00Z",
      "platform_ids": [2],
      "display_order": 0
    }
  ]
}
```

El campo `kind` puede ser: `game`, `dlc`, `update`, `expansion`, etc.  
Las `release_windows` representan fechas de lanzamiento diferenciadas por plataforma — más precisas que el `release_date` general.

### Showcase

```json
{
  "id": 300,
  "name": "PC Gaming Show",
  "slug": "pc-gaming-show-9",
  "series_key": "pc-gaming-show",
  "event_name": null,
  "event_id": null,
  "start_at": "2026-06-07T19:00:00Z",
  "end_at": "2026-06-07T21:00:00Z",
  "url": "https://www.youtube.com/watch?v=tRVxQ7VLmHI"
}
```

El campo `series_key` es el agrupador histórico: todos los "Xbox Games Showcase" de distintos años comparten `series_key: "xbox"`. Permite filtrar por franquicia de showcase independientemente del año.

### Media

```json
{
  "id": 6790,
  "type": "trailer",
  "title": "Trailer & Dev Interview",
  "url": "https://www.youtube.com/watch?v=abHROlrtuG4",
  "is_unavailable": false
}
```

### Paginación

```json
"cards": {
  "current_page": 1,
  "per_page": 60,
  "total": 1,
  "last_page": 1,
  "next_page_url": null,
  "prev_page_url": null
}
```

60 resultados por página. Navegación con `?page=N`.

---

## Parámetros de filtrado

Todos los filtros son query parameters del endpoint raíz `/`.

| Parámetro | Tipo | Ejemplo | Descripción |
|---|---|---|---|
| `q` | string | `q=control` | Búsqueda por texto |
| `platforms` | int[] | `platforms=1,2` | Incluir plataformas (AND) |
| `exclude_platforms` | int[] | `exclude_platforms=6,7` | Excluir plataformas |
| `genres` | int[] | `genres=1,23` | Incluir géneros |
| `exclude_genres` | int[] | | Excluir géneros |
| `tags` | int[] | `tags=1,5` | Incluir tags |
| `exclude_tags` | int[] | | Excluir tags |
| `showcases` | int[] | `showcases=300,280` | Filtrar por showcase(s) |
| `sort` | string | `sort=newest` | Ordenación (ver tabla) |
| `page` | int | `page=2` | Página |
| `wishlisted_ids` | int[] | `wishlisted_ids=4360,4874` | IDs en wishlist (desde SQLite local) |
| `wishlisted_mode` | string | `wishlisted_mode=only` | `only` o `exclude` |
| `seen_ids` | int[] | | IDs marcados como vistos |
| `seen_mode` | string | | `only` o `exclude` |
| `release_from` | date | `release_from=2026-01-01` | Fecha de lanzamiento desde |
| `release_to` | date | | Fecha de lanzamiento hasta |

### Valores de `sort`

| Valor | Descripción |
|---|---|
| `newest` | Más recientes primero |
| `oldest` | Más antiguos primero |
| `title_asc` | Título A→Z |
| `title_desc` | Título Z→A |
| `media_desc` | Más media primero |
| `media_asc` | Menos media primero |
| `random` | Aleatorio |

### Opciones disponibles en `props.options`

Cada respuesta incluye la lista completa de opciones de filtrado. El plugin no necesita llamadas separadas para obtenerlas.

- **14 plataformas**: Windows, PS5, Xbox Series X|S, Switch, Switch 2, iOS, Android, PS4, Xbox One, macOS, Linux, Meta Quest, PCVR, PSVR2
- **35 géneros**: Action, Adventure, Battle Royale, Beat 'em Up, Card Battler, Deckbuilder, Dungeon Crawler, Exploration, Fighting, FPS, Hack n' Slash, Horror, Management, Metroidvania, MMO, Music/Rhythm, Narrative, Party, Platformer, Puzzle, Racing, Roguelite, RPG, Sandbox, Shoot 'em Up, Simulation, Souls-like, Sports, Stealth, Strategy, Survival, Tactical, TPS, Thriller, Visual Novel
- **13 tags**: Single Player, Multiplayer, Co-op, Exclusive, Free To Play, Early Access, Game Update, DLC, Demo Available, Crossplay, Remaster, Virtual Reality, Sequel

### Ejemplo de llamada completa

```http
GET https://gamesrecap.io/?q=control&platforms=1,2&genres=1,23&showcases=300,280&sort=newest&page=1&wishlisted_ids=4360,4874&wishlisted_mode=only
X-Inertia: true
X-Inertia-Version: 91c5bce49007757d62740bf9f1aacac6
Accept: application/json
```

---

## Flujo de usuario

```
Plugin abierto (vista principal)
│
├── Filtros laterales
│   ├── Búsqueda por texto
│   ├── Showcase / Serie      (agrupado por series_key + año)
│   ├── Plataforma            (incluir / excluir)
│   ├── Género                (incluir / excluir)
│   ├── Tags                  (Single Player, Co-op, Demo, DLC...)
│   ├── Fecha de lanzamiento  (desde / hasta)
│   ├── Ordenación            (newest, title_asc, random...)
│   └── Estado                (Todos / Solo wishlist / Solo vistos / Ocultos)
│
└── Grid de cards (60 por página)
    ├── Cover (IGDB)
    ├── Título + kind badge (DLC, Update...)
    ├── Fecha de lanzamiento (por plataforma si hay release_windows)
    ├── Showcase de origen + series_key
    ├── Plataformas
    ├── Tags con iconos y colores
    ├── Botón ▶ trailer (si hay media)
    └── Botón ★ Wishlist
            │
            ▼
        Diálogo de acción
        ┌──────────────────────────────────────┐
        │  ★ "Control Resonant"                │
        │                                      │
        │  ○ Solo guardar en wishlist          │
        │    (SQLite del plugin)               │
        │                                      │
        │  ● Añadir a librería Playnite        │
        │    Fuente:  [Games Recap]            │
        │    Tags:    [☑ Wishlist]             │
        │             [☑ PC Gaming Show]       │
        │             [☐ Action]              │
        │             [☐ Sequel]              │
        │                                      │
        │         [Cancelar]  [Añadir]         │
        └──────────────────────────────────────┘
```

---

## Tipo de plugin

**Library Plugin** (no Generic Plugin), porque:

- Añade juegos a la librería de Playnite con fuente propia (`Games Recap`)
- Gestiona metadatos ricos (showcase, géneros, plataformas, trailers)
- Implementa `GetGames()` para exponer la wishlist a Playnite
- Puede implementar un `LibraryMetadataProvider` para enriquecer metadatos

---

## Arquitectura del proyecto

```
GamesRecapPlugin/
│
├── extension.yaml                     ← manifest del plugin (GUID, nombre, versión)
│
├── GamesRecapPlugin.cs                ← Library Plugin principal + GetGames()
│
├── Services/
│   ├── GamesRecapClient.cs            ← HTTP client con headers Inertia
│   ├── InertiaVersionCache.cs         ← cachea el X-Inertia-Version
│   ├── LocalDatabase.cs               ← SQLite: wishlist, cache, sync log
│   └── PlayniteLibrarySync.cs         ← crea GameMetadata en Playnite
│
├── Models/
│   ├── ApiResponse.cs                 ← raíz de la respuesta Inertia
│   ├── Card.cs
│   ├── Game.cs                        ← con Platforms, Genres, Tags, ReleaseWindows
│   ├── Showcase.cs
│   ├── Media.cs
│   ├── FilterOptions.cs               ← opciones de plataformas, géneros, tags
│   └── ActiveFilters.cs               ← filtros activos en la UI
│
├── ViewModels/
│   ├── BrowserViewModel.cs            ← lógica del browser: filtros + grid + paginación
│   └── WishlistDialogViewModel.cs     ← lógica del diálogo de wishlist
│
├── Views/
│   ├── BrowserView.xaml               ← vista principal
│   ├── BrowserView.xaml.cs
│   ├── WishlistDialog.xaml            ← diálogo al wishlistear
│   └── WishlistDialog.xaml.cs
│
├── Settings/
│   ├── PluginSettings.cs              ← preferencias: cache TTL, tema, etc.
│   ├── PluginSettingsViewModel.cs
│   └── PluginSettingsView.xaml
│
└── Data/
    └── schema.sql                     ← esquema SQLite de referencia
```

---

## Almacenamiento: SQLite

### Ruta

```csharp
string dataDir = GetPluginUserDataPath();
// → %APPDATA%\Playnite\ExtensionsData\{PluginGuid}\
string dbPath  = Path.Combine(dataDir, "gamesrecap.db");
```

Esta carpeta **se incluye en los backups automáticos de Playnite**, por lo que la wishlist se respalda junto a toda la librería sin configuración adicional.

### Esquema completo

```sql
-- -------------------------------------------------------
-- Taxonomía (sincronizada desde props.options)
-- -------------------------------------------------------

CREATE TABLE Platforms (
    Id           INTEGER PRIMARY KEY,
    Name         TEXT NOT NULL,
    Slug         TEXT NOT NULL,
    Active       INTEGER DEFAULT 1,
    Filterable   INTEGER DEFAULT 1,
    DisplayOrder INTEGER DEFAULT 0
);

CREATE TABLE Genres (
    Id           INTEGER PRIMARY KEY,
    Name         TEXT NOT NULL,
    Slug         TEXT NOT NULL,
    Active       INTEGER DEFAULT 1,
    Filterable   INTEGER DEFAULT 1,
    DisplayOrder INTEGER DEFAULT 0
);

CREATE TABLE Tags (
    Id             INTEGER PRIMARY KEY,
    Name           TEXT NOT NULL,
    Slug           TEXT NOT NULL,
    Icon           TEXT,
    Color          TEXT,
    AutoApplyRule  TEXT,
    Scope          TEXT,
    Active         INTEGER DEFAULT 1,
    Filterable     INTEGER DEFAULT 1,
    DisplayOrder   INTEGER DEFAULT 0
);

-- -------------------------------------------------------
-- Showcases
-- -------------------------------------------------------

CREATE TABLE Showcases (
    Id         INTEGER PRIMARY KEY,
    Name       TEXT NOT NULL,
    Slug       TEXT NOT NULL UNIQUE,
    SeriesKey  TEXT,        -- agrupador histórico: 'xbox', 'state-of-play'...
    SeriesLabel TEXT,       -- nombre bonito: 'Xbox', 'State of Play'...
    EventName  TEXT,        -- 'Summer Game Fest', 'E3 Expo'...
    EventId    INTEGER,
    StartAt    TEXT,
    EndAt      TEXT,
    StreamUrl  TEXT,        -- link YouTube del showcase
    CachedAt   TEXT NOT NULL
);

-- -------------------------------------------------------
-- Publishers y Developers
-- -------------------------------------------------------

CREATE TABLE Companies (
    Id   INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Slug TEXT NOT NULL UNIQUE
);

-- -------------------------------------------------------
-- Juegos
-- -------------------------------------------------------

CREATE TABLE Games (
    Id           INTEGER PRIMARY KEY,   -- game id de gamesrecap
    Title        TEXT NOT NULL,
    Slug         TEXT NOT NULL UNIQUE,
    ReleaseDate  TEXT,                  -- fecha general (puede ser null)
    CoverUrl     TEXT,                  -- IGDB cover
    ScreenshotUrl TEXT,                 -- IGDB screenshot
    IgdbId       INTEGER,
    Kind         TEXT DEFAULT 'game',   -- 'game' | 'dlc' | 'update' | 'expansion'
    PublisherId  INTEGER REFERENCES Companies(Id),
    CachedAt     TEXT NOT NULL
);

-- Relaciones juego ↔ taxonomía

CREATE TABLE GamePlatforms (
    GameId      INTEGER REFERENCES Games(Id),
    PlatformId  INTEGER REFERENCES Platforms(Id),
    ReleaseDate TEXT,       -- fecha específica de esta plataforma (del pivot)
    PRIMARY KEY (GameId, PlatformId)
);

CREATE TABLE GameGenres (
    GameId  INTEGER REFERENCES Games(Id),
    GenreId INTEGER REFERENCES Genres(Id),
    PRIMARY KEY (GameId, GenreId)
);

CREATE TABLE GameTags (
    GameId INTEGER REFERENCES Games(Id),
    TagId  INTEGER REFERENCES Tags(Id),
    PRIMARY KEY (GameId, TagId)
);

CREATE TABLE GameDevelopers (
    GameId     INTEGER REFERENCES Games(Id),
    CompanyId  INTEGER REFERENCES Companies(Id),
    PRIMARY KEY (GameId, CompanyId)
);

-- Ventanas de lanzamiento por plataforma

CREATE TABLE ReleaseWindows (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    GameId      INTEGER REFERENCES Games(Id),
    Kind        TEXT NOT NULL,       -- 'release', 'early_access', etc.
    Date        TEXT,
    PlatformIds TEXT,                -- JSON array: [1, 2, 3]
    DisplayOrder INTEGER DEFAULT 0
);

-- -------------------------------------------------------
-- Cards (juego presentado en un showcase concreto)
-- -------------------------------------------------------

CREATE TABLE Cards (
    Id         INTEGER PRIMARY KEY,   -- card id de gamesrecap
    GameId     INTEGER REFERENCES Games(Id),
    ShowcaseId INTEGER REFERENCES Showcases(Id),
    SortAt     TEXT,                  -- posición/hora dentro del showcase
    IsDraft    INTEGER DEFAULT 0
);

CREATE TABLE CardMedia (
    Id              INTEGER PRIMARY KEY,
    CardId          INTEGER REFERENCES Cards(Id),
    Type            TEXT,             -- 'trailer', 'gameplay', 'interview'...
    Title           TEXT,
    Url             TEXT,
    IsUnavailable   INTEGER DEFAULT 0
);

-- -------------------------------------------------------
-- Estado del usuario (fuente de verdad local)
-- -------------------------------------------------------

CREATE TABLE UserGameState (
    GameId         INTEGER PRIMARY KEY REFERENCES Games(Id),
    Wishlisted     INTEGER DEFAULT 0,
    WishlistedAt   TEXT,
    Seen           INTEGER DEFAULT 0,
    SeenAt         TEXT,
    Hidden         INTEGER DEFAULT 0,
    HiddenAt       TEXT,
    PlayniteId     TEXT               -- GUID si está añadido a la librería Playnite
);

-- -------------------------------------------------------
-- Log de sincronizaciones
-- -------------------------------------------------------

CREATE TABLE SyncLog (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    SyncedAt     TEXT NOT NULL,
    PagesLoaded  INTEGER DEFAULT 0,
    CardsAdded   INTEGER DEFAULT 0,
    CardsUpdated INTEGER DEFAULT 0,
    DurationMs   INTEGER,
    Notes        TEXT
);

-- -------------------------------------------------------
-- Caché de versión Inertia
-- -------------------------------------------------------

CREATE TABLE AppMeta (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
-- Uso: INSERT OR REPLACE INTO AppMeta VALUES ('inertia_version', '91c5bce4...');
```

---

## Integración con la librería de Playnite

### GetGames() — método principal del Library Plugin

```csharp
public override IEnumerable<Game> GetGames(LibraryGetGamesArgs args)
{
    // Solo devuelve juegos que el usuario ha promovido explícitamente
    // a la librería (PlayniteId != null). Lee únicamente del SQLite local,
    // nunca hace peticiones HTTP en este método.
    return db.GetLibraryGames().Select(g => new Game
    {
        GameId      = g.PlayniteId,
        Name        = g.Title,
        PluginId    = Id,
        Source      = new MetadataNameProperty("Games Recap"),
        IsInstalled = false,
        Platforms   = g.Platforms
                       .Select(p => new MetadataNameProperty(p))
                       .ToHashSet()
    });
}
```

### Cómo aparece un juego en Playnite

| Campo Playnite | Valor |
|---|---|
| Nombre | Título del juego |
| Fuente | `Games Recap` |
| Tags | `Wishlist`, nombre del showcase, géneros seleccionados |
| Géneros | Géneros de gamesrecap |
| Plataformas | Plataformas del juego |
| Instalado | `false` (solo seguimiento) |
| Descripción | Descripción de gamesrecap/IGDB |
| Portada | Cover de IGDB |
| Trailer | Link YouTube del showcase |

Esto permite al usuario filtrar en Playnite por `Source: Games Recap` o `Tag: Wishlist`, ver sus juegos pendientes mezclados con la biblioteca, y cuando el juego salga y lo compre, cambiar la fuente a Steam/GOG eliminando el tag Wishlist.

---

## Plan de implementación

### Fase 0 — Setup (1-2 días)
- Crear proyecto C# Class Library targeting `.NET 4.6.2`
- Referenciar `Playnite.SDK.dll`
- Configurar `extension.yaml` con GUID, nombre y versión
- Verificar que el plugin vacío carga en Playnite

### Fase 1 — Base de datos local (2-3 días)
- Implementar `LocalDatabase.cs` con creación/migración del schema
- CRUD para todas las tablas
- Tests manuales: verificar ruta y backup

### Fase 2 — Cliente HTTP Inertia (2-3 días)
- Implementar `GamesRecapClient.cs` con headers `X-Inertia`
- Cachear y actualizar `X-Inertia-Version` automáticamente
- Parsear la respuesta completa (cards, filters, options)
- Manejo de errores: timeouts, sin conexión, versión desactualizada
- Cache local: no repetir llamadas si los datos son recientes

### Fase 3 — Vista browser (4-5 días)
- Grid de cards con cover, título, badges, botón wishlist
- Panel de filtros: búsqueda, showcase (agrupado por series_key), plataformas, géneros, tags, fechas, ordenación, estado
- Paginación
- Loading states y manejo de errores visible

### Fase 4 — Wishlist + Library Sync (3-4 días)
- Botón "Add to Library" en el backface de cada card
- `PlayniteLibrarySync.cs`: nuevo servicio que crea `GameMetadata` y lo persiste en Playnite
- Tabla `PromotedGames` en SQLite con datos mínimos (title, cover, platforms, genres) para `GetGames()`
- `GetGames()` real — lectura síncrona de `PromotedGames`, sin HTTP
- Inverse sync opcional vía `Database.Games.ItemUpdated`
- **NOTA:** El diálogo wishlist con Opción A (toggle SQLite) ya está implementado en Fase 3

### Fase 5 — Metadata Provider (APLAZADA)
- Sin caché local de games (12 tablas eliminadas en Fase 3), no hay datos que servir sin HTTP
- Se omite por ahora. Si se necesita en el futuro: fetch bajo demanda o guardar metadata en `PromotedGames`

### Fase 6 — Pulido y settings (2-3 días)
- Settings: `DefaultWishlistAction` (SqliteOnly/AddToLibrary), `AutoSyncWishlist`, `ShowConfirmation`, botón limpiar estado
- Notificaciones de Playnite al añadir juego a biblioteca y en errores de red
- TTL de caché ya no aplica (sin taxonomy cache desde Fase 3)

### Fase 7 — Empaquetado (1 día)
- Generar `.pext`
- README con instrucciones

**Estimación total: 14-20 días** (Fase 5 aplazada)

---

## Riesgos

**Inertia version mismatch.** Si el servidor actualiza el frontend, cambia el campo `version`. El cliente debe detectarlo (el servidor responde con un redirect o código especial) y actualizar el valor cacheado. Si no se maneja, las peticiones pueden fallar silenciosamente.

**Cambios en la estructura JSON.** Al no ser una API pública documentada, cualquier cambio en el frontend de gamesrecap.io puede romper el deserializador. Conviene hacer los DTOs tolerantes a campos nulos.

**WPF dentro de Playnite.** El sistema de temas de Playnite puede entrar en conflicto con estilos personalizados. La UI debe respetar los colores y recursos del tema activo del usuario siempre que sea posible.

**`GetGames()` debe ser síncrono y rápido.** Playnite lo llama en cada arranque. Solo debe leer del SQLite local — nunca hacer peticiones HTTP en este método.

**Rate limiting.** No se conocen los límites de la API. La caché local es la principal protección, pero conviene implementar un backoff exponencial en caso de errores 429.
