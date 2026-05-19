using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using TestyLRNS_WPF.Models;
using TestyLRNS_WPF.Services; // Přidáno pro přístup k SecurityService (případně uprav dle své struktury)

namespace TestyLRNS_WPF.Data.Repositories
{
    public class UserRepository
    {
        public User? Authenticate(string username, string password)
        {
            using var connection = DatabaseHelper.GetConnection();

            // 1. Zadané heslo z formuláře ("123") převedeme na hash, aby se dal porovnat s DB
            string hashedInputPassword = SecurityService.HashPassword(password);

            using var command = new SqliteCommand(
                "SELECT id, username, password_hash, role, unit, airport_icao, linked_person_id FROM Users WHERE username = @user AND password_hash = @pass AND is_active = 1;",
                connection);

            command.Parameters.AddWithValue("@user", username);

            // 2. Do parametru už pošleme ten vygenerovaný hash
            command.Parameters.AddWithValue("@pass", hashedInputPassword);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Role = reader.GetString(3),
                    Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
                    AirportIcao = reader.IsDBNull(5) ? null : reader.GetString(5),
                    LinkedPersonId = reader.IsDBNull(6) ? null : reader.GetInt32(6)
                };
            }
            return null; // Špatné jméno nebo heslo
        }

        // Zkontroluje, zda uživatelské jméno už v databázi neexistuje
        public bool UserExists(string username)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE username = @user;", connection);
            command.Parameters.AddWithValue("@user", username);
            return (long)command.ExecuteScalar() > 0;
        }

        // Změní heslo existujícího uživatele
        public void UpdatePassword(int userId, string newPasswordHash)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("UPDATE Users SET password_hash = @pass WHERE id = @id;", connection);
            command.Parameters.AddWithValue("@pass", newPasswordHash);
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }

        // Přidá nového uživatele do databáze
        public int AddUser(User user)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand(
                "INSERT INTO Users (username, password_hash, role, unit, airport_icao, linked_person_id, is_active) " +
                "VALUES (@user, @pass, @role, @unit, @airport, @linkedPerson, 1); SELECT last_insert_rowid();",
                connection);

            command.Parameters.AddWithValue("@user", user.Username);
            command.Parameters.AddWithValue("@pass", user.PasswordHash);
            command.Parameters.AddWithValue("@role", user.Role);
            command.Parameters.AddWithValue("@unit", (object?)user.Unit ?? DBNull.Value);
            command.Parameters.AddWithValue("@airport", (object?)user.AirportIcao ?? DBNull.Value);
            command.Parameters.AddWithValue("@linkedPerson", (object?)user.LinkedPersonId ?? DBNull.Value);

            // Spustí dotaz a vrátí vygenerované ID uživatele
            return Convert.ToInt32(command.ExecuteScalar());
        }

        // Propojení uživatele na osobu po tom, co obě ID existují
        public void LinkUserToPerson(int userId, int? personId)
        {
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqliteCommand("UPDATE Users SET linked_person_id = @personId WHERE id = @userId;", connection);
            command.Parameters.AddWithValue("@personId", (object?)personId ?? DBNull.Value);
            command.Parameters.AddWithValue("@userId", userId);
            command.ExecuteNonQuery();
        }

        // Načte uživatele pro správu (Admin vidí všechny, Lokální Admin jen lidi ze své základny)
        public List<User> GetUsersForManagement(string currentRole, string? airportIcao)
        {
            var list = new List<User>();
            using var connection = DatabaseHelper.GetConnection();

            string query = currentRole == "SuperAdmin"
                ? "SELECT id, username, role, unit, airport_icao, linked_person_id FROM Users WHERE is_active = 1;"
                : "SELECT id, username, role, unit, airport_icao, linked_person_id FROM Users WHERE is_active = 1 AND airport_icao = @airport AND role = 'Instruktor';";

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
                    Username = reader.GetString(1),
                    Role = reader.GetString(2),
                    Unit = reader.IsDBNull(3) ? null : reader.GetString(3),
                    AirportIcao = reader.IsDBNull(4) ? null : reader.GetString(4),
                    LinkedPersonId = reader.IsDBNull(5) ? null : reader.GetInt32(5)
                });
            }
            return list;
        }

        // Místo tvrdého DELETE uživatele jen deaktivujeme (is_active = 0), aby se neporušila integrita testů
        public void DeleteUser(int userId)
        {
            using var connection = DatabaseHelper.GetConnection();

            // SQLite příkaz připojí k uživatelskému jménu "_deleted_" a unixový čas
            string query = @"
                UPDATE Users 
                SET is_active = 0, 
                    username = username || '_deleted_' || strftime('%s','now') 
                WHERE id = @id;";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }
    }
}