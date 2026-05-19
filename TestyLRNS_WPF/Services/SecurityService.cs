using System;
using System.Security.Cryptography;
using System.Text;

namespace TestyLRNS_WPF.Services
{
    public static class SecurityService
    {
        // Převede heslo na bezpečný Base64 Hash
        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        // Porovná zadané heslo s hashem v databázi
        public static bool VerifyPassword(string password, string storedHash)
        {
            return HashPassword(password) == storedHash;
        }
    }
}