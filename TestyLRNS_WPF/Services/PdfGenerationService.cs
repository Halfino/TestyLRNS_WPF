using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Services
{
    public class PdfGenerationService
    {
        public PdfGenerationService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public void GenerateTestPdf(string outputPath, TestResult testResult, Person person, User instructor, List<Question> questions)
        {
            var processedQuestions = PreprocessQuestions(testResult.RandomSeed, questions);

            // --- NAČTENÍ JMÉNA ZADAVATELE ---
            string creatorFullName = instructor.Username; // Výchozí záloha (login)
            if (instructor.LinkedPersonId.HasValue)
            {
                try
                {
                    var personRepo = new TestyLRNS_WPF.Data.Repositories.PersonRepository();
                    var creatorPerson = personRepo.GetById(instructor.LinkedPersonId.Value);
                    if (creatorPerson != null && !string.IsNullOrWhiteSpace(creatorPerson.FullNameWithRank))
                    {
                        creatorFullName = creatorPerson.FullNameWithRank;
                    }
                }
                catch { /* Ignorovat chybu DB, zůstane login */ }
            }

            Document.Create(container =>
            {
                // ======================================================
                // STRANA 1: TITULNÍ STRANA
                // ======================================================
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    var baseInfo = GetBaseInfo(person.AirportIcao);

                    // HORNÍ ČÁST (Vycentrovaná základna)
                    page.Header().Column(h =>
                    {
                        h.Item().AlignCenter().Text(baseInfo.Name).Bold().FontSize(14);
                        h.Item().AlignCenter().Text(baseInfo.Details).FontSize(10);
                    });

                    // PROSTŘEDNÍ ČÁST (Plně vertikálně a horizontálně vycentrovaná)
                    page.Content().AlignMiddle().Column(c =>
                    {
                        string formattedType = GetFormattedTestType(testResult.TestType, questions);
                        // Zde je text typu testu (např. vč. odřádkování) vycentrovaný
                        c.Item().AlignCenter().Text(formattedType).ExtraBold().FontSize(22).AlignCenter();
                        c.Item().PaddingTop(40);

                        string rankPart = !string.IsNullOrEmpty(person.Rank) ? person.Rank + " " : "";
                        string titlePart = !string.IsNullOrEmpty(person.TitleBefore) ? person.TitleBefore + " " : "";
                        string fullMilitaryName = $"{rankPart}{titlePart}{person.LastName} {person.FirstName}".Trim();

                        c.Item().AlignCenter().Text($"Testovaný: {fullMilitaryName}").Bold().FontSize(18);
                        // Zde používáme správný převodník třídy přímo z modelu Person!
                        c.Item().AlignCenter().PaddingTop(8).Text($"Odbornost: {person.Unit ?? "Všeobecná"}  |  Třída: {person.KnowledgeClassText}").FontSize(14);
                    });

                    // SPODNÍ ČÁST (Rozdělení na levou a pravou část pomocí Row)
                    page.Footer().Row(row =>
                    {
                        // LEVÁ STRANA: Zadavatel (Zarovnáno dolů)
                        row.RelativeItem().AlignBottom().AlignLeft().Column(l =>
                        {
                            l.Item().Text("Test vytvořil:").FontSize(9).FontColor(Colors.Grey.Medium);
                            l.Item().Text(creatorFullName).FontSize(10).Bold();
                        });

                        // PRAVÁ STRANA: Původní podpisy (Pevná šířka 260)
                        row.ConstantItem(260).AlignRight().Column(f =>
                        {
                            f.Item().Text("Mez úspěšnosti: 80 %").Bold();
                            f.Item().PaddingTop(10).Text("Datum: .......................................");
                            f.Item().PaddingTop(10).Text("Hodnocení: .......................................").Bold();
                            f.Item().PaddingTop(10).Text($"Instruktor/Inspektor: .......................................");
                            f.Item().PaddingTop(25).Text("S hodnocením souhlasím: .......................................");
                        });
                    });
                });

                // ======================================================
                // STRANA 2: PRÁZDNÁ STRANA (pro oboustranný tisk)
                // ======================================================
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Content().Text(" "); // Pro QuestPDF zde musí být alespoň "mezera", aby stranu vykreslil
                });

                // ======================================================
                // STRANA 3 A DÁL: ZKUŠEBNÍ TEST
                // ======================================================
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    // Decentní navigace na každé stránce testu
                    page.Header().Text($"Zkušební test  |  {person.LastName} {person.FirstName}").FontSize(9).FontColor(Colors.Grey.Medium);

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        ComposeTestContent(col, processedQuestions);
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Strana "); x.CurrentPageNumber(); });
                });

                // ======================================================
                // KLÍČ SPRÁVNÝCH ODPOVĚDÍ (Na nové straně)
                // ======================================================
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Text($"Klíč správných odpovědí  |  {person.LastName} {person.FirstName}").FontSize(9).FontColor(Colors.Grey.Medium);

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        ComposeAnswerKey(col, processedQuestions);
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Strana "); x.CurrentPageNumber(); });
                });
            })
            .GeneratePdf(outputPath);
        }

        // --- POMOCNÉ METODY PRO VYKRESLOVÁNÍ ---

        private void ComposeTestContent(ColumnDescriptor col, List<ProcessedQuestion> questions)
        {
            foreach (var q in questions)
            {
                col.Item().PaddingBottom(15).Column(qCol =>
                {
                    // Text otázky
                    qCol.Item().Text($"{q.Number}. {q.Question.Text}").Bold();

                    // Pokud má otázka definované odpovědi (Uzavřená otázka)
                    if (q.ShuffledAnswers != null && q.ShuffledAnswers.Any())
                    {
                        for (int i = 0; i < q.ShuffledAnswers.Count; i++)
                        {
                            char letter = (char)('A' + i);
                            qCol.Item().PaddingTop(4).PaddingLeft(15).Text($"{letter}) {q.ShuffledAnswers[i].Text}");
                        }
                    }
                    else
                    {
                        // Otevřená otázka - vygenerujeme linky pro ruční dopsání odpovědi
                        qCol.Item().PaddingTop(20).PaddingLeft(15).PaddingRight(30).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        qCol.Item().PaddingTop(25).PaddingLeft(15).PaddingRight(30).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        qCol.Item().PaddingTop(25).PaddingLeft(15).PaddingRight(30).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    }
                });
            }
        }

        private void ComposeAnswerKey(ColumnDescriptor col, List<ProcessedQuestion> questions)
        {
            col.Item().PaddingBottom(20).Text("Klíč správných odpovědí").Bold().FontSize(16);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(50); // Sloupec pro číslo
                    columns.RelativeColumn();   // Sloupec pro písmeno / instrukci
                });

                foreach (var q in questions)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{q.Number}.");

                    if (q.ShuffledAnswers != null && q.ShuffledAnswers.Any())
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(q.CorrectLetter.ToString()).Bold();
                    }
                    else
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("(Otevřená otázka - nutné individuální vyhodnocení)").FontColor(Colors.Grey.Medium);
                    }
                }
            });
        }

        // --- POMOCNÉ TŘÍDY A LOGIKA ---

        // Převede systémový název testu na ten armádní vycentrovaný formát
        private string GetFormattedTestType(string? rawType, List<Question> questions)
        {
            if (string.IsNullOrEmpty(rawType)) return "ZKUŠEBNÍ TEST";

            if (rawType.Contains("Průřezový"))
                return "Průřezový test";

            if (rawType.Contains("Teorie"))
                return "Typový výcvik - postupový test\nTeorie";

            if (rawType.Contains("Provozní"))
                return "Typový výcvik - postupový test\nProvozní výcvik";

            if (rawType.Contains("Závěrečný"))
                return "Typový výcvik\nZávěrečný test";

            if (rawType.Contains("Průběžný"))
            {
                // Pokusí se najít téma z otázek
                var topics = questions.Where(q => !string.IsNullOrEmpty(q.SystemTopic))
                                      .Select(q => q.SystemTopic)
                                      .Distinct()
                                      .ToList();

                string topicStr = topics.Count == 1 ? topics.First()! : "";
                return $"Průběžný test\nTéma: {topicStr}";
            }

            return rawType.ToUpper();
        }

        private (string Name, string Details) GetBaseInfo(string? icao)
        {
            return icao switch
            {
                "LKKB" => ("24. základna dopravního letectva Praha - Kbely", "Mladoboleslavská 300, Praha 9 - Kbely  |  Datová schránka: hjyaavk"),
                "LKCV" => ("21. základna taktického letectva Čáslav", "Chotusice, Čáslav  |  Datová schránka: hjyaavk"),
                "LKNA" => ("22. základna vrtulníkového letectva Náměšť nad Oslavou", "Sedlec, Vícenice u Náměště nad Oslavou  |  Datová schránka: hjyaavk"),
                "LKPD" => ("Správa letiště Pardubice", "Pražská 100, Pardubice  |  Datová schránka: hjyaavk"),
                _ => ("Vojenský útvar AČR", "Adresa útvaru  |  Datová schránka: 0000000")
            };
        }

        // --- EXPORT A OTEVÍRÁNÍ (Zůstává beze změny) ---

        public string GetExportFilePath(Person person, string testType, out string directoryPath)
        {
            string rootPath = AppContext.BaseDirectory;

            string odbornost = SanitizeForPath(person.Unit ?? "Vseobecna");
            string rok = DateTime.Now.ToString("yyyy");
            string mesic = DateTime.Now.ToString("MM");

            directoryPath = Path.Combine(rootPath, "testy", odbornost, rok, mesic);
            Directory.CreateDirectory(directoryPath);

            string druhTestu = SanitizeForPath(testType);
            string jmenoTestovaneho = SanitizeForPath($"{person.LastName}_{person.FirstName}");
            string datumVytvoreni = DateTime.Now.ToString("ddMMyyyy_HHmm");

            string fileName = $"test_{druhTestu}_{jmenoTestovaneho}_{datumVytvoreni}.pdf";
            return Path.Combine(directoryPath, fileName);
        }

        private string SanitizeForPath(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "nezname";

            string normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();

            foreach (char c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            string clean = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[^a-zA-Z0-9]+", "_");

            return clean.ToLower().Trim('_');
        }

        public void OpenPdfFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
        }

        public void OpenFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true });
            }
        }

        private class ProcessedQuestion
        {
            public int Number { get; set; }
            public Question Question { get; set; } = null!;
            public List<Answer> ShuffledAnswers { get; set; } = new();
            public char CorrectLetter { get; set; }
        }

        private List<ProcessedQuestion> PreprocessQuestions(int seed, List<Question> rawQuestions)
        {
            var random = new Random(seed);
            var result = new List<ProcessedQuestion>();
            int qNumber = 1;

            foreach (var q in rawQuestions)
            {
                var processed = new ProcessedQuestion
                {
                    Number = qNumber++,
                    Question = q
                };

                if (q.Answers != null && q.Answers.Any())
                {
                    var shuffled = q.Answers.OrderBy(x => random.Next()).ToList();
                    char correctLetter = 'A';

                    for (int i = 0; i < shuffled.Count; i++)
                    {
                        if (shuffled[i].IsCorrect)
                        {
                            correctLetter = (char)('A' + i);
                            break;
                        }
                    }

                    processed.ShuffledAnswers = shuffled;
                    processed.CorrectLetter = correctLetter;
                }

                result.Add(processed);
            }

            return result;
        }
    }
}