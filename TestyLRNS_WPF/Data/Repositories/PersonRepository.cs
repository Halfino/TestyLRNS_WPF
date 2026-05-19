using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Data.Repositories
{
    public class PersonRepository
    {
        public List<Person> GetAllActive(string? unit = null, string? airportIcao = null)
        {
            var persons = new List<Person>();
            using var connection = DatabaseHelper.GetConnection();

            string query = "SELECT id, rank, title_before, first_name, last_name, knowledge_class, valid_until, unit, airport_icao, is_active FROM Persons WHERE is_active = 1";

            if (!string.IsNullOrEmpty(unit)) query += " AND unit = @unit";
            if (!string.IsNullOrEmpty(airportIcao)) query += " AND airport_icao = @icao";

            using var command = new SqliteCommand(query, connection);
            if (!string.IsNullOrEmpty(unit)) command.Parameters.AddWithValue("@unit", unit);
            if (!string.IsNullOrEmpty(airportIcao)) command.Parameters.AddWithValue("@icao", airportIcao);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                persons.Add(new Person
                {
                    Id = reader.GetInt32(0),
                    Rank = reader.IsDBNull(1) ? null : reader.GetString(1),
                    TitleBefore = reader.IsDBNull(2) ? null : reader.GetString(2),
                    FirstName = reader.GetString(3),
                    LastName = reader.GetString(4),
                    KnowledgeClass = reader.GetInt32(5),
                    ValidUntil = reader.GetDateTime(6),
                    Unit = reader.IsDBNull(7) ? null : reader.GetString(7),
                    AirportIcao = reader.IsDBNull(8) ? null : reader.GetString(8),
                    IsActive = reader.GetBoolean(9)
                });
            }
            return persons;
        }

        public Person? GetById(int id)
        {
            using var connection = DatabaseHelper.GetConnection();
            string query = "SELECT id, rank, title_before, first_name, last_name, knowledge_class, valid_until, unit, airport_icao, is_active FROM Persons WHERE id = @id AND is_active = 1;";
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Person
                {
                    Id = reader.GetInt32(0),
                    Rank = reader.IsDBNull(1) ? null : reader.GetString(1),
                    TitleBefore = reader.IsDBNull(2) ? null : reader.GetString(2),
                    FirstName = reader.GetString(3),
                    LastName = reader.GetString(4),
                    KnowledgeClass = reader.GetInt32(5),
                    ValidUntil = reader.GetDateTime(6),
                    Unit = reader.IsDBNull(7) ? null : reader.GetString(7),
                    AirportIcao = reader.IsDBNull(8) ? null : reader.GetString(8),
                    IsActive = reader.GetBoolean(9)
                };
            }
            return null;
        }

        public int Add(Person person)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand(
                "INSERT INTO Persons (rank, title_before, first_name, last_name, knowledge_class, valid_until, unit, airport_icao, is_active) " +
                "VALUES (@rank, @title, @first, @last, @class, @valid, @unit, @icao, 1); SELECT last_insert_rowid();",
                connection);

            command.Parameters.AddWithValue("@rank", (object?)person.Rank ?? DBNull.Value);
            command.Parameters.AddWithValue("@title", (object?)person.TitleBefore ?? DBNull.Value);
            command.Parameters.AddWithValue("@first", person.FirstName);
            command.Parameters.AddWithValue("@last", person.LastName);
            command.Parameters.AddWithValue("@class", person.KnowledgeClass);
            command.Parameters.AddWithValue("@valid", person.ValidUntil);
            command.Parameters.AddWithValue("@unit", (object?)person.Unit ?? DBNull.Value);
            command.Parameters.AddWithValue("@icao", (object?)person.AirportIcao ?? DBNull.Value);

            return Convert.ToInt32(command.ExecuteScalar());
        }

        public void Update(Person person)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand(
                "UPDATE Persons SET rank = @rank, title_before = @title, first_name = @first, last_name = @last, " +
                "knowledge_class = @class, valid_until = @valid, unit = @unit, airport_icao = @icao WHERE id = @id;",
                connection);

            command.Parameters.AddWithValue("@id", person.Id);
            command.Parameters.AddWithValue("@rank", (object?)person.Rank ?? DBNull.Value);
            command.Parameters.AddWithValue("@title", (object?)person.TitleBefore ?? DBNull.Value);
            command.Parameters.AddWithValue("@first", person.FirstName);
            command.Parameters.AddWithValue("@last", person.LastName);
            command.Parameters.AddWithValue("@class", person.KnowledgeClass);
            command.Parameters.AddWithValue("@valid", person.ValidUntil);
            command.Parameters.AddWithValue("@unit", (object?)person.Unit ?? DBNull.Value);
            command.Parameters.AddWithValue("@icao", (object?)person.AirportIcao ?? DBNull.Value);

            command.ExecuteNonQuery();
        }

        public void SoftDelete(int personId)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("UPDATE Persons SET is_active = 0 WHERE id = @id;", connection);
            command.Parameters.AddWithValue("@id", personId);
            command.ExecuteNonQuery();
        }
    }
}