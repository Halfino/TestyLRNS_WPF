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

            // Vybere jen aktivní systémy pro danou odbornost
            using var command = new SqliteCommand("SELECT id, name, unit, is_active FROM SystemTopics WHERE unit = @unit AND is_active = 1;", connection);
            command.Parameters.AddWithValue("@unit", unit);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                topics.Add(new SystemTopic
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Unit = reader.GetString(2),
                    IsActive = reader.GetBoolean(3)
                });
            }
            return topics;
        }

        public void Add(SystemTopic topic)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("INSERT INTO SystemTopics (name, unit, is_active) VALUES (@name, @unit, 1);", connection);
            command.Parameters.AddWithValue("@name", topic.Name);
            command.Parameters.AddWithValue("@unit", topic.Unit);
            command.ExecuteNonQuery();
        }
    }
}