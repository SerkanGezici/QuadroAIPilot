using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Açık bir klasörü (Explorer penceresi) isminden bulup kapatır
    /// "ekran kartı klasörünü kapat" gibi komutları işler
    /// </summary>
    public class CloseFolderCommand : ICommand
    {
        public string CommandText { get; }
        private readonly string _folderName;

        public CloseFolderCommand(string commandText, string folderName)
        {
            CommandText = commandText;
            _folderName = folderName;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[CloseFolderCommand] Klasör kapatılıyor: {_folderName}");

                // Explorer süreçlerini al
                var explorerProcesses = Process.GetProcessesByName("explorer");

                bool foundAndClosed = false;
                int closedCount = 0;

                foreach (var process in explorerProcesses)
                {
                    try
                    {
                        // Window title kontrolü
                        if (string.IsNullOrEmpty(process.MainWindowTitle))
                            continue;

                        // Klasör adı normalizasyonu (Türkçe karakter desteği)
                        string normalizedFolderName = NormalizeTurkish(_folderName.ToLowerInvariant());
                        string normalizedWindowTitle = NormalizeTurkish(process.MainWindowTitle.ToLowerInvariant());

                        // Kelime kelime kontrol (kısmi eşleşme)
                        var folderWords = normalizedFolderName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        bool allWordsMatch = folderWords.All(word =>
                            normalizedWindowTitle.Contains(word));

                        if (allWordsMatch)
                        {
                            Debug.WriteLine($"[CloseFolderCommand] Eşleşen klasör bulundu: {process.MainWindowTitle} (PID: {process.Id})");

                            // KRİTİK: Explorer için SADECE CloseMainWindow kullan, ASLA Kill kullanma!
                            // Explorer.exe sistem kritik bir process
                            if (process.MainWindowHandle != IntPtr.Zero)
                            {
                                bool closed = process.CloseMainWindow();

                                if (closed)
                                {
                                    // Explorer genelde hızlı kapanır, kısa bir bekle
                                    await Task.Run(() => process.WaitForExit(3000));

                                    closedCount++;
                                    foundAndClosed = true;
                                    Debug.WriteLine($"[CloseFolderCommand] Klasör penceresi başarıyla kapatıldı");
                                }
                                else
                                {
                                    Debug.WriteLine($"[CloseFolderCommand] CloseMainWindow false döndü (PID: {process.Id})");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[CloseFolderCommand] MainWindowHandle boş (PID: {process.Id})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CloseFolderCommand] Process kontrolü hatası: {ex.Message}");
                    }
                }

                if (foundAndClosed)
                {
                    string message = closedCount == 1
                        ? $"{_folderName} klasörü kapatıldı"
                        : $"{closedCount} adet {_folderName} klasör penceresi kapatıldı";

                    await TextToSpeechService.SpeakTextAsync(message);
                    return true;
                }
                else
                {
                    await TextToSpeechService.SpeakTextAsync($"{_folderName} klasörü açık değil");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CloseFolderCommand] Hata: {ex.Message}");
                await TextToSpeechService.SpeakTextAsync("Klasör kapatılırken hata oluştu");
                return false;
            }
        }

        /// <summary>
        /// Türkçe karakterleri normalize eder
        /// </summary>
        private string NormalizeTurkish(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            return input
                .Replace('ı', 'i').Replace('İ', 'I')
                .Replace('ğ', 'g').Replace('Ğ', 'G')
                .Replace('ü', 'u').Replace('Ü', 'U')
                .Replace('ş', 's').Replace('Ş', 'S')
                .Replace('ö', 'o').Replace('Ö', 'O')
                .Replace('ç', 'c').Replace('Ç', 'C');
        }
    }
}
