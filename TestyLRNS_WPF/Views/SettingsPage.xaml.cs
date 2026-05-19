using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// Zkontroluj, že tyto namespacy přesně sedí s tvým WPF projektem:
using TestyLRNS_WPF.Core;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;
using TestyLRNS_WPF.Services;

namespace TestyLRNS_WPF.Views
{
    public partial class SettingsPage : Page
    {
        private readonly UserRepository _userRepo;
        private readonly PersonRepository _personRepo;
        private readonly User _currentUser;
        private List<Person> _adminBasePersons = new();

        public SettingsPage()
        {
            this.InitializeComponent();
            _userRepo = new UserRepository();
            _personRepo = new PersonRepository();

            if (SessionManager.CurrentUser != null)
            {
                _currentUser = SessionManager.CurrentUser;

                InitializeAddUserForm();
                LoadAdminPersonsDropdown();
                RefreshUsersList();
            }
        }

        private void InitializeAddUserForm()
        {
            CbNewUnit.SelectedIndex = 0;
            CbPersonClass.SelectedIndex = 0;

            if (_currentUser.Role == "SuperAdmin")
            {
                CbNewRole.SelectedIndex = 0;
                CbNewAirport.SelectedIndex = 1;
                TxtUserFormDescription.Text = "Absolutní práva (SuperAdmin): Správa všech základen a všech typů účtů.";
            }
            else if (_currentUser.Role == "LokalniAdmin")
            {
                TxtUserFormDescription.Text = $"Správa základny {_currentUser.AirportIcao}. Novým instruktorům bude automaticky generován login ve formátu 'prijmeniprvnipismeno'.";

                CbNewRole.Items.Clear();
                CbNewRole.Items.Add(new ComboBoxItem { Content = "Instruktor" });
                CbNewRole.SelectedIndex = 0;
                CbNewRole.IsEnabled = false;

                LockAirportToCurrent();
            }
            else
            {
                BorderCreateUser.Visibility = Visibility.Collapsed;
                BorderUsersTable.Visibility = Visibility.Collapsed;
            }
        }

        private void LockAirportToCurrent()
        {
            CbNewAirport.Items.Clear();
            string currentBase = _currentUser.AirportIcao ?? "LKKB";
            CbNewAirport.Items.Add(new ComboBoxItem { Content = currentBase });
            CbNewAirport.SelectedIndex = 0;
            CbNewAirport.IsEnabled = false;
        }

        private void LoadAdminPersonsDropdown()
        {
            if (_currentUser.Role != "LokalniAdmin")
            {
                BorderAdminLink.Visibility = Visibility.Collapsed;
                return;
            }

            BorderAdminLink.Visibility = Visibility.Visible;
            CbAdminLinkedPerson.Items.Clear();
            CbAdminLinkedPerson.Items.Add("Žádná přidělená osoba (Nepárovat)");

            var rawPersons = _personRepo.GetAllActive()
                                        .Where(p => p.AirportIcao == _currentUser.AirportIcao)
                                        .OrderBy(p => p.LastName)
                                        .ToList();

            _adminBasePersons = rawPersons;

            int selectedIndex = 0;
            for (int i = 0; i < _adminBasePersons.Count; i++)
            {
                var person = _adminBasePersons[i];
                string rankDisplay = !string.IsNullOrEmpty(person.Rank) ? person.Rank + " " : "";
                string titleDisplay = !string.IsNullOrEmpty(person.TitleBefore) ? person.TitleBefore + " " : "";
                string unitDisplay = string.IsNullOrEmpty(person.Unit) ? "Bez odbornosti" : person.Unit;

                CbAdminLinkedPerson.Items.Add($"{rankDisplay}{titleDisplay}{person.LastName} {person.FirstName} ({unitDisplay})".Replace("  ", " ").Trim());

                if (_currentUser.LinkedPersonId.HasValue && person.Id == _currentUser.LinkedPersonId.Value)
                {
                    selectedIndex = i + 1;
                }
            }

            CbAdminLinkedPerson.SelectedIndex = selectedIndex;
        }

        private void RefreshUsersList()
        {
            if (_currentUser.Role != "SuperAdmin" && _currentUser.Role != "LokalniAdmin") return;

            var rawUsers = _userRepo.GetUsersForManagement(_currentUser.Role, _currentUser.AirportIcao);

            var viewModels = rawUsers.Select(u => new UserManagementViewModel
            {
                Id = u.Id,
                Username = u.Username,
                Role = u.Role,
                UnitString = string.IsNullOrEmpty(u.Unit) ? "Všeobecná / Admin" : u.Unit,
                AirportString = string.IsNullOrEmpty(u.AirportIcao) ? "Globální (Celá AČR)" : u.AirportIcao
            }).ToList();

            LvUsers.ItemsSource = viewModels;
        }

        private string RemoveDiacritics(string text)
        {
            string normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();

            foreach (char c in normalizedString)
            {
                System.Globalization.UnicodeCategory unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        private void CbNewRole_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string currentPass = TxtCurrentPassword.Password;
            string newPass = TxtNewPassword.Password;
            string confirmPass = TxtConfirmPassword.Password;

            if (string.IsNullOrWhiteSpace(currentPass) || string.IsNullOrWhiteSpace(newPass))
            {
                ShowPasswordMessage("Vyplňte všechna pole.", false);
                return;
            }
            if (newPass != confirmPass)
            {
                ShowPasswordMessage("Nové heslo a potvrzení se neshodují.", false);
                return;
            }

            if (SecurityService.HashPassword(currentPass) != _currentUser.PasswordHash)
            {
                ShowPasswordMessage("Současné heslo není správné.", false);
                return;
            }

            string hashedNewPass = SecurityService.HashPassword(newPass);
            _userRepo.UpdatePassword(_currentUser.Id, hashedNewPass);
            _currentUser.PasswordHash = hashedNewPass;

            ShowPasswordMessage("Heslo bylo úspěšně změněno.", true);
            TxtCurrentPassword.Password = ""; TxtNewPassword.Password = ""; TxtConfirmPassword.Password = "";
        }

        private void BtnSaveAdminLink_Click(object sender, RoutedEventArgs e)
        {
            TxtAdminLinkMessage.Visibility = Visibility.Collapsed;

            int? selectedPersonId = null;
            if (CbAdminLinkedPerson.SelectedIndex > 0)
            {
                selectedPersonId = _adminBasePersons[CbAdminLinkedPerson.SelectedIndex - 1].Id;
            }

            try
            {
                _userRepo.LinkUserToPerson(_currentUser.Id, selectedPersonId);
                _currentUser.LinkedPersonId = selectedPersonId;

                ShowAdminLinkMessage("Odpovědná osoba byla úspěšně aktualizována.", true);
            }
            catch (Exception ex)
            {
                ShowAdminLinkMessage($"Chyba databáze při ukládání: {ex.Message}", false);
            }
        }

        private void BtnCreateUser_Click(object sender, RoutedEventArgs e)
        {
            string firstName = TxtNewFirstName.Text.Trim();
            string lastName = TxtNewLastName.Text.Trim();
            string password = TxtNewUserPassword.Password;

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(password))
            {
                ShowUserMessage("Vyplňte jméno, příjmení a výchozí heslo.", false);
                return;
            }

            string role = "";
            string? airport = null;

            if (_currentUser.Role == "LokalniAdmin")
            {
                role = "Instruktor";
                airport = _currentUser.AirportIcao;
            }
            else
            {
                var selectedRoleItem = CbNewRole.SelectedItem as ComboBoxItem;
                role = selectedRoleItem?.Content?.ToString() ?? "Instruktor";

                if (role != "SuperAdmin")
                {
                    if (CbNewAirport.SelectedIndex == 0) { ShowUserMessage("Vyberte konkrétní letiště!", false); return; }
                    airport = (CbNewAirport.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Split(' ')[0];
                }
            }

            string finalUsername = "";

            if (role == "LokalniAdmin")
            {
                finalUsername = airport!.ToLower();
            }
            else
            {
                string cleanLastName = RemoveDiacritics(lastName).ToLower().Replace(" ", "");
                string firstLetter = firstName.Length > 0 ? RemoveDiacritics(firstName.Substring(0, 1)).ToLower() : "";
                finalUsername = $"{cleanLastName}{firstLetter}";
            }

            TxtGeneratedLoginPreview.Text = finalUsername;

            if (_userRepo.UserExists(finalUsername))
            {
                ShowUserMessage($"Uživatel s loginem '{finalUsername}' již existuje.", false);
                return;
            }

            string? unit = null;
            if (role == "Instruktor" && CbNewUnit.SelectedIndex > 0)
            {
                unit = (CbNewUnit.SelectedItem as ComboBoxItem)?.Content?.ToString();
            }

            int? createdPersonId = null;

            if (role == "Instruktor")
            {
                int personKnowledgeClass = CbPersonClass.SelectedIndex == 1 ? 5 : 4;

                var newPerson = new Person
                {
                    Rank = string.IsNullOrWhiteSpace(TxtNewRank.Text) ? null : TxtNewRank.Text.Trim(),
                    TitleBefore = string.IsNullOrWhiteSpace(TxtNewTitle.Text) ? null : TxtNewTitle.Text.Trim(),
                    FirstName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(firstName.ToLower()),
                    LastName = lastName.ToUpper(),
                    KnowledgeClass = personKnowledgeClass,
                    ValidUntil = DateTime.Now.AddYears(1),
                    Unit = unit,
                    AirportIcao = airport,
                    IsActive = true
                };

                createdPersonId = _personRepo.Add(newPerson);
            }

            var newUser = new User
            {
                Username = finalUsername,
                PasswordHash = SecurityService.HashPassword(password),
                Role = role,
                Unit = unit,
                AirportIcao = airport,
                LinkedPersonId = createdPersonId
            };

            int createdUserId = _userRepo.AddUser(newUser);

            if (createdPersonId.HasValue)
            {
                _userRepo.LinkUserToPerson(createdUserId, createdPersonId.Value);
            }

            ShowUserMessage($"Účet '{finalUsername}' i karta technika byly úspěšně vytvořeny.", true);

            TxtNewRank.Text = ""; TxtNewTitle.Text = ""; TxtNewFirstName.Text = ""; TxtNewLastName.Text = ""; TxtNewUserPassword.Password = "";
            TxtGeneratedLoginPreview.Text = "Zadejte jméno...";

            RefreshUsersList();
        }

        private void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int userId)
            {
                _userRepo.DeleteUser(userId);
                RefreshUsersList();
            }
        }

        private void ShowPasswordMessage(string text, bool isSuccess)
        {
            TxtPasswordMessage.Text = text;
            TxtPasswordMessage.Foreground = new SolidColorBrush(isSuccess ? Colors.LightGreen : Colors.Salmon);
            TxtPasswordMessage.Visibility = Visibility.Visible;
        }

        private void ShowAdminLinkMessage(string text, bool isSuccess)
        {
            TxtAdminLinkMessage.Text = text;
            TxtAdminLinkMessage.Foreground = new SolidColorBrush(isSuccess ? Colors.LightGreen : Colors.Salmon);
            TxtAdminLinkMessage.Visibility = Visibility.Visible;
        }

        private void ShowUserMessage(string text, bool isSuccess)
        {
            TxtUserMessage.Text = text;
            TxtUserMessage.Foreground = new SolidColorBrush(isSuccess ? Colors.LightGreen : Colors.Salmon);
            TxtUserMessage.Visibility = Visibility.Visible;
        }

        public class UserManagementViewModel
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string UnitString { get; set; } = string.Empty;
            public string AirportString { get; set; } = string.Empty;
        }
    }
}