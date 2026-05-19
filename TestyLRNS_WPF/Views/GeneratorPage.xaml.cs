using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// Aktualizované namespacy pro WPF projekt:
using TestyLRNS_WPF.Core;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;
using TestyLRNS_WPF.Services;

namespace TestyLRNS_WPF.Views
{
    public partial class GeneratorPage : Page
    {
        private readonly User _currentUser;
        private readonly PersonRepository _personRepo;
        private readonly SystemTopicRepository _topicRepo;
        private readonly QuestionRepository _questionRepo;
        private readonly TestResultRepository _testResultRepo;
        private readonly TestGenerationService _testGenService;
        private readonly PdfGenerationService _pdfService;

        private List<Person> _availablePersons = new();
        private bool _isInitializing = true;

        public GeneratorPage()
        {
            this.InitializeComponent();

            if (SessionManager.CurrentUser != null)
            {
                _currentUser = SessionManager.CurrentUser;
                _personRepo = new PersonRepository();
                _topicRepo = new SystemTopicRepository();
                _questionRepo = new QuestionRepository();
                _testResultRepo = new TestResultRepository();
                _testGenService = new TestGenerationService();
                _pdfService = new PdfGenerationService();

                ApplySecurityAndLoadDropdowns();
                _isInitializing = false;

                LoadPersons();
            }
        }

        private void ApplySecurityAndLoadDropdowns()
        {
            CbBase.Items.Clear();
            if (_currentUser.Role == "SuperAdmin")
            {
                CbBase.Items.Add(new ComboBoxItem { Content = "LKKB" });
                CbBase.Items.Add(new ComboBoxItem { Content = "LKCV" });
                CbBase.Items.Add(new ComboBoxItem { Content = "LKNA" });
                CbBase.Items.Add(new ComboBoxItem { Content = "LKPD" });
                CbBase.SelectedIndex = 0;
            }
            else
            {
                CbBase.Items.Add(new ComboBoxItem { Content = _currentUser.AirportIcao ?? "LKKB" });
                CbBase.SelectedIndex = 0;
                CbBase.IsEnabled = false;
            }

            if (_currentUser.Role == "Instruktor")
            {
                SetComboBoxByContent(CbUnit, _currentUser.Unit ?? "SZP");
                CbUnit.IsEnabled = false;
            }
            else
            {
                CbUnit.SelectedIndex = 0;
            }

            UpdateTopics();
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

        private void CbBaseOrUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            UpdateTopics();
            LoadPersons();
        }

        private void UpdateTopics()
        {
            if (CbUnit.SelectedItem == null) return;
            string selectedUnit = (CbUnit.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";

            var topics = _topicRepo.GetAllActiveByUnit(selectedUnit).Select(t => t.Name).ToList();

            CbTopic.Items.Clear();
            CbTopic.Items.Add("Všechna témata (Průřez)");
            foreach (var topic in topics)
            {
                CbTopic.Items.Add(topic);
            }
            CbTopic.SelectedIndex = 0;
        }

        private void LoadPersons()
        {
            if (CbBase.SelectedItem == null || CbUnit.SelectedItem == null) return;

            string selectedBase = (CbBase.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            string selectedUnit = (CbUnit.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";

            _availablePersons = _personRepo.GetAllActive(selectedUnit, selectedBase)
                                           .OrderBy(p => p.LastName)
                                           .ToList();

            LvPersons.ItemsSource = _availablePersons;
        }

        private void CbTestType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbTopic == null) return;

            CbTopic.IsEnabled = CbTestType.SelectedIndex == 4;
            if (!CbTopic.IsEnabled)
            {
                CbTopic.SelectedIndex = 0;
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e) => LvPersons.SelectAll();

        // WPF ZMĚNA: WPF ListBox používá metodu UnselectAll()
        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e) => LvPersons.UnselectAll();

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (LvPersons.SelectedItems.Count == 0)
            {
                ShowStatus("Musíte vybrat alespoň jednoho technika pro generování.", false);
                return;
            }

            string testTypeStr = (CbTestType.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Test";

            // WPF ZMĚNA: ModernWPF NumberBox.Value používá double a defaultně vrací double.NaN. Bezpečné přetypování na int.
            int questionCount = !double.IsNaN(NbQuestionCount.Value) ? (int)NbQuestionCount.Value : 30;

            bool includeOp = false;
            bool onlyOp = false;
            List<string>? allowedTopics = null;

            switch (CbTestType.SelectedIndex)
            {
                case 0:
                    includeOp = false; onlyOp = false; break;
                case 1:
                    includeOp = false; onlyOp = false; break;
                case 2:
                    includeOp = true; onlyOp = true; break;
                case 3:
                    includeOp = true; onlyOp = false; break;
                case 4:
                    if (CbTopic.SelectedIndex > 0)
                    {
                        allowedTopics = new List<string> { CbTopic.SelectedItem.ToString()! };
                    }
                    break;
            }

            int successCount = 0;
            string lastUsedDirectory = "";
            string lastUsedFile = "";

            string selectedBase = (CbBase.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            string selectedUnit = (CbUnit.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            var allDbQuestions = _questionRepo.GetAllActive(selectedUnit, selectedBase);

            foreach (Person selectedPerson in LvPersons.SelectedItems)
            {
                try
                {
                    TestResult result = _testGenService.GenerateTest(
                        selectedPerson, _currentUser, testTypeStr, questionCount,
                        allowedTopics, includeOp, onlyOp);

                    var questionsForPdf = allDbQuestions.Where(q => result.QuestionIds.Contains(q.Id)).ToList();

                    lastUsedFile = _pdfService.GetExportFilePath(selectedPerson, testTypeStr, out lastUsedDirectory);
                    result.PdfPath = lastUsedFile;

                    _pdfService.GenerateTestPdf(lastUsedFile, result, selectedPerson, _currentUser, questionsForPdf);

                    _testResultRepo.SaveTestResult(result);

                    successCount++;
                }
                catch (Exception ex)
                {
                    ShowStatus($"Chyba u technika {selectedPerson.LastName}: {ex.Message}", false);
                    return;
                }
            }

            ShowStatus($"Úspěšně vygenerováno a uloženo {successCount} testů.", true);
            LvPersons.UnselectAll(); // Po úspěšném generování seznam odznačíme

            if (successCount == 1)
            {
                _pdfService.OpenPdfFile(lastUsedFile);
            }
            else if (successCount > 1)
            {
                _pdfService.OpenFolder(lastUsedDirectory);
            }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            TxtStatus.Text = message;
            TxtStatus.Foreground = new SolidColorBrush(isSuccess ? Colors.LightGreen : Colors.Salmon);
        }
    }
}