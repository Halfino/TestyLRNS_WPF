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

            string query = "SELECT id, text, written, knowledge_class, unit, system_topic, airport_icao, is_operational_training, is_active FROM Questions WHERE is_active = 1";

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
                    Text = reader.GetString(1),
                    IsWritten = reader.GetBoolean(2),
                    KnowledgeClass = reader.GetInt32(3),
                    Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
                    SystemTopic = reader.IsDBNull(5) ? null : reader.GetString(5),
                    AirportIcao = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsOperationalTraining = reader.GetBoolean(7),
                    IsActive = reader.GetBoolean(8)
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

        // NOVÁ METODA: Načtení jedné konkrétní otázky včetně odpovědí pro editační formulář
        public Question? GetById(int id)
        {
            using var connection = DatabaseHelper.GetConnection();
            string query = "SELECT id, text, written, knowledge_class, unit, system_topic, airport_icao, is_operational_training, is_active FROM Questions WHERE id = @id AND is_active = 1;";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var q = new Question
                {
                    Id = reader.GetInt32(0),
                    Text = reader.GetString(1),
                    IsWritten = reader.GetBoolean(2),
                    KnowledgeClass = reader.GetInt32(3),
                    Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
                    SystemTopic = reader.IsDBNull(5) ? null : reader.GetString(5),
                    AirportIcao = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsOperationalTraining = reader.GetBoolean(7),
                    IsActive = reader.GetBoolean(8)
                };

                // Využijeme tvou stávající metodu pro načtení odpovědí
                q.Answers = new ObservableCollection<Answer>(GetActiveAnswersForQuestion(q.Id, connection));
                q.AnswerCount = q.Answers.Count;
                return q;
            }
            return null;
        }

        // NOVÁ METODA: Kompletní uložení nové otázky (a případných testových odpovědí)
        public void Add(Question question)
        {
            using var connection = DatabaseHelper.GetConnection();

            // 1. Zápis samotné otázky a získání jejího nového ID
            string qQuery = @"INSERT INTO Questions (text, written, knowledge_class, unit, system_topic, airport_icao, is_operational_training, is_active) 
                              VALUES (@text, @written, @class, @unit, @topic, @icao, @isOp, 1);
                              SELECT last_insert_rowid();";

            using var qCmd = new SqliteCommand(qQuery, connection);
            qCmd.Parameters.AddWithValue("@text", question.Text);
            qCmd.Parameters.AddWithValue("@written", question.IsWritten);
            qCmd.Parameters.AddWithValue("@class", question.KnowledgeClass);
            qCmd.Parameters.AddWithValue("@unit", (object?)question.Unit ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@topic", (object?)question.SystemTopic ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@icao", (object?)question.AirportIcao ?? DBNull.Value);
            qCmd.Parameters.AddWithValue("@isOp", question.IsOperationalTraining);

            int newQuestionId = Convert.ToInt32(qCmd.ExecuteScalar());

            // 2. Pokud to není otevřená otázka, zapíšeme do tabulky Answers všechny 3 možnosti
            if (!question.IsWritten && question.Answers != null)
            {
                foreach (var ans in question.Answers)
                {
                    string aQuery = "INSERT INTO Answers (question_id, text, is_correct, is_active) VALUES (@qid, @text, @correct, 1);";
                    using var aCmd = new SqliteCommand(aQuery, connection);
                    aCmd.Parameters.AddWithValue("@qid", newQuestionId);
                    aCmd.Parameters.AddWithValue("@text", ans.Text);
                    aCmd.Parameters.AddWithValue("@correct", ans.IsCorrect);
                    aCmd.ExecuteNonQuery();
                }
            }
        }

        // NOVÁ METODA: Aktualizace upravené otázky
        public void Update(Question question)
        {
            using var connection = DatabaseHelper.GetConnection();

            // 1. Update parametrů otázky
            string qQuery = @"UPDATE Questions 
                              SET text = @text, written = @written, knowledge_class = @class, 
                                  unit = @unit, system_topic = @topic, airport_icao = @icao, is_operational_training = @isOp 
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
            qCmd.ExecuteNonQuery();

            // 2. Smazání starých odpovědí a vložení nových (nejbezpečnější způsob aktualizace vazby 1:N v SQLite)
            string deleteAnsQuery = "DELETE FROM Answers WHERE question_id = @qid;";
            using var delCmd = new SqliteCommand(deleteAnsQuery, connection);
            delCmd.Parameters.AddWithValue("@qid", question.Id);
            delCmd.ExecuteNonQuery();

            if (!question.IsWritten && question.Answers != null)
            {
                foreach (var ans in question.Answers)
                {
                    string aQuery = "INSERT INTO Answers (question_id, text, is_correct, is_active) VALUES (@qid, @text, @correct, 1);";
                    using var aCmd = new SqliteCommand(aQuery, connection);
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
            using var command = new SqliteCommand("SELECT id, question_id, text, is_correct, is_active FROM Answers WHERE question_id = @qid AND is_active = 1;", connection);
            command.Parameters.AddWithValue("@qid", questionId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                answers.Add(new Answer
                {
                    Id = reader.GetInt32(0),
                    QuestionId = reader.GetInt32(1),
                    Text = reader.GetString(2),
                    IsCorrect = reader.GetBoolean(3),
                    IsActive = reader.GetBoolean(4)
                });
            }
            return answers;
        }

        public void SoftDelete(int questionId)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("UPDATE Questions SET is_active = 0 WHERE id = @id;", connection);
            command.Parameters.AddWithValue("@id", questionId);
            command.ExecuteNonQuery();
        }
    }
}