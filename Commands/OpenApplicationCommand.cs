using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Uygulama açma komutunu yürüten sınıf
    /// </summary>
    public class OpenApplicationCommand : ICommand
    {
        private readonly string _appName;
        private readonly ApplicationService _appService;

        // İşlem durumunu takip etmek için statik değişkenler
        private static readonly Dictionary<string, DateTime> _lastProcessTimes = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, bool> _processingStates = new Dictionary<string, bool>();        // Dinamik cooldown süreleri (milisaniye)
        private static readonly Dictionary<string, int> _appCooldowns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
        };

        // Varsayılan cooldown süresi
        private const int DEFAULT_COOLDOWN_MS = 2000;

        public string CommandText { get; }

        /// <summary>
        /// Yeni bir uygulama açma komutu oluşturur
        /// </summary>
        /// <param name="commandText">Tam komut metni</param>
        /// <param name="appName">Açılacak uygulama adı</param>
        /// <param name="appService">Uygulama servisine referans</param>
        public OpenApplicationCommand(string commandText, string appName, ApplicationService appService)
        {
            CommandText = commandText;
            _appName = appName;
            _appService = appService ?? throw new ArgumentNullException(nameof(appService));
        }

        /// <summary>
        /// Uygulama için cooldown süresini döndürür
        /// </summary>
        private TimeSpan GetCooldownForApp(string appName)
        {
            var normalizedAppName = appName.ToLowerInvariant();

            if (_appCooldowns.TryGetValue(normalizedAppName, out int cooldownMs))
            {
                return TimeSpan.FromMilliseconds(cooldownMs);
            }

            return TimeSpan.FromMilliseconds(DEFAULT_COOLDOWN_MS);
        }

        /// <summary>
        /// Uygulamanın işleme durumunu kontrol eder
        /// </summary>
        private bool IsProcessing(string appName)
        {
            lock (_processingStates)
            {
                return _processingStates.TryGetValue(appName.ToLowerInvariant(), out bool isProcessing) && isProcessing;
            }
        }

        /// <summary>
        /// Uygulamanın işleme durumunu ayarlar
        /// </summary>
        private void SetProcessing(string appName, bool isProcessing)
        {
            lock (_processingStates)
            {
                _processingStates[appName.ToLowerInvariant()] = isProcessing;
            }
        }

        /// <summary>
        /// Son işlem zamanını günceller
        /// </summary>
        private void UpdateLastProcessTime(string appName)
        {
            lock (_lastProcessTimes)
            {
                _lastProcessTimes[appName.ToLowerInvariant()] = DateTime.Now;
            }
        }

        /// <summary>
        /// Cooldown kontrolü yapar
        /// </summary>
        private bool IsInCooldown(string appName, out TimeSpan remainingTime)
        {
            lock (_lastProcessTimes)
            {
                var normalizedAppName = appName.ToLowerInvariant();

                if (_lastProcessTimes.TryGetValue(normalizedAppName, out DateTime lastTime))
                {
                    var cooldownPeriod = GetCooldownForApp(appName);
                    var elapsed = DateTime.Now - lastTime;

                    if (elapsed < cooldownPeriod)
                    {
                        remainingTime = cooldownPeriod - elapsed;
                        return true;
                    }
                }

                remainingTime = TimeSpan.Zero;
                return false;
            }
        }

        /// <summary>
        /// Komutu çalıştırır - uygulamayı açar veya öne getirir
        /// </summary>
        /// <returns>İşlem sonucu</returns>
        public async Task<bool> ExecuteAsync()
        {
            try
            {
                var normalizedAppName = _appName.ToLowerInvariant();

                // Eğer aynı uygulama şu anda işleniyorsa tekrar işleme
                if (IsProcessing(normalizedAppName))
                {
                    Debug.WriteLine($"[OpenApplicationCommand] {_appName} şu anda zaten işleniyor, atlanıyor.");
                    return true; // Zaten işleniyor, başarılı kabul et
                }

                // Cooldown kontrolü
                if (IsInCooldown(normalizedAppName, out TimeSpan remainingTime))
                {
                    Debug.WriteLine($"[OpenApplicationCommand] {_appName} cooldown süresinde, {remainingTime.TotalSeconds:F1} saniye daha beklemeli.");

                    // Cooldown süresini dinamik olarak ayarla (başarısızlık durumunda artır)
                    AdjustCooldown(normalizedAppName, false);

                    return true; // Cooldown içinde, başarılı kabul et
                }

                // İşleme başla
                SetProcessing(normalizedAppName, true);

                Debug.WriteLine($"[OpenApplicationCommand] Uygulama açılıyor: {_appName}");

                // Uygulama adına göre işlem yap
                bool result = false;
                switch (normalizedAppName)
                {
                    case "excel":
                        result = await _appService.OpenOrFocusApplicationAsync("excel", "EXCEL.EXE", "Microsoft Excel");
                        break;
                    case "word":
                        result = await _appService.OpenOrFocusApplicationAsync("winword", "WINWORD.EXE", "Microsoft Word");
                        break;
                    case "outlook":
                        result = await _appService.OpenOrFocusApplicationAsync("outlook", "OUTLOOK.EXE", "Microsoft Outlook");
                        break;
                    default:
                        // Tanımlanmamış uygulamalar için genel açma denemesi
                        result = await _appService.OpenOrFocusApplicationAsync(_appName, $"{_appName}.exe", _appName);
                        break;
                }

                // Başarı durumuna göre cooldown'u ayarla
                AdjustCooldown(normalizedAppName, result);

                // İşlem tamamlandı
                SetProcessing(normalizedAppName, false);
                UpdateLastProcessTime(normalizedAppName);

                Debug.WriteLine($"[OpenApplicationCommand] {_appName} açma işlemi {(result ? "başarılı" : "başarısız")}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenApplicationCommand] Hata: {ex.Message}");

                // Hata durumunda işlemi kapat
                SetProcessing(_appName.ToLowerInvariant(), false);

                return false;
            }
        }

        /// <summary>
        /// Başarı durumuna göre cooldown süresini dinamik olarak ayarlar
        /// </summary>
        private void AdjustCooldown(string appName, bool success)
        {
            lock (_appCooldowns)
            {
                if (!success)
                {
                    // Başarısızlık durumunda cooldown'u %20 artır
                    if (_appCooldowns.ContainsKey(appName))
                    {
                        _appCooldowns[appName] = Math.Min(
                            (int)(_appCooldowns[appName] * 1.2),
                            10000 // Maksimum 10 saniye
                        );

                        Debug.WriteLine($"[OpenApplicationCommand] {appName} için cooldown artırıldı: {_appCooldowns[appName]}ms");
                    }
                }
                else
                {
                    // Başarı durumunda cooldown'u normale döndür
                    var defaultCooldown = GetDefaultCooldownForApp(appName);
                    if (_appCooldowns.ContainsKey(appName) && _appCooldowns[appName] > defaultCooldown)
                    {
                        _appCooldowns[appName] = Math.Max(
                            (int)(_appCooldowns[appName] * 0.9),
                            defaultCooldown
                        );

                        Debug.WriteLine($"[OpenApplicationCommand] {appName} için cooldown azaltıldı: {_appCooldowns[appName]}ms");
                    }
                }
            }
        }

        /// <summary>
        /// Uygulama için varsayılan cooldown süresini döndürür
        /// </summary>
        private int GetDefaultCooldownForApp(string appName)
        {
            switch (appName.ToLowerInvariant())
            {
                case "excel": return 3000;
                case "word": return 3000;
                case "outlook": return 4000;
                case "notepad": return 1000;
                case "belgeler":
                case "documents": return 1500;
                default: return DEFAULT_COOLDOWN_MS;
            }
        }
    }
}