using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Data.Repositories
{
    public class QuestionRepository
    {
        public List<Question> GetAllActive(string? unit = null, string? airportIcao = null, string? systemTopic = null)
        {
            var questions = new List<Question>();
            using var connection = DatabaseHelper.GetConnection();

            // PŘIDÁNO: image_path na konec
            string query = "SELECT id, global_id, sync_status, updated_at, text, written, knowledge_class, unit, system_topic, airport_icao, is_operational_training, is_active, image_path FROM Questions WHERE is_active = 1";
            if (!string.IsNullOrEmpty(unit)) query += " AND unit = @unit";
            if (!string.IsNullOrEmpty(airportIcao)) query += " AND (airport_icao IS NULL OR airport_icao = @icao)";
            if (!string.IsNullOrEmpty(systemTopic)) query += " AND system_topic = @topic";

            using var command = new SqliteCommand(query, connection);
            if (!string.IsNullOrEmpty(unit)) command.Parameters.AddWithValue("@unit", unit);
            if (!string.IsNullOrEmpty(airportIcao)) command.Parameters.AddWithValue("@icao", airportIcao);
            if (!string.IsNullOrEmpty(systemTopic)) command.Parameters.AddWithValue("@topic", systemTopic);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var question = new Question
                {
                    Id = reader.GetInt32(0),
                    GlobalId = reader.GetString(1),
                    SyncStatus = reader.GetInt32(2),
                    UpdatedAt = reader.GetDateTime(3),
                    Text = reader.GetString(4),
                    IsWritten = reader.GetBoolean(5),
                    KnowledgeClass = reader.GetInt32(6),
                    Unit = reader.IsDBNull(7) ? null : reader.GetString(7),
                    SystemTopic = reader.IsDBNull(8) ? null : reader.GetString(8),
                    AirportIcao = reader.IsDBNull(9) ? null : reader.GetString(9),
                    IsOperationalTraining = reader.GetBoolean(10),
                    IsActive = reader.GetBoolean(11),
                    ImagePath = reader.IsDBNull(12) ? null : reader.GetString(12) // PŘIDÁNO
                };
                questions.Add(question);
            }

            foreach (var q in questions)
            {
                q.Answers = new ObservableCollection<Answer>(GetActiveAnswersForQuestion(q.Id, connection));
                q.AnswerCount = q.Answers.Count;
            }

            return questions;
        }

        public Question? GetById(int id)
        {
            using var connection = DatabaseHelper.GetConnection();
            // PŘIDÁNO: image_path
            string query = "SELECT id, global_id, sync_status, updated_at, text, written, knowledge_class, unit, system_topic, airport_icao, is_operational_training, is_active, image_path FROM Questions WHERE id = @id AND is_active = 1;";
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var q = new Question
                {
                    Id = reader.GetInt32(0),
                    GlobalId = reader.GetString(1),
                    SyncStatus = reader.GetInt32(2),
                    UpdatedAt = reader.GetDateTime(3),
                    Text = reader.GetString(4),
                    IsWritten = reader.GetBoolean(5),
                    KnowledgeClass = reader.GetInt32(6),
                    Unit = reader.IsDBNull(7) ? null : reader.GetString(7),
                    SystemTopic = reader.IsDBNull(8) ? null : reader.GetString(8),
                    AirportIcao = reader.IsDBNull(9) ? null : reader.GetString(9),
                    IsOperationalTraining = reader.GetBoolean(10),
                    IsActive = reader.GetBoolean(11),
                    ImagePath = reader.IsDBNull(12) ? null : reader.GetString(12) // PŘIDÁNO
                };
                q.Answers = new ObservableCollection<Answer>(GetActiveAnswersForQuestion(q.Id, connection));
                q.AnswerCount = q.Answers.Count;
                return q;
            }
            return null;
        }

        public void Add(Question question)
        {
            if (string.IsNullOrEmpty(question.GlobalId)) question.GlobalId = Guid.NewGuid().ToString();
            using var connection = DatabaseHelper.GetConnection();

            // PŘIDÁNO: image_path do INSERT a parametrů
            string qQuery = @"INSERT INTO Questions (global_id, sync_status, updated_at, text, written, knowledge_class, unit, system_topic, airport_icao, is_operational_training, is_active, image_path) 
                              VALUES (@globalId, 0, CURRENT_TIMESTAMP, @text, @written, @class, @unit, @topic, @icao, @isOp, 1, @imgPath);
                              SELECT last_insert_rowid();";

            using var qCmd = new SqliteCommand(qQuery, connection);
            qCmd.Parameters.AddWithValue("@globalId", question.GlobalId);
            qCmd.Parameters.AddWithValue("@text", question.Text);
            qCmd.Parameters.AddWithValue("@written", question.IsWritten);
            qCmd.Parameters.AddWithValue("@class", question.KnowledgeClass);
            qCmd.Parameters.AddWithValue("@unit", (object?)question.Unit ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@topic", (object?)question.SystemTopic ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@icao", (object?)question.AirportIcao ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@isOp", question.IsOperationalTraining);
            qCmd.Parameters.AddWithValue("@imgPath", (object?)question.ImagePath ?? DBNull.Value); // PŘIDÁNO

            int newQuestionId = Convert.ToInt32(qCmd.ExecuteScalar());

            if (!question.IsWritten && question.Answers != null)
            {
                foreach (var ans in question.Answers)
                {
                    if (string.IsNullOrEmpty(ans.GlobalId)) ans.GlobalId = Guid.NewGuid().ToString();
                    string aQuery = "INSERT INTO Answers (global_id, sync_status, updated_at, question_id, text, is_correct, is_active) VALUES (@globalId, 0, CURRENT_TIMESTAMP, @qid, @text, @correct, 1);";
                    using var aCmd = new SqliteCommand(aQuery, connection);
                    aCmd.Parameters.AddWithValue("@globalId", ans.GlobalId);
                    aCmd.Parameters.AddWithValue("@qid", newQuestionId);
                    aCmd.Parameters.AddWithValue("@text", ans.Text);
                    aCmd.Parameters.AddWithValue("@correct", ans.IsCorrect);
                    aCmd.ExecuteNonQuery();
                }
            }
        }

        public void Update(Question question)
        {
            using var connection = DatabaseHelper.GetConnection();

            // PŘIDÁNO: image_path do UPDATE a parametrů
            string qQuery = @"UPDATE Questions 
                              SET text = @text, written = @written, knowledge_class = @class, 
                                  unit = @unit, system_topic = @topic, airport_icao = @icao, is_operational_training = @isOp, image_path = @imgPath,
                                  sync_status = 0, updated_at = CURRENT_TIMESTAMP
                              WHERE id = @id;";

            using var qCmd = new SqliteCommand(qQuery, connection);
            qCmd.Parameters.AddWithValue("@id", question.Id);
            qCmd.Parameters.AddWithValue("@text", question.Text);
            qCmd.Parameters.AddWithValue("@written", question.IsWritten);
            qCmd.Parameters.AddWithValue("@class", question.KnowledgeClass);
            qCmd.Parameters.AddWithValue("@unit", (object?)question.Unit ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@topic", (object?)question.SystemTopic ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@icao", (object?)question.AirportIcao ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@isOp", question.IsOperationalTraining);
            qCmd.Parameters.AddWithValue("@imgPath", (object?)question.ImagePath ?? DBNull.Value); // PŘIDÁNO
            qCmd.ExecuteNonQuery();

            string deleteAnsQuery = "UPDATE Answers SET is_active = 0, sync_status = 0, updated_at = CURRENT_TIMESTAMP WHERE question_id = @qid;";
            using var delCmd = new SqliteCommand(deleteAnsQuery, connection);
            delCmd.Parameters.AddWithValue("@qid", question.Id);
            delCmd.ExecuteNonQuery();

            if (!question.IsWritten && question.Answers != null)
            {
                foreach (var ans in question.Answers)
                {
                    if (string.IsNullOrEmpty(ans.GlobalId)) ans.GlobalId = Guid.NewGuid().ToString();
                    string aQuery = "INSERT INTO Answers (global_id, sync_status, updated_at, question_id, text, is_correct, is_active) VALUES (@globalId, 0, CURRENT_TIMESTAMP, @qid, @text, @correct, 1);";
                    using var aCmd = new SqliteCommand(aQuery, connection);
                    aCmd.Parameters.AddWithValue("@globalId", ans.GlobalId);
                    aCmd.Parameters.AddWithValue("@qid", question.Id);
                    aCmd.Parameters.AddWithValue("@text", ans.Text);
                    aCmd.Parameters.AddWithValue("@correct", ans.IsCorrect);
                    aCmd.ExecuteNonQuery();
                }
            }
        }

        private List<Answer> GetActiveAnswersForQuestion(int questionId, SqliteConnection connection)
        {
            var answers = new List<Answer>();
            using var command = new SqliteCommand("SELECT id, global_id, sync_status, updated_at, question_id, text, is_correct, is_active FROM Answers WHERE question_id = @qid AND is_active = 1;", connection);
            command.Parameters.AddWithValue("@qid", questionId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                answers.Add(new Answer
                {
                    Id = reader.GetInt32(0),
                    GlobalId = reader.GetString(1),
                    SyncStatus = reader.GetInt32(2),
                    UpdatedAt = reader.GetDateTime(3),
                    QuestionId = reader.GetInt32(4),
                    Text = reader.GetString(5),
                    IsCorrect = reader.GetBoolean(6),
                    IsActive = reader.GetBoolean(7)
                });
            }
            return answers;
        }

        public void SoftDelete(int questionId)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("UPDATE Questions SET is_active = 0, sync_status = 0, updated_at = CURRENT_TIMESTAMP WHERE id = @id;", connection);
            command.Parameters.AddWithValue("@id", questionId);
            command.ExecuteNonQuery();
        }
    }
}