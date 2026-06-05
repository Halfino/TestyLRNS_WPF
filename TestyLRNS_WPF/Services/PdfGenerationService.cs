using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
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

                    page.Header().Column(h =>
                    {
                        h.Item().AlignCenter().Text(baseInfo.Name).Bold().FontSize(14);
                        h.Item().AlignCenter().Text(baseInfo.Details).FontSize(10);
                    });

                    page.Content().AlignMiddle().Column(c =>
                    {
                        string formattedType = GetFormattedTestType(testResult.TestType, questions);
                        c.Item().AlignCenter().Text(formattedType).ExtraBold().FontSize(22).AlignCenter();
                        c.Item().PaddingTop(40);

                        string rankPart = !string.IsNullOrEmpty(person.Rank) ? person.Rank + " " : "";
                        string titlePart = !string.IsNullOrEmpty(person.TitleBefore) ? person.TitleBefore + " " : "";
                        string fullMilitaryName = $"{rankPart}{titlePart}{person.LastName} {person.FirstName}".Trim();

                        c.Item().AlignCenter().Text($"Testovaný: {fullMilitaryName}").Bold().FontSize(18);
                        c.Item().AlignCenter().PaddingTop(8).Text($"Odbornost: {person.Unit ?? "Všeobecná"}  |  Třída: {person.KnowledgeClassText}").FontSize(14);
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().AlignBottom().AlignLeft().Column(l =>
                        {
                            l.Item().Text("Test vytvořil:").FontSize(9).FontColor(Colors.Grey.Medium);
                            l.Item().Text(creatorFullName).FontSize(10).Bold();
                        });

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
                    page.Content().Text(" ");
                });

                // ======================================================
                // STRANA 3 A DÁL: ZKUŠEBNÍ TEST
                // ======================================================
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Text($"Zkušební test  |  {person.LastName} {person.FirstName}").FontSize(9).FontColor(Colors.Grey.Medium);

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        ComposeTestContent(col, processedQuestions);
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Strana "); x.CurrentPageNumber(); });
                });

                // ======================================================
                // ODDĚLOVACÍ STRANA PO TESTU
                // ======================================================
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Content().Text(" ");
                });

                // ======================================================
                // PŘÍLOHY (Samostatné stránky pro obrázky/schémata)
                // ======================================================
                var questionsWithImages = processedQuestions.Where(q => !string.IsNullOrEmpty(q.Question.ImagePath)).ToList();
                if (questionsWithImages.Any())
                {
                    foreach (var q in questionsWithImages)
                    {
                        string imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", q.Question.ImagePath!);
                        if (File.Exists(imgPath))
                        {
                            bool isLandscape = false;
                            try
                            {
                                using (var stream = File.OpenRead(imgPath))
                                using (var bitmap = SKBitmap.Decode(stream))
                                {
                                    if (bitmap != null)
                                    {
                                        isLandscape = bitmap.Width > bitmap.Height;
                                    }
                                }
                            }
                            catch { /* V případě chyby čtení zůstane výchozí portrét */ }

                            container.Page(page =>
                            {
                                page.Size(isLandscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                                page.Margin(2, Unit.Centimetre);
                                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                                page.Header().Text($"Příloha k testu  |  {person.LastName} {person.FirstName}").FontSize(9).FontColor(Colors.Grey.Medium);

                                page.Content().PaddingTop(15).Column(col =>
                                {
                                    col.Item().PaddingBottom(15).Text($"Schéma k otázce č. {q.Number}:").Bold().FontSize(14);
                                    col.Item().Image(imgPath).FitArea();
                                });

                                page.Footer().AlignCenter().Text(x => { x.Span("Strana "); x.CurrentPageNumber(); });
                            });

                            // ======================================================
                            // ODDĚLOVACÍ STRANA PO KAŽDÉM SCHÉMATU
                            // ======================================================
                            container.Page(page =>
                            {
                                page.Size(PageSizes.A4);
                                page.Content().Text(" ");
                            });
                        }
                    }
                }

                // ======================================================
                // KLÍČ SPRÁVNÝCH ODPOVĚDÍ
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
                col.Item().ShowEntire().PaddingBottom(15).Column(qCol =>
                {
                    qCol.Item().Text($"{q.Number}. {q.Question.Text}").Bold();

                    if (!string.IsNullOrEmpty(q.Question.ImagePath))
                    {
                        qCol.Item().PaddingTop(2).Text("(K této otázce je připojeno schéma v přílohách na konci testu)")
                            .FontSize(10).FontColor(Colors.Grey.Medium).Italic();
                    }

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

            // Vypočítáme, kolik otázek bude v jednom sloupci (zaokrouhleno nahoru)
            int itemsPerColumn = (int)Math.Ceiling(questions.Count / 3.0);

            col.Item().Row(row =>
            {
                // Vytvoříme 3 sloupce
                for (int i = 0; i < 3; i++)
                {
                    // Získáme výřez otázek pro daný sloupec
                    var columnQuestions = questions.Skip(i * itemsPerColumn).Take(itemsPerColumn).ToList();

                    // Vytvoříme tabulku pro konkrétní sloupec (s mezerou mezi sloupci)
                    row.RelativeItem().PaddingRight(i < 2 ? 15 : 0).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30); // Zúžený sloupec pro číslo
                            columns.RelativeColumn();   // Sloupec pro odpověď
                        });

                        foreach (var q in columnQuestions)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text($"{q.Number}.");

                            if (q.ShuffledAnswers != null && q.ShuffledAnswers.Any())
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(q.CorrectLetter.ToString()).Bold();
                            }
                            else
                            {
                                // Zkrácený text, aby se vešel do třetinového sloupce
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text("(Otevřená)").FontColor(Colors.Grey.Medium);
                            }
                        }
                    });
                }
            });
        }

        // --- POMOCNÉ TŘÍDY A LOGIKA ---

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

        // --- EXPORT A OTEVÍRÁNÍ ---

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