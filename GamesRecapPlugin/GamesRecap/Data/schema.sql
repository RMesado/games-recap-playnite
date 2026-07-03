-- Schema de referencia para GamesRecap Plugin (minimal)
-- SQLite local en GetPluginUserDataPath()/gamesrecap.db
-- Solo persiste estado de usuario, metadatos del sistema y juegos promovidos a librería.
-- Los datos de juegos y catálogo vienen exclusivamente de la respuesta HTTP de gamesrecap.io.

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
