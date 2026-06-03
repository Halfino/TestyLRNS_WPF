using System.Collections.ObjectModel;
using System.ComponentModel;

namespace TestyLRNS_WPF.Models
{
    public class Question : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsWritten { get; set; }
        public int KnowledgeClass { get; set; }

        public string? Unit { get; set; }
        public string? SystemTopic { get; set; }
        public string? AirportIcao { get; set; }
        public bool IsOperationalTraining { get; set; } // Provozní výcvik
        public bool IsActive { get; set; } = true;

        private int _answerCount;
        // Přidat do Person, Question, Answer, TestResult, SystemTopic, User:
        public string GlobalId { get; set; } = Guid.NewGuid().ToString();
        public int SyncStatus { get; set; } = 0; // 0 = Nové/Změněné, 1 = Synchronizováno
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string? ImagePath { get; set; } // Obsahuje pouze název souboru, např. "schema_123.webp"
        public int AnswerCount
        {
            get => _answerCount;
            set { _answerCount = value; OnPropertyChanged(nameof(AnswerCount)); }
        }

        private ObservableCollection<Answer> _answers = new();
        public ObservableCollection<Answer> Answers
        {
            get => _answers;
            set { _answers = value; OnPropertyChanged(nameof(Answers)); }
        }

        public bool IsGlobal => string.IsNullOrEmpty(AirportIcao);

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}