using System.ComponentModel;

namespace TestyLRNS_WPF.Models
{
    public class SystemTopic : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;       // ILS, PAPI, atd.
        public string Unit { get; set; } = string.Empty;       // RNS, SZP...
        public bool IsActive { get; set; } = true;
        // Přidat do Person, Question, Answer, TestResult, SystemTopic, User:
        public string GlobalId { get; set; } = Guid.NewGuid().ToString();
        public int SyncStatus { get; set; } = 0; // 0 = Nové/Změněné, 1 = Synchronizováno
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}