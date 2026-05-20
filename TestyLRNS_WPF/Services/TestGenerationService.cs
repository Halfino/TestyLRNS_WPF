using System;
using System.Collections.Generic;
using System.Linq;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Services
{
    public class TestGenerationService
    {
        private readonly QuestionRepository _questionRepo;

        public TestGenerationService()
        {
            _questionRepo = new QuestionRepository();
        }




        /// <summary>
        /// Vygeneruje unikátní test pro technika podle striktních byznys pravidel (20/80, RBAC, Odbornost).
        /// </summary>
        public TestResult GenerateTest(
            Person person,
            User generatedBy,
            string testType,
            int questionCount,
            List<string>? allowedTopics = null,
            bool includeOperationalTraining = false,
            bool onlyOperationalTraining = false) // Přidáno pro specifické "Provozní" testy
        {
            // --- 1. BEZPEČNOSTNÍ ZÁMKY (RBAC) ---
            if (generatedBy.Role == "Instruktor")
            {
                // Instruktor smí generovat jen pro své letiště a svou odbornost
                if (generatedBy.AirportIcao != person.AirportIcao || generatedBy.Unit != person.Unit)
                {
                    throw new UnauthorizedAccessException("Instruktor smí generovat testy pouze pro techniky ze své základny a ze své vlastní odbornosti.");
                }
            }
            else if (generatedBy.Role == "LokalniAdmin")
            {
                // Lokální admin smí generovat jen pro své letiště, ale napříč všemi odbornostmi (SZP, RNS...)
                if (generatedBy.AirportIcao != person.AirportIcao)
                {
                    throw new UnauthorizedAccessException("Lokální administrátor smí generovat testy pouze pro příslušníky své základny.");
                }
            }
            // SuperAdmin má absolutní volnost, kontroly se ho netýkají.

            // --- 2. STANOVENÍ CÍLOVÉ ZNALOSTNÍ TŘÍDY (Převod na DB hodnoty) ---
            // Db hodnoty: 1 = 3. třída, 2 = 2. třída, 3 = 1. třída.
            int targetDbClass = person.KnowledgeClass switch
            {
                0 => 1, // Typový výcvik / Žák -> zkouší se na 3. třídu
                4 => 3, // Instruktor -> zkouší se jako 1. třída
                5 => 3, // Inspektor -> zkouší se jako 1. třída
                _ => person.KnowledgeClass // Standardní 1, 2, nebo 3
            };

            // --- 3. NAČTENÍ A PŘEDFILTRACE POOLU OTÁZEK ---
            // Repo nám vytáhne aktivní otázky pro danou odbornost (a "Všeobecné") a letiště (a "Globální")
            var allQuestions = _questionRepo.GetAllActive(person.Unit, person.AirportIcao);

            // Filtrace podle témat (Průběžný test vs Průřezový)
            if (allowedTopics != null && allowedTopics.Any())
            {
                allQuestions = allQuestions.Where(q =>
                    (q.SystemTopic != null && allowedTopics.Contains(q.SystemTopic)) ||
                    (q.SystemTopic == null && allowedTopics.Contains("Obecná"))
                ).ToList();
            }

            // Filtrace provozního výcviku
            if (onlyOperationalTraining)
            {
                allQuestions = allQuestions.Where(q => q.IsOperationalTraining).ToList();
            }
            else if (!includeOperationalTraining)
            {
                allQuestions = allQuestions.Where(q => !q.IsOperationalTraining).ToList();
            }

            // Odstranění otázek, které přesahují znalosti technika
            var validQuestions = allQuestions.Where(q => q.KnowledgeClass <= targetDbClass).ToList();

            if (validQuestions.Count < questionCount)
            {
                throw new InvalidOperationException($"V databázi není dostatek otázek odpovídajících kritériím. Požadováno: {questionCount}, Nalezeno jen: {validQuestions.Count}.");
            }

            // --- 4. ROZDĚLENÍ 20% (Target) / 80% (Lower) ---
            var exactClassQs = validQuestions.Where(q => q.KnowledgeClass == targetDbClass).ToList();
            var lowerClassQs = validQuestions.Where(q => q.KnowledgeClass < targetDbClass).ToList();

            int exactCount = (int)Math.Ceiling(questionCount * 0.20);
            int lowerCount = questionCount - exactCount;

            // Výjimka pro 3. třídu (DB=1): nemá žádnou "nižší" třídu, takže 100% jde z exactClass
            if (targetDbClass == 1)
            {
                exactCount = questionCount;
                lowerCount = 0;
            }

            var random = new Random();
            var selectedQs = new List<Question>();

            // --- 5. NÁHODNÝ VÝBĚR A SHUFFLING OTÁZEK ---
            // Vezmeme 20 % z přesné třídy (náhodně zamíchaných)
            selectedQs.AddRange(exactClassQs.OrderBy(x => random.Next()).Take(exactCount));

            // Vezmeme zbytek z nižších tříd
            if (lowerCount > 0)
            {
                selectedQs.AddRange(lowerClassQs.OrderBy(x => random.Next()).Take(lowerCount));
            }

            // Záchranná brzda (pokud v daných "šuplících" nebylo přesně dost otázek, dober z celého validního poolu, bez duplicit)
            if (selectedQs.Count < questionCount)
            {
                int remainingNeeded = questionCount - selectedQs.Count;
                var unusedQs = validQuestions.Except(selectedQs).OrderBy(x => random.Next()).ToList();
                selectedQs.AddRange(unusedQs.Take(remainingNeeded));
            }

            // Finální zamíchání vybraných otázek (aby 20 % těžkých nebylo vždy na začátku testu)
            selectedQs = selectedQs.OrderBy(x => random.Next()).ToList();

            // --- 6. SESTAVENÍ VÝSLEDKU ---
            var result = new TestResult
            {
                PersonId = person.Id,
                DateGenerated = DateTime.Now,
                GeneratedByUserId = generatedBy.Id,
                TestType = testType,
                RandomSeed = random.Next(), // Seed pro náhodné zamíchání A/B/C odpovědí při tisku PDF
                MaxScore = selectedQs.Count,
                QuestionIds = selectedQs.Select(q => q.Id).ToList()
            };

            return result;
        }
    }
}