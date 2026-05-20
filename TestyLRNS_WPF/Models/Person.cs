using System;
using System.ComponentModel;

namespace TestyLRNS_WPF.Models
{
    public class Person
    {
        public int Id { get; set; }
        public string? Rank { get; set; }          // Hodnost
        public string? TitleBefore { get; set; }    // Titul před jménem
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int KnowledgeClass { get; set; }
        public DateTime ValidUntil { get; set; }
        public string? Unit { get; set; }
        public string? AirportIcao { get; set; }
        public bool IsActive { get; set; }
        // Přidat do Person, Question, Answer, TestResult, SystemTopic, User:
        public string GlobalId { get; set; } = Guid.NewGuid().ToString();
        public int SyncStatus { get; set; } = 0; // 0 = Nové/Změněné, 1 = Synchronizováno
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        // Pomocná vlastnost, která složí celé jméno i s hodností pro zobrazení v roletkách nebo tabulkách
        public string FullNameWithRank =>
            $"{Rank} {TitleBefore} {LastName} {FirstName}".Replace("  ", " ").Trim();

        // SLOVNÍ NÁHRADA TŘÍDY ZNALOSTÍ
        public string KnowledgeClassText => KnowledgeClass switch
        {
            0 => "Typový výcvik",
            1 => "3. třída",
            2 => "2. třída",
            3 => "1. třída",
            4 => "Instruktor",
            5 => "Inspektor",
            _ => $"{KnowledgeClass}. třída" // Výchozí záloha, pokud by v DB bylo jiné číslo
        };

        // Předpokládám, že ValidUntilString už máš vyřešené podobně:
        public string ValidUntilString => ValidUntil.ToString("dd.MM.yyyy");
    }
}
