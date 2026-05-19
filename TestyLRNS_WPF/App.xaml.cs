using System;
using System.Windows;
// Přidáme jmenný prostor, kde se nachází tvůj DatabaseHelper
using TestyLRNS_WPF.Data;

namespace TestyLRNS_WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Tímto jediným příkazem vnutíme tmavý režim napříč celou aplikací
            ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;

            // 2. Inicializace SQLite databáze (Vytvoření souboru, tabulek a nahrání výchozích dat)
            try
            {
                SQLitePCL.Batteries.Init();
                DatabaseHelper.InitializeDatabase();
            }
            catch (Exception ex)
            {
                // Pokud inicializace selže (např. zamčený soubor, práva disku), ukážeme přehledné hlášení
                MessageBox.Show(
                    $"Kritická chyba při spouštění aplikace. Nepodařilo se inicializovat databázi zkušebního systému:\n\n{ex.Message}",
                    "Chyba databáze LRNS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Ukončíme aplikaci, protože bez databáze by následně padaly všechny podstránky
                Application.Current.Shutdown();
            }
        }
    }
}