using System;
using System.Windows;
using System.Net.NetworkInformation;
using System.Threading.Tasks; // Přidáno pro Task.Run
using TestyLRNS_WPF.Data;
using TestyLRNS_WPF.Services;

namespace TestyLRNS_WPF
{
    public partial class App : Application
    {
        // PŘIDÁNO 'async' aby zde fungoval 'await'
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;

            try
            {
                SQLitePCL.Batteries.Init();
                DatabaseHelper.InitializeDatabase();

                // KONTROLA INTERNETU A PULL PŘI STARTU
                if (IsInternetAvailable())
                {
                    // Vytvoření instance SyncService
                    var syncService = new SyncService();
                    await syncService.PullFromServerAsync();
                }
                else
                {
                    MessageBox.Show(
                        "Počítač není připojen k internetu. Aplikace běží v plném offline režimu. Synchronizace dat se serverem nyní není možná.",
                        "Offline režim",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kritická chyba při spouštění: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // Velmi rychlá a spolehlivá kontrola konektivity
        public static bool IsInternetAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    // Zkusíme "pingnout" Google DNS (8.8.8.8) s limitem 2000 ms
                    PingReply reply = ping.Send("8.8.8.8", 2000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}