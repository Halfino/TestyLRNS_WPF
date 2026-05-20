using ModernWpf.Controls;
using System;
using System.Windows;
using System.Windows.Controls;

namespace TestyLRNS_WPF
{
    public partial class MainWindow : Window
    {
        private bool _isSyncingAndClosing = false;

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

        // METODA ODHLÁŠENÍ
        public void Logout()
        {
            // 1. Tiše vymažeme aktuálního uživatele ze session paměti
            TestyLRNS_WPF.Core.SessionManager.Logout();

            // 2. Schováme panely bočního menu
            MainNavigation.IsPaneVisible = false;
            MainNavigation.IsSettingsVisible = false;

            // 3. Vyčistíme vnitřní obsah hlavní aplikace, aby nezůstal viset na pozadí
            ContentFrame.Content = null;

            // 4. Odkryjeme kořenový překryv a natvrdo do něj vložíme novou instanci přihlašování
            RootFrame.Visibility = Visibility.Visible;
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

        // Asynchronní synchronizace při zavření aplikace křížkem / Alt+F4
        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Pokud už ukládání na pozadí běží, ignorujeme další pokusy o zavření
            if (_isSyncingAndClosing)
            {
                e.Cancel = true;
                return;
            }

            // Ověříme, zda jsme online přes naši spolehlivou metodu z App třídy
            if (App.IsInternetAvailable())
            {
                // 1. Zastavíme okamžité zničení okna, abychom stihli odeslat data
                e.Cancel = true;
                _isSyncingAndClosing = true;

                // 2. Aktivujeme tmavý překryv s textem a točícím se načítáním
                SyncOverlay.Visibility = Visibility.Visible;

                try
                {
                    // 3. Spustíme odeslání všech lokálních změn (sync_status = 0) do Supabase
                    var syncService = new Services.SyncService();
                    await syncService.PushToServerAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Zálohování dat do cloudu nebylo dokončeno. Změny se bezpečně uloží lokálně a odešlou se při příštím spuštění.\n\nDetaily: {ex.Message}",
                        "Upozornění zálohy",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // 4. Jakmile je hotovo, zničíme celou aplikaci
                Application.Current.Shutdown();
            }
            // Pokud počítač vůbec nemá internet, e.Cancel zůstane false a okno se okamžitě zavře.
        }
    }
}