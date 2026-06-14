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

                DROP TABLE IF EXISTS Platforms;
                DROP TABLE IF EXISTS Genres;
                DROP TABLE IF EXISTS Tags;
                DROP TABLE IF EXISTS Showcases;
                DROP TABLE IF EXISTS Companies;
                DROP TABLE IF EXISTS Games;
                DROP TABLE IF EXISTS GamePlatforms;
                DROP TABLE IF EXISTS GameGenres;
                DROP TABLE IF EXISTS GameTags;
                DROP TABLE IF EXISTS GameDevelopers;
                DROP TABLE IF EXISTS ReleaseWindows;
                DROP TABLE IF EXISTS Cards;
                DROP TABLE IF EXISTS CardMedia;
                DROP TABLE IF EXISTS SyncLog;";

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
}
