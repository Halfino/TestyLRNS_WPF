using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace TestyLRNS_WPF.Data
{
    public static class DatabaseHelper
    {
        private static readonly string DbFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string DbPath = System.IO.Path.Combine(DbFolder, "testy_lrns.db");
        private static readonly string ConnectionString = $"Data Source={DbPath};";

        public static SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using (var command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection))
            {
                command.ExecuteNonQuery();
            }
            return connection;
        }

        public static SqliteConnection GetConnectionNoPragma()
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        public static void InitializeDatabase()
        {
            EnsureDatabaseExists();
            EnsureTablesExist();
            SeedDefaultData();

            // VYPNUTO PRO PRODUKCI: Máme cloud, nechceme generovat 300 falešných otázek na každém novém PC!
            // SeedDummyQuestions(); 
        }

        private static void EnsureDatabaseExists()
        {
            if (!File.Exists(DbPath))
            {
                using (File.Create(DbPath)) { }
            }
        }

        private static void EnsureTablesExist()
        {
            using var connection = GetConnection();
            using var command = new SqliteCommand(connection.ConnectionString);
            command.Connection = connection;

            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    global_id TEXT UNIQUE NOT NULL,
                    sync_status INTEGER DEFAULT 0,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    username TEXT UNIQUE NOT NULL,
                    password_hash TEXT NOT NULL,
                    role TEXT NOT NULL,
                    unit TEXT,
                    airport_icao TEXT,
                    linked_person_id INTEGER,
                    is_active BOOLEAN DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS SystemTopics (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    global_id TEXT UNIQUE NOT NULL,
                    sync_status INTEGER DEFAULT 0,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    name TEXT NOT NULL,
                    unit TEXT NOT NULL,
                    is_active BOOLEAN DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Persons (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    global_id TEXT UNIQUE NOT NULL,
                    sync_status INTEGER DEFAULT 0,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    rank TEXT,
                    title_before TEXT,
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    knowledge_class INTEGER NOT NULL,
                    valid_until DATETIME NOT NULL,
                    unit TEXT,
                    airport_icao TEXT,
                    is_active BOOLEAN DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Questions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    global_id TEXT UNIQUE NOT NULL,
                    sync_status INTEGER DEFAULT 0,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    text TEXT NOT NULL,
                    written BOOLEAN NOT NULL,
                    knowledge_class INTEGER NOT NULL,
                    unit TEXT,
                    system_topic TEXT,
                    airport_icao TEXT,
                    is_operational_training BOOLEAN DEFAULT 0,
                    is_active BOOLEAN DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Answers (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    global_id TEXT UNIQUE NOT NULL,
                    sync_status INTEGER DEFAULT 0,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    question_id INTEGER NOT NULL,
                    text TEXT NOT NULL,
                    is_correct BOOLEAN NOT NULL,
                    is_active BOOLEAN DEFAULT 1,
                    FOREIGN KEY (question_id) REFERENCES Questions(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS TestResults (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    global_id TEXT UNIQUE NOT NULL,
                    sync_status INTEGER DEFAULT 0,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    person_id INTEGER NOT NULL,
                    date_generated DATETIME NOT NULL,
                    date_completed DATETIME,
                    score INTEGER,
                    max_score INTEGER,
                    note TEXT,
                    pdf_path TEXT,
                    generated_by_user_id INTEGER,
                    random_seed INTEGER NOT NULL,
                    test_type TEXT,
                    FOREIGN KEY (person_id) REFERENCES Persons(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS TestQuestions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    global_id TEXT UNIQUE NOT NULL,
                    sync_status INTEGER DEFAULT 0,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    test_id INTEGER NOT NULL,
                    question_id INTEGER NOT NULL,
                    FOREIGN KEY (test_id) REFERENCES TestResults(id) ON DELETE CASCADE,
                    FOREIGN KEY (question_id) REFERENCES Questions(id) ON DELETE CASCADE
                );";

            command.ExecuteNonQuery();

            try
            {
                command.CommandText = "ALTER TABLE Questions ADD COLUMN image_path TEXT;";
                command.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Ignorujeme - sloupec už existuje
            }
        }

        private static void SeedDefaultData()
        {
            using var connection = GetConnection();

            // 1. Seed Systémů s PEVNÝMI (statickými) GUID
            using var checkSystemsCmd = new SqliteCommand("SELECT COUNT(*) FROM SystemTopics", connection);
            if (Convert.ToInt64(checkSystemsCmd.ExecuteScalar()) == 0)
            {
                var topics = new (string Gid, string Name, string Unit)[] {
                    ("11111111-0000-0000-0000-000000000001", "PAPI", "SZP"),
                    ("11111111-0000-0000-0000-000000000002", "FLASH", "SZP"),
                    ("11111111-0000-0000-0000-000000000003", "ENERGETIKA", "SZP"),
                    ("11111111-0000-0000-0000-000000000004", "ICAO CAT I", "SZP"),
                    ("11111111-0000-0000-0000-000000000005", "ILS", "RNS"),
                    ("11111111-0000-0000-0000-000000000006", "DME", "RNS"),
                    ("11111111-0000-0000-0000-000000000007", "MKR", "RNS"),
                    ("11111111-0000-0000-0000-000000000008", "NDB", "RNS"),
                    ("11111111-0000-0000-0000-000000000009", "RL-2000", "RSP"),
                    ("11111111-0000-0000-0000-000000000010", "RP", "RSP"),
                    ("11111111-0000-0000-0000-000000000011", "VCS", "LSLPS"),
                    ("11111111-0000-0000-0000-000000000012", "LETVIS", "LSLPS"),
                    ("11111111-0000-0000-0000-000000000013", "SITE", "LSLPS"),
                    ("11111111-0000-0000-0000-000000000014", "ENERGETIKA", "LSLPS")
                };

                using var transaction = connection.BeginTransaction();
                foreach (var t in topics)
                {
                    using var cmd = new SqliteCommand("INSERT INTO SystemTopics (global_id, sync_status, updated_at, name, unit, is_active) VALUES (@gId, 0, CURRENT_TIMESTAMP, @name, @unit, 1)", connection, transaction);
                    cmd.Parameters.AddWithValue("@gId", t.Gid);
                    cmd.Parameters.AddWithValue("@name", t.Name);
                    cmd.Parameters.AddWithValue("@unit", t.Unit);
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 2. Seed Uživatelů s PEVNÝMI (statickými) GUID
            using var checkUsersCmd = new SqliteCommand("SELECT COUNT(*) FROM Users", connection);
            if (Convert.ToInt64(checkUsersCmd.ExecuteScalar()) == 0)
            {
                string defaultPassword = Services.SecurityService.HashPassword("123");

                var users = new (string Gid, string Username, string Role, string? Unit, string? Icao)[] {
                    ("22222222-0000-0000-0000-000000000001", "SuperAdmin", "SuperAdmin", null, null),
                    ("22222222-0000-0000-0000-000000000002", "LKKB", "LokalniAdmin", null, "LKKB"),
                    ("22222222-0000-0000-0000-000000000003", "LKCV", "LokalniAdmin", null, "LKCV"),
                    ("22222222-0000-0000-0000-000000000004", "LKNA", "LokalniAdmin", null, "LKNA"),
                    ("22222222-0000-0000-0000-000000000005", "LKPD", "LokalniAdmin", null, "LKPD")
                };

                using var transaction = connection.BeginTransaction();
                foreach (var u in users)
                {
                    using var cmd = new SqliteCommand(@"
                        INSERT INTO Users (global_id, sync_status, updated_at, username, password_hash, role, unit, airport_icao, is_active) 
                        VALUES (@gId, 0, CURRENT_TIMESTAMP, @user, @pwd, @role, @unit, @icao, 1)", connection, transaction);

                    cmd.Parameters.AddWithValue("@gId", u.Gid);
                    cmd.Parameters.AddWithValue("@user", u.Username);
                    cmd.Parameters.AddWithValue("@pwd", defaultPassword);
                    cmd.Parameters.AddWithValue("@role", u.Role);
                    cmd.Parameters.AddWithValue("@unit", (object?)u.Unit ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@icao", (object?)u.Icao ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }
    }
}