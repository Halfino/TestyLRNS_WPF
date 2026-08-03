using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TestyLRNS_WPF.Core;
using TestyLRNS_WPF.Data.Repositories;
using TestyLRNS_WPF.Models;

namespace TestyLRNS_WPF.Views
{
    public partial class AddQuestionDialog : Window
    {
        public Question? NewQuestion { get; private set; }
        private readonly Question? _editingQuestion;
        private readonly SystemTopicRepository _topicRepository;
        private readonly User _currentUser;
        private bool _isInitializing = true;

        private string? _selectedImageTempPath = null;
        private string? _finalImageFileName = null;
        private bool _removeExistingImage = false;

        public AddQuestionDialog(User? currentUser, Question? questionToEdit = null)
        {
            this.InitializeComponent();
            _topicRepository = new SystemTopicRepository();

            _currentUser = currentUser ?? new User
            {
                Role = "SuperAdmin",
                Unit = "SZP",
                AirportIcao = "LKKB"
            };

            _editingQuestion = questionToEdit;

            PopulateAirports();

            if (_editingQuestion != null)
            {
                this.Title = "Úprava zkušební otázky";
                TxtQuestionText.Text = _editingQuestion.Text;
                CbType.SelectedIndex = _editingQuestion.IsWritten ? 1 : 0;
                TsOperational.IsOn = _editingQuestion.IsOperationalTraining;

                SelectUnitInComboBox(_editingQuestion.Unit);
                UpdateTopicsDropdown(_editingQuestion.Unit);

                if (!string.IsNullOrEmpty(_editingQuestion.SystemTopic))
                {
                    CbTopic.SelectedItem = _editingQuestion.SystemTopic;
                }

                CbClass.SelectedIndex = Math.Clamp(_editingQuestion.KnowledgeClass - 1, 0, 2);
                SelectAirportInComboBox(_editingQuestion.AirportIcao);

                if (!_editingQuestion.IsWritten && _editingQuestion.Answers != null && _editingQuestion.Answers.Count >= 3)
                {
                    TxtAns1.Text = _editingQuestion.Answers[0].Text;
                    Rb1.IsChecked = _editingQuestion.Answers[0].IsCorrect;
                    TxtAns2.Text = _editingQuestion.Answers[1].Text;
                    Rb2.IsChecked = _editingQuestion.Answers[1].IsCorrect;
                    TxtAns3.Text = _editingQuestion.Answers[2].Text;
                    Rb3.IsChecked = _editingQuestion.Answers[2].IsCorrect;
                }

                if (_editingQuestion.IsWritten && !string.IsNullOrEmpty(_editingQuestion.ImagePath))
                {
                    _finalImageFileName = _editingQuestion.ImagePath;
                    TxtImageName.Text = _finalImageFileName;
                    BtnRemoveImage.Visibility = Visibility.Visible;
                }
            }
            else
            {
                CbType.SelectedIndex = 0;
                string defaultUnit = _currentUser.Unit ?? "SZP";
                SelectUnitInComboBox(defaultUnit);
                UpdateTopicsDropdown(defaultUnit);

                CbClass.SelectedIndex = 0;

                string defaultAirport = _currentUser.AirportIcao ?? "LKKB";
                SelectAirportInComboBox(defaultAirport);
            }

            if (_currentUser.Role == "SuperAdmin" || _currentUser.Role == "LokalniAdmin")
            {
                CbUnit.IsEnabled = true;
            }
            else
            {
                CbUnit.IsEnabled = false;
            }

            _isInitializing = false;

            if (PanelAnswers != null && PanelImageUpload != null)
            {
                bool isWritten = CbType.SelectedIndex == 1;
                PanelAnswers.Visibility = isWritten ? Visibility.Collapsed : Visibility.Visible;
                PanelImageUpload.Visibility = isWritten ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void PopulateAirports()
        {
            CbAirport.Items.Clear();
            CbAirport.Items.Add("Globální (Všechna)");

            if (_currentUser.Role == "SuperAdmin")
            {
                CbAirport.Items.Add("LKKB (Kbely)");
                CbAirport.Items.Add("LKCV (Čáslav)");
                CbAirport.Items.Add("LKNAM (Náměšť)");
                CbAirport.Items.Add("LKPD (Pardubice)");
            }
            else
            {
                string userIcao = _currentUser.AirportIcao ?? "LKKB";
                string display = userIcao switch
                {
                    "LKKB" => "LKKB (Kbely)",
                    "LKCV" => "LKCV (Čáslav)",
                    "LKNAM" => "LKNAM (Náměšť)",
                    "LKPD" => "LKPD (Pardubice)",
                    _ => $"{userIcao} (Lokální)"
                };
                CbAirport.Items.Add(display);
            }
            CbAirport.IsEnabled = true;
        }

        private void SelectUnitInComboBox(string? unit)
        {
            if (string.IsNullOrEmpty(unit)) return;
            for (int i = 0; i < CbUnit.Items.Count; i++)
            {
                if ((CbUnit.Items[i] as ComboBoxItem)?.Content.ToString() == unit)
                {
                    CbUnit.SelectedIndex = i;
                    return;
                }
            }
            CbUnit.SelectedIndex = 0;
        }

        private void SelectAirportInComboBox(string? icao)
        {
            if (string.IsNullOrEmpty(icao))
            {
                CbAirport.SelectedIndex = 0;
                return;
            }

            for (int i = 0; i < CbAirport.Items.Count; i++)
            {
                string? itemContent = CbAirport.Items[i].ToString();
                if (itemContent != null && itemContent.StartsWith(icao, StringComparison.OrdinalIgnoreCase))
                {
                    CbAirport.SelectedIndex = i;
                    return;
                }
            }
            CbAirport.SelectedIndex = 0;
        }

        private string? GetSelectedAirportIcao()
        {
            if (CbAirport.SelectedIndex < 0) return null;
            string? fullContent = CbAirport.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(fullContent)) return null;
            string icao = fullContent.Split(' ')[0];

            if (icao == "Globální") return null;

            return icao;
        }

        private void UpdateTopicsDropdown(string? unit)
        {
            if (string.IsNullOrEmpty(unit) || CbTopic == null) return;
            var availableSystems = _topicRepository.GetAllActiveByUnit(unit)
                                                   .Select(t => t.Name)
                                                   .ToList();

            CbTopic.ItemsSource = null;
            CbTopic.ItemsSource = availableSystems;

            if (availableSystems.Count > 0 && _editingQuestion == null)
            {
                CbTopic.SelectedIndex = -1;
            }
        }

        private void CbUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            var selectedItem = e.AddedItems.OfType<ComboBoxItem>().FirstOrDefault();
            if (selectedItem != null)
            {
                string? selectedUnit = selectedItem.Content.ToString();
                UpdateTopicsDropdown(selectedUnit);
            }
        }

        private void CbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelAnswers == null || PanelImageUpload == null) return;
            bool isWritten = CbType.SelectedIndex == 1;
            PanelAnswers.Visibility = isWritten ? Visibility.Collapsed : Visibility.Visible;
            PanelImageUpload.Visibility = isWritten ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TsOperational_Toggled(object sender, RoutedEventArgs e)
        {
            if (CbClass == null || CbAirport == null) return;
            if (TsOperational.IsOn)
            {
                CbClass.SelectedIndex = 0;
                CbClass.IsEnabled = false;

                if (CbAirport.Items.Count > 1 && CbAirport.SelectedIndex == 0)
                {
                    string defaultAirport = _currentUser.AirportIcao ?? "LKKB";
                    SelectAirportInComboBox(defaultAirport);
                }
            }
            else
            {
                CbClass.IsEnabled = true;
            }
        }

        private void BtnSelectImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Obrázky (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedImageTempPath = openFileDialog.FileName;
                TxtImageName.Text = Path.GetFileName(_selectedImageTempPath);
                BtnRemoveImage.Visibility = Visibility.Visible;
                _removeExistingImage = false;
            }
        }

        private void BtnRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            _selectedImageTempPath = null;
            _finalImageFileName = null;
            _removeExistingImage = true;
            TxtImageName.Text = "Žádný obrázek";
            BtnRemoveImage.Visibility = Visibility.Collapsed;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtErrorMessage.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(TxtQuestionText.Text))
            {
                TxtErrorMessage.Text = "Znění zkušební otázky nesmí být prázdné!";
                TxtErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            bool isWritten = CbType.SelectedIndex == 1;

            if (!isWritten && (string.IsNullOrWhiteSpace(TxtAns1.Text) || string.IsNullOrWhiteSpace(TxtAns2.Text) || string.IsNullOrWhiteSpace(TxtAns3.Text)))
            {
                TxtErrorMessage.Text = "Pro uzavřený test musíte vyplnit všechny tři možnosti odpovědí!";
                TxtErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            string? airport = GetSelectedAirportIcao();

            if (TsOperational.IsOn && airport == "Globální")
            {
                TxtErrorMessage.Text = "Provozní výcvik musí být vždy vázaný na konkrétní letiště (nelze zvolit Globální).";
                TxtErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            if (_removeExistingImage)
            {
                _finalImageFileName = null;
            }
            else if (!string.IsNullOrEmpty(_selectedImageTempPath))
            {
                try
                {
                    string imgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                    Directory.CreateDirectory(imgDir);

                    _finalImageFileName = Guid.NewGuid().ToString() + ".webp";
                    string targetPath = Path.Combine(imgDir, _finalImageFileName);

                    using (var inputStream = File.OpenRead(_selectedImageTempPath))
                    using (var originalBitmap = SKBitmap.Decode(inputStream))
                    {
                        int maxWidth = 1600;
                        int maxHeight = 2200;
                        SKBitmap bitmapToEncode = originalBitmap;
                        bool isResized = false;

                        if (originalBitmap.Width > maxWidth || originalBitmap.Height > maxHeight)
                        {
                            float ratioX = (float)maxWidth / originalBitmap.Width;
                            float ratioY = (float)maxHeight / originalBitmap.Height;
                            float ratio = Math.Min(ratioX, ratioY);

                            int newWidth = (int)(originalBitmap.Width * ratio);
                            int newHeight = (int)(originalBitmap.Height * ratio);

                            bitmapToEncode = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKCubicResampler.Mitchell));
                            isResized = true;
                        }

                        using (var imageToSave = SKImage.FromBitmap(bitmapToEncode))
                        using (var data = imageToSave.Encode(SKEncodedImageFormat.Webp, 80))
                        using (var outputStream = File.OpenWrite(targetPath))
                        {
                            data.SaveTo(outputStream);
                        }

                        if (isResized) bitmapToEncode.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    TxtErrorMessage.Text = $"Chyba při zpracování obrázku: {ex.Message}";
                    TxtErrorMessage.Visibility = Visibility.Visible;
                    return;
                }
            }

            int dbClass = CbClass.SelectedIndex + 1;
            string? unit = (CbUnit.SelectedItem as ComboBoxItem)?.Content.ToString();
            string? selectedTopic = CbTopic.SelectedItem?.ToString();

            var questionAnswers = new List<Answer>();

            if (!isWritten)
            {
                questionAnswers.Add(new Answer { Text = TxtAns1.Text.Trim(), IsCorrect = Rb1.IsChecked == true });
                questionAnswers.Add(new Answer { Text = TxtAns2.Text.Trim(), IsCorrect = Rb2.IsChecked == true });
                questionAnswers.Add(new Answer { Text = TxtAns3.Text.Trim(), IsCorrect = Rb3.IsChecked == true });
            }

            if (_editingQuestion != null)
            {
                _editingQuestion.Text = TxtQuestionText.Text.Trim();
                _editingQuestion.IsWritten = isWritten;
                _editingQuestion.KnowledgeClass = dbClass;
                _editingQuestion.Unit = unit;
                _editingQuestion.SystemTopic = selectedTopic;
                _editingQuestion.AirportIcao = airport;
                _editingQuestion.IsOperationalTraining = TsOperational.IsOn;
                _editingQuestion.Answers = new ObservableCollection<Answer>(questionAnswers);
                _editingQuestion.ImagePath = isWritten ? _finalImageFileName : null;

                NewQuestion = _editingQuestion;
            }
            else
            {
                NewQuestion = new Question
                {
                    Text = TxtQuestionText.Text.Trim(),
                    OwnerId = _currentUser.Id, // ZDE PŘIDÁN VLASTNÍK
                    IsWritten = isWritten,
                    KnowledgeClass = dbClass,
                    Unit = unit,
                    SystemTopic = selectedTopic,
                    AirportIcao = airport,
                    IsOperationalTraining = TsOperational.IsOn,
                    Answers = new ObservableCollection<Answer>(questionAnswers),
                    IsActive = true,
                    ImagePath = isWritten ? _finalImageFileName : null
                };
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}