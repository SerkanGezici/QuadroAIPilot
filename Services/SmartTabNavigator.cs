using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Browser tab'larını klavye navigasyonu ile kapatma servisi
    /// Extension olmadan çalışır - Ctrl+Tab ile tab döngüsü yapar
    /// </summary>
    public class SmartTabNavigator
    {
        // ============================================
        // WIN32 API IMPORTS
        // ============================================

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // Virtual Key Codes
        private const byte VK_CONTROL = 0x11;
        private const byte VK_TAB = 0x09;
        private const byte VK_W = 0x57;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // ============================================
        // CONFIGURATION
        // ============================================

        private const int MAX_TABS_TO_SCAN = 50; // Maksimum taranacak tab sayısı
        private const int TAB_SWITCH_DELAY_MS = 150; // Tab geçiş animasyon bekleme süresi
        private const int WINDOW_FOCUS_DELAY_MS = 300; // Window focus bekleme süresi
        private const int KEY_PRESS_DELAY_MS = 50; // Tuş basımı arası bekleme

        // ============================================
        // FIELDS
        // ============================================

        private readonly ILogger _logger;

        // ============================================
        // CONSTRUCTOR
        // ============================================

        public SmartTabNavigator()
        {
            _logger = LoggingService.CreateLogger<SmartTabNavigator>();
        }

        // ============================================
        // PUBLIC METHODS
        // ============================================

        /// <summary>
        /// Verilen keyword ile eşleşen tab'ı bulup kapatır
        /// </summary>
        public async Task<TabCloseResult> CloseTabByKeyboard(string keyword)
        {
            try
            {
                _logger.LogInformation($"[SmartTabNavigator] Tab kapatma başlatıldı: {keyword}");

                // Browser process'lerini bul
                var browserNames = new[] { "chrome", "msedge", "firefox" };

                foreach (var browserName in browserNames)
                {
                    var processes = Process.GetProcessesByName(browserName);
                    if (processes.Length == 0)
                    {
                        _logger.LogDebug($"[SmartTabNavigator] {browserName} process'i bulunamadı");
                        continue;
                    }

                    _logger.LogInformation($"[SmartTabNavigator] {browserName} bulundu, {processes.Length} process");

                    // Her process için tab taraması yap
                    foreach (var process in processes)
                    {
                        if (process.MainWindowHandle == IntPtr.Zero)
                        {
                            _logger.LogDebug($"[SmartTabNavigator] Process {process.Id} MainWindowHandle yok");
                            continue;
                        }

                        if (!IsWindow(process.MainWindowHandle) || !IsWindowVisible(process.MainWindowHandle))
                        {
                            _logger.LogDebug($"[SmartTabNavigator] Window {process.MainWindowHandle} görünür değil");
                            continue;
                        }

                        // Bu window'da tab taraması yap
                        var result = await ScanTabsInWindow(process.MainWindowHandle, keyword, browserName);
                        if (result.Success)
                        {
                            _logger.LogInformation($"[SmartTabNavigator] ✓ Tab başarıyla kapatıldı: {result.TabTitle}");
                            return result;
                        }
                    }
                }

                _logger.LogWarning($"[SmartTabNavigator] ✗ Tab bulunamadı: {keyword}");
                return new TabCloseResult
                {
                    Success = false,
                    Error = "Tab bulunamadı"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SmartTabNavigator] Hata: {ex.Message}", ex);
                return new TabCloseResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        // ============================================
        // PRIVATE METHODS
        // ============================================

        /// <summary>
        /// Verilen window içindeki tab'ları tarar ve keyword ile eşleşeni bulur
        /// </summary>
        private async Task<TabCloseResult> ScanTabsInWindow(IntPtr hWnd, string keyword, string browserName)
        {
            try
            {
                _logger.LogDebug($"[SmartTabNavigator] Window taraması başlatıldı: {hWnd}");

                // Window'u öne getir
                bool focused = SetForegroundWindow(hWnd);
                if (!focused)
                {
                    _logger.LogWarning($"[SmartTabNavigator] Window focus başarısız: {hWnd}");
                    return new TabCloseResult { Success = false };
                }

                await Task.Delay(WINDOW_FOCUS_DELAY_MS);

                // Döngü tespiti için ziyaret edilen title'ları sakla
                var visitedTitles = new HashSet<string>();
                int tabCount = 0;

                // Tab tarama döngüsü
                for (int i = 0; i < MAX_TABS_TO_SCAN; i++)
                {
                    // Aktif tab'ın title'ını oku
                    string currentTitle = GetWindowTitle(hWnd);

                    if (string.IsNullOrWhiteSpace(currentTitle))
                    {
                        _logger.LogDebug($"[SmartTabNavigator] Tab {i}: Title okunamadı");
                        await Task.Delay(TAB_SWITCH_DELAY_MS);
                        SendCtrlTab();
                        await Task.Delay(TAB_SWITCH_DELAY_MS);
                        continue;
                    }

                    _logger.LogDebug($"[SmartTabNavigator] Tab {i}: {currentTitle}");

                    // Döngü tespiti - Aynı title'ı ikinci kez gördüysek tüm tab'lar tarandı
                    if (visitedTitles.Contains(currentTitle))
                    {
                        _logger.LogInformation($"[SmartTabNavigator] Döngü tespiti: {tabCount} tab tarandı");
                        break;
                    }

                    visitedTitles.Add(currentTitle);
                    tabCount++;

                    // Title matching kontrolü
                    if (TitleMatchesKeyword(currentTitle, keyword))
                    {
                        _logger.LogInformation($"[SmartTabNavigator] ✓ Eşleşme bulundu: '{currentTitle}' ~ '{keyword}'");

                        // Tab'ı kapat
                        SendCtrlW();
                        await Task.Delay(200); // Kapatma işleminin tamamlanması için bekle

                        return new TabCloseResult
                        {
                            Success = true,
                            TabTitle = currentTitle,
                            TabUrl = "", // Keyboard navigation'da URL bilgisi yok
                            Source = "keyboard-nav",
                            ClosedTab = new TabCloseResult.TabInfo
                            {
                                Title = currentTitle,
                                Url = "",
                                TabId = -1
                            }
                        };
                    }

                    // Sonraki tab'a geç
                    SendCtrlTab();
                    await Task.Delay(TAB_SWITCH_DELAY_MS);
                }

                _logger.LogDebug($"[SmartTabNavigator] Window taraması tamamlandı: {tabCount} tab kontrol edildi");
                return new TabCloseResult { Success = false };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SmartTabNavigator] Window tarama hatası: {ex.Message}", ex);
                return new TabCloseResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Window title'ını okur
        /// </summary>
        private string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                const int maxLength = 256;
                var sb = new StringBuilder(maxLength);
                int length = GetWindowText(hWnd, sb, maxLength);

                if (length > 0)
                {
                    return sb.ToString();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SmartTabNavigator] GetWindowTitle hatası: {ex.Message}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Title ile keyword eşleşmesi kontrolü (Türkçe normalizasyon ile)
        /// Gelişmiş logging ve başlangıç eşleşmesi ile
        /// </summary>
        private bool TitleMatchesKeyword(string title, string keyword)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(keyword))
                return false;

            // Her iki string'i de normalize et
            var normalizedTitle = NormalizeTurkish(title.ToLowerInvariant());
            var normalizedKeyword = NormalizeTurkish(keyword.ToLowerInvariant());

            _logger.LogDebug($"[SmartTabNavigator] 🔍 Matching check:");
            _logger.LogDebug($"[SmartTabNavigator]   Original title: '{title}'");
            _logger.LogDebug($"[SmartTabNavigator]   Original keyword: '{keyword}'");
            _logger.LogDebug($"[SmartTabNavigator]   Normalized title: '{normalizedTitle}'");
            _logger.LogDebug($"[SmartTabNavigator]   Normalized keyword: '{normalizedKeyword}'");

            // 1. Basit contains kontrolü
            bool containsMatch = normalizedTitle.Contains(normalizedKeyword);
            _logger.LogDebug($"[SmartTabNavigator]   Contains match: {containsMatch}");

            if (containsMatch)
            {
                _logger.LogInformation($"[SmartTabNavigator] ✓ Title match (contains): '{title}' contains '{keyword}'");
                return true;
            }

            // 2. Başlangıç eşleşmesi - "Milliyet - Haberler..." → "Milliyet" ✓
            var titleWords = normalizedTitle.Split(new[] { ' ', '-', '|', '–', '—' }, StringSplitOptions.RemoveEmptyEntries);
            if (titleWords.Length > 0 && titleWords[0] == normalizedKeyword)
            {
                _logger.LogInformation($"[SmartTabNavigator] ✓ Title match (starts-with): '{title}' starts with '{keyword}'");
                return true;
            }
            _logger.LogDebug($"[SmartTabNavigator]   Starts-with match: FALSE (first word: '{titleWords.FirstOrDefault()}')");

            // 3. Keyword'ün her kelimesi title'da var mı?
            var keywordWords = normalizedKeyword.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (keywordWords.Length > 1)
            {
                bool allWordsMatch = keywordWords.All(word => normalizedTitle.Contains(word));
                _logger.LogDebug($"[SmartTabNavigator]   Multi-word match: {allWordsMatch}");

                if (allWordsMatch)
                {
                    _logger.LogInformation($"[SmartTabNavigator] ✓ Title match (multi-word): '{title}' ~ '{keyword}'");
                    return true;
                }
            }

            // 4. Fuzzy matching - Levenshtein distance
            var originalNormalizedTitle = normalizedTitle;
            var originalNormalizedKeyword = normalizedKeyword;

            // Levenshtein distance için çok uzun string'leri kısalt
            if (normalizedTitle.Length > 100) normalizedTitle = normalizedTitle.Substring(0, 100);
            if (normalizedKeyword.Length > 50) normalizedKeyword = normalizedKeyword.Substring(0, 50);

            int distance = LevenshteinDistance(normalizedTitle, normalizedKeyword);
            int maxDistance = Math.Max(3, normalizedKeyword.Length / 4); // %25 tolerance

            _logger.LogDebug($"[SmartTabNavigator]   Fuzzy match: distance={distance}, maxDistance={maxDistance}");

            if (distance <= maxDistance)
            {
                _logger.LogInformation($"[SmartTabNavigator] ✓ Title match (fuzzy): '{title}' ~ '{keyword}' (distance: {distance})");
                return true;
            }

            // Eşleşme yok
            _logger.LogDebug($"[SmartTabNavigator]   ✗ NO MATCH - All methods failed");
            return false;
        }

        /// <summary>
        /// Türkçe karakterleri normalize eder (ı→i, ğ→g, ü→u, ş→s, ö→o, ç→c)
        /// </summary>
        private string NormalizeTurkish(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            return text
                .Replace('ı', 'i').Replace('İ', 'I')
                .Replace('ğ', 'g').Replace('Ğ', 'G')
                .Replace('ü', 'u').Replace('Ü', 'U')
                .Replace('ş', 's').Replace('Ş', 'S')
                .Replace('ö', 'o').Replace('Ö', 'O')
                .Replace('ç', 'c').Replace('Ç', 'C');
        }

        /// <summary>
        /// Levenshtein distance algoritması - String benzerlik ölçümü
        /// </summary>
        private int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return string.IsNullOrEmpty(target) ? 0 : target.Length;

            if (string.IsNullOrEmpty(target))
                return source.Length;

            int sourceLength = source.Length;
            int targetLength = target.Length;

            var distance = new int[sourceLength + 1, targetLength + 1];

            // İlk satır ve sütunu doldur
            for (int i = 0; i <= sourceLength; distance[i, 0] = i++) { }
            for (int j = 0; j <= targetLength; distance[0, j] = j++) { }

            // Matrix'i doldur
            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                    distance[i, j] = Math.Min(
                        Math.Min(
                            distance[i - 1, j] + 1,      // Silme
                            distance[i, j - 1] + 1),     // Ekleme
                        distance[i - 1, j - 1] + cost); // Değiştirme
                }
            }

            return distance[sourceLength, targetLength];
        }

        /// <summary>
        /// Ctrl+W tuş kombinasyonunu gönderir (tab kapatma)
        /// </summary>
        private void SendCtrlW()
        {
            try
            {
                _logger.LogDebug("[SmartTabNavigator] Ctrl+W gönderiliyor");

                // Ctrl tuşunu bas
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                Thread.Sleep(KEY_PRESS_DELAY_MS);

                // W tuşunu bas
                keybd_event(VK_W, 0, 0, UIntPtr.Zero);
                Thread.Sleep(KEY_PRESS_DELAY_MS);

                // W tuşunu bırak
                keybd_event(VK_W, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(KEY_PRESS_DELAY_MS);

                // Ctrl tuşunu bırak
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SmartTabNavigator] SendCtrlW hatası: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Ctrl+Tab tuş kombinasyonunu gönderir (sonraki tab'a geçiş)
        /// </summary>
        private void SendCtrlTab()
        {
            try
            {
                _logger.LogDebug("[SmartTabNavigator] Ctrl+Tab gönderiliyor");

                // Ctrl tuşunu bas
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                Thread.Sleep(KEY_PRESS_DELAY_MS);

                // Tab tuşunu bas
                keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
                Thread.Sleep(KEY_PRESS_DELAY_MS);

                // Tab tuşunu bırak
                keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(KEY_PRESS_DELAY_MS);

                // Ctrl tuşunu bırak
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SmartTabNavigator] SendCtrlTab hatası: {ex.Message}", ex);
            }
        }
    }
}
