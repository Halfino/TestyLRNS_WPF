using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;
using TestyLRNS_WPF.Core;

namespace TestyLRNS_WPF.Views
{
    public partial class AddQuestionDialog : Window
    {
        public Question? NewQuestion { get; private set; }
        private readonly Question? _editingQuestion;
        private readonly SystemTopicRepository _topicRepository;
        private readonly User _currentUser;
        private bool _isInitializing = true;

        public AddQuestionDialog(User? currentUser, Question? questionToEdit = null)
        {
            this.InitializeComponent();
            _topicRepository = new SystemTopicRepository();

            // OŠETŘENÍ NEPŘIHLÁŠENÉHO UŽIVATELE (pro testování bez loginu)
            _currentUser = currentUser ?? new User
            {
                Role = "SuperAdmin",
                Unit = "SZP",
                AirportIcao = "LKKB"
            };

            _editingQuestion = questionToEdit;

            PopulateAirports();

            if (_editingQuestion != null)
            {
                // --- REŽIM EDITACE ---
                this.Title = "Úprava zkušební otázky";
                TxtQuestionText.Text = _editingQuestion.Text;
                CbType.SelectedIndex = _editingQuestion.IsWritten ? 1 : 0;
                TsOperational.IsOn = _editingQuestion.IsOperationalTraining;

                SelectUnitInComboBox(_editingQuestion.Unit);
                UpdateTopicsDropdown(_editingQuestion.Unit);

                if (!string.IsNullOrEmpty(_editingQuestion.SystemTopic))
                {
                    CbTopic.SelectedItem = _editingQuestion.SystemTopic;
                }

                CbClass.SelectedIndex = Math.Clamp(_editingQuestion.KnowledgeClass - 1, 0, 2);
                SelectAirportInComboBox(_editingQuestion.AirportIcao);

                if (!_editingQuestion.IsWritten && _editingQuestion.Answers != null && _editingQuestion.Answers.Count >= 3)
                {
                    TxtAns1.Text = _editingQuestion.Answers[0].Text;
                    Rb1.IsChecked = _editingQuestion.Answers[0].IsCorrect;
                    TxtAns2.Text = _editingQuestion.Answers[1].Text;
                    Rb2.IsChecked = _editingQuestion.Answers[1].IsCorrect;
                    TxtAns3.Text = _editingQuestion.Answers[2].Text;
                    Rb3.IsChecked = _editingQuestion.Answers[2].IsCorrect;
                }
            }
            else
            {
                // --- REŽIM NOVÝ ---
                CbType.SelectedIndex = 0;

                string defaultUnit = _currentUser.Unit ?? "SZP";
                SelectUnitInComboBox(defaultUnit);
                UpdateTopicsDropdown(defaultUnit);

                CbClass.SelectedIndex = 0;

                string defaultAirport = _currentUser.AirportIcao ?? "LKKB";
                SelectAirportInComboBox(defaultAirport);
            }

            // ŘÍZENÍ PRÁV 
            if (_currentUser.Role == "SuperAdmin" || _currentUser.Role == "LokalniAdmin")
            {
                CbUnit.IsEnabled = true;
            }
            else
            {
                CbUnit.IsEnabled = false;
            }

            CbAirport.IsEnabled = true;

            _isInitializing = false;

            if (PanelAnswers != null)
            {
                PanelAnswers.Visibility = CbType.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void PopulateAirports()
        {
            CbAirport.Items.Clear();
            CbAirport.Items.Add("Globální (Všechna)");

            // Oprava: U role kontrolujeme jak SuperAdmina, tak LokalnihoAdmina
            if (_currentUser.Role == "SuperAdmin" || _currentUser.Role == "LokalniAdmin")
            {
                CbAirport.Items.Add("LKKB (Kbely)");
                CbAirport.Items.Add("LKCV (Čáslav)");
                CbAirport.Items.Add("LKNAM (Náměšť)");
                CbAirport.Items.Add("LKPD (Pardubice)");
            }
            else
            {
                string userIcao = _currentUser.AirportIcao ?? "LKKB";
                string display = userIcao switch
                {
                    "LKKB" => "LKKB (Kbely)",
                    "LKCV" => "LKCV (Čáslav)",
                    "LKNAM" => "LKNAM (Náměšť)",
                    "LKPD" => "LKPD (Pardubice)",
                    _ => $"{userIcao} (Lokální)"
                };
                CbAirport.Items.Add(display);
            }
        }

        private void SelectUnitInComboBox(string? unit)
        {
            if (string.IsNullOrEmpty(unit)) return;

            for (int i = 0; i < CbUnit.Items.Count; i++)
            {
                if ((CbUnit.Items[i] as ComboBoxItem)?.Content.ToString() == unit)
                {
                    CbUnit.SelectedIndex = i;
                    return;
                }
            }
            CbUnit.SelectedIndex = 0;
        }

        private void SelectAirportInComboBox(string? icao)
        {
            if (string.IsNullOrEmpty(icao))
            {
                CbAirport.SelectedIndex = 0;
                return;
            }

            for (int i = 1; i < CbAirport.Items.Count; i++)
            {
                string? itemContent = CbAirport.Items[i].ToString();
                if (itemContent != null && itemContent.StartsWith(icao, StringComparison.OrdinalIgnoreCase))
                {
                    CbAirport.SelectedIndex = i;
                    return;
                }
            }
            CbAirport.SelectedIndex = 0;
        }

        private string? GetSelectedAirportIcao()
        {
            if (CbAirport.SelectedIndex <= 0) return null;

            string? fullContent = CbAirport.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(fullContent)) return null;

            return fullContent.Split(' ')[0];
        }

        private void UpdateTopicsDropdown(string? unit)
        {
            if (string.IsNullOrEmpty(unit) || CbTopic == null) return;

            var availableSystems = _topicRepository.GetAllActiveByUnit(unit)
                                                   .Select(t => t.Name)
                                                   .ToList();

            // WPF způsob znovunačtení položek v ComboBoxu
            CbTopic.ItemsSource = null;
            CbTopic.ItemsSource = availableSystems;

            if (availableSystems.Count > 0 && _editingQuestion == null)
            {
                CbTopic.SelectedIndex = -1;
            }
        }

        private void CbUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            // OPRAVA: Přidáno .OfType<ComboBoxItem>() před FirstOrDefault, aby WPF LINQ pochopil, co hledá
            var selectedItem = e.AddedItems.OfType<ComboBoxItem>().FirstOrDefault();

            if (selectedItem != null)
            {
                string? selectedUnit = selectedItem.Content.ToString();
                UpdateTopicsDropdown(selectedUnit);
            }
        }

        private void CbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelAnswers == null) return;
            PanelAnswers.Visibility = CbType.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void TsOperational_Toggled(object sender, RoutedEventArgs e)
        {
            if (CbClass == null || CbAirport == null) return;

            if (TsOperational.IsOn)
            {
                CbClass.SelectedIndex = 0;
                CbClass.IsEnabled = false;

                if (CbAirport.SelectedIndex == 0)
                {
                    string defaultAirport = _currentUser.AirportIcao ?? "LKKB";
                    SelectAirportInComboBox(defaultAirport);
                }
            }
            else
            {
                CbClass.IsEnabled = true;
            }
        }

        // TLAČÍTKO: ULOŽIT (S chytrou validací do červeného pole)
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtErrorMessage.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(TxtQuestionText.Text))
            {
                TxtErrorMessage.Text = "Znění zkušební otázky nesmí být prázdné!";
                TxtErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            bool isWritten = CbType.SelectedIndex == 1;

            if (!isWritten && (string.IsNullOrWhiteSpace(TxtAns1.Text) || string.IsNullOrWhiteSpace(TxtAns2.Text) || string.IsNullOrWhiteSpace(TxtAns3.Text)))
            {
                TxtErrorMessage.Text = "Pro uzavřený test musíte vyplnit všechny tři možnosti odpovědí!";
                TxtErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            if (TsOperational.IsOn && CbAirport.SelectedIndex == 0)
            {
                TxtErrorMessage.Text = "Provozní výcvik musí být vždy vázaný na konkrétní letiště (nelze zvolit Globální).";
                TxtErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            int dbClass = CbClass.SelectedIndex + 1;
            string? unit = (CbUnit.SelectedItem as ComboBoxItem)?.Content.ToString();
            string? selectedTopic = CbTopic.SelectedItem?.ToString();
            string? airport = GetSelectedAirportIcao();

            var questionAnswers = new List<Answer>();
            if (!isWritten)
            {
                questionAnswers.Add(new Answer { Text = TxtAns1.Text.Trim(), IsCorrect = Rb1.IsChecked == true });
                questionAnswers.Add(new Answer { Text = TxtAns2.Text.Trim(), IsCorrect = Rb2.IsChecked == true });
                questionAnswers.Add(new Answer { Text = TxtAns3.Text.Trim(), IsCorrect = Rb3.IsChecked == true });
            }

            if (_editingQuestion != null)
            {
                _editingQuestion.Text = TxtQuestionText.Text.Trim();
                _editingQuestion.IsWritten = isWritten;
                _editingQuestion.KnowledgeClass = dbClass;
                _editingQuestion.Unit = unit;
                _editingQuestion.SystemTopic = selectedTopic;
                _editingQuestion.AirportIcao = airport;
                _editingQuestion.IsOperationalTraining = TsOperational.IsOn;
                _editingQuestion.Answers = new ObservableCollection<Answer>(questionAnswers);
                NewQuestion = _editingQuestion;
            }
            else
            {
                NewQuestion = new Question
                {
                    Text = TxtQuestionText.Text.Trim(),
                    IsWritten = isWritten,
                    KnowledgeClass = dbClass,
                    Unit = unit,
                    SystemTopic = selectedTopic,
                    AirportIcao = airport,
                    IsOperationalTraining = TsOperational.IsOn,
                    Answers = new ObservableCollection<Answer>(questionAnswers),
                    IsActive = true
                };
            }

            this.DialogResult = true;
            this.Close();
        }

        // TLAČÍTKO: ZRUŠIT
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}