namespace TestyLRNS_WPF.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;       // Admin, Instruktor, Inspektor
        public string? Unit { get; set; }                      // Omezení na odbornost
        public string? AirportIcao { get; set; }               // Omezení na základnu
        public int? LinkedPersonId { get; set; }
        // Přidat do Person, Question, Answer, TestResult, SystemTopic, User:
        public string GlobalId { get; set; } = Guid.NewGuid().ToString();
        public int SyncStatus { get; set; } = 0; // 0 = Nové/Změněné, 1 = Synchronizováno
        public DateTime UpdatedAt { get; set; } = DateTime.Now;// Vazba na tabulku Persons
    }
}