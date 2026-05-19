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

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}