-- Schema de referencia para GamesRecap Plugin
-- SQLite local en GetPluginUserDataPath()/gamesrecap.db

-- Taxonomía (sincronizada desde props.options)
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

-- Showcases
CREATE TABLE Showcases (
    Id          INTEGER PRIMARY KEY,
    Name        TEXT NOT NULL,
    Slug        TEXT NOT NULL UNIQUE,
    SeriesKey   TEXT,
    EventName   TEXT,
    EventId     INTEGER,
    StartAt     TEXT,
    EndAt       TEXT,
    StreamUrl   TEXT,
    CachedAt    TEXT NOT NULL
);

-- Publishers y Developers
CREATE TABLE Companies (
    Id   INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Slug TEXT NOT NULL UNIQUE
);

-- Juegos
CREATE TABLE Games (
    Id            INTEGER PRIMARY KEY,
    Title         TEXT NOT NULL,
    Slug          TEXT NOT NULL UNIQUE,
    ReleaseDate   TEXT,
    CoverUrl      TEXT,
    ScreenshotUrl TEXT,
    IgdbId        INTEGER,
    Kind          TEXT DEFAULT 'game',
    PublisherId   INTEGER REFERENCES Companies(Id),
    CachedAt      TEXT NOT NULL
);

-- Relaciones juego ↔ taxonomía
CREATE TABLE GamePlatforms (
    GameId      INTEGER REFERENCES Games(Id),
    PlatformId  INTEGER REFERENCES Platforms(Id),
    ReleaseDate TEXT,
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
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    GameId       INTEGER REFERENCES Games(Id),
    Kind         TEXT NOT NULL,
    Date         TEXT,
    PlatformIds  TEXT,
    DisplayOrder INTEGER DEFAULT 0
);

-- Cards (juego presentado en un showcase concreto)
CREATE TABLE Cards (
    Id         INTEGER PRIMARY KEY,
    GameId     INTEGER REFERENCES Games(Id),
    ShowcaseId INTEGER REFERENCES Showcases(Id),
    SortAt     TEXT,
    IsDraft    INTEGER DEFAULT 0
);

CREATE TABLE CardMedia (
    Id            INTEGER PRIMARY KEY,
    CardId        INTEGER REFERENCES Cards(Id),
    Type          TEXT,
    Title         TEXT,
    Url           TEXT,
    IsUnavailable INTEGER DEFAULT 0
);

-- Estado del usuario
CREATE TABLE UserGameState (
    GameId       INTEGER PRIMARY KEY REFERENCES Games(Id),
    Wishlisted   INTEGER DEFAULT 0,
    WishlistedAt TEXT,
    Seen         INTEGER DEFAULT 0,
    SeenAt       TEXT,
    Hidden       INTEGER DEFAULT 0,
    HiddenAt     TEXT,
    PlayniteId   TEXT
);

-- Log de sincronizaciones
CREATE TABLE SyncLog (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    SyncedAt     TEXT NOT NULL,
    PagesLoaded  INTEGER DEFAULT 0,
    CardsAdded   INTEGER DEFAULT 0,
    CardsUpdated INTEGER DEFAULT 0,
    DurationMs   INTEGER,
    Notes        TEXT
);

-- Caché de versión Inertia
CREATE TABLE AppMeta (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
