-- Schema de referencia para GamesRecap Plugin (minimal)
-- SQLite local en GetPluginUserDataPath()/gamesrecap.db
-- Solo persiste estado de usuario y metadatos del sistema.
-- Los datos de juegos vienen de la respuesta HTTP de gamesrecap.io.

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
