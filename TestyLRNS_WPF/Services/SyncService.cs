using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TestyLRNS_WPF.Data;

namespace TestyLRNS_WPF.Services
{
    public class SyncService
    {
        // ==============================================================
        // KLÍČE ZE SUPABASE - ZDE DOPLŇ SVÉ ÚDAJE
        // ==============================================================
        private readonly string _supabaseUrl = "SupabaseApiURI";
        private readonly string _anonKey = "SupaBaseAnonKey";

        private readonly HttpClient _httpClient;
        private readonly string _lastSyncFilePath;

        public SyncService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("apikey", _anonKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_anonKey}");

            _lastSyncFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_sync.txt");
        }

        // ==============================================================
        // 1. PUSH (Odeslání dat do cloudu při vypnutí)
        // ==============================================================
        public async Task PushToServerAsync()
        {
            // Odeslání všech lokálních WEBP obrázků jako první
            await PushImagesAsync();

            // ODESÍLÁME V RELAČNÍM POŘADÍ (Kvůli cizím klíčům)

            // 1. SystemTopics
            await PushDataAsync("SystemTopics", "system_topics", "SELECT * FROM SystemTopics WHERE sync_status = 0;", reader => new {
                global_id = reader.GetString(reader.GetOrdinal("global_id")),
                name = reader.GetString(reader.GetOrdinal("name")),
                unit = reader.GetString(reader.GetOrdinal("unit")),
                is_active = reader.GetBoolean(reader.GetOrdinal("is_active")),
                updated_at = reader.GetDateTime(reader.GetOrdinal("updated_at")).ToString("o")
            });

            // 2. Persons
            await PushDataAsync("Persons", "persons", "SELECT * FROM Persons WHERE sync_status = 0;", reader => new {
                global_id = reader.GetString(reader.GetOrdinal("global_id")),
                rank = reader.IsDBNull(reader.GetOrdinal("rank")) ? null : reader.GetString(reader.GetOrdinal("rank")),
                title_before = reader.IsDBNull(reader.GetOrdinal("title_before")) ? null : reader.GetString(reader.GetOrdinal("title_before")),
                first_name = reader.GetString(reader.GetOrdinal("first_name")),
                last_name = reader.GetString(reader.GetOrdinal("last_name")),
                knowledge_class = reader.GetInt32(reader.GetOrdinal("knowledge_class")),
                valid_until = reader.GetDateTime(reader.GetOrdinal("valid_until")).ToString("o"),
                unit = reader.IsDBNull(reader.GetOrdinal("unit")) ? null : reader.GetString(reader.GetOrdinal("unit")),
                airport_icao = reader.IsDBNull(reader.GetOrdinal("airport_icao")) ? null : reader.GetString(reader.GetOrdinal("airport_icao")),
                is_active = reader.GetBoolean(reader.GetOrdinal("is_active")),
                updated_at = reader.GetDateTime(reader.GetOrdinal("updated_at")).ToString("o")
            });

            // 3. Users (Vyžaduje person_global_id)
            await PushDataAsync("Users", "users",
                "SELECT u.*, p.global_id as person_global_id FROM Users u LEFT JOIN Persons p ON u.linked_person_id = p.id WHERE u.sync_status = 0;",
                reader => new {
                    global_id = reader.GetString(reader.GetOrdinal("global_id")),
                    username = reader.GetString(reader.GetOrdinal("username")),
                    password_hash = reader.GetString(reader.GetOrdinal("password_hash")),
                    role = reader.GetString(reader.GetOrdinal("role")),
                    unit = reader.IsDBNull(reader.GetOrdinal("unit")) ? null : reader.GetString(reader.GetOrdinal("unit")),
                    airport_icao = reader.IsDBNull(reader.GetOrdinal("airport_icao")) ? null : reader.GetString(reader.GetOrdinal("airport_icao")),
                    linked_person_id = reader.IsDBNull(reader.GetOrdinal("person_global_id")) ? null : reader.GetString(reader.GetOrdinal("person_global_id")),
                    is_active = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    updated_at = reader.GetDateTime(reader.GetOrdinal("updated_at")).ToString("o")
                });

            // 4. Questions (Přidán image_path)
            await PushDataAsync("Questions", "questions", "SELECT * FROM Questions WHERE sync_status = 0;", reader => new {
                global_id = reader.GetString(reader.GetOrdinal("global_id")),
                text = reader.GetString(reader.GetOrdinal("text")),
                written = reader.GetBoolean(reader.GetOrdinal("written")),
                knowledge_class = reader.GetInt32(reader.GetOrdinal("knowledge_class")),
                unit = reader.IsDBNull(reader.GetOrdinal("unit")) ? null : reader.GetString(reader.GetOrdinal("unit")),
                system_topic = reader.IsDBNull(reader.GetOrdinal("system_topic")) ? null : reader.GetString(reader.GetOrdinal("system_topic")),
                airport_icao = reader.IsDBNull(reader.GetOrdinal("airport_icao")) ? null : reader.GetString(reader.GetOrdinal("airport_icao")),
                is_operational_training = reader.GetBoolean(reader.GetOrdinal("is_operational_training")),
                is_active = reader.GetBoolean(reader.GetOrdinal("is_active")),
                image_path = reader.IsDBNull(reader.GetOrdinal("image_path")) ? null : reader.GetString(reader.GetOrdinal("image_path")),
                updated_at = reader.GetDateTime(reader.GetOrdinal("updated_at")).ToString("o")
            });

            // 5. Answers (Vyžaduje question_global_id)
            await PushDataAsync("Answers", "answers",
                "SELECT a.*, q.global_id as question_global_id FROM Answers a JOIN Questions q ON a.question_id = q.id WHERE a.sync_status = 0;",
                reader => new {
                    global_id = reader.GetString(reader.GetOrdinal("global_id")),
                    question_id = reader.GetString(reader.GetOrdinal("question_global_id")),
                    text = reader.GetString(reader.GetOrdinal("text")),
                    is_correct = reader.GetBoolean(reader.GetOrdinal("is_correct")),
                    is_active = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    updated_at = reader.GetDateTime(reader.GetOrdinal("updated_at")).ToString("o")
                });

            // 6. TestResults (Vyžaduje person_global_id a user_global_id)
            await PushDataAsync("TestResults", "test_results",
                "SELECT t.*, p.global_id as person_global_id, u.global_id as user_global_id FROM TestResults t JOIN Persons p ON t.person_id = p.id LEFT JOIN Users u ON t.generated_by_user_id = u.id WHERE t.sync_status = 0;",
                reader => new {
                    global_id = reader.GetString(reader.GetOrdinal("global_id")),
                    person_id = reader.GetString(reader.GetOrdinal("person_global_id")),
                    date_generated = reader.GetDateTime(reader.GetOrdinal("date_generated")).ToString("o"),
                    date_completed = reader.IsDBNull(reader.GetOrdinal("date_completed")) ? null : reader.GetDateTime(reader.GetOrdinal("date_completed")).ToString("o"),
                    score = reader.IsDBNull(reader.GetOrdinal("score")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("score")),
                    max_score = reader.GetInt32(reader.GetOrdinal("max_score")),
                    note = reader.IsDBNull(reader.GetOrdinal("note")) ? null : reader.GetString(reader.GetOrdinal("note")),
                    pdf_path = reader.IsDBNull(reader.GetOrdinal("pdf_path")) ? null : reader.GetString(reader.GetOrdinal("pdf_path")),
                    generated_by_user_id = reader.IsDBNull(reader.GetOrdinal("user_global_id")) ? null : reader.GetString(reader.GetOrdinal("user_global_id")),
                    random_seed = reader.GetInt32(reader.GetOrdinal("random_seed")),
                    test_type = reader.IsDBNull(reader.GetOrdinal("test_type")) ? null : reader.GetString(reader.GetOrdinal("test_type")),
                    updated_at = reader.GetDateTime(reader.GetOrdinal("updated_at")).ToString("o")
                });

            // 7. TestQuestions (Vyžaduje test_global_id a question_global_id)
            await PushDataAsync("TestQuestions", "test_questions",
                "SELECT tq.*, t.global_id as test_global_id, q.global_id as question_global_id FROM TestQuestions tq JOIN TestResults t ON tq.test_id = t.id JOIN Questions q ON tq.question_id = q.id WHERE tq.sync_status = 0;",
                reader => new {
                    global_id = reader.GetString(reader.GetOrdinal("global_id")),
                    test_id = reader.GetString(reader.GetOrdinal("test_global_id")),
                    question_id = reader.GetString(reader.GetOrdinal("question_global_id")),
                    updated_at = reader.GetDateTime(reader.GetOrdinal("updated_at")).ToString("o")
                });
        }

        // ==============================================================
        // 2. PULL (Stažení dat z cloudu při startu)
        // ==============================================================
        public async Task PullFromServerAsync()
        {
            DateTime lastSync = GetLastSyncTime();

            // 1. SystemTopics
            await PullTableAsync("system_topics", lastSync, (jsonObj, connection, transaction) =>
            {
                using var cmd = new SqliteCommand(@"INSERT INTO SystemTopics (global_id, sync_status, updated_at, name, unit, is_active)
                    VALUES (@g, 1, @u, @name, @unit, @act)
                    ON CONFLICT(global_id) DO UPDATE SET updated_at=@u, name=@name, unit=@unit, is_active=@act, sync_status=1;", connection, transaction);
                cmd.Parameters.AddWithValue("@g", jsonObj.GetProperty("global_id").GetString());
                cmd.Parameters.AddWithValue("@u", jsonObj.GetProperty("updated_at").GetDateTime());
                cmd.Parameters.AddWithValue("@name", jsonObj.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@unit", jsonObj.GetProperty("unit").GetString());
                cmd.Parameters.AddWithValue("@act", jsonObj.GetProperty("is_active").GetBoolean());
                cmd.ExecuteNonQuery();
            });

            // 2. Persons
            await PullTableAsync("persons", lastSync, (jsonObj, connection, transaction) =>
            {
                using var cmd = new SqliteCommand(@"INSERT INTO Persons (global_id, sync_status, updated_at, rank, title_before, first_name, last_name, knowledge_class, valid_until, unit, airport_icao, is_active)
                    VALUES (@g, 1, @u, @rank, @title, @first, @last, @class, @valid, @unit, @icao, @act)
                    ON CONFLICT(global_id) DO UPDATE SET updated_at=@u, rank=@rank, title_before=@title, first_name=@first, last_name=@last, knowledge_class=@class, valid_until=@valid, unit=@unit, airport_icao=@icao, is_active=@act, sync_status=1;", connection, transaction);

                cmd.Parameters.AddWithValue("@g", jsonObj.GetProperty("global_id").GetString());
                cmd.Parameters.AddWithValue("@u", jsonObj.GetProperty("updated_at").GetDateTime());
                cmd.Parameters.AddWithValue("@rank", GetStringOrNull(jsonObj, "rank"));
                cmd.Parameters.AddWithValue("@title", GetStringOrNull(jsonObj, "title_before"));
                cmd.Parameters.AddWithValue("@first", jsonObj.GetProperty("first_name").GetString());
                cmd.Parameters.AddWithValue("@last", jsonObj.GetProperty("last_name").GetString());
                cmd.Parameters.AddWithValue("@class", jsonObj.GetProperty("knowledge_class").GetInt32());
                cmd.Parameters.AddWithValue("@valid", jsonObj.GetProperty("valid_until").GetDateTime());
                cmd.Parameters.AddWithValue("@unit", GetStringOrNull(jsonObj, "unit"));
                cmd.Parameters.AddWithValue("@icao", GetStringOrNull(jsonObj, "airport_icao"));
                cmd.Parameters.AddWithValue("@act", jsonObj.GetProperty("is_active").GetBoolean());
                cmd.ExecuteNonQuery();
            });

            // 3. Users
            await PullTableAsync("users", lastSync, (jsonObj, connection, transaction) =>
            {
                using var cmd = new SqliteCommand(@"INSERT INTO Users (global_id, sync_status, updated_at, username, password_hash, role, unit, airport_icao, linked_person_id, is_active)
                    VALUES (@g, 1, @u, @user, @pass, @role, @unit, @icao, (SELECT id FROM Persons WHERE global_id = @puuid), @act)
                    ON CONFLICT(global_id) DO UPDATE SET updated_at=@u, username=@user, password_hash=@pass, role=@role, unit=@unit, airport_icao=@icao, linked_person_id=(SELECT id FROM Persons WHERE global_id = @puuid), is_active=@act, sync_status=1;", connection, transaction);

                cmd.Parameters.AddWithValue("@g", jsonObj.GetProperty("global_id").GetString());
                cmd.Parameters.AddWithValue("@u", jsonObj.GetProperty("updated_at").GetDateTime());
                cmd.Parameters.AddWithValue("@user", jsonObj.GetProperty("username").GetString());
                cmd.Parameters.AddWithValue("@pass", jsonObj.GetProperty("password_hash").GetString());
                cmd.Parameters.AddWithValue("@role", jsonObj.GetProperty("role").GetString());
                cmd.Parameters.AddWithValue("@unit", GetStringOrNull(jsonObj, "unit"));
                cmd.Parameters.AddWithValue("@icao", GetStringOrNull(jsonObj, "airport_icao"));
                cmd.Parameters.AddWithValue("@puuid", GetStringOrNull(jsonObj, "linked_person_id"));
                cmd.Parameters.AddWithValue("@act", jsonObj.GetProperty("is_active").GetBoolean());
                cmd.ExecuteNonQuery();
            });

            // 4. Questions (Přidán image_path)
            await PullTableAsync("questions", lastSync, (jsonObj, connection, transaction) =>
            {
                using var cmd = new SqliteCommand(@"INSERT INTO Questions (global_id, sync_status, updated_at, text, written, knowledge_class, unit, system_topic, airport_icao, is_operational_training, is_active, image_path)
                    VALUES (@g, 1, @u, @text, @written, @class, @unit, @topic, @icao, @isOp, @act, @imgPath)
                    ON CONFLICT(global_id) DO UPDATE SET updated_at=@u, text=@text, written=@written, knowledge_class=@class, unit=@unit, system_topic=@topic, airport_icao=@icao, is_operational_training=@isOp, is_active=@act, image_path=@imgPath, sync_status=1;", connection, transaction);

                cmd.Parameters.AddWithValue("@g", jsonObj.GetProperty("global_id").GetString());
                cmd.Parameters.AddWithValue("@u", jsonObj.GetProperty("updated_at").GetDateTime());
                cmd.Parameters.AddWithValue("@text", jsonObj.GetProperty("text").GetString());
                cmd.Parameters.AddWithValue("@written", jsonObj.GetProperty("written").GetBoolean());
                cmd.Parameters.AddWithValue("@class", jsonObj.GetProperty("knowledge_class").GetInt32());
                cmd.Parameters.AddWithValue("@unit", GetStringOrNull(jsonObj, "unit"));
                cmd.Parameters.AddWithValue("@topic", GetStringOrNull(jsonObj, "system_topic"));
                cmd.Parameters.AddWithValue("@icao", GetStringOrNull(jsonObj, "airport_icao"));
                cmd.Parameters.AddWithValue("@isOp", jsonObj.GetProperty("is_operational_training").GetBoolean());
                cmd.Parameters.AddWithValue("@act", jsonObj.GetProperty("is_active").GetBoolean());
                cmd.Parameters.AddWithValue("@imgPath", GetStringOrNull(jsonObj, "image_path"));
                cmd.ExecuteNonQuery();
            });

            // 5. Answers
            await PullTableAsync("answers", lastSync, (jsonObj, connection, transaction) =>
            {
                using var cmd = new SqliteCommand(@"INSERT INTO Answers (global_id, sync_status, updated_at, question_id, text, is_correct, is_active)
                    VALUES (@g, 1, @u, (SELECT id FROM Questions WHERE global_id = @quuid), @text, @correct, @act)
                    ON CONFLICT(global_id) DO UPDATE SET updated_at=@u, question_id=(SELECT id FROM Questions WHERE global_id = @quuid), text=@text, is_correct=@correct, is_active=@act, sync_status=1;", connection, transaction);

                cmd.Parameters.AddWithValue("@g", jsonObj.GetProperty("global_id").GetString());
                cmd.Parameters.AddWithValue("@u", jsonObj.GetProperty("updated_at").GetDateTime());
                cmd.Parameters.AddWithValue("@quuid", jsonObj.GetProperty("question_id").GetString());
                cmd.Parameters.AddWithValue("@text", jsonObj.GetProperty("text").GetString());
                cmd.Parameters.AddWithValue("@correct", jsonObj.GetProperty("is_correct").GetBoolean());
                cmd.Parameters.AddWithValue("@act", jsonObj.GetProperty("is_active").GetBoolean());
                cmd.ExecuteNonQuery();
            });

            // 6. TestResults
            await PullTableAsync("test_results", lastSync, (jsonObj, connection, transaction) =>
            {
                using var cmd = new SqliteCommand(@"INSERT INTO TestResults (global_id, sync_status, updated_at, person_id, date_generated, date_completed, score, max_score, note, pdf_path, generated_by_user_id, random_seed, test_type)
                    VALUES (@g, 1, @u, (SELECT id FROM Persons WHERE global_id = @puuid), @dGen, @dComp, @score, @mScore, @note, @pdf, (SELECT id FROM Users WHERE global_id = @uuuid), @seed, @type)
                    ON CONFLICT(global_id) DO UPDATE SET updated_at=@u, person_id=(SELECT id FROM Persons WHERE global_id = @puuid), date_generated=@dGen, date_completed=@dComp, score=@score, max_score=@mScore, note=@note, pdf_path=@pdf, generated_by_user_id=(SELECT id FROM Users WHERE global_id = @uuuid), random_seed=@seed, test_type=@type, sync_status=1;", connection, transaction);

                cmd.Parameters.AddWithValue("@g", jsonObj.GetProperty("global_id").GetString());
                cmd.Parameters.AddWithValue("@u", jsonObj.GetProperty("updated_at").GetDateTime());
                cmd.Parameters.AddWithValue("@puuid", jsonObj.GetProperty("person_id").GetString());
                cmd.Parameters.AddWithValue("@dGen", jsonObj.GetProperty("date_generated").GetDateTime());
                cmd.Parameters.AddWithValue("@dComp", GetDateTimeOrNull(jsonObj, "date_completed"));
                cmd.Parameters.AddWithValue("@score", GetIntOrNull(jsonObj, "score"));
                cmd.Parameters.AddWithValue("@mScore", jsonObj.GetProperty("max_score").GetInt32());
                cmd.Parameters.AddWithValue("@note", GetStringOrNull(jsonObj, "note"));
                cmd.Parameters.AddWithValue("@pdf", GetStringOrNull(jsonObj, "pdf_path"));
                cmd.Parameters.AddWithValue("@uuuid", GetStringOrNull(jsonObj, "generated_by_user_id"));
                cmd.Parameters.AddWithValue("@seed", jsonObj.GetProperty("random_seed").GetInt32());
                cmd.Parameters.AddWithValue("@type", GetStringOrNull(jsonObj, "test_type"));
                cmd.ExecuteNonQuery();
            });

            // 7. TestQuestions
            await PullTableAsync("test_questions", lastSync, (jsonObj, connection, transaction) =>
            {
                using var cmd = new SqliteCommand(@"INSERT INTO TestQuestions (global_id, sync_status, updated_at, test_id, question_id)
                    VALUES (@g, 1, @u, (SELECT id FROM TestResults WHERE global_id = @tuuid), (SELECT id FROM Questions WHERE global_id = @quuid))
                    ON CONFLICT(global_id) DO UPDATE SET updated_at=@u, test_id=(SELECT id FROM TestResults WHERE global_id = @tuuid), question_id=(SELECT id FROM Questions WHERE global_id = @quuid), sync_status=1;", connection, transaction);

                cmd.Parameters.AddWithValue("@g", jsonObj.GetProperty("global_id").GetString());
                cmd.Parameters.AddWithValue("@u", jsonObj.GetProperty("updated_at").GetDateTime());
                cmd.Parameters.AddWithValue("@tuuid", jsonObj.GetProperty("test_id").GetString());
                cmd.Parameters.AddWithValue("@quuid", jsonObj.GetProperty("question_id").GetString());
                cmd.ExecuteNonQuery();
            });

            // Stažení chybějících schémat k otázkám
            await PullMissingImagesAsync();

            // Uložení aktuálního času po úspěšném stáhnutí všeho
            SaveLastSyncTime(DateTime.UtcNow);
        }

        // ==============================================================
        // POMOCNÉ METODY PRO SYNC OBRÁZKŮ (Storage API)
        // ==============================================================

        private async Task PushImagesAsync()
        {
            string imgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            if (!Directory.Exists(imgDir)) return;

            var files = Directory.GetFiles(imgDir, "*.webp");
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                byte[] fileBytes = await File.ReadAllBytesAsync(file);

                using var content = new ByteArrayContent(fileBytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");

                string endpoint = $"{_supabaseUrl.Replace("/rest/v1", "")}/storage/v1/object/question-images/{fileName}";
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Content = content;

                // Přidáno pro povolení přepisu (upsert) pokud soubor už existuje
                request.Headers.Add("x-upsert", "true");

                // Pošleme na pozadí, abychom zbytečně nezdržovali celou aplikaci pokud by jeden soubor selhal
                try
                {
                    await _httpClient.SendAsync(request);
                }
                catch { /* Bezpečná ignorace v rámci robustní offline architektury */ }
            }
        }

        private async Task PullMissingImagesAsync()
        {
            string imgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            Directory.CreateDirectory(imgDir);

            using var connection = DatabaseHelper.GetConnectionNoPragma();
            using var command = new SqliteCommand("SELECT image_path FROM Questions WHERE image_path IS NOT NULL AND image_path != '';", connection);
            using var reader = command.ExecuteReader();

            var imagesToDownload = new List<string>();
            while (reader.Read())
            {
                string img = reader.GetString(0);
                if (!File.Exists(Path.Combine(imgDir, img)))
                {
                    imagesToDownload.Add(img);
                }
            }

            foreach (var img in imagesToDownload)
            {
                // Používáme public endpoint, protože bucket pro schémata jsme založili jako Public
                string endpoint = $"{_supabaseUrl.Replace("/rest/v1", "")}/storage/v1/object/public/question-images/{img}";
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

                try
                {
                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(Path.Combine(imgDir, img), bytes);
                    }
                }
                catch { /* Bezpečná ignorace lokálního selhání stahování */ }
            }
        }

        // ==============================================================
        // POMOCNÉ METODY PRO SYNC RELAČNÍCH DAT
        // ==============================================================

        private async Task PushDataAsync(string localTableName, string remoteEndpoint, string selectQuery, Func<SqliteDataReader, object> rowMapper)
        {
            var itemsToPush = new List<object>();
            var globalIds = new List<string>();

            using var connection = DatabaseHelper.GetConnection();
            using var readCmd = new SqliteCommand(selectQuery, connection);
            using var reader = readCmd.ExecuteReader();

            while (reader.Read())
            {
                itemsToPush.Add(rowMapper(reader));
                globalIds.Add(reader.GetString(reader.GetOrdinal("global_id")));
            }

            if (itemsToPush.Count == 0) return;

            string json = JsonSerializer.Serialize(itemsToPush);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/{remoteEndpoint}");
            request.Content = content;
            request.Headers.Add("Prefer", "resolution=merge-duplicates");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                using var transaction = connection.BeginTransaction();
                using var updateCmd = new SqliteCommand($"UPDATE {localTableName} SET sync_status = 1 WHERE global_id = @gId;", connection, transaction);
                var pId = updateCmd.Parameters.Add("@gId", SqliteType.Text);

                foreach (var gId in globalIds)
                {
                    pId.Value = gId;
                    updateCmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            else
            {
                throw new Exception($"Chyba při odesílání {localTableName}: {response.ReasonPhrase}");
            }
        }

        private async Task PullTableAsync(string remoteEndpoint, DateTime lastSync, Action<JsonElement, SqliteConnection, SqliteTransaction> saveToDb)
        {
            string formattedTime = lastSync.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_supabaseUrl}/{remoteEndpoint}?updated_at=gt.{formattedTime}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            string json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.GetArrayLength() == 0) return;

            using var connection = DatabaseHelper.GetConnection();
            using var transaction = connection.BeginTransaction();

            foreach (var element in document.RootElement.EnumerateArray())
            {
                saveToDb(element, connection, transaction);
            }
            transaction.Commit();
        }

        private object GetStringOrNull(JsonElement el, string prop) => el.TryGetProperty(prop, out var val) && val.ValueKind != JsonValueKind.Null ? val.GetString()! : DBNull.Value;
        private object GetIntOrNull(JsonElement el, string prop) => el.TryGetProperty(prop, out var val) && val.ValueKind != JsonValueKind.Null ? val.GetInt32() : DBNull.Value;
        private object GetDateTimeOrNull(JsonElement el, string prop) => el.TryGetProperty(prop, out var val) && val.ValueKind != JsonValueKind.Null ? val.GetDateTime() : DBNull.Value;

        private DateTime GetLastSyncTime()
        {
            if (File.Exists(_lastSyncFilePath) && DateTime.TryParse(File.ReadAllText(_lastSyncFilePath), out DateTime time))
                return time;

            return DateTime.MinValue; // Nikdy nesynchronizováno -> Stáhne vše
        }

        private void SaveLastSyncTime(DateTime time)
        {
            File.WriteAllText(_lastSyncFilePath, time.ToString("o"));
        }
    }
}
