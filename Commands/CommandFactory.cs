using System;
using System.Threading.Tasks;
using System.Diagnostics;
using QuadroAIPilot.Services;
using QuadroAIPilot.Models;

// Yeni intent tipi ve modeli
public enum CommandIntentType
{
    Unknown,
    OpenApplication,
    CloseApplication,
    FindFile,
    FocusWindow,
    SystemCommand
}

public class CommandIntentResult
{
    public CommandIntentType Type { get; set; } = CommandIntentType.Unknown;
    public string Command { get; set; } = string.Empty;
    public string[] Parameters { get; set; } = null;
    public float Confidence { get; set; } = 0f;
}

// Komut doğrulama sonucu
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Suggestion { get; set; }
    public string AlternativeCommand { get; set; }
}

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Intent türüne göre doğru komut nesnesini oluşturan fabrika sınıfı
    /// </summary>
    public class CommandFactory
    {
        private readonly ApplicationService _applicationService;
        private readonly FileSearchService _fileSearchService;
        private readonly WindowsApiService _windowsApiService;
        private readonly CommandRegistry _commandRegistry;
        private readonly SystemCommandRegistry _systemCommandRegistry;
        
        // Çift komut işleme önleme
        private string _lastCommand = string.Empty;
        private DateTime _lastCommandTime = DateTime.MinValue;
        private readonly TimeSpan _duplicateCommandThreshold = TimeSpan.FromMilliseconds(1000); // 1 saniye threshold

        /// <summary>
        /// CommandFactory kurucu metodu
        /// </summary>
        /// <param name="applicationService">Uygulama servisi</param>
        /// <param name="fileSearchService">Dosya arama servisi</param>
        /// <param name="windowsApiService">Windows API servisi</param>
        public CommandFactory(
            ApplicationService applicationService,
            FileSearchService fileSearchService,
            WindowsApiService windowsApiService)
        {
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _fileSearchService = fileSearchService ?? throw new ArgumentNullException(nameof(fileSearchService));
            _windowsApiService = windowsApiService ?? throw new ArgumentNullException(nameof(windowsApiService));
            _commandRegistry = CommandRegistry.Instance;
            _systemCommandRegistry = SystemCommandRegistry.Instance;
        }
        
        /// <summary>
        /// Mod değişikliğinde state'i temizler
        /// </summary>
        public void ResetState()
        {
            _lastCommand = string.Empty;
            _lastCommandTime = DateTime.MinValue;
            Debug.WriteLine("[CommandFactory] State sıfırlandı");
        }

        /// <summary>
        /// Metne göre uygun komutu oluşturur (yeni yöntem)
        /// </summary>
        /// <param name="commandText">Komut metni</param>
        /// <returns>İlgili ICommand nesnesi veya null</returns>
        public ICommand CreateCommandFromText(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
            {
                Debug.WriteLine("[CommandFactory] Komut metni boş, komut oluşturulamadı.");
                return null;
            }
            
            // Çift komut işleme kontrolü - hızlı tekrar gerektiren komutlar için muafiyet
            string lowerCommandText = commandText.ToLowerInvariant();
            
            // Muaf komutları kontrol et
            bool isExemptCommand = 
                // Ses komutları
                (lowerCommandText.Contains("ses") && 
                 (lowerCommandText.Contains("art") || lowerCommandText.Contains("azalt") || 
                  lowerCommandText.Contains("kapat") || lowerCommandText.Contains("aç"))) ||
                // Navigasyon komutları
                lowerCommandText == "sağ" || lowerCommandText == "sol" || 
                lowerCommandText == "yukarı" || lowerCommandText == "aşağı" ||
                // Sayfa kontrolleri
                lowerCommandText.Contains("sayfa") && (lowerCommandText.Contains("yukarı") || lowerCommandText.Contains("aşağı")) ||
                // Temel tuşlar
                lowerCommandText == "enter" || lowerCommandText == "tab" || lowerCommandText == "boşluk" ||
                lowerCommandText == "önceki" || lowerCommandText == "sonraki" ||
                // Scroll komutları
                lowerCommandText.Contains("kaydır") ||
                // Geri al/İleri al
                lowerCommandText == "geri al" || lowerCommandText == "ileri al";
            
            if (!isExemptCommand && _lastCommand == commandText && DateTime.Now - _lastCommandTime < _duplicateCommandThreshold)
            {
                Debug.WriteLine($"[CommandFactory] Aynı komut çok kısa sürede tekrarlandı, işlenmiyor: '{commandText}'");
                return null;
            }

            try
            {
                // EN ÖNCE özel komutları kontrol et
                string lowerCommand = commandText.ToLowerInvariant();
                
                // Pattern tanıma - Dosya/Klasör/Uygulama açma komutları
                if (lowerCommand.Contains("dosyasını aç") || lowerCommand.Contains("klasörünü aç"))
                {
                    Debug.WriteLine($"[CommandFactory] Dosya/Klasör açma komutu algılandı: {commandText}");
                    var fileIntent = new CommandIntentResult
                    {
                        Type = CommandIntentType.FindFile,
                        Command = commandText,
                        Parameters = new[] { commandText }
                    };
                    var command = CreateCommand(fileIntent);
                    if (command != null)
                    {
                        _lastCommand = commandText;
                        _lastCommandTime = DateTime.Now;
                    }
                    return command;
                }
                
                if (lowerCommand.Contains("uygulamasını aç"))
                {
                    Debug.WriteLine($"[CommandFactory] Uygulama açma komutu algılandı: {commandText}");
                    var appIntent = new CommandIntentResult
                    {
                        Type = CommandIntentType.OpenApplication,
                        Command = commandText,
                        Parameters = new[] { commandText }
                    };
                    var command = CreateCommand(appIntent);
                    if (command != null)
                    {
                        _lastCommand = commandText;
                        _lastCommandTime = DateTime.Now;
                    }
                    return command;
                }
                
                // Edge TTS komutu
                if (lowerCommand.Contains("edge") && (lowerCommand.Contains("tts") || lowerCommand.Contains("ses") || lowerCommand.Contains("seslendirme") || lowerCommand.Contains("konuş")))
                {
                    Debug.WriteLine($"[CommandFactory] Edge TTS komut algılandı: {commandText}");
                    var edgeCommand = new EdgeTTSCommand(commandText);
                    _lastCommand = commandText;
                    _lastCommandTime = DateTime.Now;
                    return edgeCommand;
                }
                
                // Test ses komutu
                if (lowerCommand.Contains("test") && lowerCommand.Contains("ses"))
                {
                    Debug.WriteLine($"[CommandFactory] Test ses komutu algılandı: {commandText}");
                    var testCommand = new TestAudioCommand();
                    testCommand.SetCommandText(commandText);
                    _lastCommand = commandText;
                    _lastCommandTime = DateTime.Now;
                    return testCommand;
                }
                
                // SONRA Web komutlarını kontrol et
                var webCommand = TryCreateWebCommand(commandText);
                if (webCommand != null)
                {
                    Debug.WriteLine($"[CommandFactory] Web komut oluşturuldu: {webCommand.GetType().Name}");
                    _lastCommand = commandText;
                    _lastCommandTime = DateTime.Now;
                    return webCommand;
                }
                
                // Outlook Stats komutu kontrolü
                var outlookStatsCommand = new OutlookStatsCommand();
                if (outlookStatsCommand.CanHandle(commandText))
                {
                    Debug.WriteLine($"[CommandFactory] OutlookStatsCommand oluşturuldu: {commandText}");
                    outlookStatsCommand.SetCommandText(commandText);
                    _lastCommand = commandText;
                    _lastCommandTime = DateTime.Now;
                    return outlookStatsCommand;
                }
                
                // SONRA MAPI komutlarını kontrol et (mail komutları)
                var mapiCommand = TryCreateMAPICommand(commandText);
                if (mapiCommand != null)
                {
                    Debug.WriteLine($"[CommandFactory] MAPI komut oluşturuldu: {mapiCommand.GetType().Name}");
                    _lastCommand = commandText;
                    _lastCommandTime = DateTime.Now;
                    return mapiCommand;
                }
                
                // EN SON komut kaydından meta veri bulma (sistem komutları için)
                var metadata = _commandRegistry.FindCommand(commandText);
                
                if (metadata == null)
                {
                    // Haber komutlarını tekrar kontrol et (kategori bazlı)
                    var newsCategories = new[] { "spor", "ekonomi", "teknoloji", "sağlık", "dünya", "magazin", "siyaset", "finans", "borsa" };
                    var newsKeywords = new[] { "haberleri", "haberlerini", "haber", "haberler" };
                    bool isNewsCommand = false;
                    
                    foreach (var category in newsCategories)
                    {
                        if (lowerCommand.Contains(category))
                        {
                            foreach (var keyword in newsKeywords)
                            {
                                if (lowerCommand.Contains(keyword))
                                {
                                    isNewsCommand = true;
                                    break;
                                }
                            }
                        }
                        if (isNewsCommand) break;
                    }
                    
                    if (isNewsCommand)
                    {
                        Debug.WriteLine($"[CommandFactory] Kategori bazlı haber komutu algılandı: {commandText}");
                        var webInfoCmd = new WebInfoCommand();
                        var context = new CommandContext { RawCommand = commandText };
                        _lastCommand = commandText;
                        _lastCommandTime = DateTime.Now;
                        return new CommandWrapper(webInfoCmd, context);
                    }
                    
                    // Son olarak eski yöntemi dene
                    Debug.WriteLine($"[CommandFactory] Komut kaydında '{commandText}' bulunamadı, eski yönteme yönlendiriliyor.");
                    
                    // Eski yönteme yönlendir
                    var legacyIntent = DetermineLegacyIntent(commandText);
                    var command = CreateCommand(legacyIntent);
                    if (command != null)
                    {
                        _lastCommand = commandText;
                        _lastCommandTime = DateTime.Now;
                    }
                    return command;
                }

                // Meta veriye göre komut oluştur
                Debug.WriteLine($"[CommandFactory] '{commandText}' komutu için meta veri bulundu: {metadata.CommandId} ({metadata.FocusType})");
                
                // Komut başarıyla oluşturulacak, _lastCommand'ı güncelle
                _lastCommand = commandText;
                _lastCommandTime = DateTime.Now;
                
                switch (metadata.FocusType)
                {
                    case CommandFocusType.SystemWide:
                        return new SystemWideCommand(commandText, metadata, _windowsApiService);
                        
                    case CommandFocusType.ActiveWindow:
                        return new ActiveWindowCommand(commandText, metadata, _windowsApiService);
                        
                    case CommandFocusType.SpecificApp:
                        return new SpecificAppCommand(commandText, metadata, _windowsApiService);
                        
                    default:
                        Debug.WriteLine($"[CommandFactory] Bilinmeyen odak türü: {metadata.FocusType}");
                        return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandFactory] Komut oluşturma hatası: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Web komutlarını kontrol eder ve oluşturur
        /// </summary>
        private ICommand TryCreateWebCommand(string commandText)
        {
            // ÖNCE DynamicWebsiteCommand'ı kontrol et (Google arama ile çalışan)
            if (DynamicWebsiteCommand.IsWebsiteCommand(commandText))
            {
                var siteName = DynamicWebsiteCommand.ExtractSiteName(commandText);
                if (!string.IsNullOrWhiteSpace(siteName))
                {
                    Debug.WriteLine($"[CommandFactory] DynamicWebsiteCommand oluşturuldu: {siteName}");
                    return new DynamicWebsiteCommand(commandText, siteName);
                }
            }

            // Sonra eski OpenWebsiteCommand'ı kontrol et (statik liste)
            if (OpenWebsiteCommand.IsWebsiteCommand(commandText))
            {
                var siteName = OpenWebsiteCommand.ExtractSiteName(commandText);
                if (!string.IsNullOrWhiteSpace(siteName))
                {
                    Debug.WriteLine($"[CommandFactory] OpenWebsiteCommand oluşturuldu: {siteName}");
                    return new OpenWebsiteCommand(commandText, siteName);
                }
            }

            // Haber açma komutunu kontrol et
            var openNewsCommand = new OpenNewsCommand();
            if (openNewsCommand.CanHandle(commandText))
            {
                Debug.WriteLine($"[CommandFactory] OpenNewsCommand oluşturuldu: {commandText}");
                var context = new CommandContext { RawCommand = commandText };
                return new CommandWrapper(openNewsCommand, context);
            }

            try
            {
                var lowerText = commandText.ToLowerInvariant();

                var webCommand = new WebInfoCommand();
                bool canHandle = webCommand.CanHandle(lowerText);

                if (canHandle)
                {
                    var context = new CommandContext { RawCommand = commandText };
                    var wrappedCommand = new CommandWrapper(webCommand, context);
                    Debug.WriteLine($"[CommandFactory] WebInfoCommand oluşturuldu");
                    return wrappedCommand;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandFactory] Web komut oluşturma hatası: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// MAPI komutlarını kontrol eder ve oluşturur
        /// </summary>
        private ICommand TryCreateMAPICommand(string commandText)
        {
            try
            {
                var lowerText = commandText.ToLowerInvariant();
                // Debug.WriteLine($"[CommandFactory] MAPI komut kontrol ediliyor: '{lowerText}'");
                
                // Local Outlook Command - MAPI ile direkt local erişim
                var localOutlookCommand = new LocalOutlookCommand();
                bool localCanHandle = localOutlookCommand.CanHandle(lowerText);
                // Debug.WriteLine($"[CommandFactory] LocalOutlookCommand.CanHandle('{lowerText}'): {localCanHandle}");
                if (localCanHandle)
                {
                    localOutlookCommand.SetCommandText(commandText);
                    Debug.WriteLine($"[CommandFactory] LocalOutlookCommand oluşturuldu");
                    return localOutlookCommand;
                }
                
                
                // PracticalMAPICommand geçici olarak devre dışı (COM Interop sorunu)
                
                // Debug.WriteLine($"[CommandFactory] Hiçbir MAPI komutu eşleşmedi: '{lowerText}'");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandFactory] MAPI komut oluşturma hatası: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Eski yöntem: Metin analizi ile intent belirleme
        /// </summary>
        private CommandIntentResult DetermineLegacyIntent(string commandText)
        {
            // TODO: Daha gelişmiş bir metin analizi yapılabilir
            string lowercaseCommand = commandText.ToLowerInvariant();
            
            if (lowercaseCommand.Contains("aç") || lowercaseCommand.Contains("başlat"))
            {
                return new CommandIntentResult
                {
                    Type = CommandIntentType.OpenApplication,
                    Command = commandText,
                    Parameters = new[] { commandText }
                };
            }
            else if (lowercaseCommand.Contains("kapat") || lowercaseCommand.Contains("sonlandır"))
            {
                return new CommandIntentResult
                {
                    Type = CommandIntentType.CloseApplication,
                    Command = commandText
                };
            }
            else
            {
                return new CommandIntentResult
                {
                    Type = CommandIntentType.SystemCommand,
                    Command = commandText
                };
            }
        }

        /// <summary>
        /// Intent sonucuna göre uygun komutu oluşturur (eski yöntem)
        /// </summary>
        /// <param name="intentResult">Belirlenen intent</param>
        /// <returns>İlgili ICommand nesnesi veya null</returns>
        public ICommand CreateCommand(CommandIntentResult intentResult)
        {
            // Eski kod buraya gelecek (mevcut kod korunuyor)
            if (intentResult == null)
            {
                Debug.WriteLine("[CommandFactory] Intent sonucu null, komut oluşturulamadı.");
                return null;
            }

            try
            {
                // Önce komut doğrulaması yap
                var validationResult = ValidateCommand(intentResult);
                if (!validationResult.IsValid)
                {
                    Debug.WriteLine($"[CommandFactory] Komut doğrulaması başarısız: {validationResult.Suggestion}");

                    // Alternatif komut önerilmişse onu oluştur
                    if (!string.IsNullOrEmpty(validationResult.AlternativeCommand))
                    {
                        var alternativeIntent = new CommandIntentResult
                        {
                            Type = CommandIntentType.OpenApplication,
                            Command = validationResult.AlternativeCommand,
                            Parameters = new[] { "outlook" }
                        };
                        return CreateCommand(alternativeIntent);
                    }

                    return null;
                }

                // Intent türüne göre komut oluştur
                switch (intentResult.Type)
                {                    case CommandIntentType.OpenApplication:
                        return CreateOpenApplicationCommand(intentResult);

                    case CommandIntentType.CloseApplication:
                        return CreateCloseApplicationCommand(intentResult);

                    case CommandIntentType.FindFile:
                        return CreateFindFileCommand(intentResult);
                        
                    case CommandIntentType.SystemCommand:
                        return CreateSystemCommand(intentResult);
                        
                    default:
                        Debug.WriteLine($"[CommandFactory] Desteklenmeyen intent türü: {intentResult.Type}");
                        return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandFactory] Komut oluşturma hatası: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Komutu aktif uygulamaya göre doğrular
        /// </summary>
        private ValidationResult ValidateCommand(CommandIntentResult intentResult)
        {
            try
            {
                var activeWindowTitle = _windowsApiService.GetActiveWindowTitle();
                var command = intentResult.Command.ToLowerInvariant();

                // Outlook komutları kontrolü
                if ((command.Contains("mail") || command.Contains("e-posta") ||
                     command.Contains("yanıtla") || command.Contains("gönder") ||
                     command.Contains("inbox") || command.Contains("takvim")) &&
                    !command.Contains("outlook"))
                {
                    // Outlook açık değilse
                    if (!activeWindowTitle.Contains("Outlook", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            Suggestion = "Bu komut için önce Outlook'u açmalısınız.",
                            AlternativeCommand = "outlook aç"
                        };
                    }
                }

                // Excel'de mail komutları mantıksız
                if (activeWindowTitle.Contains("Excel") &&
                    (command.Contains("mail") || command.Contains("e-posta")))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Suggestion = "Excel'de mail komutu kullanılamaz. Outlook'a geçmek ister misiniz?",
                        AlternativeCommand = "outlook aç"
                    };
                }

                // Word'de Outlook komutları
                if (activeWindowTitle.Contains("Word") &&
                    (command.Contains("inbox") || command.Contains("takvim")))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Suggestion = "Word'de bu komut kullanılamaz. Outlook'a geçmek ister misiniz?",
                        AlternativeCommand = "outlook aç"
                    };
                }

                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandFactory] Doğrulama hatası: {ex.Message}");
                // Hata durumunda komutu geçerli kabul et
                return new ValidationResult { IsValid = true };
            }
        }

        /// <summary>
        /// Uygulama açma komutu oluşturur
        /// </summary>
        private ICommand CreateOpenApplicationCommand(CommandIntentResult intentResult)
        {
            if (intentResult.Parameters == null || intentResult.Parameters.Length == 0)
            {
                Debug.WriteLine("[CommandFactory] Uygulama parametreleri bulunamadı.");
                return null;
            }

            string appName = intentResult.Parameters[0];
            return new OpenApplicationCommand(intentResult.Command, appName, _applicationService);
        }

        /// <summary>
        /// Uygulama kapatma komutu oluşturur
        /// </summary>
        private ICommand CreateCloseApplicationCommand(CommandIntentResult intentResult)
        {
            // Komut metninden uygulama ismini çıkar
            string commandText = intentResult.Command.ToLowerInvariant();

            // "whatsapp uygulamasını kapat" -> "whatsapp"
            // "chrome'u kapat" -> "chrome"
            string appName = ExtractApplicationNameFromCloseCommand(commandText);

            if (string.IsNullOrWhiteSpace(appName))
            {
                Debug.WriteLine("[CommandFactory] Kapatılacak uygulama ismi bulunamadı.");
                return null;
            }

            Debug.WriteLine($"[CommandFactory] CloseApplicationCommand oluşturuluyor: {appName}");
            return new CloseApplicationCommand(intentResult.Command, appName);
        }

        /// <summary>
        /// Kapatma komutundan uygulama ismini çıkarır
        /// </summary>
        private string ExtractApplicationNameFromCloseCommand(string commandText)
        {
            // "uygulamasını kapat", "'u kapat", "'ı kapat", "kapat" gibi kalıpları temizle
            commandText = commandText.Replace("uygulamasını kapat", "")
                                     .Replace("uygulaması kapat", "")
                                     .Replace("uygulamasını", "")
                                     .Replace("uygulaması", "")
                                     .Replace("'u kapat", "")
                                     .Replace("'ı kapat", "")
                                     .Replace("'i kapat", "")
                                     .Replace("'ü kapat", "")
                                     .Replace(" kapat", "")
                                     .Replace("kapat", "")
                                     .Trim();

            return commandText;
        }

        /// <summary>
        /// Dosya arama komutu oluşturur
        /// </summary>
        private ICommand CreateFindFileCommand(CommandIntentResult intentResult)
        {
            if (intentResult.Parameters == null || intentResult.Parameters.Length == 0)
            {
                Debug.WriteLine("[CommandFactory] Dosya parametreleri bulunamadı.");
                return null;
            }

            string fileName = intentResult.Parameters[0];
            string fileType = intentResult.Parameters.Length > 1 ? intentResult.Parameters[1] : string.Empty;

            return new FindFileCommand(intentResult.Command, fileName, fileType, _fileSearchService);
        }

        /// <summary>
        /// Sistem komutu oluşturur (Kopyala, Yapıştır, Kes vb.)
        /// </summary>
        private ICommand CreateSystemCommand(CommandIntentResult intentResult)
        {
            string command = intentResult.Command.ToLowerInvariant().Trim();
            Debug.WriteLine($"[CommandFactory] Sistem komutu oluşturuluyor: {command}");

            // Metni analiz ederek komutu belirleyelim
            return TryCreateSystemCommandFromText(command);
        }

        /// <summary>
        /// Metin içeriğini analiz ederek sistem komutu oluşturur
        /// </summary>
        private ICommand TryCreateSystemCommandFromText(string commandText)
        {
            // Nokta, ünlem, soru işareti gibi noktalama işaretlerini temizle
            string command = commandText.ToLowerInvariant().Trim().TrimEnd('.', '!', '?', ' ');
            
            // Önce SystemCommandRegistry'den fuzzy matching ile komut ara
            string mappedCommand = _systemCommandRegistry.FindCommand(command);
            if (!string.IsNullOrEmpty(mappedCommand))
            {
                Debug.WriteLine($"[CommandFactory] SystemCommandRegistry'den komut bulundu: '{command}' -> '{mappedCommand}'");
                return new SystemCommand(commandText, mappedCommand, _windowsApiService);
            }
            
            // Registry'de bulunamayan özel durumlar için
            else if (command == "geri")
            {
                return new SystemCommand(commandText, "tarayıcıda geri git", _windowsApiService);
            }
            else if (command == "ileri")
            {
                return new SystemCommand(commandText, "tarayıcıda ileri git", _windowsApiService);
            }
            else if (command == "yenile")
            {
                return new SystemCommand(commandText, "yenile", _windowsApiService);
            }
            else if (command == "kaydet")
            {
                return new SystemCommand(commandText, "kaydet", _windowsApiService);
            }
            else if (command == "yazdır")
            {
                return new SystemCommand(commandText, "yazdır", _windowsApiService);
            }
            else if (command == "sağ")
            {
                return new SystemCommand(commandText, "sağ", _windowsApiService);
            }
            else if (command == "sol")
            {
                return new SystemCommand(commandText, "sol", _windowsApiService);
            }
            // Debug Outlook komutları (yeni eklenen)
            else if (command.Contains("outlook") && command.Contains("hesap") && 
                    (command.Contains("göster") || command.Contains("listele") || command.Contains("kontrol")))
            {
                return new DebugOutlookAccountsCommand();
            }
            // Outlook specifik komutlar
            else if (command == "yeni e-posta" || command == "yeni mail" || command == "yeni sayfa aç" || command == "yeni e-posta oluştur")
            {
                return new SystemCommand(commandText, "yeni e-posta oluştur", _windowsApiService);
            }
            else if (command == "yanıtla" || command == "e-postayı yanıtla" || command == "maili yanıtla")
            {
                return new SystemCommand(commandText, "e-postayı yanıtla", _windowsApiService);
            }
            else if (command == "herkese yanıtla" || command == "tümüne yanıtla" || command == "tümünü yanıtla")
            {
                return new SystemCommand(commandText, "herkese yanıtla", _windowsApiService);
            }
            else if (command == "ilet" || command == "e-postayı ilet" || command == "maili ilet" || command == "forward")
            {
                return new SystemCommand(commandText, "e-postayı ilet", _windowsApiService);
            }
            else if (command == "gönder" || command == "e-postayı gönder" || command == "mail gönder")
            {
                return new SystemCommand(commandText, "e-postayı gönder", _windowsApiService);
            }
            else if (command == "okunmadı olarak işaretle" || command == "okunmadı yap")
            {
                return new SystemCommand(commandText, "okunmadı olarak işaretle", _windowsApiService);
            }
            else if (command == "okundu olarak işaretle" || command == "okundu yap")
            {
                return new SystemCommand(commandText, "okundu olarak işaretle", _windowsApiService);
            }
            else if (command == "inbox" || command == "gelen kutusu" || command == "posta kutusu" || command == "gelen kutusuna git")
            {
                return new SystemCommand(commandText, "posta kutusuna git", _windowsApiService);
            }
            else if (command == "takvim" || command == "takvime git" || command == "takvimi aç" || command == "calendar" || command == "takvim aç")
            {
                return new SystemCommand(commandText, "takvim aç", _windowsApiService);
            }
            else if ((command.Contains("yeni") && (command.Contains("e-posta") || command.Contains("mail") || command.Contains("eposta"))) || command.Contains("mail oluştur"))
            {
                return new SystemCommand(commandText, "yeni e-posta oluştur", _windowsApiService);
            }
            else if (command.Contains("posta kutusu") || command.Contains("inbox") || command.Contains("gelen kutusu"))
            {
                return new SystemCommand(commandText, "posta kutusuna git", _windowsApiService);
            }
            else if (command == "dosya ekle" || command == "ek ekle" || command == "attachment")
            {
                return new SystemCommand(commandText, "dosya ekle", _windowsApiService);
            }
            else if (command == "randevular" || command == "randevulara git" || command == "appointments")
            {
                return new SystemCommand(commandText, "randevulara git", _windowsApiService);
            }
            else if (command == "mail kontrol et" || command == "e-postaları al" || command == "postaları gönder/al" || command == "mailleri kontrol et")
            {
                return new SystemCommand(commandText, "postaları gönder/al", _windowsApiService);
            }
            else if (command == "mail ara" || command == "e-posta ara" || command == "outlook ara")
            {
                return new SystemCommand(commandText, "e-posta ara", _windowsApiService);
            }
            else if (command == "okunmamış postalar" || command == "okunmamış mailler" || command == "okunmamışlar")
            {
                return new SystemCommand(commandText, "okunmamış postalar", _windowsApiService);
            }
            else if (command == "taslaklar" || command == "taslaklara git" || command == "drafts")
            {
                return new SystemCommand(commandText, "taslaklara git", _windowsApiService);
            }
            else if (command == "gönderilenler" || command == "gönderilenlere git" || command == "sent items" || command == "gönderilen postalar")
            {
                return new SystemCommand(commandText, "gönderilenler", _windowsApiService);
            }
            else if (command.Contains("outlook") && (command.Contains("aç") || command.Contains("çalıştır") || command.Contains("başlat")))
            {
                return new OpenApplicationCommand(commandText, "outlook", _applicationService);
            }
            else if (command.Contains("dosyasını aç") || command.Contains("dosyası aç"))
            {
                Debug.WriteLine($"[CommandFactory] Dosya açma komutu algılandı: {command}");
                return new SystemCommand(commandText, "dosya aç", _windowsApiService);
            }
            else if (command.Contains("ileri al"))
            {
                return new SystemCommand(commandText, "ileri al", _windowsApiService);
            }
            else if (command.Contains("geri al"))
            {
                return new SystemCommand(commandText, "geri al", _windowsApiService);
            }
            else if (command.Contains("tümünü seç"))
            {
                return new SystemCommand(commandText, "tümünü seç", _windowsApiService);
            }
            else if (command.Contains("yeni sekme"))
            {
                return new SystemCommand(commandText, "yeni sekme", _windowsApiService);
            }
            else if (command.Contains("yeni sayfa"))
            {
                return new SystemCommand(commandText, "yeni sayfa", _windowsApiService);
            }
            else if (command.Contains("yeni pencere"))
            {
                return new SystemCommand(commandText, "yeni pencere", _windowsApiService);
            }
            else if (command.Contains("bilgisayarı kilitle"))
            {
                return new SystemCommand(commandText, "bilgisayarı kilitle", _windowsApiService);
            }
            else if (command.Contains("masaüstünü göster"))
            {
                return new SystemCommand(commandText, "masaüstünü göster", _windowsApiService);
            }
            else if (command.Contains("ekran görüntüsü"))
            {
                return new SystemCommand(commandText, "ekran görüntüsü al", _windowsApiService);
            }
            else if (command.Contains("uygulamayı kapat"))
            {
                return new SystemCommand(commandText, "uygulamayı kapat", _windowsApiService);
            }
            else if (command.Contains("pencereyi kapat"))
            {
                return new SystemCommand(commandText, "pencereyi kapat", _windowsApiService);
            }
            else if (command.Contains("çalıştır penceresi"))
            {
                return new SystemCommand(commandText, "çalıştır penceresini aç", _windowsApiService);
            }
            else if (command.Contains("görev görünümü"))
            {
                return new SystemCommand(commandText, "görev görünümünü aç", _windowsApiService);
            }
            else if (command.Contains("sekmeyi kapat"))
            {
                return new SystemCommand(commandText, "sekmeyi kapat", _windowsApiService);
            }
            else if (command.Contains("pencereyi sağa"))
            {
                return new SystemCommand(commandText, "pencereyi sağa hizala", _windowsApiService);
            }
            else if (command.Contains("pencereyi sola"))
            {
                return new SystemCommand(commandText, "pencereyi sola hizala", _windowsApiService);
            }
            else if (command.Contains("sayfayı aşağı"))
            {
                return new SystemCommand(commandText, "sayfayı aşağı kaydır", _windowsApiService);
            }
            else if (command.Contains("sayfayı yukarı"))
            {
                return new SystemCommand(commandText, "sayfayı yukarı kaydır", _windowsApiService);
            }
            else if (command.Contains("sesi arttır"))
            {
                return new SystemCommand(commandText, "sesi arttır", _windowsApiService);
            }
            else if (command.Contains("sesi azalt"))
            {
                return new SystemCommand(commandText, "sesi azalt", _windowsApiService);
            }
            else if (command.Contains("sesi kapat"))
            {
                return new SystemCommand(commandText, "sesi kapat", _windowsApiService);
            }
            else if (command.Contains("sesi aç"))
            {
                return new SystemCommand(commandText, "sesi aç", _windowsApiService);
            }
            else if (command.Contains("sayfa başına"))
            {
                return new SystemCommand(commandText, "sayfa başına git", _windowsApiService);
            }
            else if (command.Contains("sayfa sonuna"))
            {
                return new SystemCommand(commandText, "sayfa sonuna git", _windowsApiService);
            }
            else if (command.Contains("caps lock"))
            {
                return new SystemCommand(commandText, "caps lock aç/kapat", _windowsApiService);
            }
            // Klasör açma komutları
            else if (command.Contains("belgeler") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "belgeler aç", _windowsApiService);
            }
            else if (command.Contains("belgelerim") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "belgelerim aç", _windowsApiService);
            }
            else if (command.Contains("resimler") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "resimler aç", _windowsApiService);
            }
            else if (command.Contains("resimlerim") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "resimlerim aç", _windowsApiService);
            }
            else if (command.Contains("müzik") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "müzik aç", _windowsApiService);
            }
            else if (command.Contains("müziğim") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "müziğim aç", _windowsApiService);
            }
            else if (command.Contains("videolar") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "videolar aç", _windowsApiService);
            }
            else if (command.Contains("videolarım") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "videolarım aç", _windowsApiService);
            }
            else if ((command.Contains("indirilenler") || command.Contains("downloads")) && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "indirilenler aç", _windowsApiService);
            }
            else if ((command.Contains("İndirilenler") || command.Contains("downloads")) && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "İndirilenler aç", _windowsApiService);
            }
            else if ((command.Contains("masaüstü") || command.Contains("desktop")) && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "masaüstü aç", _windowsApiService);
            }
            else if (command.Contains("dosya gezgini") && command.Contains("aç"))
            {
                return new SystemCommand(commandText, "dosya gezginini aç", _windowsApiService);
            }

            // Temel klavye komutları
            else if (command.Equals("enter") || command.Equals("enter tuşu"))
            {
                return new SystemCommand(commandText, "enter", _windowsApiService);
            }
            else if (command.Equals("tamam") || command.Equals("kabul") || command.Equals("onayla") || 
                     command.Equals("evet") || command.Equals("kabul et") || command.Equals("onay") ||
                     command.Equals("onaylıyorum") || command.Equals("devam"))
            {
                return new SystemCommand(commandText, "kabul et", _windowsApiService);
            }
            else if (command.Equals("vazgeç") || command.Equals("iptal") || command.Equals("iptal et") || 
                     command.Equals("hayır") || command.Equals("red") || command.Equals("reddet"))
            {
                return new SystemCommand(commandText, "vazgeç", _windowsApiService);
            }
            else if (command.Equals("escape") || command.Equals("esc"))
            {
                return new SystemCommand(commandText, "escape", _windowsApiService);
            }            else if (command.Equals("tab") || command.Equals("sonraki"))
            {
                return new SystemCommand(commandText, "tab", _windowsApiService);
            }
            else if (command.Equals("önceki"))
            {
                return new SystemCommand(commandText, "önceki", _windowsApiService);
            }
            else if (command.Equals("sağ")) 
            {
                return new SystemCommand(commandText, "sağ", _windowsApiService);
            }
            else if (command.Equals("sol"))
            {
                return new SystemCommand(commandText, "sol", _windowsApiService);
            }
            else if (command.Equals("yukarı"))
            {
                return new SystemCommand(commandText, "yukarı", _windowsApiService);
            }
            else if (command.Equals("aşağı"))
            {
                return new SystemCommand(commandText, "aşağı", _windowsApiService);
            }
            else if (command.Equals("boşluk"))
            {
                return new SystemCommand(commandText, "boşluk", _windowsApiService);
            }

            Debug.WriteLine($"[CommandFactory] Metin için uygun sistem komutu bulunamadı: {commandText}");
            return null;
        }
    }
}