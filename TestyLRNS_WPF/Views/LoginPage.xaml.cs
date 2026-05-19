using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Nutné pro KeyEventArgs a Key
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Core;

namespace TestyLRNS_WPF.Views
{
    public partial class LoginPage : Page
    {
        private readonly UserRepository _userRepository;

        public LoginPage()
        {
            this.InitializeComponent();
            _userRepository = new UserRepository();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e) => PerformLogin();

        // WPF ZMĚNA: Použití nativních KeyEventArgs místo WinUI verzí
        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) PerformLogin();
        }

        private void PerformLogin()
        {
            TxtError.Visibility = Visibility.Collapsed;

            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password.Trim(); // WPF PasswordBox má vlastnost Password jako string

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                TxtError.Text = "Vyplňte jméno i heslo.";
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            var user = _userRepository.Authenticate(username, password);

            if (user != null)
            {
                SessionManager.Login(user);

                // Přepne zobrazení v hlavním okně
                MainWindow.Instance.CompleteLogin();
            }
            else
            {
                TxtError.Text = "Nesprávné uživatelské jméno nebo heslo.";
                TxtError.Visibility = Visibility.Visible;
            }
        }
    }
}