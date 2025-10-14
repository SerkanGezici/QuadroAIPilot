using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Belirli bir uygulamayı ismiyle kapatma komutu
    /// "whatsapp uygulamasını kapat", "chrome'u kapat" gibi komutları işler
    /// </summary>
    public class CloseApplicationCommand : ICommand
    {
        public string CommandText { get; }
        private readonly string _applicationName;

        public CloseApplicationCommand(string commandText, string applicationName)
        {
            CommandText = commandText;
            _applicationName = applicationName;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[CloseApplicationCommand] Uygulama kapatılıyor: {_applicationName}");

                // Uygulama ismi eşleştirme (Türkçe karakterler ve yaygın varyantlar)
                var normalizedAppName = NormalizeApplicationName(_applicationName);

                // Çalışan tüm process'leri al
                var allProcesses = Process.GetProcesses();
                var matchedProcesses = allProcesses.Where(p =>
                    ProcessMatches(p, normalizedAppName)).ToList();

                if (matchedProcesses.Count == 0)
                {
                    Debug.WriteLine($"[CloseApplicationCommand] Uygulama bulunamadı: {_applicationName}");
                    await TextToSpeechService.SpeakTextAsync($"{_applicationName} uygulaması çalışmıyor");
                    return false;
                }

                // Eşleşen process'leri kapat
                int closedCount = 0;
                foreach (var process in matchedProcesses)
                {
                    try
                    {
                        Debug.WriteLine($"[CloseApplicationCommand] Kapatılıyor: {process.ProcessName} (PID: {process.Id})");

                        // Önce güvenli kapatma dene (CloseMainWindow)
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            process.CloseMainWindow();

                            // Kapatmasını bekle (5 saniye timeout)
                            if (process.WaitForExit(5000))
                            {
                                closedCount++;
                                continue;
                            }
                        }

                        // Güvenli kapatma işe yaramadıysa zorla kapat
                        if (!process.HasExited)
                        {
                            process.Kill();
                            closedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CloseApplicationCommand] Process kapatma hatası: {ex.Message}");
                    }
                }

                if (closedCount > 0)
                {
                    Debug.WriteLine($"[CloseApplicationCommand] {closedCount} adet {_applicationName} process'i kapatıldı");

                    string message = closedCount == 1
                        ? $"{_applicationName} kapatıldı"
                        : $"{closedCount} adet {_applicationName} penceresi kapatıldı";

                    await TextToSpeechService.SpeakTextAsync(message);
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[CloseApplicationCommand] Hiçbir process kapatılamadı");
                    await TextToSpeechService.SpeakTextAsync($"{_applicationName} kapatılamadı");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CloseApplicationCommand] Hata: {ex.Message}");
                await TextToSpeechService.SpeakTextAsync("Uygulama kapatılırken hata oluştu");
                return false;
            }
        }

        /// <summary>
        /// Uygulama ismini normalize eder (Türkçe karakterler, boşluklar vb.)
        /// </summary>
        private string NormalizeApplicationName(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return string.Empty;

            // Küçük harfe çevir
            var normalized = appName.ToLowerInvariant();

            // Türkçe karakterleri İngilizce karşılıklarına çevir
            normalized = normalized
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ı", "i")
                .Replace("ö", "o")
                .Replace("ç", "c");

            // Boşlukları ve özel karakterleri temizle
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-z0-9]", "");

            return normalized;
        }

        /// <summary>
        /// Process'in belirtilen uygulama ile eşleşip eşleşmediğini kontrol eder
        /// </summary>
        private bool ProcessMatches(Process process, string normalizedAppName)
        {
            try
            {
                // Process adını normalize et
                var processName = NormalizeApplicationName(process.ProcessName);

                // Tam eşleşme
                if (processName == normalizedAppName)
                    return true;

                // Kısmi eşleşme (process adı aranan ismi içeriyorsa)
                if (processName.Contains(normalizedAppName) || normalizedAppName.Contains(processName))
                    return true;

                // Main window title kontrolü
                if (!string.IsNullOrWhiteSpace(process.MainWindowTitle))
                {
                    var windowTitle = NormalizeApplicationName(process.MainWindowTitle);
                    if (windowTitle.Contains(normalizedAppName))
                        return true;
                }

                // Yaygın uygulama isimleri eşleştirmesi
                return MatchKnownApplications(processName, normalizedAppName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Bilinen uygulama isimlerini eşleştirir
        /// </summary>
        private bool MatchKnownApplications(string processName, string normalizedAppName)
        {
            // Tarayıcılar
            if (normalizedAppName.Contains("chrome") && processName.Contains("chrome"))
                return true;
            if (normalizedAppName.Contains("firefox") && processName.Contains("firefox"))
                return true;
            if (normalizedAppName.Contains("edge") && processName.Contains("msedge"))
                return true;
            if (normalizedAppName.Contains("opera") && processName.Contains("opera"))
                return true;

            // Office uygulamaları
            if (normalizedAppName.Contains("word") && processName.Contains("winword"))
                return true;
            if (normalizedAppName.Contains("excel") && processName.Contains("excel"))
                return true;
            if (normalizedAppName.Contains("powerpoint") && (processName.Contains("powerpnt") || processName.Contains("powerpoint")))
                return true;
            if (normalizedAppName.Contains("outlook") && processName.Contains("outlook"))
                return true;

            // Mesajlaşma uygulamaları
            if (normalizedAppName.Contains("whatsapp") && processName.Contains("whatsapp"))
                return true;
            if (normalizedAppName.Contains("telegram") && processName.Contains("telegram"))
                return true;
            if (normalizedAppName.Contains("discord") && processName.Contains("discord"))
                return true;
            if (normalizedAppName.Contains("slack") && processName.Contains("slack"))
                return true;
            if (normalizedAppName.Contains("teams") && processName.Contains("teams"))
                return true;

            // Geliştirme araçları
            if (normalizedAppName.Contains("vscode") && processName.Contains("code"))
                return true;
            if (normalizedAppName.Contains("visual studio") && processName.Contains("devenv"))
                return true;
            if (normalizedAppName.Contains("notepad") && (processName.Contains("notepad") || processName.Contains("notdefteri")))
                return true;

            // Medya oynatıcılar
            if (normalizedAppName.Contains("vlc") && processName.Contains("vlc"))
                return true;
            if (normalizedAppName.Contains("spotify") && processName.Contains("spotify"))
                return true;
            if (normalizedAppName.Contains("wmplayer") && processName.Contains("wmplayer"))
                return true;

            return false;
        }
    }
}
