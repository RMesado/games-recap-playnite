using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GamesRecap.Services
{
    public class LocalDatabase
    {
        private readonly string connectionString;

        public LocalDatabase(string dbPath)
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            connectionString = $"Data Source={dbPath};Version=3;Pooling=True;";
            Initialize();
        }

        public SQLiteConnection GetConnection()
        {
            var conn = new SQLiteConnection(connectionString);
            conn.Open();
            return conn;
        }

        private void Initialize()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Platforms (
                    Id           INTEGER PRIMARY KEY,
                    Name         TEXT NOT NULL,
                    Slug         TEXT NOT NULL,
                    Active       INTEGER DEFAULT 1,
                    Filterable   INTEGER DEFAULT 1,
                    DisplayOrder INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS Genres (
                    Id           INTEGER PRIMARY KEY,
                    Name         TEXT NOT NULL,
                    Slug         TEXT NOT NULL,
                    Active       INTEGER DEFAULT 1,
                    Filterable   INTEGER DEFAULT 1,
                    DisplayOrder INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS Tags (
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

                CREATE TABLE IF NOT EXISTS Showcases (
                    Id          INTEGER PRIMARY KEY,
                    Name        TEXT NOT NULL,
                    Slug        TEXT,
                    SeriesKey   TEXT,
                    EventName   TEXT,
                    EventId     INTEGER,
                    StartAt     TEXT,
                    EndAt       TEXT,
                    StreamUrl   TEXT,
                    CachedAt    TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Companies (
                    Id   INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Slug TEXT NOT NULL UNIQUE
                );

                CREATE TABLE IF NOT EXISTS Games (
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

                CREATE TABLE IF NOT EXISTS GamePlatforms (
                    GameId      INTEGER REFERENCES Games(Id),
                    PlatformId  INTEGER REFERENCES Platforms(Id),
                    ReleaseDate TEXT,
                    PRIMARY KEY (GameId, PlatformId)
                );

                CREATE TABLE IF NOT EXISTS GameGenres (
                    GameId  INTEGER REFERENCES Games(Id),
                    GenreId INTEGER REFERENCES Genres(Id),
                    PRIMARY KEY (GameId, GenreId)
                );

                CREATE TABLE IF NOT EXISTS GameTags (
                    GameId INTEGER REFERENCES Games(Id),
                    TagId  INTEGER REFERENCES Tags(Id),
                    PRIMARY KEY (GameId, TagId)
                );

                CREATE TABLE IF NOT EXISTS GameDevelopers (
                    GameId     INTEGER REFERENCES Games(Id),
                    CompanyId  INTEGER REFERENCES Companies(Id),
                    PRIMARY KEY (GameId, CompanyId)
                );

                CREATE TABLE IF NOT EXISTS ReleaseWindows (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    GameId       INTEGER REFERENCES Games(Id),
                    Kind         TEXT NOT NULL,
                    Date         TEXT,
                    PlatformIds  TEXT,
                    DisplayOrder INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS Cards (
                    Id         INTEGER PRIMARY KEY,
                    GameId     INTEGER REFERENCES Games(Id),
                    ShowcaseId INTEGER REFERENCES Showcases(Id),
                    SortAt     TEXT,
                    IsDraft    INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS CardMedia (
                    Id            INTEGER PRIMARY KEY,
                    CardId        INTEGER REFERENCES Cards(Id),
                    Type          TEXT,
                    Title         TEXT,
                    Url           TEXT,
                    IsUnavailable INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS UserGameState (
                    GameId       INTEGER PRIMARY KEY REFERENCES Games(Id),
                    Wishlisted   INTEGER DEFAULT 0,
                    WishlistedAt TEXT,
                    Seen         INTEGER DEFAULT 0,
                    SeenAt       TEXT,
                    Hidden       INTEGER DEFAULT 0,
                    HiddenAt     TEXT,
                    PlayniteId   TEXT
                );

                CREATE TABLE IF NOT EXISTS SyncLog (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    SyncedAt     TEXT NOT NULL,
                    PagesLoaded  INTEGER DEFAULT 0,
                    CardsAdded   INTEGER DEFAULT 0,
                    CardsUpdated INTEGER DEFAULT 0,
                    DurationMs   INTEGER,
                    Notes        TEXT
                );

                CREATE TABLE IF NOT EXISTS AppMeta (
                    Key   TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );";

            cmd.ExecuteNonQuery();
        }

        public string GetInertiaVersion()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM AppMeta WHERE Key = 'inertia_version'";
            var result = cmd.ExecuteScalar();
            return result as string;
        }

        public void SetInertiaVersion(string version)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO AppMeta (Key, Value) VALUES ('inertia_version', @v)";
            cmd.Parameters.AddWithValue("@v", version);
            cmd.ExecuteNonQuery();
        }

        private void SetInertiaVersionInTransaction(SQLiteConnection conn, string version)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO AppMeta (Key, Value) VALUES ('inertia_version', @v)";
            cmd.Parameters.AddWithValue("@v", version);
            cmd.ExecuteNonQuery();
        }

        public void UpsertFromApiResponse(Models.HomeProps props, string inertiaVersion)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();

            try
            {
                UpsertTaxonomy(conn, props.Options);
                SetInertiaVersionInTransaction(conn, inertiaVersion);

                if (props.Pages?.Data != null)
                {
                    foreach (var card in props.Pages.Data)
                    {
                        UpsertGame(conn, card.Game);
                        UpsertCard(conn, card);
                    }
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private void UpsertTaxonomy(SQLiteConnection conn, Models.FilterOptions options)
        {
            if (options?.Platforms != null)
                foreach (var p in options.Platforms)
                    UpsertPlatform(conn, p.Id, p.Name, p.Slug);

            if (options?.Genres != null)
                foreach (var g in options.Genres)
                    UpsertGenre(conn, g.Id, g.Name, g.Slug);

            if (options?.Tags != null)
                foreach (var t in options.Tags)
                    UpsertTag(conn, t.Id, t.Name, t.Slug);

            if (options?.Showcases != null)
                foreach (var s in options.Showcases)
                    UpsertShowcase(conn, s);
        }

        private void UpsertPlatform(SQLiteConnection conn, int id, string name, string slug)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO Platforms (Id, Name, Slug) VALUES (@id, @name, @slug)";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@slug", slug);
            cmd.ExecuteNonQuery();
        }

        private void UpsertGenre(SQLiteConnection conn, int id, string name, string slug)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO Genres (Id, Name, Slug) VALUES (@id, @name, @slug)";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@slug", slug);
            cmd.ExecuteNonQuery();
        }

        private void UpsertTag(SQLiteConnection conn, int id, string name, string slug)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO Tags (Id, Name, Slug) VALUES (@id, @name, @slug)";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@slug", slug);
            cmd.ExecuteNonQuery();
        }

        private void UpsertShowcase(SQLiteConnection conn, Models.Showcase s)
        {
            if (s == null) return;
            var slug = s.Slug;
            if (string.IsNullOrEmpty(slug))
                slug = s.Name?.ToLower().Replace(" ", "-").Replace("'", "") ?? "showcase-" + s.Id;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Showcases (Id, Name, Slug, SeriesKey, EventName, EventId, StartAt, EndAt, StreamUrl, CachedAt)
                VALUES (@id, @name, @slug, @sk, @en, @ei, @start, @end, @url, @cached)";
            cmd.Parameters.AddWithValue("@id", s.Id);
            cmd.Parameters.AddWithValue("@name", s.Name);
            cmd.Parameters.AddWithValue("@slug", slug);
            cmd.Parameters.AddWithValue("@sk", (object)s.SeriesKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@en", (object)s.EventName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ei", s.EventId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@start", (object)s.StartAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@end", (object)s.EndAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@url", (object)s.Url ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cached", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        private void UpsertCompany(SQLiteConnection conn, int id, string name, string slug)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO Companies (Id, Name, Slug) VALUES (@id, @name, @slug)";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@slug", slug);
            cmd.ExecuteNonQuery();
        }

        private void UpsertGame(SQLiteConnection conn, Models.GrGame game)
        {
            if (game == null) return;

            if (game.Publisher != null)
                UpsertCompany(conn, game.Publisher.Id, game.Publisher.Name, game.Publisher.Slug);

            if (game.Developers != null)
                foreach (var dev in game.Developers)
                    UpsertCompany(conn, dev.Id, dev.Name, dev.Slug);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Games (Id, Title, Slug, ReleaseDate, CoverUrl, ScreenshotUrl, IgdbId, Kind, PublisherId, CachedAt)
                VALUES (@id, @title, @slug, @rd, @cover, @ss, @igdb, @kind, @pub, @cached)";
            cmd.Parameters.AddWithValue("@id", game.Id);
            cmd.Parameters.AddWithValue("@title", game.Title);
            cmd.Parameters.AddWithValue("@slug", game.Slug);
            cmd.Parameters.AddWithValue("@rd", (object)game.ReleaseDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cover", (object)game.CoverImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ss", (object)game.ScreenshotUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@igdb", game.IgdbId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@kind", game.Kind ?? "game");
            cmd.Parameters.AddWithValue("@pub", game.Publisher != null ? (object)game.Publisher.Id : DBNull.Value);
            cmd.Parameters.AddWithValue("@cached", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();

            if (game.Platforms != null)
            {
                foreach (var p in game.Platforms)
                {
                    using var gp = conn.CreateCommand();
                    gp.CommandText = @"INSERT OR REPLACE INTO GamePlatforms (GameId, PlatformId, ReleaseDate)
                        VALUES (@gid, @pid, @rd)";
                    gp.Parameters.AddWithValue("@gid", game.Id);
                    gp.Parameters.AddWithValue("@pid", p.Id);
                    gp.Parameters.AddWithValue("@rd", (object)p.Pivot?.ReleaseDate ?? DBNull.Value);
                    gp.ExecuteNonQuery();
                }
            }

            if (game.Genres != null)
            {
                foreach (var g in game.Genres)
                {
                    using var gg = conn.CreateCommand();
                    gg.CommandText = @"INSERT OR IGNORE INTO GameGenres (GameId, GenreId) VALUES (@gid, @genid)";
                    gg.Parameters.AddWithValue("@gid", game.Id);
                    gg.Parameters.AddWithValue("@genid", g.Id);
                    gg.ExecuteNonQuery();
                }
            }

            if (game.Tags != null)
            {
                foreach (var t in game.Tags)
                {
                    using var gt = conn.CreateCommand();
                    gt.CommandText = @"INSERT OR IGNORE INTO GameTags (GameId, TagId) VALUES (@gid, @tid)";
                    gt.Parameters.AddWithValue("@gid", game.Id);
                    gt.Parameters.AddWithValue("@tid", t.Id);
                    gt.ExecuteNonQuery();
                }
            }

            if (game.Developers != null)
            {
                foreach (var d in game.Developers)
                {
                    using var gd = conn.CreateCommand();
                    gd.CommandText = @"INSERT OR IGNORE INTO GameDevelopers (GameId, CompanyId) VALUES (@gid, @cid)";
                    gd.Parameters.AddWithValue("@gid", game.Id);
                    gd.Parameters.AddWithValue("@cid", d.Id);
                    gd.ExecuteNonQuery();
                }
            }

            if (game.ReleaseWindows != null)
            {
                foreach (var rw in game.ReleaseWindows)
                {
                    using var rwc = conn.CreateCommand();
                    rwc.CommandText = @"INSERT OR REPLACE INTO ReleaseWindows (Id, GameId, Kind, Date, PlatformIds, DisplayOrder)
                        VALUES (@id, @gid, @kind, @date, @pids, @ord)";
                    rwc.Parameters.AddWithValue("@id", rw.Id);
                    rwc.Parameters.AddWithValue("@gid", game.Id);
                    rwc.Parameters.AddWithValue("@kind", rw.Kind);
                    rwc.Parameters.AddWithValue("@date", (object)rw.Date ?? DBNull.Value);
                    rwc.Parameters.AddWithValue("@pids", rw.PlatformIds != null ? string.Join(",", rw.PlatformIds) : (object)DBNull.Value);
                    rwc.Parameters.AddWithValue("@ord", rw.DisplayOrder);
                    rwc.ExecuteNonQuery();
                }
            }
        }

        private void UpsertCard(SQLiteConnection conn, Models.Card card)
        {
            UpsertShowcase(conn, card.Showcase);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Cards (Id, GameId, ShowcaseId, SortAt, IsDraft)
                VALUES (@id, @gid, @sid, @sort, @draft)";
            cmd.Parameters.AddWithValue("@id", card.Id);
            cmd.Parameters.AddWithValue("@gid", card.GameId);
            cmd.Parameters.AddWithValue("@sid", card.ShowcaseId);
            cmd.Parameters.AddWithValue("@sort", (object)card.SortAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@draft", card.IsDraft ? 1 : 0);
            cmd.ExecuteNonQuery();

            if (card.Media != null)
            {
                foreach (var m in card.Media)
                {
                    using var mc = conn.CreateCommand();
                    mc.CommandText = @"INSERT OR REPLACE INTO CardMedia (Id, CardId, Type, Title, Url, IsUnavailable)
                        VALUES (@id, @cid, @type, @title, @url, @unavail)";
                    mc.Parameters.AddWithValue("@id", m.Id);
                    mc.Parameters.AddWithValue("@cid", card.Id);
                    mc.Parameters.AddWithValue("@type", (object)m.Type ?? DBNull.Value);
                    mc.Parameters.AddWithValue("@title", (object)m.Title ?? DBNull.Value);
                    mc.Parameters.AddWithValue("@url", (object)m.Url ?? DBNull.Value);
                    mc.Parameters.AddWithValue("@unavail", m.IsUnavailable ? 1 : 0);
                    mc.ExecuteNonQuery();
                }
            }
        }

        public List<int> GetWishlistedIds()
        {
            return QueryIntList("SELECT GameId FROM UserGameState WHERE Wishlisted = 1");
        }

        public List<int> GetSeenIds()
        {
            return QueryIntList("SELECT GameId FROM UserGameState WHERE Seen = 1");
        }

        public List<int> GetHiddenIds()
        {
            return QueryIntList("SELECT GameId FROM UserGameState WHERE Hidden = 1");
        }

        public List<LibraryGameEntry> GetLibraryGames()
        {
            var result = new List<LibraryGameEntry>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT g.Id, g.Title, g.Slug, g.CoverUrl, g.ScreenshotUrl,
                       g.ReleaseDate, g.Kind, ugs.PlayniteId
                FROM UserGameState ugs
                JOIN Games g ON g.Id = ugs.GameId
                WHERE ugs.PlayniteId IS NOT NULL AND ugs.PlayniteId != ''";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var entry = new LibraryGameEntry
                {
                    GameId = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    PlayniteId = reader["PlayniteId"] as string
                };
                result.Add(entry);
            }
            return result;
        }

        public void SetGameState(int gameId, bool wishlisted, bool seen, bool hidden)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO UserGameState (GameId, Wishlisted, WishlistedAt, Seen, SeenAt, Hidden, HiddenAt)
                VALUES (@gid, @w, @wa, @s, @sa, @h, @ha)";
            cmd.Parameters.AddWithValue("@gid", gameId);
            cmd.Parameters.AddWithValue("@w", wishlisted ? 1 : 0);
            cmd.Parameters.AddWithValue("@wa", wishlisted ? (object)DateTime.UtcNow.ToString("O") : DBNull.Value);
            cmd.Parameters.AddWithValue("@s", seen ? 1 : 0);
            cmd.Parameters.AddWithValue("@sa", seen ? (object)DateTime.UtcNow.ToString("O") : DBNull.Value);
            cmd.Parameters.AddWithValue("@h", hidden ? 1 : 0);
            cmd.Parameters.AddWithValue("@ha", hidden ? (object)DateTime.UtcNow.ToString("O") : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void SetPlayniteId(int gameId, string playniteId)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE UserGameState SET PlayniteId = @pid WHERE GameId = @gid";
            cmd.Parameters.AddWithValue("@pid", (object)playniteId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@gid", gameId);
            cmd.ExecuteNonQuery();
        }

        public void LogSync(int pagesLoaded, int cardsAdded, int cardsUpdated, long durationMs)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO SyncLog (SyncedAt, PagesLoaded, CardsAdded, CardsUpdated, DurationMs)
                VALUES (@at, @pl, @ca, @cu, @ms)";
            cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@pl", pagesLoaded);
            cmd.Parameters.AddWithValue("@ca", cardsAdded);
            cmd.Parameters.AddWithValue("@cu", cardsUpdated);
            cmd.Parameters.AddWithValue("@ms", durationMs);
            cmd.ExecuteNonQuery();
        }

        public int GetCachedCardCount()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Cards";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public int GetCachedGameCount()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Games";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private List<int> QueryIntList(string sql)
        {
            var result = new List<int>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(reader.GetInt32(0));
            return result;
        }
    }

    public class LibraryGameEntry
    {
        public int GameId { get; set; }
        public string Title { get; set; }
        public string PlayniteId { get; set; }
    }
}
