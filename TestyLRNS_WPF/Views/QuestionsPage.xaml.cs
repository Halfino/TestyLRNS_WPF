using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// Ujisti se, že namespace odpovídá tvému novému projektu:
using TestyLRNS_WPF.Core;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Views
{
    public partial class QuestionsPage : Page
    {
        private readonly QuestionRepository _questionRepository;
        private List<QuestionViewModel> _allQuestions = new();
        private string _currentAirportIcao;

        private readonly User _currentUser;

        public QuestionsPage()
        {
            this.InitializeComponent();
            _questionRepository = new QuestionRepository();

            if (SessionManager.CurrentUser != null)
            {
                _currentUser = SessionManager.CurrentUser;
                _currentAirportIcao = _currentUser.AirportIcao ?? "LKKB";

                InitializeFilters();
                RefreshData();
            }
        }

        private void InitializeFilters()
        {
            if (_currentUser.Role == "Instruktor")
            {
                SetComboBoxByContent(CbUnitFilter, _currentUser.Unit ?? "SZP");
                CbUnitFilter.IsEnabled = false;
            }
            else
            {
                CbUnitFilter.SelectedIndex = 0;
            }

            CbClassFilter.SelectedIndex = 0;
            CbLocalityFilter.SelectedIndex = 0;
            CbOperationalFilter.SelectedIndex = 0;
        }

        private void SetComboBoxByContent(ComboBox cb, string content)
        {
            if (cb == null) return;
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if ((cb.Items[i] as ComboBoxItem)?.Content?.ToString() == content)
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
            cb.SelectedIndex = 0;
        }

        private void RefreshData()
        {
            var rawQuestions = _questionRepository.GetAllActive();

            _allQuestions = rawQuestions.Select(q => new QuestionViewModel
            {
                Id = q.Id,
                Text = q.Text,
                UnitRaw = q.Unit,
                SystemTopicRaw = q.SystemTopic,
                SystemTopicString = string.IsNullOrEmpty(q.SystemTopic) ? "Obecná" : q.SystemTopic,
                TypeString = q.IsWritten ? "Otevřená (Psaná)" : "Test (Single Choice)",
                KnowledgeClassString = q.KnowledgeClass switch
                {
                    1 => "3. třída",
                    2 => "2. třída",
                    3 => "1. třída",
                    _ => "Neznámá"
                },
                KnowledgeClassRaw = q.KnowledgeClass,
                IsOperational = q.IsOperationalTraining,
                IsOperationalString = q.IsOperationalTraining ? "ANO" : "NE", // Trochu počeštěno pro lepší dojem :)
                OperationalColor = q.IsOperationalTraining ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Colors.Gray),
                AirportIcao = q.AirportIcao,
                LocalityString = string.IsNullOrEmpty(q.AirportIcao) ? "Globální" : $"Místní ({q.AirportIcao})"
            }).ToList();

            UpdateTopicsDropdown();
            ApplyFilter();
        }

        private void UpdateTopicsDropdown()
        {
            if (CbTopicFilter == null) return;

            string? currentTopicSelection = CbTopicFilter.SelectedItem as string;
            CbTopicFilter.Items.Clear();
            CbTopicFilter.Items.Add("Všechna témata");
            CbTopicFilter.Items.Add("Obecná (bez tématu)");

            string selectedUnit = "";
            if (CbUnitFilter != null && CbUnitFilter.SelectedIndex > 0)
            {
                selectedUnit = (CbUnitFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            }
            else if (_currentUser.Role == "Instruktor")
            {
                selectedUnit = _currentUser.Unit ?? "";
            }

            var source = _allQuestions.AsEnumerable();
            if (!string.IsNullOrEmpty(selectedUnit))
            {
                source = source.Where(q => q.UnitRaw == selectedUnit);
            }

            var distinctTopics = source
                .Where(q => !string.IsNullOrEmpty(q.SystemTopicRaw))
                .Select(q => q.SystemTopicRaw!)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            foreach (var topic in distinctTopics) CbTopicFilter.Items.Add(topic);

            if (currentTopicSelection != null && CbTopicFilter.Items.Contains(currentTopicSelection))
                CbTopicFilter.SelectedItem = currentTopicSelection;
            else
                CbTopicFilter.SelectedIndex = 0;
        }

        private void CbUnitFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTopicsDropdown();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filtered = _allQuestions.AsEnumerable();

            if (_currentUser.Role == "SuperAdmin")
            {
                // Vidí vše
            }
            else if (_currentUser.Role == "LokalniAdmin")
            {
                filtered = filtered.Where(q => string.IsNullOrEmpty(q.AirportIcao) || q.AirportIcao == _currentAirportIcao);
            }
            else
            {
                filtered = filtered.Where(q => (string.IsNullOrEmpty(q.AirportIcao) || q.AirportIcao == _currentAirportIcao)
                                               && q.UnitRaw == _currentUser.Unit);
            }

            if (CbUnitFilter != null && CbUnitFilter.SelectedIndex > 0)
            {
                string selectedUnit = (CbUnitFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                filtered = filtered.Where(q => q.UnitRaw == selectedUnit);
            }

            string searchText = TxtSearch.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(q => q.Text.ToLower().Contains(searchText));
            }

            if (CbTopicFilter.SelectedIndex == 1)
            {
                filtered = filtered.Where(q => string.IsNullOrEmpty(q.SystemTopicRaw));
            }
            else if (CbTopicFilter.SelectedIndex > 1)
            {
                string selectedTopic = CbTopicFilter.SelectedItem?.ToString() ?? "";
                filtered = filtered.Where(q => q.SystemTopicRaw == selectedTopic);
            }

            if (CbClassFilter.SelectedIndex > 0)
            {
                int targetClass = CbClassFilter.SelectedIndex;
                filtered = filtered.Where(q => q.KnowledgeClassRaw == targetClass);
            }

            if (CbLocalityFilter.SelectedIndex == 1)
            {
                filtered = filtered.Where(q => string.IsNullOrEmpty(q.AirportIcao));
            }
            else if (CbLocalityFilter.SelectedIndex == 2)
            {
                filtered = filtered.Where(q => q.AirportIcao == _currentAirportIcao);
            }

            if (CbOperationalFilter.SelectedIndex == 1)
            {
                filtered = filtered.Where(q => q.IsOperational);
            }
            else if (CbOperationalFilter.SelectedIndex == 2)
            {
                filtered = filtered.Where(q => !q.IsOperational);
            }

            LvQuestions.ItemsSource = filtered.ToList();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void FilterSelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void BtnAddQuestion_Click(object sender, RoutedEventArgs e)
        {
            /* ODPOZNÁMKUJ PO VYTVOŘENÍ OKNA AddQuestionDialog (Jako Window WPF)
            var dialog = new AddQuestionDialog(_currentUser);
            
            bool? result = dialog.ShowDialog();

            if (result == true && dialog.NewQuestion != null)
            {
                _questionRepository.Add(dialog.NewQuestion);
                RefreshData();
            }
            */
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int questionId)
            {
                var question = _questionRepository.GetById(questionId);
                if (question == null) return;

                /* ODPOZNÁMKUJ PO VYTVOŘENÍ OKNA AddQuestionDialog
                var dialog = new AddQuestionDialog(_currentUser, question);
                
                bool? result = dialog.ShowDialog();

                if (result == true && dialog.NewQuestion != null)
                {
                    _questionRepository.Update(dialog.NewQuestion);
                    RefreshData();
                }
                */
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int questionId)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Opravdu chcete tuto otázku vyřadit z databáze?",
                    "Odstranit otázku?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _questionRepository.SoftDelete(questionId);
                    RefreshData();
                }
            }
        }

        public class QuestionViewModel
        {
            public int Id { get; set; }
            public string Text { get; set; } = string.Empty;
            public string? UnitRaw { get; set; }
            public string? SystemTopicRaw { get; set; }
            public string SystemTopicString { get; set; } = string.Empty;
            public string TypeString { get; set; } = string.Empty;
            public string KnowledgeClassString { get; set; } = string.Empty;
            public int KnowledgeClassRaw { get; set; }
            public bool IsOperational { get; set; }
            public string IsOperationalString { get; set; } = string.Empty;
            public SolidColorBrush OperationalColor { get; set; } = new(Colors.White);
            public string? AirportIcao { get; set; }
            public string LocalityString { get; set; } = string.Empty;
        }
    }
}