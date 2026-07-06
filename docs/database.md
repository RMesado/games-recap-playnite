# Database Schema

## Overview

The plugin uses a local SQLite database located at:

```csharp
string dataDir = GetPluginUserDataPath();
// → %APPDATA%\Playnite\ExtensionsData\{PluginGuid}\
string dbPath = Path.Combine(dataDir, "gamesrecap.db");
```

This folder is **included in Playnite's automatic backups**, so the wishlist is backed up with the library.

## Tables

### UserGameState

Source of truth for user interaction with games.

| Column | Type | Notes |
|--------|------|-------|
| GameId | INTEGER PK | Game ID on gamesrecap.io |
| Wishlisted | INTEGER DEFAULT 0 | 0/1 |
| WishlistedAt | TEXT | UTC ISO8601 |
| Seen | INTEGER DEFAULT 0 | 0/1 |
| SeenAt | TEXT | UTC ISO8601 |
| Hidden | INTEGER DEFAULT 0 | 0/1 |
| HiddenAt | TEXT | UTC ISO8601 |
| PlayniteId | TEXT | GUID of the game in Playnite library |

### AppMeta

Key-value store for system metadata.

| Column | Type | Notes |
|--------|------|-------|
| Key | TEXT PK | e.g. `inertia_version` |
| Value | TEXT NOT NULL | e.g. MD5 hash of Inertia version |

### PromotedGames

Games that have been promoted to the Playnite library.

| Column | Type | Notes |
|--------|------|-------|
| GameId | INTEGER PK | Game ID on gamesrecap.io |
| Title | TEXT NOT NULL | Game title |
| CoverUrl | TEXT | Cover URL (IGDB) |
| PlatformsJson | TEXT | JSON array of platforms |
| GenresJson | TEXT | JSON array of genres |
| TagsJson | TEXT | JSON array of tags |
| ReleaseDate | TEXT | Release date |
| Description | TEXT | Game description |
| PlayniteId | TEXT UNIQUE | GUID in Playnite library |

### CalendarGames

Games added to the release calendar.

| Column | Type | Notes |
|--------|------|-------|
| GameId | INTEGER PK | Game ID on gamesrecap.io |
| Title | TEXT NOT NULL | Game title |
| CoverUrl | TEXT | Cover URL (IGDB) |
| ReleaseDate | TEXT NOT NULL | `yyyy-MM-dd` (complete date only) |
| AddedAt | TEXT NOT NULL | UTC ISO8601 |

### CalendarNotifications

Tracks which release notifications have been sent to the user.

| Column | Type | Notes |
|--------|------|-------|
| GameId | INTEGER | Game ID |
| Type | TEXT | Notification type |
| SentAt | TEXT | When it was sent |
| PK | | Composite: (GameId, Type) |

## Schema SQL

```sql
CREATE TABLE UserGameState (
    GameId       INTEGER PRIMARY KEY,
    Wishlisted   INTEGER DEFAULT 0,
    WishlistedAt TEXT,
    Seen         INTEGER DEFAULT 0,
    SeenAt       TEXT,
    Hidden       INTEGER DEFAULT 0,
    HiddenAt     TEXT,
    PlayniteId   TEXT
);

CREATE TABLE AppMeta (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE TABLE PromotedGames (
    GameId        INTEGER PRIMARY KEY,
    Title         TEXT NOT NULL,
    CoverUrl      TEXT,
    PlatformsJson TEXT,
    GenresJson    TEXT,
    TagsJson      TEXT,
    ReleaseDate   TEXT,
    Description   TEXT,
    PlayniteId    TEXT UNIQUE
);

CREATE TABLE CalendarGames (
    GameId      INTEGER PRIMARY KEY,
    Title       TEXT NOT NULL,
    CoverUrl    TEXT,
    ReleaseDate TEXT NOT NULL,
    AddedAt     TEXT NOT NULL
);
```

## Design Decisions

- **No game catalog caching**: games and catalog data come exclusively from the HTTP response from gamesrecap.io. Only user state is persisted locally.
- **Minimal schema**: only 4 tables + 1 notification table. 12 cache tables were removed during refactoring.
- **Backup compatibility**: the database file lives in Playnite's extension data directory, which is automatically included in backups.
