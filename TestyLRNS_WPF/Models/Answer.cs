using System.ComponentModel;

namespace TestyLRNS_WPF.Models
{
    public class Answer : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; set; }
        public int QuestionId { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public bool IsActive { get; set; } = true;
        // Přidat do Person, Question, Answer, TestResult, SystemTopic, User:
        public string GlobalId { get; set; } = Guid.NewGuid().ToString();
        public int SyncStatus { get; set; } = 0; // 0 = Nové/Změněné, 1 = Synchronizováno
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}