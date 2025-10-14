using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Komut kayıt sistemi - tüm komutların meta verilerini yönetir.
    /// JSON dosyasından komutları yükler ve belirli bir girdiyle eşleşen komutları bulur.
    /// </summary>
    public class CommandRegistry
    {
        private readonly List<CommandMetadata> _commands = new List<CommandMetadata>();
        private readonly string _commandsFilePath;
        private static CommandRegistry _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Singleton instance için erişim noktası.
        /// </summary>
        public static CommandRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CommandRegistry();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Özel constructor - singleton pattern için.
        /// </summary>
        private CommandRegistry()
        {
            // Komutlar dosyasının yolunu belirle - uygulama klasöründe
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string commandsFolder = Path.Combine(appDirectory, "Commands");
            
            // Commands klasörü yoksa oluştur
            if (!Directory.Exists(commandsFolder))
            {
                Directory.CreateDirectory(commandsFolder);
            }
            
            _commandsFilePath = Path.Combine(commandsFolder, "commands.json");
            
            // Komutları yükle veya varsayılan komutları oluştur
            if (File.Exists(_commandsFilePath))
            {
                LoadCommands();
            }
            else
            {
                // Önce default_commands.json'dan yüklemeyi dene
                string defaultCommandsPath = Path.Combine(commandsFolder, "default_commands.json");
                if (File.Exists(defaultCommandsPath))
                {
                    LoadCommandsFromFile(defaultCommandsPath);
                }
                else
                {
                    CreateDefaultCommands();
                }
                SaveCommands();
            }
        }

        /// <summary>
        /// Tüm kayıtlı komutları döndürür.
        /// </summary>
        public IReadOnlyList<CommandMetadata> GetAllCommands()
        {
            return _commands.AsReadOnly();
        }

        /// <summary>
        /// Girilen metin ile eşleşen komutu bulur.
        /// </summary>
        /// <param name="input">Kullanıcı girdisi</param>
        /// <returns>Eşleşen komut meta verisi veya null</returns>
        public CommandMetadata FindCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;
                
            string normalizedInput = input.Trim().ToLowerInvariant();
            
            // E-posta/e posta/eposta normalizasyonu (ana normalizasyonu MainWindow'da da yaptık)
            List<string> inputVariations = new List<string> { normalizedInput };
            if (normalizedInput.Contains("e-posta"))
            {
                inputVariations.Add(normalizedInput.Replace("e-posta", "e posta"));
                inputVariations.Add(normalizedInput.Replace("e-posta", "eposta"));
            }
            if (normalizedInput.Contains("e posta"))
            {
                inputVariations.Add(normalizedInput.Replace("e posta", "e-posta"));
                inputVariations.Add(normalizedInput.Replace("e posta", "eposta"));
            }
            if (normalizedInput.Contains("eposta"))
            {
                inputVariations.Add(normalizedInput.Replace("eposta", "e-posta"));
                inputVariations.Add(normalizedInput.Replace("eposta", "e posta"));
            }
            
            // Komut varyasyonları: {string.Join(", ", inputVariations)}
            
            // Tam eşleşmeyi dene
            foreach (var variation in inputVariations)
            {
                var command = _commands.FirstOrDefault(c => 
                    c.CommandTriggers.Any(t => t.Equals(variation, StringComparison.OrdinalIgnoreCase)));
                    
                if (command != null)
                {
                    // Tam eşleşme bulundu: {command.CommandId}
                    return command;
                }
            }
            
            // İçerme kontrolü yap
            foreach (var variation in inputVariations)
            {
                // Kısa komutlar dahil tüm komutları kontrol et (artık filtreleme yok)
                var command = _commands.FirstOrDefault(c => 
                    c.CommandTriggers.Any(t => variation.Contains(t, StringComparison.OrdinalIgnoreCase)));
                    
                if (command != null)
                {
                    // İçerme eşleşmesi bulundu: {command.CommandId}
                    return command;
                }
            }
            
            // Hiçbir komut eşleşmesi bulunamadı
            return null;
        }

        /// <summary>
        /// Yeni bir komut ekler veya var olanı günceller.
        /// </summary>
        /// <param name="command">Eklenecek/güncellenecek komut</param>
        public void RegisterCommand(CommandMetadata command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
                
            // Var olan komutu bul
            var existingCommand = _commands.FirstOrDefault(c => c.CommandId == command.CommandId);
            
            if (existingCommand != null)
            {
                // Var olan komutu güncelle
                int index = _commands.IndexOf(existingCommand);
                _commands[index] = command;
            }
            else
            {
                // Yeni komut ekle
                _commands.Add(command);
            }
            
            // Değişiklikleri kaydet
            SaveCommands();
        }
        
        /// <summary>
        /// Komut kaydını siler.
        /// </summary>
        /// <param name="commandId">Silinecek komutun ID'si</param>
        public bool UnregisterCommand(string commandId)
        {
            if (string.IsNullOrEmpty(commandId))
                return false;
                
            var command = _commands.FirstOrDefault(c => c.CommandId == commandId);
            if (command != null)
            {
                _commands.Remove(command);
                SaveCommands();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Komutları belirtilen JSON dosyasından yükler.
        /// </summary>
        /// <param name="filePath">JSON dosyasının yolu</param>
        private void LoadCommandsFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var commands = JsonSerializer.Deserialize<List<CommandMetadata>>(json);
                
                if (commands != null && commands.Count > 0)
                {
                    _commands.Clear();
                    _commands.AddRange(commands);
                    // {commands.Count} komut '{filePath}' dosyasından yüklendi.
                }
                else
                {
                    // '{filePath}' dosyası boş veya geçersiz.
                }
            }
            catch (Exception)
            {
                // '{filePath}' dosyası yüklenirken hata
                // Hata durumunda varsayılan komutları oluştur
                CreateDefaultCommands();
            }
        }

        /// <summary>
        /// Komutları JSON dosyasından yükler.
        /// </summary>
        private void LoadCommands()
        {
            try
            {
                string json = File.ReadAllText(_commandsFilePath);
                var commands = JsonSerializer.Deserialize<List<CommandMetadata>>(json);
                
                if (commands != null && commands.Count > 0)
                {
                    _commands.Clear();
                    _commands.AddRange(commands);
                    // {_commands.Count} komut yüklendi.
                }
                else
                {
                    // Komut dosyası boş veya geçersiz. Varsayılan komutlar oluşturuluyor.
                    CreateDefaultCommands();
                    SaveCommands();
                }
            }
            catch (Exception)
            {
                // Komut yükleme hatası
                CreateDefaultCommands();
                SaveCommands();
            }
        }

        /// <summary>
        /// Komutları JSON dosyasına kaydeder.
        /// </summary>
        private void SaveCommands()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_commands, options);
                File.WriteAllText(_commandsFilePath, json);
                // {_commands.Count} komut kaydedildi.
            }
            catch (Exception)
            {
                // Komut kaydetme hatası
            }
        }
        
        /// <summary>
        /// Varsayılan komutları oluşturur.
        /// </summary>
        private void CreateDefaultCommands()
        {
            _commands.Clear();
            
            // Sistem geneli komutlar
            AddSystemWideCommands();
            
            // Aktif pencere komutları
            AddActiveWindowCommands();
            
            // Belirli uygulama komutları
            AddSpecificAppCommands();
            
            // {_commands.Count} varsayılan komut oluşturuldu.
        }
        
        /// <summary>
        /// Sistem geneli komutları ekler.
        /// </summary>
        private void AddSystemWideCommands()
        {
            // Ses kontrol komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "volume_up",
                CommandName = "Sesi Arttır",
                CommandTriggers = new List<string> { "sesi arttır", "ses arttır", "sesi yükselt", "ses aç" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "VolumeUp",
                Description = "Sistem ses seviyesini arttırır."
            });
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "volume_down",
                CommandName = "Sesi Azalt",
                CommandTriggers = new List<string> { "sesi azalt", "ses azalt", "sesi düşür", "sesi kıs" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "VolumeDown",
                Description = "Sistem ses seviyesini azaltır."
            });
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "volume_mute",
                CommandName = "Sesi Kapat",
                CommandTriggers = new List<string> { "sesi kapat", "ses kapat", "sesi sustur", "sessiz" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "VolumeMute",
                Description = "Sistem sesini kapatır/açar."
            });

            // Dikte kontrol komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "dictation_stop",
                CommandName = "Dikte Durdur",
                CommandTriggers = new List<string> { "[dikte_durduruldu]", "dikte durdur", "dictation stop", "diktasyon durdur" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Ses tanıma dikteyi durdurur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "dictation_start",
                CommandName = "Dikte Başlat",
                CommandTriggers = new List<string> { "[dikte_başlatıldı]", "dikte başlat", "dictation start", "diktasyon başlat" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Ses tanıma dikteyi başlatır."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "speech_synthesis",
                CommandName = "Metni Seslendirme",
                CommandTriggers = new List<string> { "[ses_sentezi]", "metni oku", "text to speech", "seslendirme" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Metni sesli olarak okur."
            });

            // Sistem komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "lock_computer",
                CommandName = "Bilgisayarı Kilitle",
                CommandTriggers = new List<string> { "bilgisayarı kilitle", "lock computer", "kilitle" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Win+L",
                DelayAfterFocusChange = 300,
                Description = "Bilgisayarı kilitler."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "show_desktop",
                CommandName = "Masaüstünü Göster",
                CommandTriggers = new List<string> { "masaüstünü göster", "show desktop", "masaüstü" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Win+D",
                DelayAfterFocusChange = 300,
                Description = "Masaüstünü gösterir."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "screenshot",
                CommandName = "Ekran Görüntüsü Al",
                CommandTriggers = new List<string> { "ekran görüntüsü", "screenshot", "ekran görüntüsü al" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Win+PrintScreen",
                DelayAfterFocusChange = 300,
                Description = "Ekran görüntüsü alır."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "run_dialog",
                CommandName = "Çalıştır Penceresini Aç",
                CommandTriggers = new List<string> { "çalıştır penceresi", "run dialog", "çalıştır" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Win+R",
                DelayAfterFocusChange = 300,
                Description = "Çalıştır penceresini açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "task_view",
                CommandName = "Görev Görünümünü Aç",
                CommandTriggers = new List<string> { "görev görünümü", "task view", "görev görünümünü aç" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Win+Tab",
                DelayAfterFocusChange = 300,
                Description = "Görev görünümünü açar."
            });

            // Pencere düzenleme komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "snap_window_left",
                CommandName = "Pencereyi Sola Hizala",
                CommandTriggers = new List<string> { "pencereyi sola", "sola hizala", "snap left" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Win+Left",
                DelayAfterFocusChange = 300,
                Description = "Aktif pencereyi ekranın sol yarısına hizalar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "snap_window_right",
                CommandName = "Pencereyi Sağa Hizala",
                CommandTriggers = new List<string> { "pencereyi sağa", "sağa hizala", "snap right" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Win+Right",
                DelayAfterFocusChange = 300,
                Description = "Aktif pencereyi ekranın sağ yarısına hizalar."
            });

            // File Explorer komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "open_file_explorer",
                CommandName = "Dosya Gezginini Aç",
                CommandTriggers = new List<string> { "dosya gezgini", "file explorer", "dosya gezginini aç" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Win+E",
                DelayAfterFocusChange = 300,
                Description = "Dosya gezginini açar."
            });

            // Email sistem komutları (MAPI/LocalOutlook)
            _commands.Add(new CommandMetadata
            {
                CommandId = "read_emails",
                CommandName = "E-postaları Oku",
                CommandTriggers = new List<string> { "maillerimi oku", "mailleri oku", "e-postaları oku", "e postaları oku", "epostaları oku" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "MAPI ile e-postaları okur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_unread_emails",
                CommandName = "Okunmamış E-postaları Oku",
                CommandTriggers = new List<string> { "outlook okunmamış mailler", "okunmamış mailler", "okunmamış e-postalar", "okunmamış mailleri göster" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "MAPI ile okunmamış e-postaları okur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_today_meetings",
                CommandName = "Bugünkü Toplantıları Göster",
                CommandTriggers = new List<string> { "outlook bugünkü toplantılarım", "bugünkü toplantılarım", "bugünkü toplantılar", "bugün toplantı" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "MAPI ile bugünkü toplantıları gösterir."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "get_news",
                CommandName = "Gündem Haberlerini Getir",
                CommandTriggers = new List<string> { "gündem haberleri", "haberleri getir", "haber getir", "son haberler", "gündem" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "RSS ve diğer kaynaklardan güncel haberleri getirir."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_news",
                CommandName = "Haberleri Oku",
                CommandTriggers = new List<string> { "haberleri oku", "haber oku", "gündem oku", "son haberleri oku" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Web'den haberleri getirir ve okur."
            });

            // Kategori bazlı haber komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "read_sports_news",
                CommandName = "Spor Haberleri",
                CommandTriggers = new List<string> { "spor haberleri", "spor haberlerini oku", "spor haberleri oku" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Spor haberlerini getirir ve okur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_economy_news",
                CommandName = "Ekonomi Haberleri",
                CommandTriggers = new List<string> { "ekonomi haberleri", "ekonomi haberlerini oku", "ekonomi haberleri oku", "finans haberleri", "borsa haberleri" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Ekonomi haberlerini getirir ve okur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_technology_news",
                CommandName = "Teknoloji Haberleri",
                CommandTriggers = new List<string> { "teknoloji haberleri", "teknoloji haberlerini oku", "teknoloji haberleri oku", "bilim haberleri" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Teknoloji haberlerini getirir ve okur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_health_news",
                CommandName = "Sağlık Haberleri",
                CommandTriggers = new List<string> { "sağlık haberleri", "sağlık haberlerini oku", "sağlık haberleri oku" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Sağlık haberlerini getirir ve okur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_world_news",
                CommandName = "Dünya Haberleri",
                CommandTriggers = new List<string> { "dünya haberleri", "dünya haberlerini oku", "dünya haberleri oku", "uluslararası haberler" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Dünya haberlerini getirir ve okur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_politics_news",
                CommandName = "Siyaset Haberleri",
                CommandTriggers = new List<string> { "siyaset haberleri", "siyaset haberlerini oku", "siyaset haberleri oku", "politika haberleri" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Siyaset haberlerini getirir ve okur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "read_entertainment_news",
                CommandName = "Magazin Haberleri",
                CommandTriggers = new List<string> { "magazin haberleri", "magazin haberlerini oku", "magazin haberleri oku", "eğlence haberleri" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Magazin haberlerini getirir ve okur."
            });

            // Wikipedia ve bilgi sorgulama komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "wikipedia_search",
                CommandName = "Wikipedia Araması",
                CommandTriggers = new List<string> { "nedir", "kimdir", "ne demek", "hakkında bilgi", "vikipedi", "wikipedia" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Wikipedia'dan bilgi arar."
            });

            // Twitter/X gündem komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "twitter_trends",
                CommandName = "Twitter Gündem",
                CommandTriggers = new List<string> { "twitter gündem", "twitter trend", "x gündem", "x trend", "trendler" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Twitter/X gündem konularını gösterir."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "compose_email",
                CommandName = "E-posta Yaz",
                CommandTriggers = new List<string> { "mail yaz", "e-posta yaz", "e posta yaz", "eposta yaz", "yeni mail" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Yeni e-posta oluşturur."
            });

            // Klasör açma komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "open_documents",
                CommandName = "Belgeler Klasörünü Aç",
                CommandTriggers = new List<string> { "belgeler aç", "belgelerim aç", "documents" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "belgeler aç",
                DelayAfterFocusChange = 300,
                Description = "Belgeler klasörünü açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "open_pictures",
                CommandName = "Resimler Klasörünü Aç",
                CommandTriggers = new List<string> { "resimler aç", "resimlerim aç", "pictures" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "resimler aç",
                DelayAfterFocusChange = 300,
                Description = "Resimler klasörünü açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "open_music",
                CommandName = "Müzik Klasörünü Aç",
                CommandTriggers = new List<string> { "müzik aç", "müziğim aç", "music" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "müzik aç",
                DelayAfterFocusChange = 300,
                Description = "Müzik klasörünü açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "open_videos",
                CommandName = "Videolar Klasörünü Aç",
                CommandTriggers = new List<string> { "videolar aç", "videolarım aç", "videos" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "videolar aç",
                DelayAfterFocusChange = 300,
                Description = "Videolar klasörünü açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "open_downloads",
                CommandName = "İndirilenler Klasörünü Aç",
                CommandTriggers = new List<string> { "indirilenler aç", "İndirilenler aç", "downloads" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "indirilenler aç",
                DelayAfterFocusChange = 300,
                Description = "İndirilenler klasörünü açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "open_desktop_folder",
                CommandName = "Masaüstü Klasörünü Aç",
                CommandTriggers = new List<string> { "masaüstü aç", "desktop" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "masaüstü aç",
                DelayAfterFocusChange = 300,
                Description = "Masaüstü klasörünü açar."            });
            
            // Yeni klasör
            _commands.Add(new CommandMetadata
            {
                CommandId = "new_folder",
                CommandName = "Yeni Klasör Oluştur",
                CommandTriggers = new List<string> { "yeni klasör", "new folder", "klasör oluştur" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Ctrl+Shift+N",
                DelayAfterFocusChange = 300,
                Description = "Yeni klasör oluşturur."
            });            // Pencere değiştirme komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "previous_window",
                CommandName = "Önceki Pencere",
                CommandTriggers = new List<string> { "önceki pencere", "previous window" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Alt+Shift+Tab",
                DelayAfterFocusChange = 300,
                Description = "Önceki pencereye geçer."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "next_window",
                CommandName = "Sonraki Pencere",
                CommandTriggers = new List<string> { "sonraki pencere", "next window" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "Alt+Tab",
                DelayAfterFocusChange = 300,
                Description = "Sonraki pencereye geçer."
            });

            // Tarayıcı komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "browser_back",
                CommandName = "Tarayıcıda Geri Git",
                CommandTriggers = new List<string> { "geri", "back", "geriye git" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Alt+Left",
                DelayAfterFocusChange = 300,
                Description = "Tarayıcıda geri gider."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "browser_forward",
                CommandName = "Tarayıcıda İleri Git",
                CommandTriggers = new List<string> { "ileri", "forward", "ileriye git" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Alt+Right",
                DelayAfterFocusChange = 300,
                Description = "Tarayıcıda ileri gider."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "refresh",
                CommandName = "Yenile",
                CommandTriggers = new List<string> { "yenile", "refresh", "sayfayı yenile" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "F5",
                DelayAfterFocusChange = 300,
                Description = "Sayfayı yeniler."
            });

            // Tab komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "new_tab",
                CommandName = "Yeni Sekme",
                CommandTriggers = new List<string> { "yeni sekme", "new tab" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+T",
                DelayAfterFocusChange = 300,
                Description = "Yeni sekme açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "close_tab",
                CommandName = "Sekmeyi Kapat",
                CommandTriggers = new List<string> { "sekmeyi kapat", "close tab" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+W",
                DelayAfterFocusChange = 300,
                Description = "Aktif sekmeyi kapatır."
            });

            // Pencere komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "new_window",
                CommandName = "Yeni Pencere",
                CommandTriggers = new List<string> { "yeni pencere", "new window" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+N",
                DelayAfterFocusChange = 300,
                Description = "Yeni pencere açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "close_window",
                CommandName = "Pencereyi Kapat",
                CommandTriggers = new List<string> { "pencereyi kapat", "close window" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Alt+F4",
                DelayAfterFocusChange = 300,
                Description = "Aktif pencereyi kapatır."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "close_app",
                CommandName = "Uygulamayı Kapat",
                CommandTriggers = new List<string> { "uygulamayı kapat", "close app" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Alt+F4",
                DelayAfterFocusChange = 300,
                Description = "Aktif uygulamayı kapatır."
            });
        }

        /// <summary>
        /// Aktif pencere komutlarını ekler.
        /// </summary>
        private void AddActiveWindowCommands()
        {
            // Dosya komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "save",
                CommandName = "Kaydet",
                CommandTriggers = new List<string> { "kaydet", "save", "dosyayı kaydet" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+S",
                DelayAfterFocusChange = 300,
                Description = "Aktif belgeyi kaydeder."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "save_as",
                CommandName = "Farklı Kaydet",
                CommandTriggers = new List<string> { "farklı kaydet", "save as", "yeni isimle kaydet" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+Shift+S",
                DelayAfterFocusChange = 300,
                Description = "Aktif belgeyi farklı isimle kaydeder."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "print",
                CommandName = "Yazdır",
                CommandTriggers = new List<string> { "yazdır", "print", "belgeyi yazdır" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+P",
                DelayAfterFocusChange = 300,
                Description = "Aktif belgeyi yazdırır."
            });

            // Düzenleme komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "copy",
                CommandName = "Kopyala",
                CommandTriggers = new List<string> { "kopyala", "copy" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+C",
                DelayAfterFocusChange = 300,
                Description = "Seçili içeriği kopyalar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "cut",
                CommandName = "Kes",
                CommandTriggers = new List<string> { "kes", "cut" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+X",
                DelayAfterFocusChange = 300,
                Description = "Seçili içeriği keser."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "paste",
                CommandName = "Yapıştır",
                CommandTriggers = new List<string> { "yapıştır", "paste" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+V",
                DelayAfterFocusChange = 300,
                Description = "Panodaki içeriği yapıştırır."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "undo",
                CommandName = "Geri Al",
                CommandTriggers = new List<string> { "geri al", "undo", "geri" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+Z",
                DelayAfterFocusChange = 300,
                Description = "Son işlemi geri alır."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "redo",
                CommandName = "Yinele",
                CommandTriggers = new List<string> { "yinele", "redo", "ileri al" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+Y",
                DelayAfterFocusChange = 300,
                Description = "Geri alınan işlemi yineler."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "select_all",
                CommandName = "Tümünü Seç",
                CommandTriggers = new List<string> { "tümünü seç", "select all", "hepsini seç" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+A",
                DelayAfterFocusChange = 300,
                Description = "Tüm içeriği seçer."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "find",
                CommandName = "Bul",
                CommandTriggers = new List<string> { "bul", "find", "ara" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+F",
                DelayAfterFocusChange = 300,
                Description = "Arama penceresini açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "replace",
                CommandName = "Değiştir",
                CommandTriggers = new List<string> { "değiştir", "replace", "bul ve değiştir" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+H",
                DelayAfterFocusChange = 300,
                Description = "Bul ve değiştir penceresini açar."
            });

            // Yakınlaştırma komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "zoom_in",
                CommandName = "Yakınlaştır",
                CommandTriggers = new List<string> { "yakınlaştır", "zoom in", "büyüt" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+Plus",
                DelayAfterFocusChange = 300,
                Description = "Görünümü yakınlaştırır."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "zoom_out",
                CommandName = "Uzaklaştır",
                CommandTriggers = new List<string> { "uzaklaştır", "zoom out", "küçült" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+Minus",
                DelayAfterFocusChange = 300,
                Description = "Görünümü uzaklaştırır."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "reset_zoom",
                CommandName = "Yakınlaştırmayı Sıfırla",
                CommandTriggers = new List<string> { "yakınlaştırmayı sıfırla", "reset zoom", "normal boyut" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+0",
                DelayAfterFocusChange = 300,
                Description = "Yakınlaştırmayı varsayılan değere döndürür."
            });            // Tam ekran
            _commands.Add(new CommandMetadata
            {
                CommandId = "fullscreen",
                CommandName = "Tam Ekran",
                CommandTriggers = new List<string> { "tam ekran", "fullscreen", "full screen" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "F11",
                DelayAfterFocusChange = 300,
                Description = "Tam ekran moduna geçer."
            });

            // Navigasyon komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "navigate_up",
                CommandName = "Yukarı",
                CommandTriggers = new List<string> { "yukarı", "up", "yukarı ok" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Up",
                DelayAfterFocusChange = 300,
                Description = "Yukarı yönlü navigasyon."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "navigate_down",
                CommandName = "Aşağı",
                CommandTriggers = new List<string> { "aşağı", "down", "aşağı ok" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Down",
                DelayAfterFocusChange = 300,
                Description = "Aşağı yönlü navigasyon."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "navigate_left",
                CommandName = "Sol",
                CommandTriggers = new List<string> { "sol", "left", "sol ok" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Left",
                DelayAfterFocusChange = 300,
                Description = "Sol yönlü navigasyon."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "navigate_right",
                CommandName = "Sağ",
                CommandTriggers = new List<string> { "sağ", "right", "sağ ok" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Right",
                DelayAfterFocusChange = 300,
                Description = "Sağ yönlü navigasyon."
            });            _commands.Add(new CommandMetadata
            {
                CommandId = "navigate_home",
                CommandName = "Başlangıca Git",
                CommandTriggers = new List<string> { "başlangıç", "home", "başa git", "en başa git", "sayfa başına git" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+Home",
                DelayAfterFocusChange = 300,
                Description = "Sayfa veya belgenin başlangıcına gider."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "navigate_end",
                CommandName = "Sona Git",
                CommandTriggers = new List<string> { "son", "end", "sona git", "sayfa sonuna git" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "Ctrl+End",
                DelayAfterFocusChange = 300,
                Description = "Sayfa veya belgenin sonuna gider."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "page_up",
                CommandName = "Sayfa Yukarı",
                CommandTriggers = new List<string> { "sayfa yukarı", "page up", "yukarı sayfa" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "PageUp",
                DelayAfterFocusChange = 300,
                Description = "Bir sayfa yukarı çıkar."
            });            _commands.Add(new CommandMetadata
            {
                CommandId = "page_down",
                CommandName = "Sayfa Aşağı",
                CommandTriggers = new List<string> { "sayfa aşağı", "page down", "aşağı sayfa" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "PageDown",
                DelayAfterFocusChange = 300,
                Description = "Bir sayfa aşağı iner."
            });

            // Uygulama içi navigasyon komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "navigate_previous",
                CommandName = "Önceki",
                CommandTriggers = new List<string> { "önceki", "previous" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "PageUp",
                DelayAfterFocusChange = 300,
                Description = "Önceki sayfaya veya öğeye gider."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "navigate_next",
                CommandName = "Sonraki",
                CommandTriggers = new List<string> { "sonraki", "next" },
                FocusType = CommandFocusType.ActiveWindow,
                KeyCombination = "PageDown",
                DelayAfterFocusChange = 300,
                Description = "Sonraki sayfaya veya öğeye gider."
            });
        }
        
        /// <summary>
        /// Belirli uygulamalara özgü komutları ekler.
        /// </summary>
        private void AddSpecificAppCommands()
        {
            // Outlook komutları (MAIL KOMUTLARİ KALDIRILDI - MAPI ile çakışmayı önlemek için)
            // Sadece takvim, kişiler ve genel Outlook komutları kalıyor



            
            _commands.Add(new CommandMetadata
            {
                CommandId = "mark_as_unread",
                CommandName = "Okunmadı Olarak İşaretle",
                CommandTriggers = new List<string> { "okunmadı olarak işaretle", "okunmadı yap" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+U",
                DelayAfterFocusChange = 500,
                Description = "E-postayı okunmadı olarak işaretler."
            });
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "go_to_inbox",
                CommandName = "Gelen Kutusuna Git",
                CommandTriggers = new List<string> { "inbox", "gelen kutusu", "posta kutusu", "gelen kutusuna git", "posta kutusuna git" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+I",
                DelayAfterFocusChange = 500,
                Description = "Gelen kutusuna gider."
            });
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "open_calendar",
                CommandName = "Takvim Aç",
                CommandTriggers = new List<string> { "takvim", "takvime git", "takvimi aç", "calendar", "takvim aç" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+2",
                DelayAfterFocusChange = 500,
                Description = "Outlook takvimini açar."
            });
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "add_attachment",
                CommandName = "Dosya Ekle",
                CommandTriggers = new List<string> { "dosya ekle", "ek ekle", "attachment" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+A",
                DelayAfterFocusChange = 500,
                Description = "E-postaya dosya ekler."
            });
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "check_mail",
                CommandName = "Postaları Kontrol Et",
                CommandTriggers = new List<string> { "e postaları kontrol et", "e-postaları al", "postaları gönder/al", "mail kontrol et", "mailleri kontrol et" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "F9",
                DelayAfterFocusChange = 500,
                Description = "Yeni postaları kontrol eder."
            });
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "search_email",
                CommandName = "E-posta Ara",
                CommandTriggers = new List<string> { "e posta ara", "e-posta ara", "mail ara", "outlook ara" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+E",
                DelayAfterFocusChange = 500,
                Description = "E-posta arama yapar."
            });
            
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "go_to_drafts",
                CommandName = "Taslaklara Git",
                CommandTriggers = new List<string> { "taslaklar", "taslaklara git", "drafts" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+D",
                DelayAfterFocusChange = 500,
                Description = "Taslaklar klasörüne gider."
            });
            
            _commands.Add(new CommandMetadata
            {
                CommandId = "go_to_sent",
                CommandName = "Gönderilenler",
                CommandTriggers = new List<string> { "gönderilenler", "gönderilenlere git", "sent items", "gönderilen postalar" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+S",
                DelayAfterFocusChange = 500,
                Description = "Gönderilen postalar klasörüne gider."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "delete_email",
                CommandName = "E-postayı Sil",
                CommandTriggers = new List<string> { "sil", "e-postayı sil", "e postayı sil", "mail sil", "delete" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Delete",
                DelayAfterFocusChange = 500,
                Description = "Seçili e-postayı siler."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "flag_email",
                CommandName = "E-postayı İşaretle",
                CommandTriggers = new List<string> { "işaretle", "bayrakla", "flag" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+G",
                DelayAfterFocusChange = 500,
                Description = "E-postayı işaretler."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "new_appointment",
                CommandName = "Yeni Randevu",
                CommandTriggers = new List<string> { "yeni randevu", "randevu oluştur", "new appointment" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+A",
                DelayAfterFocusChange = 500,
                Description = "Yeni randevu oluşturur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "new_meeting",
                CommandName = "Yeni Toplantı",
                CommandTriggers = new List<string> { "yeni toplantı", "toplantı oluştur", "new meeting" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+Q",
                DelayAfterFocusChange = 500,
                Description = "Yeni toplantı oluşturur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "new_contact",
                CommandName = "Yeni Kişi",
                CommandTriggers = new List<string> { "yeni kişi", "kişi ekle", "new contact" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+C",
                DelayAfterFocusChange = 500,
                Description = "Yeni kişi ekler."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "new_task",
                CommandName = "Yeni Görev",
                CommandTriggers = new List<string> { "yeni görev", "görev ekle", "new task" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+K",
                DelayAfterFocusChange = 500,
                Description = "Yeni görev oluşturur."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "address_book",
                CommandName = "Adres Defteri",
                CommandTriggers = new List<string> { "adres defteri", "address book", "kişiler" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Shift+B",
                DelayAfterFocusChange = 500,
                Description = "Adres defterini açar."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "go_to_folder",
                CommandName = "Klasöre Git",
                CommandTriggers = new List<string> { "klasöre git", "go to folder" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Y",
                DelayAfterFocusChange = 500,
                Description = "Belirli bir klasöre gider."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "previous_email",
                CommandName = "Önceki E-posta",
                CommandTriggers = new List<string> { "önceki e posta", "önceki e-posta", "önceki mail", "previous" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Comma",
                DelayAfterFocusChange = 500,
                Description = "Önceki e-postaya gider."
            });

            _commands.Add(new CommandMetadata
            {
                CommandId = "next_email",
                CommandName = "Sonraki E-posta",
                CommandTriggers = new List<string> { "sonraki e posta", "sonraki e-posta", "sonraki mail", "next" },
                FocusType = CommandFocusType.SpecificApp,
                TargetApplication = "outlook",
                KeyCombination = "Ctrl+Period",
                DelayAfterFocusChange = 500,
                Description = "Sonraki e-postaya gider."
            });


            // MAPI Test komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "mapi_test",
                CommandName = "MAPI Test Et",
                CommandTriggers = new List<string> { "mapi test", "mapi test et", "mapi testi", "mapi sistemini test et" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "MAPI sistem entegrasyonunu test eder."
            });

            // Debug komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "debug_outlook_accounts",
                CommandName = "Outlook Hesaplarını Göster",
                CommandTriggers = new List<string> { "outlook hesaplarımı göster", "outlook hesapları", "hesaplarımı göster" },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Sisteme kurulu Outlook hesaplarını listeler."
            });
            
            // Outlook İstatistik komutları
            _commands.Add(new CommandMetadata
            {
                CommandId = "outlook_stats",
                CommandName = "Outlook İstatistikleri",
                CommandTriggers = new List<string> { 
                    "outlook durum", 
                    "outlook istatistik", 
                    "mail sayısı", 
                    "toplantı sayısı",
                    "outlook özet",
                    "hızlı durum",
                    "outlook stats"
                },
                FocusType = CommandFocusType.SystemWide,
                KeyCombination = "custom_command",
                DelayAfterFocusChange = 300,
                Description = "Outlook'tan okunmamış mail ve bugünkü toplantı sayılarını TTS olmadan gösterir."
            });

            // Diğer uygulama komutları burada eklenebilir...
            // Excel, Word, Chrome vs. için özel komutlar
        }
    }
}