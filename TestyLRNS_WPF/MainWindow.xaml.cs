using ModernWpf.Controls;
using System;
using System.Windows;
using System.Windows.Controls;

namespace TestyLRNS_WPF
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            // START APLIKACE: Schováme hlavní menu a načteme přihlašovací stránku přes celou obrazovku
            MainNavigation.IsPaneVisible = false;
            MainNavigation.IsSettingsVisible = false;

            // Načteme LoginPage do kořenového překryvu
            RootFrame.Visibility = Visibility.Visible;
            RootFrame.Content = new Views.LoginPage();
        }

        // Tuto metodu zavolá LoginPage, jakmile instruktor zadá správné údaje
        public void CompleteLogin()
        {
            // Schováme přihlašovací frame
            RootFrame.Visibility = Visibility.Collapsed;
            RootFrame.Content = null;

            // Zobrazíme boční navigační panel
            MainNavigation.IsPaneVisible = true;
            MainNavigation.IsSettingsVisible = true;

            // Automaticky skočíme na domovskou stránku (Dashboard)
            MainNavigation.SelectedItem = MainNavigation.MenuItems[0];
            NavigateTo("Dashboard");
        }

        private void MainNavigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                NavigateTo("Settings");
            }
            else
            {
                var item = args.InvokedItemContainer as NavigationViewItem;
                if (item != null && item.Tag != null)
                {
                    string tag = item.Tag.ToString();

                    if (tag == "Logout")
                    {
                        Logout();
                        return;
                    }

                    NavigateTo(tag);
                }
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            Logout();
        }

        // OPRAVENÁ METODA ODHLÁŠENÍ
        public void Logout()
        {
            // 1. Tiše vymažeme aktuálního uživatele ze session paměti
            TestyLRNS_WPF.Core.SessionManager.Logout();

            // 2. Schováme panely bočního menu (přesně stejný princip jako při startu v konstruktoru)
            MainNavigation.IsPaneVisible = false;
            MainNavigation.IsSettingsVisible = false;

            // 3. Vyčistíme vnitřní obsah hlavní aplikace, aby nezůstal viset na pozadí
            ContentFrame.Content = null;

            // 4. Odkryjeme kořenový překryv a natvrdo do něj vložíme novou čistou instanci přihlašování
            RootFrame.Visibility = Visibility.Visible;

            // WPF ZMĚNA: Přímé přiřazení do .Content obchází navigační cache a okamžitě LoginPage vykreslí
            RootFrame.Content = new Views.LoginPage();
        }

        private void NavigateTo(string pageTag)
        {
            // Přepínání stránek ve WPF
            if (pageTag == "Dashboard")
            {
                ContentFrame.Navigate(new Views.DashboardPage());
            }
            else if (pageTag == "Persons")
            {
                ContentFrame.Navigate(new Views.PersonsPage());
            }
            else if (pageTag == "Questions")
            {
                ContentFrame.Navigate(new Views.QuestionsPage());
            }
            else if (pageTag == "Generator")
            {
                ContentFrame.Navigate(new Views.GeneratorPage());
            }
            else if (pageTag == "History")
            {
                ContentFrame.Navigate(new Views.TestHistoryPage());
            }
            else if (pageTag == "Settings")
            {
                ContentFrame.Navigate(new Views.SettingsPage());
            }
            else
            {
                ContentFrame.Content = new TextBlock
                {
                    Text = $"Zde se brzy načte obsah pro: {pageTag}",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }
    }
}