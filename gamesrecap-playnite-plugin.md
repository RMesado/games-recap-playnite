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

Plugin de tipo **GenericPlugin** que integra gamesrecap.io dentro de Playnite como un browser de juegos anunciados en showcases y conferencias gaming. El usuario descubre juegos desde el historial de showcases (Summer Game Fest, Xbox, State of Play, Nintendo Direct, etc.), los wishlistea, y opcionalmente los promueve a su librería principal de Playnite.

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
- El valor de `X-Inertia-Version` corresponde al campo `version` de cualquier respuesta previa. Debe cachearse en SQLite (tabla `AppMeta`) y actualizarse con cada respuesta exitosa. En caso de 409 Conflict (versión desactualizada), la nueva versión se obtiene del campo `version` de la respuesta JSON, no de headers HTTP.

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
  "per_page": 24,
  "total": 1,
  "last_page": 1,
  "next_page_url": null,
  "prev_page_url": null
}
```

24 resultados por página (valor por defecto de la API, no configurable). Navegación con `?page=N`.

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
| `seen_ids` | int[] | | IDs marcados como vistos |
| `seen_mode` | string | | `only` o `exclude` |
| `release_from` | date | `release_from=2026-01-01` | Fecha de lanzamiento desde |
| `release_to` | date | | Fecha de lanzamiento hasta |

### Headers HTTP especiales

| Header | Tipo | Descripción |
|---|---|---|
| `X-Wishlisted-Ids` | string | IDs en wishlist (desde SQLite local), separados por coma |
| `X-Wishlisted-Mode` | string | `only` o `exclude` |

> **Nota:** `wishlisted_ids` y `wishlisted_mode` se envían como headers HTTP, no como query parameters.

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
GET https://gamesrecap.io/?q=control&platforms=1,2&genres=1,23&showcases=300,280&sort=newest&page=1
X-Inertia: true
X-Inertia-Version: 91c5bce49007757d62740bf9f1aacac6
X-Wishlisted-Ids: 4360,4874
X-Wishlisted-Mode: only
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
└── Grid de cards (24 por página)
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

Plugin de tipo **GenericPlugin** (no Library Plugin). Inicialmente se implementó como `LibraryPlugin` por la capacidad de añadir juegos con fuente propia, pero tras analizar el flujo real se migró a `GenericPlugin` porque:

- No hay auto-sync de juegos desde una fuente externa (como Steam, GOG)
- Los juegos se añaden exclusivamente de forma manual vía `ImportGame(GameMetadata)`
- No se necesita `GetGames()`, `HasCustomizedGameImport` ni `LibraryClient`
- El campo "Fuente" se establece vía `metadata.Source = new MetadataNameProperty("Games Recap")`, no requiere `PluginId`
- La gestión de juegos promovidos se hace en `PromotedGames` (SQLite local), no en la DB de Playnite

**Ventaja principal**: Playnite no llama al plugin en el ciclo de import de startup, eliminando cualquier posibilidad de re-descarga de metadata al reiniciar.

---

## Arquitectura del proyecto

```
GamesRecapPlugin/GamesRecap/
│
├── GamesRecap.cs                  ← Entry point, menús, sidebar view
├── GamesRecap.csproj              ← Target v4.8.1, LangVersion 12.0
├── GamesRecapSettings.cs          ← Settings viewmodel
├── GamesRecapSettingsView.xaml/cs ← UI de settings
├── extension.yaml                 ← manifest del plugin (GUID, nombre, versión)
├── icon.svg / icon.png            ← icono #ff506e
│
├── Models/
│   ├── InertiaModels.cs           ← 20 DTOs con DataContract/DataMember
│   └── MetadataFieldConfig.cs     ← configuración de fuentes de metadata
│
├── Services/
│   ├── GamesRecapApiClient.cs     ← HTTP client con headers Inertia
│   ├── LocalDatabase.cs           ← SQLite: UserGameState + AppMeta + PromotedGames
│   └── PlayniteLibrarySync.cs     ← sync con librería de Playnite
│
└── Data/
    └── schema.sql                 ← esquema SQLite de referencia
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
-- Estado del usuario (fuente de verdad local)
-- -------------------------------------------------------

CREATE TABLE UserGameState (
    GameId       INTEGER PRIMARY KEY,
    Wishlisted   INTEGER DEFAULT 0,
    WishlistedAt TEXT,
    Seen         INTEGER DEFAULT 0,
    SeenAt       TEXT,
    Hidden       INTEGER DEFAULT 0,
    HiddenAt     TEXT,
    PlayniteId   TEXT               -- GUID si está añadido a la librería Playnite
);

-- -------------------------------------------------------
-- Caché de versión Inertia
-- -------------------------------------------------------

CREATE TABLE AppMeta (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
-- Uso: INSERT OR REPLACE INTO AppMeta VALUES ('inertia_version', '91c5bce4...');

-- -------------------------------------------------------
-- Juegos promovidos a la librería de Playnite
-- -------------------------------------------------------

CREATE TABLE PromotedGames (
    GameId        INTEGER PRIMARY KEY,   -- game id de gamesrecap
    Title         TEXT NOT NULL,
    CoverUrl      TEXT,
    PlatformsJson TEXT,                  -- JSON array de plataformas
    GenresJson    TEXT,                  -- JSON array de géneros
    TagsJson      TEXT,                  -- JSON array de tags
    ReleaseDate   TEXT,
    Description   TEXT,
    PlayniteId    TEXT UNIQUE            -- GUID del juego en librería Playnite
);
```

---

## Integración con la librería de Playnite

### AddToLibrary() — añadido manual desde el plugin

Cuando el usuario hace clic en "Add to Library" desde el sidebar del plugin, se llama a `PlayniteLibrarySync.AddToLibrary()` que:

1. Crea `GameMetadata` con `GameId = "gr-{gameId}"`, `Source = "Games Recap"`, tag `Wishlist`
2. Llama a `api.Database.ImportGame(metadata)` — sin parámetro de plugin, el juego se crea sin `PluginId` asociado
3. Persiste en `PromotedGames` el mapping entre `GameId` y `PlayniteId`
4. Descarga metadata desde las fuentes configuradas por el usuario (IGDB, SteamGridDB, etc.) vía `ActivateGlobalProgress`

```csharp
public void AddToLibrary(int gameId, string title, int? igdbId, IPlayniteAPI api, LocalDatabase db)
{
    var metadata = new GameMetadata
    {
        GameId = $"gr-{gameId}",
        Name = title,
        Source = new MetadataNameProperty("Games Recap"),
        Tags = new HashSet<MetadataProperty> { new MetadataNameProperty("Wishlist") },
        IsInstalled = false
    };
    var playniteGame = api.Database.ImportGame(metadata);
    // ...
}
```

### Cómo aparece un juego en Playnite

| Campo Playnite | Valor |
|---|---|
| Nombre | Título del juego |
| Fuente | `Games Recap` |
| GameId | `gr-{gameId}` (ID de gamesrecap) |
| Tags | `Wishlist` (se preserva siempre) + tags descargados de fuentes configuradas |
| Géneros | Descargados de fuentes configuradas en Configuración → Metadata |
| Plataformas | Descargadas de fuentes configuradas |
| Instalado | `false` (solo seguimiento) |
| Descripción | Descargada de fuentes configuradas |
| Portada | Descargada de fuentes configuradas |

Esto permite al usuario filtrar en Playnite por `Source: Games Recap` o `Tag: Wishlist`, ver sus juegos pendientes mezclados con la biblioteca, y cuando el juego salga y lo compre, cambiar la fuente a Steam/GOG eliminando el tag Wishlist.

### Descarga de metadata — Flujo completo

#### Orden de procesamiento de campos
```
Name → Genre → ReleaseDate → Developer → Publisher → Tag → Feature →
Description → CoverImage → BackgroundImage → Icon → Links →
CriticScore → CommunityScore → AgeRating → Series → Region → Platform → InstallSize
```

#### Cascada de providers por campo
Para cada campo, se itera la lista de fuentes en el orden de prioridad del usuario (Configuración → Metadata → [Campo]). La primera que devuelve datos válidos gana.

```
Campo: BackgroundImage
  → Provider 1 (ej: IGDB) → inválido o 404 → continue
  → Provider 2 (ej: SteamGridDB) → inválido o 404 → continue
  → Provider 3 (ej: Steam) → imagen válida → asignar y break
```

#### Validación de URLs de imágenes
**Problema**: Cuando un juego no tiene `library_hero` en Steam, la URL `https://steamcdn-a.akamaihd.net/steam/apps/{appid}/library_hero.jpg` devuelve 404. Playnite guarda esta URL como background y luego falla al cargarla.

**Solución**: Antes de asignar `game.CoverImage` o `game.BackgroundImage`, se valida la URL con un HTTP HEAD request (timeout 8s). Si la validación falla:
1. Se loggea un warning con la URL problemática
2. Se retorna `false` desde `TryApplyField()`
3. El siguiente provider en la lista de prioridades intenta con su imagen

**Flujo de validación**:
```csharp
// En TryApplyField(), para CoverImage y BackgroundImage:
var cover = provider.GetCoverImage(args);
if (cover == null || cover.Path == null) return false;
if (!IsImageUrlValid(cover.Path))   // HEAD request a URLs HTTP
{
    logger.Warn($"Cover image not available: {cover.Path}");
    return false;  // → siguiente provider intenta
}
game.CoverImage = cover.Path;
return true;
```

**`IsImageUrlValid`**:
- Paths locales (ya descargados): retorna `true` sin validación
- URLs HTTP/HTTPS: hace HEAD request, retorna `true` solo si status es 2xx
- Excepciones de red/timeout: retorna `false`

---

## Plan de implementación

### Fase 0 — Setup (1-2 días)
- Crear proyecto C# Class Library targeting `.NET Framework 4.8.1`
- Referenciar `Playnite.SDK.dll`
- Configurar `extension.yaml` con GUID, nombre y versión
- Verificar que el plugin vacío carga en Playnite

### Fase 1 — Base de datos local (2-3 días)
- Implementar `LocalDatabase.cs` con creación/migración del schema (3 tablas: UserGameState, AppMeta, PromotedGames)
- CRUD para UserGameState y PromotedGames
- Tests manuales: verificar ruta y backup

### Fase 2 — Cliente HTTP Inertia (2-3 días)
- Implementar `GamesRecapApiClient.cs` con headers `X-Inertia`
- Cachear `X-Inertia-Version` en tabla `AppMeta`
- Parsear la respuesta completa (cards, filters, options)
- Manejo de errores: timeouts, sin conexión, 409 (version mismatch), 429 (rate limiting)

### Fase 3 — Vista browser (4-5 días)
- Grid de cards con cover, título, badges, botón wishlist/flip
- Panel de filtros: búsqueda, showcase (agrupado por series_key + año), plataformas (incluir/excluir), géneros (incluir/excluir), tags (incluir/excluir), fechas de lanzamiento, ordenación, estado
- Paginación
- Loading states y manejo de errores visible
- Chips de showcase multi-año

### Fase 4 — Wishlist + Library Sync (3-4 días)
- Botón "Add to Library" en el backface de cada card
- `PlayniteLibrarySync.cs`: servicio que crea `GameMetadata` y lo persiste en Playnite
- Tabla `PromotedGames` en SQLite con datos mínimos (title, cover, platforms, genres, description)
- `AddToLibrary()` — añade juego a Playnite vía `ImportGame()`, descarga metadata de fuentes configuradas
- Badge "In Library" con icono verde en cada card

### Fase 5 — Metadata download por prioridad del usuario (completada)
- Descarga de metadata usando `ActivateGlobalProgress` (popup nativo sin congelar UI)
- Fuentes de metadata leídas de `config.json` → `MetadataSettings`, respetando el orden de prioridad del usuario
- Por cada campo, se itera las fuentes en orden; la primera que devuelve datos válidos gana
- Se omite `Guid.Empty` (Tienda oficial)
- `GameId` como `igdbId?.ToString()` para lookup directo en IGDB
- `Games.Update(game)` reemplaza `ImportGame()` para actualizar metadata
- **Validación HTTP HEAD** para URLs de cover y background: evita guardar URLs 404 (ej: Steam CDN `library_hero.jpg` para juegos sin artwork). Si la validación falla, el siguiente provider en la prioridad intenta su imagen.

### Fase 6 — Pulido y settings (completada)
- `DefaultWishlistAction` (enum: SqliteOnly/AddToLibrary): cuando es AddToLibrary, el toggle de wishlist también añade a la biblioteca de Playnite automáticamente
- `ShowConfirmation` (bool): muestra diálogo de confirmación antes de añadir a biblioteca vía botón manual; se omite cuando viene del toggle de wishlist
- UI de Settings con ComboBox y nota informativa condicional

### Fase 7 — Empaquetado (1 día)
- Generar `.pext`
- README con instrucciones

**Estimación total: 14-20 días** (Fases 4-6 completadas, Fase 7 pendiente)

---

## Riesgos

**Inertia version mismatch.** Si el servidor actualiza el frontend, cambia el campo `version`. El servidor responde con 409 Conflict. La nueva versión se obtiene del campo `version` de la respuesta JSON y se actualiza en `AppMeta`. Si no se maneja, las peticiones pueden fallar silenciosamente.

**Cambios en la estructura JSON.** Al no ser una API pública documentada, cualquier cambio en el frontend de gamesrecap.io puede romper el deserializador. Conviene hacer los DTOs tolerantes a campos nulos.

**WPF dentro de Playnite.** El sistema de temas de Playnite puede entrar en conflicto con estilos personalizados. La UI debe respetar los colores y recursos del tema activo del usuario siempre que sea posible.

**`GenericPlugin` en vez de `LibraryPlugin`.** Al no exponer `GetGames()`, Playnite no re-importa juegos al reiniciar. Esto elimina la re-descarga de metadata, pero otros plugins (HLTB, ProtonDB, SteamTagsImporter) siguen detectando juegos nuevos por timestamp en `OnLibraryUpdated`. No hay manera de evitarlo desde nuestro plugin.

**Rate limiting.** No se conocen los límites de la API. La caché local es la principal protección, pero conviene implementar un backoff exponencial en caso de errores 429.
