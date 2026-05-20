using System;
using System.Collections.Generic;

namespace TestyLRNS_WPF.Models
{
    public class TestResult
    {
        public int Id { get; set; }
        public int PersonId { get; set; }
        public DateTime DateGenerated { get; set; }
        public DateTime? DateCompleted { get; set; }
        public int? Score { get; set; }
        public int MaxScore { get; set; }
        public string? Note { get; set; }
        public string? PdfPath { get; set; }

        public int? GeneratedByUserId { get; set; }
        public int RandomSeed { get; set; }
        public string? TestType { get; set; }
        // Přidat do Person, Question, Answer, TestResult, SystemTopic, User:
        public string GlobalId { get; set; } = Guid.NewGuid().ToString();
        public int SyncStatus { get; set; } = 0; // 0 = Nové/Změněné, 1 = Synchronizováno
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public List<int> QuestionIds { get; set; } = new();
    }
}