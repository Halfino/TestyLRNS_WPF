using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Core
{
    public static class SessionManager
    {
        // Obsahuje data aktuálně přihlášeného uživatele
        public static User? CurrentUser { get; private set; }

        public static void Login(User user)
        {
            CurrentUser = user;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}