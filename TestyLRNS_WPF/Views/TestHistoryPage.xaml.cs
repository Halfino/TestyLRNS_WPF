using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
// Tvoje přejmenované namespaces pro WPF projekt
using TestyLRNS_WPF.Core;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;
using TestyLRNS_WPF.Services;

namespace TestyLRNS_WPF.Views
{
    public partial class TestHistoryPage : Page
    {
        private readonly TestResultRepository _testResultRepo;
        private readonly PersonRepository _personRepo;
        private readonly QuestionRepository _questionRepo;
        private readonly PdfGenerationService _pdfService;
        private readonly User _currentUser;

        public TestHistoryPage()
        {
            this.InitializeComponent();

            _testResultRepo = new TestResultRepository();
            _personRepo = new PersonRepository();
            _questionRepo = new QuestionRepository();
            _pdfService = new PdfGenerationService();

            // Ošetření pádu, pokud je uživatel náhodou null (při design-time zobrazení ve VS)
            if (SessionManager.CurrentUser != null)
            {
                _currentUser = SessionManager.CurrentUser;
                InitializeFilters();
                LoadHistory();
            }
        }

        private void InitializeFilters()
        {
            // 1. Naplnění roků
            CbYear.Items.Clear();
            CbYear.Items.Add("Všechny roky");
            int currentYear = DateTime.Now.Year;
            for (int y = currentYear; y >= 2025; y--)
            {
                CbYear.Items.Add(y.ToString());
            }
            CbYear.SelectedIndex = 0;
            CbMonth.SelectedIndex = 0;

            // 2. ŘÍZENÍ PŘÍSTUPU K FILTRU ODBORNOSTÍ (RBAC)
            if (_currentUser.Role == "Instruktor")
            {
                SetComboBoxByContent(CbUnitFilter, _currentUser.Unit ?? "SZP");
                CbUnitFilter.IsEnabled = false;
            }
            else
            {
                CbUnitFilter.SelectedIndex = 0; // Výchozí: Všechny
            }
        }

        private void SetComboBoxByContent(ComboBox cb, string content)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if ((cb.Items[i] as ComboBoxItem)?.Content.ToString() == content)
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
        }

        private void LoadHistory()
        {
            if (CbYear == null || CbMonth == null || CbUnitFilter == null) return;

            int? selectedYear = CbYear.SelectedIndex > 0 ? (int?)int.Parse(CbYear.SelectedItem.ToString()!) : null;
            int? selectedMonth = CbMonth.SelectedIndex > 0 ? (int?)CbMonth.SelectedIndex : null;

            var historyData = _testResultRepo.GetTestHistory(_currentUser, selectedYear, selectedMonth);

            if (CbUnitFilter.SelectedIndex > 0)
            {
                string selectedUnit = (CbUnitFilter.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                historyData = historyData.Where(h => h.Unit == selectedUnit).ToList();
            }

            foreach (var item in historyData)
            {
                int bracketIndex = item.TestType.IndexOf('(');
                if (bracketIndex > 0)
                {
                    item.TestType = item.TestType.Substring(0, bracketIndex).Trim();
                }
            }

            LvHistory.ItemsSource = historyData;
            TxtInfo.Text = $"Nalezeno záznamů: {historyData.Count}";
        }

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadHistory();

        private void BtnOpenPdf_Click(object sender, RoutedEventArgs e)
        {
            // Pokud jsme klikli na tlačítko ve WPF, data řádku (TestHistoryDto) jsou v btn.DataContext
            if (sender is Button btn && btn.DataContext is TestHistoryDto historyItem)
            {
                string targetPath = historyItem.PdfPath;

                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                {
                    var testResult = _testResultRepo.GetById(historyItem.TestId);
                    if (testResult == null) return;

                    var person = _personRepo.GetById(testResult.PersonId);
                    if (person == null) return;

                    var originalQuestions = new List<Question>();
                    foreach (var qId in testResult.QuestionIds)
                    {
                        var question = _questionRepo.GetById(qId);
                        if (question != null)
                        {
                            originalQuestions.Add(question);
                        }
                    }

                    string newDir;
                    targetPath = _pdfService.GetExportFilePath(person, testResult.TestType ?? "test", out newDir);

                    testResult.PdfPath = targetPath;
                    _pdfService.GenerateTestPdf(targetPath, testResult, person, _currentUser, originalQuestions);
                    _testResultRepo.UpdatePdfPath(testResult.Id, targetPath);

                    historyItem.PdfPath = targetPath;
                }

                _pdfService.OpenPdfFile(targetPath);
            }
        }
    }
}