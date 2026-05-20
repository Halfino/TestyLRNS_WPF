using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Data.Repositories
{
    public class SystemTopicRepository
    {
        public List<SystemTopic> GetAllActiveByUnit(string unit)
        {
            var topics = new List<SystemTopic>();
            using var connection = DatabaseHelper.GetConnection();

            // PŘIDÁNO: global_id, sync_status, updated_at a posunuty indexy
            string query = "SELECT id, global_id, sync_status, updated_at, name, unit, is_active FROM SystemTopics WHERE unit = @unit AND is_active = 1;";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@unit", unit);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                topics.Add(new SystemTopic
                {
                    Id = reader.GetInt32(0),
                    GlobalId = reader.GetString(1),          // Načtení GlobalId
                    SyncStatus = reader.GetInt32(2),         // Načtení SyncStatus
                    UpdatedAt = reader.GetDateTime(3),       // Načtení UpdatedAt
                    Name = reader.GetString(4),
                    Unit = reader.GetString(5),
                    IsActive = reader.GetBoolean(6)
                });
            }
            return topics;
        }

        public void Add(SystemTopic topic)
        {
            // Zajištění vygenerování GUID pro cloud synchronizaci
            if (string.IsNullOrEmpty(topic.GlobalId)) topic.GlobalId = Guid.NewGuid().ToString();

            using var connection = DatabaseHelper.GetConnection();

            // PŘIDÁNO: Zápis global_id, sync_status a updated_at
            string query = "INSERT INTO SystemTopics (global_id, sync_status, updated_at, name, unit, is_active) VALUES (@globalId, 0, CURRENT_TIMESTAMP, @name, @unit, 1);";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@globalId", topic.GlobalId);
            command.Parameters.AddWithValue("@name", topic.Name);
            command.Parameters.AddWithValue("@unit", topic.Unit);

            command.ExecuteNonQuery();
        }

        public void SoftDelete(int id)
        {
            using var connection = DatabaseHelper.GetConnection();

            // Při smazání označíme řádek k odeslání do cloudu (sync_status = 0)
            using var command = new SqliteCommand("UPDATE SystemTopics SET is_active = 0, sync_status = 0, updated_at = CURRENT_TIMESTAMP WHERE id = @id;", connection);
            command.Parameters.AddWithValue("@id", id);

            command.ExecuteNonQuery();
        }
    }
}