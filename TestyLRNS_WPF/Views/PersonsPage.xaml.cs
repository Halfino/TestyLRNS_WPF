using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
// Předpokládám, že jsi pomocí Ctrl+R, Ctrl+R přejmenoval původní namespaces na tyto:
using TestyLRNS_WPF.Core;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Views
{
    public partial class PersonsPage : Page
    {
        private readonly PersonRepository _personRepository;
        private readonly User _currentUser;
        private List<Person> _allPersons = new();

        public PersonsPage()
        {
            this.InitializeComponent();
            _personRepository = new PersonRepository();

            // Pojistka, pokud by session z nějakého důvodu spadla
            if (SessionManager.CurrentUser != null)
            {
                _currentUser = SessionManager.CurrentUser;
                LoadData();
            }
        }

        private void LoadData()
        {
            var rawPersons = _personRepository.GetAllActive();

            if (_currentUser.Role != "SuperAdmin")
            {
                rawPersons = rawPersons.Where(p => p.AirportIcao == _currentUser.AirportIcao).ToList();
            }

            if (_currentUser.Role == "Instruktor")
            {
                rawPersons = rawPersons.Where(p => p.Unit == _currentUser.Unit).ToList();
            }

            _allPersons = rawPersons.OrderBy(p => p.LastName).ToList();
            LvPersons.ItemsSource = _allPersons;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string search = TxtSearch.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(search))
            {
                LvPersons.ItemsSource = _allPersons;
                return;
            }

            var filtered = _allPersons.Where(p =>
                p.FirstName.ToLower().Contains(search) ||
                p.LastName.ToLower().Contains(search) ||
                (p.Rank != null && p.Rank.ToLower().Contains(search))
            ).ToList();

            LvPersons.ItemsSource = filtered;
        }

        private void BtnAddPerson_Click(object sender, RoutedEventArgs e)
        {
            /* * WPF ZMĚNA: WPF používá pro dialogy standardní "Window". 
             * Až vytvoříš AddPersonDialog, udělej ho jako "Okno (WPF)".
             * Volání pak vypadá takto synchronně a čistě:
             */


            var dialog = new AddPersonDialog(_currentUser);
            
            // ShowDialog() zastaví kód a čeká, dokud uživatel okno nezavře
            bool? result = dialog.ShowDialog(); 

            if (result == true && dialog.ResultPerson != null)
            {
                _personRepository.Add(dialog.ResultPerson);
                LoadData(); 
            }
            
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Person selectedPerson)
            {

                var dialog = new AddPersonDialog(_currentUser, selectedPerson);
                
                bool? result = dialog.ShowDialog();

                if (result == true && dialog.ResultPerson != null)
                {
                    _personRepository.Update(dialog.ResultPerson);
                    LoadData(); 
                }
                
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Person selectedPerson)
            {
                // Zde ideálně přidat MessageBox s potvrzením smazání (Chcete opravdu smazat...?)
                MessageBoxResult confirm = MessageBox.Show(
                    $"Opravdu chcete smazat technika {selectedPerson.FullNameWithRank}?",
                    "Potvrzení smazání",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm == MessageBoxResult.Yes)
                {
                    _personRepository.SoftDelete(selectedPerson.Id);
                    LoadData();
                }
            }
        }
    }
}