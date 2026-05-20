using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TestyLRNS_WPF.Models;
using TestyLRNS_WPF.Services;

namespace TestyLRNS_WPF.Data.Repositories
{
    public class UserRepository
    {
        public User? Authenticate(string username, string password)
        {
            using var connection = DatabaseHelper.GetConnection();
            string hashedInputPassword = SecurityService.HashPassword(password);

            // Načítáme i global_id
            using var command = new SqliteCommand(
                "SELECT id, global_id, username, password_hash, role, unit, airport_icao, linked_person_id FROM Users WHERE username = @user AND password_hash = @pass AND is_active = 1;",
                connection);

            command.Parameters.AddWithValue("@user", username);
            command.Parameters.AddWithValue("@pass", hashedInputPassword);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    GlobalId = reader.GetString(1),
                    Username = reader.GetString(2),
                    PasswordHash = reader.GetString(3),
                    Role = reader.GetString(4),
                    Unit = reader.IsDBNull(5) ? null : reader.GetString(5),
                    AirportIcao = reader.IsDBNull(6) ? null : reader.GetString(6),
                    LinkedPersonId = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                };
            }
            return null;
        }

        public bool UserExists(string username)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE username = @user AND is_active = 1;", connection);
            command.Parameters.AddWithValue("@user", username);
            return (long)command.ExecuteScalar() > 0;
        }

        public void UpdatePassword(int userId, string newPasswordHash)
        {
            using var connection = DatabaseHelper.GetConnection();
            // PŘIDÁNO: sync_status = 0 a updated_at
            using var command = new SqliteCommand("UPDATE Users SET password_hash = @pass, sync_status = 0, updated_at = CURRENT_TIMESTAMP WHERE id = @id;", connection);
            command.Parameters.AddWithValue("@pass", newPasswordHash);
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }

        public int AddUser(User user)
        {
            // Zajištění GUID pro synchronizaci
            if (string.IsNullOrEmpty(user.GlobalId)) user.GlobalId = Guid.NewGuid().ToString();

            using var connection = DatabaseHelper.GetConnection();
            // PŘIDÁNO: global_id, sync_status, updated_at
            using var command = new SqliteCommand(
                "INSERT INTO Users (global_id, sync_status, updated_at, username, password_hash, role, unit, airport_icao, linked_person_id, is_active) " +
                "VALUES (@globalId, 0, CURRENT_TIMESTAMP, @user, @pass, @role, @unit, @airport, @linkedPerson, 1); SELECT last_insert_rowid();",
                connection);

            command.Parameters.AddWithValue("@globalId", user.GlobalId);
            command.Parameters.AddWithValue("@user", user.Username);
            command.Parameters.AddWithValue("@pass", user.PasswordHash);
            command.Parameters.AddWithValue("@role", user.Role);
            command.Parameters.AddWithValue("@unit", (object?)user.Unit ?? DBNull.Value);
            command.Parameters.AddWithValue("@airport", (object?)user.AirportIcao ?? DBNull.Value);
            command.Parameters.AddWithValue("@linkedPerson", (object?)user.LinkedPersonId ?? DBNull.Value);

            return Convert.ToInt32(command.ExecuteScalar());
        }

        public void LinkUserToPerson(int userId, int? personId)
        {
            using var connection = DatabaseHelper.GetConnection();
            // PŘIDÁNO: sync_status = 0
            using var command = new SqliteCommand("UPDATE Users SET linked_person_id = @personId, sync_status = 0, updated_at = CURRENT_TIMESTAMP WHERE id = @userId;", connection);
            command.Parameters.AddWithValue("@personId", (object?)personId ?? DBNull.Value);
            command.Parameters.AddWithValue("@userId", userId);
            command.ExecuteNonQuery();
        }

        public List<User> GetUsersForManagement(string currentRole, string? airportIcao)
        {
            var list = new List<User>();
            using var connection = DatabaseHelper.GetConnection();

            string query = currentRole == "SuperAdmin"
                ? "SELECT id, global_id, username, role, unit, airport_icao, linked_person_id FROM Users WHERE is_active = 1;"
                : "SELECT id, global_id, username, role, unit, airport_icao, linked_person_id FROM Users WHERE is_active = 1 AND airport_icao = @airport AND role = 'Instruktor';";

            using var command = new SqliteCommand(query, connection);
            if (currentRole != "SuperAdmin")
            {
                command.Parameters.AddWithValue("@airport", airportIcao ?? "");
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new User
                {
                    Id = reader.GetInt32(0),
                    GlobalId = reader.GetString(1),
                    Username = reader.GetString(2),
                    Role = reader.GetString(3),
                    Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
                    AirportIcao = reader.IsDBNull(5) ? null : reader.GetString(5),
                    LinkedPersonId = reader.IsDBNull(6) ? null : reader.GetInt32(6)
                });
            }
            return list;
        }

        public void DeleteUser(int userId)
        {
            using var connection = DatabaseHelper.GetConnection();
            // PŘIDÁNO: sync_status = 0 při soft-deletu
            string query = @"
                UPDATE Users 
                SET is_active = 0, 
                    username = username || '_deleted_' || strftime('%s','now'),
                    sync_status = 0,
                    updated_at = CURRENT_TIMESTAMP 
                WHERE id = @id;";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }
    }
}