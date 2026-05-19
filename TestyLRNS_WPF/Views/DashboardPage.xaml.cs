using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
// Tvoje upravené namespacy pro nový WPF projekt
using TestyLRNS_WPF.Core;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Views
{
    public partial class DashboardPage : Page
    {
        private readonly PersonRepository _personRepo;
        private readonly QuestionRepository _questionRepo;
        private readonly TestResultRepository _testResultRepo;
        private readonly User _currentUser;

        public DashboardPage()
        {
            this.InitializeComponent();

            _personRepo = new PersonRepository();
            _questionRepo = new QuestionRepository();
            _testResultRepo = new TestResultRepository();

            if (SessionManager.CurrentUser != null)
            {
                _currentUser = SessionManager.CurrentUser;
                LoadDashboardData();
            }
        }

        private void LoadDashboardData()
        {
            string? filterUnit = _currentUser.Role == "Instruktor" ? _currentUser.Unit : null;
            string? filterIcao = _currentUser.Role == "SuperAdmin" ? null : _currentUser.AirportIcao;

            // 1. OTÁZKY TVÉ ODBORNOSTÍ
            var allQuestions = _questionRepo.GetAllActive(filterUnit, filterIcao);
            TxtTotalQuestionsCount.Text = allQuestions.Count.ToString();

            // 2. EXPIRUJÍCÍ TECHNICI
            var allPersons = _personRepo.GetAllActive(filterUnit, filterIcao);
            var expiringDateThreshold = DateTime.Now.Date.AddMonths(9);

            var expiringPersons = allPersons
                .Where(p => p.ValidUntil.Date <= expiringDateThreshold)
                .OrderBy(p => p.ValidUntil)
                .Select(p => new
                {
                    Name = p.FullNameWithRank,
                    Unit = p.Unit ?? "Všeobecná",
                    ValidUntilString = FormatExpiration(p.ValidUntil)
                })
                .ToList();

            TxtExpiringCount.Text = expiringPersons.Count.ToString();
            LvExpiringPersons.ItemsSource = expiringPersons;

            // 3. TESTY K VYHODNOCENÍ
            var pendingTests = _testResultRepo.GetPendingTests(_currentUser);

            foreach (var pt in pendingTests)
            {
                int bracketIndex = pt.TestType.IndexOf('(');
                if (bracketIndex > 0)
                {
                    pt.TestType = pt.TestType.Substring(0, bracketIndex).Trim();
                }
            }

            TxtPendingTestsCount.Text = pendingTests.Count.ToString();
            LvPendingTests.ItemsSource = pendingTests;
        }

        private string FormatExpiration(DateTime validUntil)
        {
            int daysRemaining = (validUntil.Date - DateTime.Today).Days;
            string relativeText = "";

            if (daysRemaining < 0) relativeText = "Propadlo!";
            else if (daysRemaining == 0) relativeText = "Končí dnes!";
            else if (daysRemaining == 1) relativeText = "Končí zítra";
            else if (daysRemaining > 60)
            {
                int months = (int)Math.Round(daysRemaining / 30.44);
                if (months >= 2 && months <= 4) relativeText = $"za {months} měsíce";
                else relativeText = $"za {months} měsíců";
            }
            else if (daysRemaining >= 30) relativeText = "za 1 měsíc";
            else
            {
                if (daysRemaining > 1 && daysRemaining < 5) relativeText = $"za {daysRemaining} dny";
                else relativeText = $"za {daysRemaining} dní";
            }

            return $"{relativeText} ({validUntil.ToString("dd.MM.yyyy")})";
        }

        // AKCE: ZADAT VÝSLEDEK (Upraveno pro čisté WPF Window)
        private void BtnEvaluate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is PendingTestDto pendingTest)
            {
                var fullTest = _testResultRepo.GetById(pendingTest.TestId);
                if (fullTest == null) return;

                /* WPF ZMĚNA: EvaluateTestDialog předěláme na klasické WPF Window.
                 * Volání pak bude synchronní přes ShowDialog() stejně jako u techniků.
                 */

                var dialog = new EvaluateTestDialog(pendingTest.TestId, pendingTest.PersonName, pendingTest.TestType, fullTest.MaxScore);
                dialog.Owner = Window.GetWindow(this); // Vycentrování nad hlavní aplikací

                if (dialog.ShowDialog() == true)
                {
                    // Po úspěšném uložení živě překreslíme čísla na Dashboardu
                    LoadDashboardData();
                }
               
            }
        }
    }
}