using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

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
                CREATE TABLE IF NOT EXISTS UserGameState (
                    GameId       INTEGER PRIMARY KEY,
                    Wishlisted   INTEGER DEFAULT 0,
                    WishlistedAt TEXT,
                    Seen         INTEGER DEFAULT 0,
                    SeenAt       TEXT,
                    Hidden       INTEGER DEFAULT 0,
                    HiddenAt     TEXT,
                    PlayniteId   TEXT
                );

                CREATE TABLE IF NOT EXISTS AppMeta (
                    Key   TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PromotedGames (
                    GameId        INTEGER PRIMARY KEY,
                    Title         TEXT NOT NULL,
                    CoverUrl      TEXT,
                    PlatformsJson TEXT,
                    GenresJson    TEXT,
                    TagsJson      TEXT,
                    ReleaseDate   TEXT,
                    PlayniteId    TEXT UNIQUE
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

        public void SetGameState(int gameId, bool wishlisted, bool seen, bool hidden)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Wishlisted, Seen, Hidden FROM UserGameState WHERE GameId = @gid";
            cmd.Parameters.AddWithValue("@gid", gameId);
            var now = DateTime.UtcNow.ToString("O");

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    var oldW = reader.GetInt32(0) == 1;
                    var oldS = reader.GetInt32(1) == 1;
                    var oldH = reader.GetInt32(2) == 1;
                    if (oldW == wishlisted && oldS == seen && oldH == hidden)
                        return;
                }
            }

            cmd.Parameters.Clear();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO UserGameState (GameId, Wishlisted, WishlistedAt, Seen, SeenAt, Hidden, HiddenAt)
                VALUES (@gid, @w, @wa, @s, @sa, @h, @ha)";
            cmd.Parameters.AddWithValue("@gid", gameId);
            cmd.Parameters.AddWithValue("@w", wishlisted ? 1 : 0);
            cmd.Parameters.AddWithValue("@wa", wishlisted ? (object)now : DBNull.Value);
            cmd.Parameters.AddWithValue("@s", seen ? 1 : 0);
            cmd.Parameters.AddWithValue("@sa", seen ? (object)now : DBNull.Value);
            cmd.Parameters.AddWithValue("@h", hidden ? 1 : 0);
            cmd.Parameters.AddWithValue("@ha", hidden ? (object)now : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void SetPlayniteId(int gameId, string playniteId)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE UserGameState SET PlayniteId = @pid WHERE GameId = @gid";
            cmd.Parameters.AddWithValue("@pid", string.IsNullOrEmpty(playniteId) ? DBNull.Value : (object)playniteId);
            cmd.Parameters.AddWithValue("@gid", gameId);
            cmd.ExecuteNonQuery();
        }

        public string GetPlayniteId(int gameId)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT PlayniteId FROM UserGameState WHERE GameId = @gid";
            cmd.Parameters.AddWithValue("@gid", gameId);
            var result = cmd.ExecuteScalar();
            return result as string;
        }

        public List<int> GetAllPlayniteIds()
        {
            var result = new List<int>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT GameId FROM UserGameState WHERE PlayniteId IS NOT NULL";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(reader.GetInt32(0));
            return result;
        }

        public void UpsertPromotedGame(int gameId, string title, string coverUrl,
            string platformsJson, string genresJson, string tagsJson,
            string releaseDate, string playniteId)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO PromotedGames
                    (GameId, Title, CoverUrl, PlatformsJson, GenresJson, TagsJson, ReleaseDate, PlayniteId)
                VALUES (@gid, @title, @cover, @pjson, @gjson, @tjson, @rdate, @pid)";
            cmd.Parameters.AddWithValue("@gid", gameId);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@cover", coverUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pjson", platformsJson ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@gjson", genresJson ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tjson", tagsJson ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@rdate", releaseDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pid", playniteId);
            cmd.ExecuteNonQuery();
        }

        public List<PromotedGameEntry> GetAllPromotedGames()
        {
            var result = new List<PromotedGameEntry>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT GameId, Title, CoverUrl, PlatformsJson, GenresJson, TagsJson, ReleaseDate, PlayniteId FROM PromotedGames";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new PromotedGameEntry
                {
                    GameId = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    CoverUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
                    PlatformsJson = reader.IsDBNull(3) ? null : reader.GetString(3),
                    GenresJson = reader.IsDBNull(4) ? null : reader.GetString(4),
                    TagsJson = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ReleaseDate = reader.IsDBNull(6) ? null : reader.GetString(6),
                    PlayniteId = reader.GetString(7)
                });
            }
            return result;
        }

        public int? GetGameIdByPlayniteId(Guid playniteId)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT GameId FROM PromotedGames WHERE PlayniteId = @pid";
            cmd.Parameters.AddWithValue("@pid", playniteId.ToString());
            var result = cmd.ExecuteScalar();
            return result != null ? (int?)Convert.ToInt32(result) : null;
        }

        public void RemovePromotedGame(int gameId)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM PromotedGames WHERE GameId = @gid";
            cmd.Parameters.AddWithValue("@gid", gameId);
            cmd.ExecuteNonQuery();
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

    public class PromotedGameEntry
    {
        public int GameId { get; set; }
        public string Title { get; set; }
        public string CoverUrl { get; set; }
        public string PlatformsJson { get; set; }
        public string GenresJson { get; set; }
        public string TagsJson { get; set; }
        public string ReleaseDate { get; set; }
        public string PlayniteId { get; set; }
    }
}
