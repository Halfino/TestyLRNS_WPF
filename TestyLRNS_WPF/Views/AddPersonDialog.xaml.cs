using System;
using System.Windows;
using System.Windows.Controls;
// Tvoje přejmenované namespaces pro nový projekt
using TestyLRNS_WPF.Core;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Views
{
    public partial class AddPersonDialog : Window
    {
        public Person? ResultPerson { get; private set; }
        private readonly Person? _editingPerson;
        private readonly User _currentUser;

        public AddPersonDialog(User currentUser, Person? personToEdit = null)
        {
            this.InitializeComponent();
            _currentUser = currentUser;
            _editingPerson = personToEdit;

            // Výchozí nastavení data ve WPF (SelectedDate místo Date)
            DpValidUntil.SelectedDate = DateTime.Now.AddYears(1);

            if (_editingPerson != null)
            {
                // --- REŽIM EDITACE EXISTUJÍCÍHO TECHNIKA ---
                this.Title = "Úprava karty vojenského technika";
                TxtRank.Text = _editingPerson.Rank;
                TxtTitle.Text = _editingPerson.TitleBefore;
                TxtFirstName.Text = _editingPerson.FirstName;
                TxtLastName.Text = _editingPerson.LastName;

                SelectUnitInComboBox(_editingPerson.Unit);
                SelectAirportInComboBox(_editingPerson.AirportIcao);
                CbClass.SelectedIndex = Math.Clamp(_editingPerson.KnowledgeClass, 0, 5);
                DpValidUntil.SelectedDate = _editingPerson.ValidUntil;
            }
            else
            {
                // --- REŽIM NOVÉHO TECHNIKA ---
                this.Title = "Nový vojenský technik";
                CbClass.SelectedIndex = 0; // Výchozí: Typový výcvik

                SelectUnitInComboBox(_currentUser.Unit);
                SelectAirportInComboBox(_currentUser.AirportIcao ?? "LKKB");
            }

            ApplySecurityRestrictions();
        }

        private void ApplySecurityRestrictions()
        {
            if (_currentUser.Role == "SuperAdmin")
            {
                CbAirport.IsEnabled = true;
                CbUnit.IsEnabled = true;
            }
            else if (_currentUser.Role == "LokalniAdmin")
            {
                CbAirport.IsEnabled = false;
                CbUnit.IsEnabled = true;
            }
            else // Instruktor
            {
                CbAirport.IsEnabled = false;
                CbUnit.IsEnabled = false;
            }
        }

        private void SelectUnitInComboBox(string? unit)
        {
            if (string.IsNullOrEmpty(unit))
            {
                CbUnit.SelectedIndex = 0;
                return;
            }

            for (int i = 0; i < CbUnit.Items.Count; i++)
            {
                string itemContent = (CbUnit.Items[i] as ComboBoxItem)?.Content?.ToString()
                                     ?? CbUnit.Items[i]?.ToString()
                                     ?? "";

                if (itemContent.Contains(unit))
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

            for (int i = 0; i < CbAirport.Items.Count; i++)
            {
                string? itemText = (CbAirport.Items[i] as ComboBoxItem)?.Content.ToString();
                if (itemText != null && itemText.StartsWith(icao, StringComparison.OrdinalIgnoreCase))
                {
                    CbAirport.SelectedIndex = i;
                    return;
                }
            }
            CbAirport.SelectedIndex = 0;
        }

        private string? GetSelectedAirportIcao()
        {
            string? fullContent = (CbAirport.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (string.IsNullOrEmpty(fullContent)) return "LKKB";
            return fullContent.Split(' ')[0];
        }

        // TLAČÍTKO: ULOŽIT TECHNIKA (Nahrazuje původní PrimaryButtonClick z WinUI)
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtErrorMessage.Visibility = Visibility.Collapsed;

            string firstName = TxtFirstName.Text.Trim();
            string lastName = TxtLastName.Text.Trim();

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                TxtErrorMessage.Text = "Jméno a příjmení technika jsou povinná pole!";
                TxtErrorMessage.Visibility = Visibility.Visible;
                return; // Okno zůstane otevřené, dokud to neopraví
            }

            string? unit = null;
            if (CbUnit.SelectedIndex >= 0)
            {
                string itemContent = (CbUnit.Items[CbUnit.SelectedIndex] as ComboBoxItem)?.Content?.ToString()
                                     ?? CbUnit.Items[CbUnit.SelectedIndex]?.ToString()
                                     ?? "";

                if (itemContent.Contains("SZP")) unit = "SZP";
                else if (itemContent.Contains("RNS")) unit = "RNS";
                else if (itemContent.Contains("RSP")) unit = "RSP";
                else if (itemContent.Contains("OSZ")) unit = "OSZ";
                else if (itemContent.Contains("LSLPS")) unit = "LSLPS";
            }

            string? airport = GetSelectedAirportIcao();
            int knowledgeClass = CbClass.SelectedIndex;

            // Bezpečné načtení data z WPF DatePickeru
            DateTime validUntil = DpValidUntil.SelectedDate ?? DateTime.Now.AddYears(1);

            string formattedLastName = lastName.ToUpper();
            string formattedFirstName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(firstName.ToLower());

            if (_editingPerson != null)
            {
                // UPDATE
                _editingPerson.Rank = string.IsNullOrWhiteSpace(TxtRank.Text) ? null : TxtRank.Text.Trim();
                _editingPerson.TitleBefore = string.IsNullOrWhiteSpace(TxtTitle.Text) ? null : TxtTitle.Text.Trim();
                _editingPerson.FirstName = formattedFirstName;
                _editingPerson.LastName = formattedLastName;
                _editingPerson.Unit = unit;
                _editingPerson.AirportIcao = airport;
                _editingPerson.KnowledgeClass = knowledgeClass;
                _editingPerson.ValidUntil = validUntil;

                ResultPerson = _editingPerson;
            }
            else
            {
                // INSERT
                ResultPerson = new Person
                {
                    Rank = string.IsNullOrWhiteSpace(TxtRank.Text) ? null : TxtRank.Text.Trim(),
                    TitleBefore = string.IsNullOrWhiteSpace(TxtTitle.Text) ? null : TxtTitle.Text.Trim(),
                    FirstName = formattedFirstName,
                    LastName = formattedLastName,
                    Unit = unit,
                    AirportIcao = airport,
                    KnowledgeClass = knowledgeClass,
                    ValidUntil = validUntil,
                    IsActive = true
                };
            }

            // Označíme, že dialog skončil úspěchem a zavřeme okno
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