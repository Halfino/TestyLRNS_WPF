using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ModernWpf.Controls; // Důležité pro NumberBoxValueChangedEventArgs
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Views
{
    public partial class EvaluateTestDialog : Window
    {
        private readonly int _testId;
        private readonly int _maxScore;
        private readonly TestResultRepository _testResultRepo;

        public EvaluateTestDialog(int testId, string personName, string testType, int maxScore)
        {
            this.InitializeComponent();
            _testId = testId;
            _maxScore = maxScore;
            _testResultRepo = new TestResultRepository();

            TxtPersonName.Text = personName;
            TxtTestType.Text = $"{testType} (Maximum: {maxScore} bodů)";
            NbScore.Maximum = maxScore;

            UpdateLiveEvaluation(0);
        }

        // Událost změny hodnoty v ModernWpf NumberBoxu
        private void NbScore_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (NbScore == null) return;
            if (double.IsNaN(NbScore.Value)) return;

            UpdateLiveEvaluation((int)NbScore.Value);
        }

        private void UpdateLiveEvaluation(int currentScore)
        {
            if (_maxScore == 0) return;
            double pct = ((double)currentScore / _maxScore) * 100;
            TxtPercentage.Text = $"{Math.Round(pct)} %";

            // Limit pro úspěšné složení vojenské zkoušky (80 %)
            if (pct >= 80.0)
            {
                TxtStatus.Text = "Prospěl (Limit 80% splněn)";
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 204, 102)); // Svěží zelená
            }
            else
            {
                TxtStatus.Text = "Neprospěl (Pod limitem 80%)";
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 68, 68)); // Výstražná červená
            }
        }

        // TLAČÍTKO: ULOŽIT HODNOCENÍ
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            if (double.IsNaN(NbScore.Value))
            {
                TxtError.Text = "Zadejte platný počet bodů.";
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            int finalScore = (int)NbScore.Value;
            string? note = string.IsNullOrWhiteSpace(TxtNote.Text) ? null : TxtNote.Text.Trim();

            try
            {
                // Uložíme výsledek do SQLite přes hotové Repository
                _testResultRepo.UpdateTestResultScore(_testId, finalScore, note);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                TxtError.Text = $"Chyba při ukládání: {ex.Message}";
                TxtError.Visibility = Visibility.Visible;
            }
        }

        // TLAČÍTKO: ZRUŠIT
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}