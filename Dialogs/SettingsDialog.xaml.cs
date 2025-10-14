using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using QuadroAIPilot.Configuration;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Managers;
using QuadroAIPilot.Services;
using QuadroAIPilot.Models;
using QuadroAIPilot.Models.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace QuadroAIPilot.Dialogs
{
    /// <summary>
    /// Settings dialog with Liquid Glass design
    /// </summary>
    public sealed partial class SettingsDialog : ContentDialog
    {
        private readonly SettingsManager _settingsManager;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly PersonalProfileService _profileService;
        private AppSettings _tempSettings;
        private PersonalProfile _tempProfile;
        private Dictionary<string, CheckBox> _newsSourceCheckBoxes = new Dictionary<string, CheckBox>();

        public SettingsDialog()
        {
            this.InitializeComponent();
            
            _settingsManager = SettingsManager.Instance;
            _performanceMonitor = PerformanceMonitor.Instance;
            _profileService = ServiceContainer.GetService<PersonalProfileService>();
            _tempSettings = _settingsManager.Settings.Clone();
            
            InitializeControls();
            SetupEventHandlers();
            LoadSystemInfo();
            LoadProfileDataAsync();
        }

        private void InitializeControls()
        {
            // Load current settings to UI
            LoadSettingsToUI(_tempSettings);
            
            // Update animation speed text
            UpdateAnimationSpeedText();
        }

        private void SetupEventHandlers()
        {
            // ComboBox selection changed
            ThemeComboBox.SelectionChanged += (s, e) =>
            {
                if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    _tempSettings.Theme = Enum.Parse<AppTheme>(item.Tag.ToString());
                }
            };
            
            PerformanceComboBox.SelectionChanged += (s, e) =>
            {
                if (PerformanceComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    _tempSettings.Performance = Enum.Parse<PerformanceProfile>(item.Tag.ToString());
                }
            };
            
            // Sliders
            AnimationSpeedSlider.ValueChanged += (s, e) =>
            {
                if (s is Slider slider)
                {
                    _tempSettings.AnimationSpeed = slider.Value;
                    UpdateAnimationSpeedText();
                }
            };
            
            BlurIntensitySlider.ValueChanged += (s, e) =>
            {
                if (s is Slider slider)
                {
                    _tempSettings.BlurIntensity = slider.Value;
                }
            };
            
            // Toggle switches
            EnableAnimationsToggle.Toggled += (s, e) =>
            {
                if (s is ToggleSwitch toggle)
                {
                    _tempSettings.EnableAnimations = toggle.IsOn;
                }
            };
            
            EnableGlowToggle.Toggled += (s, e) =>
            {
                if (s is ToggleSwitch toggle)
                {
                    _tempSettings.EnableGlowEffects = toggle.IsOn;
                }
            };
            
            EnableParallaxToggle.Toggled += (s, e) =>
            {
                if (s is ToggleSwitch toggle)
                {
                    _tempSettings.EnableParallaxEffects = toggle.IsOn;
                }
            };
            
            // Voice selection
            VoiceComboBox.SelectionChanged += (s, e) =>
            {
                if (VoiceComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    _tempSettings.TTSVoice = item.Tag.ToString();
                }
            };
            
            // Dialog buttons
            this.PrimaryButtonClick += async (s, e) => await SaveSettingsAsync();
            this.SecondaryButtonClick += (s, e) => { /* Cancel - do nothing */ };
            
            // Profile photo buttons
            SelectPhotoButton.Click += SelectPhoto_Click;
            RemovePhotoButton.Click += RemovePhoto_Click;
            
            // Profile delete button
            DeleteProfileButton.Click += DeleteProfile_Click;
        }

        private void LoadSettingsToUI(AppSettings settings)
        {
            // Theme
            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag?.ToString() == settings.Theme.ToString())
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Performance
            foreach (ComboBoxItem item in PerformanceComboBox.Items)
            {
                if (item.Tag?.ToString() == settings.Performance.ToString())
                {
                    PerformanceComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Sliders
            AnimationSpeedSlider.Value = settings.AnimationSpeed;
            BlurIntensitySlider.Value = settings.BlurIntensity;
            
            // Toggles
            EnableAnimationsToggle.IsOn = settings.EnableAnimations;
            EnableGlowToggle.IsOn = settings.EnableGlowEffects;
            EnableParallaxToggle.IsOn = settings.EnableParallaxEffects;
            
            // Voice selection
            foreach (ComboBoxItem item in VoiceComboBox.Items)
            {
                if (item.Tag?.ToString() == settings.TTSVoice)
                {
                    VoiceComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Haber tercihleri
            LoadNewsPreferences();
            
            // Haber kaynakları listesini yükle
            LoadNewsSourcesList();
        }

        private void UpdateAnimationSpeedText()
        {
            AnimationSpeedText.Text = $"{AnimationSpeedSlider.Value:F1}x";
        }

        private async void LoadSystemInfo()
        {
            try
            {
                var perfInfo = await _performanceMonitor.GetSystemPerformanceAsync();
                var gpuLevel = await _performanceMonitor.GetGPULevelAsync();
                
                SystemInfoText.Text = $"GPU: {gpuLevel}\n" +
                                    $"CPU Kullanımı: {perfInfo.CPUUsage:F0}%\n" +
                                    $"RAM: {perfInfo.AvailableMemoryMB}/{perfInfo.TotalMemoryMB} MB\n" +
                                    $"Pil: {perfInfo.BatteryChargePercent}% ({perfInfo.BatteryStatus})";
            }
            catch
            {
                SystemInfoText.Text = "Sistem bilgisi alınamadı";
            }
        }

        private async Task SaveSettingsAsync()
        {
            // Profil verilerini doğrula ve kaydet
            if (!await ValidateAndSaveProfileAsync())
            {
                // Profil doğrulama başarısız, dialog açık kalsın
                return;
            }
            
            // Apply all settings
            await _settingsManager.UpdateSettingsAsync(_tempSettings);
            
            // Haber tercihlerini kaydet
            await SaveNewsPreferences();
            
            // Theme'i yeniden uygula ki şeffaflık değişiklikleri hemen etkin olsun
            var themeManager = ThemeManager.Instance;
            await themeManager.ApplyThemeAsync(_tempSettings.Theme);
        }
        
        private void LoadNewsPreferences()
        {
            try
            {
                var configService = ServiceContainer.GetOptionalService<ConfigurationService>();
                if (configService != null)
                {
                    var preferences = configService.User.NewsPreferences;
                    
                    // Checkbox'ları güncelle
                    CatGenel.IsChecked = preferences.SelectedCategories.Contains("Genel");
                    CatGundem.IsChecked = preferences.SelectedCategories.Contains("Gündem");
                    CatSpor.IsChecked = preferences.SelectedCategories.Contains("Spor");
                    CatEkonomi.IsChecked = preferences.SelectedCategories.Contains("Ekonomi");
                    CatPolitika.IsChecked = preferences.SelectedCategories.Contains("Politika");
                    CatDunya.IsChecked = preferences.SelectedCategories.Contains("Dünya");
                    CatTeknoloji.IsChecked = preferences.SelectedCategories.Contains("Teknoloji");
                    CatSaglik.IsChecked = preferences.SelectedCategories.Contains("Sağlık");
                    CatKultur.IsChecked = preferences.SelectedCategories.Contains("Kültür");
                    CatMagazin.IsChecked = preferences.SelectedCategories.Contains("Magazin");
                    CatYasam.IsChecked = preferences.SelectedCategories.Contains("Yaşam");
                    
                    // Özel kategori
                    foreach (ComboBoxItem item in CustomCategoryCombo.Items)
                    {
                        if (item.Content?.ToString() == preferences.CustomCategory)
                        {
                            CustomCategoryCombo.SelectedItem = item;
                            break;
                        }
                    }
                    
                    // Haber kaynakları ayarları
                    ShowAllSourcesToggle.IsOn = preferences.ShowAllSources;
                    AutoTranslateToggle.IsOn = preferences.AutoTranslateEnglishSources;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadNewsPreferences error: {ex.Message}");
            }
        }
        
        private void LoadNewsSourcesList()
        {
            try
            {
                var configService = ServiceContainer.GetOptionalService<ConfigurationService>();
                if (configService == null) return;
                
                var preferences = configService.User.NewsPreferences;
                
                // Tüm haber kaynaklarını tanımla
                var allSources = new List<(string Name, SourceType Type)>
                {
                    // Türkçe Kaynaklar
                    ("CNN Türk", SourceType.Turkish),
                    ("Hürriyet", SourceType.Turkish),
                    ("Milliyet", SourceType.Turkish),
                    ("Sabah", SourceType.Turkish),
                    ("NTV", SourceType.Turkish),
                    ("TRT Haber", SourceType.Turkish),
                    ("T24", SourceType.Turkish),
                    ("Cumhuriyet", SourceType.Turkish),
                    ("Anadolu Ajansı", SourceType.Turkish),
                    ("BloombergHT", SourceType.Turkish),
                    ("Donanimhaber", SourceType.Turkish),
                    ("Webrazzi", SourceType.Turkish),
                    ("ShiftDelete", SourceType.Turkish),
                    ("Medimagazin", SourceType.Turkish),
                    ("TÜBİTAK Bilim Genç", SourceType.Turkish),
                    ("Posta Magazin", SourceType.Turkish),
                    ("Para Analiz", SourceType.Turkish),
                    ("Google News Türkiye", SourceType.Turkish),
                    ("Google News Teknoloji", SourceType.Turkish),
                    ("Google News Ekonomi", SourceType.Turkish),
                    ("Google News Spor", SourceType.Turkish),
                    ("Google News Magazin", SourceType.Turkish),
                    ("Google News Sağlık", SourceType.Turkish),
                    ("Google News Bilim", SourceType.Turkish),
                    ("AA Ekonomi", SourceType.Turkish),
                    ("AA Spor", SourceType.Turkish),
                    ("CNN Türk Spor", SourceType.Turkish),
                    ("Hürriyet Spor", SourceType.Turkish),
                    ("Hürriyet Magazin", SourceType.Turkish),
                    
                    // İngilizce Kaynaklar
                    ("BBC World", SourceType.English),
                    ("CNN International", SourceType.English),
                    ("Reuters World News", SourceType.English),
                    ("The Guardian World", SourceType.English),
                    ("Al Jazeera English", SourceType.English),
                    ("France 24", SourceType.English),
                    ("Bloomberg", SourceType.English),
                    ("Financial Times", SourceType.English),
                    ("CNBC Top News", SourceType.English),
                    ("Wall Street Journal", SourceType.English),
                    ("Forbes", SourceType.English),
                    
                    // Uluslararası Türkçe Kaynaklar
                    ("BBC Türkçe", SourceType.International),
                    ("Deutsche Welle Türkçe", SourceType.International),
                    ("VOA Türkçe", SourceType.International),
                    ("Euronews Türkçe", SourceType.International)
                };
                
                // Kaynakları türlerine göre grupla ve UI'ya ekle
                var groupedSources = allSources.GroupBy(s => s.Type).OrderBy(g => g.Key);
                
                NewsSourcesPanel.Children.Clear();
                _newsSourceCheckBoxes.Clear();
                
                foreach (var group in groupedSources)
                {
                    // Grup başlığı ekle
                    var groupHeader = new TextBlock
                    {
                        Text = group.Key switch
                        {
                            SourceType.Turkish => "Türkçe Kaynaklar",
                            SourceType.English => "İngilizce Kaynaklar",
                            SourceType.International => "Uluslararası Türkçe Kaynaklar",
                            _ => "Diğer Kaynaklar"
                        },
                        Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                        Margin = new Thickness(0, 10, 0, 5)
                    };
                    
                    if (_newsSourceCheckBoxes.Count > 0) // İlk grup değilse boşluk ekle
                    {
                        groupHeader.Margin = new Thickness(0, 15, 0, 5);
                    }
                    
                    NewsSourcesPanel.Children.Add(groupHeader);
                    
                    // Grid ile 2 sütunlu düzen oluştur
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    
                    int row = 0;
                    int col = 0;
                    
                    foreach (var source in group)
                    {
                        var checkBox = new CheckBox
                        {
                            Content = source.Name,
                            IsChecked = preferences.SelectedNewsSources.Contains(source.Name),
                            Margin = new Thickness(0, 2, 10, 2)
                        };
                        
                        // Grid'e ekle
                        if (col == 0 && row > 0)
                        {
                            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        }
                        
                        Grid.SetRow(checkBox, row);
                        Grid.SetColumn(checkBox, col);
                        grid.Children.Add(checkBox);
                        
                        _newsSourceCheckBoxes[source.Name] = checkBox;
                        
                        // Sonraki pozisyona geç
                        col++;
                        if (col >= 2)
                        {
                            col = 0;
                            row++;
                        }
                    }
                    
                    NewsSourcesPanel.Children.Add(grid);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadNewsSourcesList error: {ex.Message}");
            }
        }
        
        private async Task SaveNewsPreferences()
        {
            try
            {
                var configService = ServiceContainer.GetOptionalService<ConfigurationService>();
                if (configService != null)
                {
                    var selectedCategories = new List<string>();
                    
                    // Seçili kategorileri topla
                    if (CatGenel.IsChecked == true) selectedCategories.Add("Genel");
                    if (CatGundem.IsChecked == true) selectedCategories.Add("Gündem");
                    if (CatSpor.IsChecked == true) selectedCategories.Add("Spor");
                    if (CatEkonomi.IsChecked == true) selectedCategories.Add("Ekonomi");
                    if (CatPolitika.IsChecked == true) selectedCategories.Add("Politika");
                    if (CatDunya.IsChecked == true) selectedCategories.Add("Dünya");
                    if (CatTeknoloji.IsChecked == true) selectedCategories.Add("Teknoloji");
                    if (CatSaglik.IsChecked == true) selectedCategories.Add("Sağlık");
                    if (CatKultur.IsChecked == true) selectedCategories.Add("Kültür");
                    if (CatMagazin.IsChecked == true) selectedCategories.Add("Magazin");
                    if (CatYasam.IsChecked == true) selectedCategories.Add("Yaşam");
                    
                    // Özel kategori
                    var customCategory = (CustomCategoryCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Otomobil";
                    
                    // Seçili haber kaynaklarını topla
                    var selectedSources = new List<string>();
                    foreach (var kvp in _newsSourceCheckBoxes)
                    {
                        if (kvp.Value.IsChecked == true)
                        {
                            selectedSources.Add(kvp.Key);
                        }
                    }
                    
                    var newsPrefs = new NewsPreferences
                    {
                        SelectedCategories = selectedCategories,
                        CustomCategory = customCategory,
                        SelectedNewsSources = selectedSources,
                        ShowAllSources = ShowAllSourcesToggle.IsOn,
                        AutoTranslateEnglishSources = AutoTranslateToggle.IsOn,
                        LastUpdated = DateTime.Now
                    };
                    
                    await configService.UpdateNewsPreferencesAsync(newsPrefs);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveNewsPreferences error: {ex.Message}");
            }
        }
        
        // Haber kaynakları seçim butonları için event handler'lar
        private void SelectAllSources_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkBox in _newsSourceCheckBoxes.Values)
            {
                checkBox.IsChecked = true;
            }
        }
        
        private void DeselectAllSources_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkBox in _newsSourceCheckBoxes.Values)
            {
                checkBox.IsChecked = false;
            }
        }
        
        private void SelectTurkishSources_Click(object sender, RoutedEventArgs e)
        {
            var turkishSources = new[] {
                "CNN Türk", "Hürriyet", "Milliyet", "Sabah", "NTV", "TRT Haber", "T24", 
                "Cumhuriyet", "Anadolu Ajansı", "BloombergHT", "Donanimhaber", "Webrazzi", 
                "ShiftDelete", "Medimagazin", "TÜBİTAK Bilim Genç", "Posta Magazin", "Para Analiz",
                "Google News Türkiye", "Google News Teknoloji", "Google News Ekonomi", "Google News Spor",
                "Google News Magazin", "Google News Sağlık", "Google News Bilim", "AA Ekonomi", "AA Spor",
                "CNN Türk Spor", "Hürriyet Spor", "Hürriyet Magazin"
            };
            
            foreach (var kvp in _newsSourceCheckBoxes)
            {
                kvp.Value.IsChecked = turkishSources.Contains(kvp.Key);
            }
        }
        
        private void SelectEnglishSources_Click(object sender, RoutedEventArgs e)
        {
            var englishSources = new[] {
                "BBC World", "CNN International", "Reuters World News", "The Guardian World",
                "Al Jazeera English", "France 24", "Bloomberg", "Financial Times", "CNBC Top News",
                "Wall Street Journal", "Forbes"
            };
            
            foreach (var kvp in _newsSourceCheckBoxes)
            {
                kvp.Value.IsChecked = englishSources.Contains(kvp.Key);
            }
        }
        
        #region User Profile Methods
        
        /// <summary>
        /// Profil verilerini yükle
        /// </summary>
        private async void LoadProfileDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[SettingsDialog] Profil verileri yükleniyor...");
                
                var profile = await _profileService.LoadProfileAsync();
                if (profile == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsDialog] Profil bulunamadı, yeni profil oluşturuluyor");
                    profile = _profileService.CreateDefaultProfile();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsDialog] Profil yüklendi - Ad: {profile.FirstName}, Soyad: {profile.LastName}");
                }
                
                _tempProfile = profile;
                
                // UI'ya verileri yükle
                DispatcherQueue.TryEnqueue(() =>
                {
                    FirstNameTextBox.Text = profile.FirstName;
                    LastNameTextBox.Text = profile.LastName;
                    EmailTextBox.Text = profile.Email;
                    PhoneTextBox.Text = profile.Phone ?? "";
                    CountryTextBox.Text = profile.Country ?? "Türkiye";
                    CityTextBox.Text = profile.City ?? "";
                    
                    // Doğum tarihi
                    if (profile.BirthDate.HasValue)
                    {
                        BirthDatePicker.Date = profile.BirthDate.Value;
                    }
                    
                    // Cinsiyet
                    if (!string.IsNullOrEmpty(profile.Gender))
                    {
                        foreach (ComboBoxItem item in GenderComboBox.Items)
                        {
                            if (item.Tag?.ToString() == profile.Gender)
                            {
                                GenderComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    
                    // Sosyal medya
                    TwitterTextBox.Text = profile.SocialMediaAccounts?.GetValueOrDefault("Twitter") ?? "";
                    LinkedInTextBox.Text = profile.SocialMediaAccounts?.GetValueOrDefault("LinkedIn") ?? "";
                    InstagramTextBox.Text = profile.SocialMediaAccounts?.GetValueOrDefault("Instagram") ?? "";
                    GitHubTextBox.Text = profile.SocialMediaAccounts?.GetValueOrDefault("GitHub") ?? "";
                    
                    // GDPR onayı
                    GdprConsentCheckBox.IsChecked = profile.HasGdprConsent;
                    
                    // Profil fotoğrafı
                    if (!string.IsNullOrEmpty(profile.ProfilePhotoPath) && System.IO.File.Exists(profile.ProfilePhotoPath))
                    {
                        var bitmap = new BitmapImage(new Uri(profile.ProfilePhotoPath));
                        ProfilePicture.ProfilePicture = bitmap;
                        RemovePhotoButton.IsEnabled = true;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProfileData error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Profil verilerini doğrula ve kaydet
        /// </summary>
        private async Task<bool> ValidateAndSaveProfileAsync()
        {
            try
            {
                // UI'dan verileri topla
                _tempProfile.FirstName = FirstNameTextBox.Text.Trim();
                _tempProfile.LastName = LastNameTextBox.Text.Trim();
                _tempProfile.Email = EmailTextBox.Text.Trim();
                _tempProfile.Phone = PhoneTextBox.Text.Trim();
                _tempProfile.Country = CountryTextBox.Text.Trim();
                _tempProfile.City = CityTextBox.Text.Trim();
                
                // Doğum tarihi
                _tempProfile.BirthDate = BirthDatePicker.Date.DateTime;
                
                // Cinsiyet
                if (GenderComboBox.SelectedItem is ComboBoxItem genderItem)
                {
                    _tempProfile.Gender = genderItem.Tag?.ToString();
                }
                
                // Sosyal medya hesapları
                var socialAccounts = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(TwitterTextBox.Text))
                    socialAccounts["Twitter"] = TwitterTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(LinkedInTextBox.Text))
                    socialAccounts["LinkedIn"] = LinkedInTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(InstagramTextBox.Text))
                    socialAccounts["Instagram"] = InstagramTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(GitHubTextBox.Text))
                    socialAccounts["GitHub"] = GitHubTextBox.Text.Trim();
                
                _tempProfile.SocialMediaAccounts = socialAccounts.Count > 0 ? socialAccounts : null;
                
                // GDPR onayı
                _tempProfile.HasGdprConsent = GdprConsentCheckBox.IsChecked ?? false;
                
                // Zorunlu alanları kontrol et
                if (string.IsNullOrWhiteSpace(_tempProfile.FirstName) ||
                    string.IsNullOrWhiteSpace(_tempProfile.LastName) ||
                    string.IsNullOrWhiteSpace(_tempProfile.Email))
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Eksik Bilgi",
                        Content = "Lütfen zorunlu alanları doldurun (Ad, Soyad, E-posta).",
                        CloseButtonText = "Tamam",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return false;
                }
                
                // Email formatını kontrol et
                if (!IsValidEmail(_tempProfile.Email))
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Geçersiz E-posta",
                        Content = "Lütfen geçerli bir e-posta adresi girin.",
                        CloseButtonText = "Tamam",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return false;
                }
                
                // GDPR onayı kontrolü
                if (!_tempProfile.HasGdprConsent)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "GDPR Onayı",
                        Content = "Kişisel verilerinizin saklanması için GDPR onayı vermeniz gerekiyor.",
                        CloseButtonText = "Tamam",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return false;
                }
                
                // Profili kaydet
                System.Diagnostics.Debug.WriteLine($"[SettingsDialog] Profil kaydediliyor - Ad: {_tempProfile.FirstName}, Soyad: {_tempProfile.LastName}, Email: {_tempProfile.Email}");
                
                // Önce Credential Manager'ı test et
                _profileService.TestCredentialManager();
                
                // Test amaçlı şifrelenmemiş kaydet
                var testSaved = await _profileService.SaveProfileUnencryptedAsync(_tempProfile);
                System.Diagnostics.Debug.WriteLine($"[SettingsDialog] Test kaydetme sonucu: {testSaved}");
                
                var saved = await _profileService.SaveProfileAsync(_tempProfile);
                
                System.Diagnostics.Debug.WriteLine($"[SettingsDialog] Profil kaydetme sonucu: {saved}");
                
                if (!saved)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Kayıt Hatası",
                        Content = "Profil kaydedilirken bir hata oluştu. Lütfen Debug Output penceresini kontrol edin.",
                        CloseButtonText = "Tamam",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return false;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsDialog] Profil başarıyla kaydedildi");
                    
                    // Kaydetme başarılı, dosyanın varlığını kontrol et
                    var profileExists = _profileService.HasProfile();
                    System.Diagnostics.Debug.WriteLine($"[SettingsDialog] Profil dosyası var mı: {profileExists}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ValidateAndSaveProfile error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Email format kontrolü
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Profil fotoğrafı seç
        /// </summary>
        private async void SelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".bmp");
                
                // Initialize with window handle
                var window = (Application.Current as App)?.MainWindow;
                if (window != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }
                
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    // Fotoğrafı kaydet
                    var photoPath = await _profileService.SaveProfilePhotoAsync(file.Path);
                    if (!string.IsNullOrEmpty(photoPath))
                    {
                        _tempProfile.ProfilePhotoPath = photoPath;
                        
                        // UI'da göster
                        var bitmap = new BitmapImage(new Uri(photoPath));
                        ProfilePicture.ProfilePicture = bitmap;
                        RemovePhotoButton.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectPhoto error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Profil fotoğrafını kaldır
        /// </summary>
        private void RemovePhoto_Click(object sender, RoutedEventArgs e)
        {
            _tempProfile.ProfilePhotoPath = null;
            ProfilePicture.ProfilePicture = null;
            RemovePhotoButton.IsEnabled = false;
        }
        
        /// <summary>
        /// Profili sil
        /// </summary>
        private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "Profili Sil",
                    Content = "Profilinizi silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.",
                    PrimaryButtonText = "Sil",
                    CloseButtonText = "İptal",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                
                var result = await confirmDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var deleted = await _profileService.DeleteProfileAsync();
                    if (deleted)
                    {
                        // Yeni boş profil oluştur
                        _tempProfile = _profileService.CreateDefaultProfile();
                        
                        // UI'yı temizle
                        LoadProfileDataAsync();
                        
                        var infoDialog = new ContentDialog
                        {
                            Title = "Başarılı",
                            Content = "Profiliniz başarıyla silindi.",
                            CloseButtonText = "Tamam",
                            XamlRoot = this.XamlRoot
                        };
                        await infoDialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteProfile error: {ex.Message}");
            }
        }
        
        #endregion
    }

    /// <summary>
    /// Extension method for cloning settings
    /// </summary>
    public static class AppSettingsExtensions
    {
        public static AppSettings Clone(this AppSettings original)
        {
            return new AppSettings
            {
                Theme = original.Theme,
                Performance = original.Performance,
                AnimationSpeed = original.AnimationSpeed,
                EnableAnimations = original.EnableAnimations,
                EnableGlowEffects = original.EnableGlowEffects,
                EnableParallaxEffects = original.EnableParallaxEffects,
                BlurIntensity = original.BlurIntensity,
                TTSVoice = original.TTSVoice
            };
        }
    }
}