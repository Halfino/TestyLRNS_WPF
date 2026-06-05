using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Data.Repositories
{
    // Pomocná třída pro přenos dat do UI Dashboardu
    public class PendingTestDto
    {
        public int TestId { get; set; }
        public string PersonName { get; set; } = string.Empty;
        public string TestType { get; set; } = string.Empty;
    }

    // Přepravní třída pro zobrazení historie v tabulce
    public class TestHistoryDto
    {
        public int TestId { get; set; }
        public string PersonName { get; set; } = string.Empty;
        public string TestType { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public DateTime DateGenerated { get; set; }
        public string DateGeneratedString => DateGenerated.ToString("dd.MM.yyyy");

        public DateTime? DateCompleted { get; set; }
        public int? Score { get; set; }
        public int MaxScore { get; set; }
        public string PdfPath { get; set; } = string.Empty;

        // PŘIDÁNO: Podpora pro poznámku
        public string? Note { get; set; }
        public bool HasNote => !string.IsNullOrWhiteSpace(Note);

        public string DisplayScore => Score.HasValue ? $"{Score.Value} / {MaxScore}" : $"-- / {MaxScore}";
        public string DisplayPercentage => Score.HasValue ? $"{Math.Round(((double)Score.Value / MaxScore) * 100)} %" : "-- %";
        public string Status => Score.HasValue ? (((double)Score.Value / MaxScore) >= 0.80 ? "Prospěl" : "Neprospěl") : "Čeká";
        public string StatusColor => Status == "Prospěl" ? "#00CC66" : (Status == "Neprospěl" ? "#FF4444" : "#FFCC00");
    }

    public class TestResultRepository
    {
        public void SaveTestResult(TestResult testResult)
        {
            if (string.IsNullOrEmpty(testResult.GlobalId)) testResult.GlobalId = Guid.NewGuid().ToString();

            using var connection = DatabaseHelper.GetConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                using var cmdTest = new SqliteCommand(@"
                    INSERT INTO TestResults (global_id, sync_status, updated_at, person_id, date_generated, date_completed, score, max_score, note, generated_by_user_id, random_seed, test_type, pdf_path) 
                    VALUES (@globalId, 0, CURRENT_TIMESTAMP, @personId, @dateGen, @dateComp, @score, @maxScore, @note, @genUserId, @seed, @testType, @pdfPath);
                    SELECT last_insert_rowid();", connection, transaction);

                cmdTest.Parameters.AddWithValue("@globalId", testResult.GlobalId);
                cmdTest.Parameters.AddWithValue("@personId", testResult.PersonId);
                cmdTest.Parameters.AddWithValue("@dateGen", testResult.DateGenerated);
                cmdTest.Parameters.AddWithValue("@dateComp", (object?)testResult.DateCompleted ?? DBNull.Value);
                cmdTest.Parameters.AddWithValue("@score", (object?)testResult.Score ?? DBNull.Value);
                cmdTest.Parameters.AddWithValue("@maxScore", testResult.MaxScore);
                cmdTest.Parameters.AddWithValue("@note", (object?)testResult.Note ?? DBNull.Value);
                cmdTest.Parameters.AddWithValue("@genUserId", (object?)testResult.GeneratedByUserId ?? DBNull.Value);
                cmdTest.Parameters.AddWithValue("@seed", testResult.RandomSeed);
                cmdTest.Parameters.AddWithValue("@testType", (object?)testResult.TestType ?? DBNull.Value);
                cmdTest.Parameters.AddWithValue("@pdfPath", (object?)testResult.PdfPath ?? DBNull.Value);

                long lastId = (long)cmdTest.ExecuteScalar();
                testResult.Id = (int)lastId;

                foreach (int questionId in testResult.QuestionIds)
                {
                    string tqGlobalId = Guid.NewGuid().ToString();
                    using var cmdTQ = new SqliteCommand("INSERT INTO TestQuestions (global_id, sync_status, updated_at, test_id, question_id) VALUES (@gId, 0, CURRENT_TIMESTAMP, @testId, @questionId);", connection, transaction);
                    cmdTQ.Parameters.AddWithValue("@gId", tqGlobalId);
                    cmdTQ.Parameters.AddWithValue("@testId", testResult.Id);
                    cmdTQ.Parameters.AddWithValue("@questionId", questionId);
                    cmdTQ.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public List<PendingTestDto> GetPendingTests(User currentUser)
        {
            var list = new List<PendingTestDto>();
            using var connection = DatabaseHelper.GetConnection();

            string query = @"
                SELECT t.id, p.rank, p.title_before, p.last_name, p.first_name, t.test_type
                FROM TestResults t
                JOIN Persons p ON t.person_id = p.id
                WHERE t.score IS NULL AND p.is_active = 1";

            if (currentUser.Role == "Instruktor")
            {
                query += " AND p.unit = @unit AND p.airport_icao = @icao";
            }
            else if (currentUser.Role == "LokalniAdmin")
            {
                query += " AND p.airport_icao = @icao";
            }

            using var command = new SqliteCommand(query, connection);
            if (currentUser.Role != "SuperAdmin")
            {
                if (currentUser.Role == "Instruktor") command.Parameters.AddWithValue("@unit", currentUser.Unit);
                command.Parameters.AddWithValue("@icao", currentUser.AirportIcao);
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string rank = reader.IsDBNull(1) ? "" : reader.GetString(1) + " ";
                string title = reader.IsDBNull(2) ? "" : reader.GetString(2) + " ";

                list.Add(new PendingTestDto
                {
                    TestId = reader.GetInt32(0),
                    PersonName = $"{rank}{title}{reader.GetString(3)} {reader.GetString(4)}".Trim(),
                    TestType = reader.IsDBNull(5) ? "Neznámý test" : reader.GetString(5)
                });
            }
            return list;
        }

        public TestResult? GetById(int id)
        {
            using var connection = DatabaseHelper.GetConnection();
            string query = "SELECT id, global_id, sync_status, updated_at, person_id, date_generated, date_completed, score, max_score, note, pdf_path, generated_by_user_id, random_seed, test_type FROM TestResults WHERE id = @id;";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var tr = new TestResult
                {
                    Id = reader.GetInt32(0),
                    GlobalId = reader.GetString(1),
                    SyncStatus = reader.GetInt32(2),
                    UpdatedAt = reader.GetDateTime(3),
                    PersonId = reader.GetInt32(4),
                    DateGenerated = reader.GetDateTime(5),
                    DateCompleted = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    Score = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    MaxScore = reader.GetInt32(8),
                    Note = reader.IsDBNull(9) ? null : reader.GetString(9),
                    PdfPath = reader.IsDBNull(10) ? null : reader.GetString(10),
                    GeneratedByUserId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    RandomSeed = reader.GetInt32(12),
                    TestType = reader.IsDBNull(13) ? null : reader.GetString(13)
                };

                using var qCmd = new SqliteCommand("SELECT question_id FROM TestQuestions WHERE test_id = @tid", connection);
                qCmd.Parameters.AddWithValue("@tid", tr.Id);

                using var qReader = qCmd.ExecuteReader();
                while (qReader.Read())
                {
                    tr.QuestionIds.Add(qReader.GetInt32(0));
                }
                return tr;
            }
            return null;
        }

        public void UpdateTestResultScore(int testId, int score, string? note)
        {
            using var connection = DatabaseHelper.GetConnection();
            string query = "UPDATE TestResults SET score = @score, date_completed = @dateComp, note = @note, sync_status = 0, updated_at = CURRENT_TIMESTAMP WHERE id = @id;";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@id", testId);
            command.Parameters.AddWithValue("@score", score);
            command.Parameters.AddWithValue("@dateComp", DateTime.Now);
            command.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        public void UpdatePdfPath(int testId, string pdfPath)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("UPDATE TestResults SET pdf_path = @path, sync_status = 0, updated_at = CURRENT_TIMESTAMP WHERE id = @id;", connection);

            command.Parameters.AddWithValue("@id", testId);
            command.Parameters.AddWithValue("@path", pdfPath);
            command.ExecuteNonQuery();
        }

        public List<TestHistoryDto> GetTestHistory(User currentUser, int? year, int? month)
        {
            var list = new List<TestHistoryDto>();
            using var connection = DatabaseHelper.GetConnection();

            // PŘIDÁNO: t.note na konec SQL dotazu
            string query = @"
                SELECT t.id, p.rank, p.title_before, p.last_name, p.first_name, t.test_type, t.date_generated, t.date_completed, t.score, t.max_score, t.pdf_path, p.unit, t.note
                FROM TestResults t
                JOIN Persons p ON t.person_id = p.id
                WHERE p.is_active = 1";

            if (currentUser.Role == "Instruktor")
            {
                query += " AND p.unit = @unit AND p.airport_icao = @icao";
            }
            else if (currentUser.Role == "LokalniAdmin")
            {
                query += " AND p.airport_icao = @icao";
            }

            if (year.HasValue) query += " AND strftime('%Y', t.date_generated) = @year";
            if (month.HasValue) query += " AND CAST(strftime('%m', t.date_generated) AS INTEGER) = @month";

            query += " ORDER BY t.date_generated DESC";

            using var command = new SqliteCommand(query, connection);
            if (currentUser.Role != "SuperAdmin")
            {
                if (currentUser.Role == "Instruktor") command.Parameters.AddWithValue("@unit", currentUser.Unit);
                command.Parameters.AddWithValue("@icao", currentUser.AirportIcao);
            }
            if (year.HasValue) command.Parameters.AddWithValue("@year", year.Value.ToString());
            if (month.HasValue) command.Parameters.AddWithValue("@month", month.Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string rank = reader.IsDBNull(1) ? "" : reader.GetString(1) + " ";
                string title = reader.IsDBNull(2) ? "" : reader.GetString(2) + " ";

                list.Add(new TestHistoryDto
                {
                    TestId = reader.GetInt32(0),
                    PersonName = $"{rank}{title}{reader.GetString(3)} {reader.GetString(4)}".Trim(),
                    TestType = reader.IsDBNull(5) ? "Neznámý test" : reader.GetString(5),
                    DateGenerated = reader.GetDateTime(6),
                    DateCompleted = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    Score = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    MaxScore = reader.GetInt32(9),
                    PdfPath = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    Unit = reader.IsDBNull(11) ? "Všeobecná" : reader.GetString(11),
                    // PŘIDÁNO: Načtení poznámky z 12. indexu
                    Note = reader.IsDBNull(12) ? null : reader.GetString(12)
                });
            }
            return list;
        }
    }
}