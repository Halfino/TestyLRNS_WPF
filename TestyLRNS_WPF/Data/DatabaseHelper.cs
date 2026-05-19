using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace TestyLRNS_WPF.Data
{
    public static class DatabaseHelper
    {
        // Zjistí přesnou složku, kde je aplikace spuštěná (např. bin/Debug/net8.0-windows...)
        private static readonly string DbFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string DbPath = System.IO.Path.Combine(DbFolder, "testy_lrns.db");

        // Výsledný připojovací řetězec, který teď bude ukazovat na správné místo
        private static readonly string ConnectionString = $"Data Source={DbPath};";

        public static SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            // Aktivace cizích klíčů (Foreign Keys)
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
            SeedDummyQuestions(); // PŘIDÁNO: Automaticky vygeneruje testovací otázky, pokud je DB prázdná
        }

        private static void EnsureDatabaseExists()
        {
            if (!File.Exists(DbPath))
            {
                // V Microsoft.Data.Sqlite stačí otevřít připojení a soubor se vytvoří sám.
                // Pro jistotu ale můžeme vytvořit prázdný soubor správným příkazem:
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
                    name TEXT NOT NULL,
                    unit TEXT NOT NULL,
                    is_active BOOLEAN DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Persons (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    rank TEXT,             -- Hodnost (např. npor., prap.)
                    title_before TEXT,     -- Titul před jménem (např. Ing.)
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    knowledge_class INTEGER NOT NULL,
                    valid_until TEXT NOT NULL,
                    unit TEXT,
                    airport_icao TEXT,
                    is_active INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Questions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
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
                    question_id INTEGER NOT NULL,
                    text TEXT NOT NULL,
                    is_correct BOOLEAN NOT NULL,
                    is_active BOOLEAN DEFAULT 1,
                    FOREIGN KEY (question_id) REFERENCES Questions(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS TestResults (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
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
                    test_id INTEGER NOT NULL,
                    question_id INTEGER NOT NULL,
                    FOREIGN KEY (test_id) REFERENCES TestResults(id) ON DELETE CASCADE,
                    FOREIGN KEY (question_id) REFERENCES Questions(id) ON DELETE CASCADE
                );";

            command.ExecuteNonQuery();
        }

        private static void SeedDefaultData()
        {
            using var connection = GetConnection();

            // 1. Seed Systémů
            using var checkSystemsCmd = new SqliteCommand("SELECT COUNT(*) FROM SystemTopics", connection);
            if ((long)checkSystemsCmd.ExecuteScalar() == 0)
            {
                string insertSystems = @"
                    INSERT INTO SystemTopics (name, unit) VALUES 
                    ('PAPI', 'SZP'), ('FLASH', 'SZP'), ('ENERGETIKA', 'SZP'), ('VYSKY', 'SZP'),
                    ('ILS', 'RNS'), ('DME', 'RNS'), ('MKR', 'RNS'), ('NDB', 'RNS'),
                    ('RL', 'RSP'), ('RP', 'RSP'),
                    ('VCS', 'LSLPS'), ('LETVIS', 'LSLPS'), ('SITE', 'LSLPS'), ('ENERGETIKA', 'LSLPS');";
                using var insertSysCmd = new SqliteCommand(insertSystems, connection);
                insertSysCmd.ExecuteNonQuery();
            }

            // 2. Seed Uživatelů podle nové hierarchie rolí
            using var checkUsersCmd = new SqliteCommand("SELECT COUNT(*) FROM Users", connection);
            if ((long)checkUsersCmd.ExecuteScalar() == 0)
            {
                // Všechna výchozí konta budou mít pro začátek heslo "123"
                string defaultPassword = Services.SecurityService.HashPassword("123");

                string insertUsers = @"
                    INSERT INTO Users (username, password_hash, role, unit, airport_icao, is_active) VALUES 
                    ('SuperAdmin', @pwd, 'SuperAdmin', NULL, NULL, 1),
                    ('LKKB', @pwd, 'LokalniAdmin', NULL, 'LKKB', 1),
                    ('LKCV', @pwd, 'LokalniAdmin', NULL, 'LKCV', 1),
                    ('LKNA', @pwd, 'LokalniAdmin', NULL, 'LKNA', 1),
                    ('LKPD', @pwd, 'LokalniAdmin', NULL, 'LKPD', 1),
                    ('LKKB_novak', @pwd, 'Instruktor', 'SZP', 'LKKB', 1),
                    ('LKCV_novak', @pwd, 'Instruktor', 'SZP', 'LKCV', 1),
                    ('LKKB_radar', @pwd, 'Instruktor', 'RSP', 'LKKB', 1);";

                using var insertUserCmd = new SqliteCommand(insertUsers, connection);
                insertUserCmd.Parameters.AddWithValue("@pwd", defaultPassword);
                insertUserCmd.ExecuteNonQuery();
            }
        }

        // --- NOVÁ METODA PRO GENEROVÁNÍ FIKTIVNÍCH OTÁZEK ---
        private static void SeedDummyQuestions()
        {
            using var connection = GetConnection();

            // Ověření, jestli už tam otázky nejsou. Pokud ano, přeskočíme, ať se neduplikují.
            using var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM Questions", connection);
            if ((long)checkCmd.ExecuteScalar() > 0) return;

            var random = new Random(100);
            string[] units = { "SZP", "RNS", "RSP", "OSZ", "LSLPS" };

            // Systémy podle tvého Seedu výše
            string[] topicsSZP = { "PAPI", "FLASH", "ENERGETIKA", "VYSKY" };
            string[] topicsRNS = { "ILS", "DME", "MKR", "NDB" };
            string[] topicsRSP = { "RL", "RP" };
            string[] topicsLSLPS = { "VCS", "LETVIS", "SITE", "ENERGETIKA" };

            // Vyšší šance na globální otázku (vícekrát null)
            string?[] airports = { null, null, null, null, "LKKB", "LKCV", "LKNA", "LKPD" };

            using var transaction = connection.BeginTransaction();
            try
            {
                // Vygenerujeme 300 testovacích otázek
                for (int i = 1; i <= 300; i++)
                {
                    string unit = units[random.Next(units.Length)];
                    string? topic = null;

                    // Někdy necháme téma prázdné (Obecná otázka)
                    if (random.Next(100) < 80 && unit != "OSZ")
                    {
                        switch (unit)
                        {
                            case "SZP": topic = topicsSZP[random.Next(topicsSZP.Length)]; break;
                            case "RNS": topic = topicsRNS[random.Next(topicsRNS.Length)]; break;
                            case "RSP": topic = topicsRSP[random.Next(topicsRSP.Length)]; break;
                            case "LSLPS": topic = topicsLSLPS[random.Next(topicsLSLPS.Length)]; break;
                        }
                    }

                    string? airport = airports[random.Next(airports.Length)];
                    int knowledgeClass = random.Next(1, 4); // Třídy 1, 2, nebo 3
                    bool isWritten = random.Next(100) < 15; // 15 % šance na otevřenou (psanou) otázku
                    bool isOp = random.Next(100) < 10;      // 10 % šance na provozní výcvik

                    string typeText = isWritten ? "Otevřená" : "Uzavřená";
                    string qText = $"[TEST ID:{i}] Toto je fiktivní {typeText.ToLower()} otázka pro odbornost {unit}. Jaký postup zvolíte pro zařízení {topic ?? "Všeobecné povahy"}, pokud se jedná o znalosti {knowledgeClass}. třídy?";

                    using var qCmd = new SqliteCommand(@"
                        INSERT INTO Questions (text, written, knowledge_class, unit, system_topic, airport_icao, is_operational_training, is_active) 
                        VALUES (@text, @written, @class, @unit, @topic, @icao, @isOp, 1);
                        SELECT last_insert_rowid();", connection, transaction);

                    qCmd.Parameters.AddWithValue("@text", qText);
                    qCmd.Parameters.AddWithValue("@written", isWritten);
                    qCmd.Parameters.AddWithValue("@class", knowledgeClass);
                    qCmd.Parameters.AddWithValue("@unit", (object?)unit ?? DBNull.Value);
                    qCmd.Parameters.AddWithValue("@topic", (object?)topic ?? DBNull.Value);
                    qCmd.Parameters.AddWithValue("@icao", (object?)airport ?? DBNull.Value);
                    qCmd.Parameters.AddWithValue("@isOp", isOp);

                    long qId = (long)qCmd.ExecuteScalar();

                    // Pokud to je uzavřená otázka, vygenerujeme 3 odpovědi
                    if (!isWritten)
                    {
                        for (int a = 1; a <= 3; a++)
                        {
                            using var aCmd = new SqliteCommand("INSERT INTO Answers (question_id, text, is_correct, is_active) VALUES (@qid, @text, @correct, 1);", connection, transaction);
                            aCmd.Parameters.AddWithValue("@qid", qId);

                            if (a == 1)
                            {
                                // Do databáze dáváme vždy první jako správnou 
                                // (Tvůj generátor PDF si je pak stejně automaticky zamíchá díky Seedu)
                                aCmd.Parameters.AddWithValue("@text", $"[SPRÁVNĚ] Toto je fiktivní správná odpověď na testovou otázku č. {i}.");
                                aCmd.Parameters.AddWithValue("@correct", true);
                            }
                            else
                            {
                                aCmd.Parameters.AddWithValue("@text", $"[ŠPATNĚ] Zcela nesprávná varianta {a} pro otázku č. {i}.");
                                aCmd.Parameters.AddWithValue("@correct", false);
                            }
                            aCmd.ExecuteNonQuery();
                        }
                    }
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}