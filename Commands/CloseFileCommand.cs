using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Belirli bir dosyayı isminden bulup kapatma komutu
    /// "bilgehan excel dosyasını kapat" gibi komutları işler
    /// </summary>
    public class CloseFileCommand : ICommand
    {
        public string CommandText { get; }
        private readonly string _fileName;
        private readonly string _fileType;

        public CloseFileCommand(string commandText, string fileName, string fileType = "")
        {
            CommandText = commandText;
            _fileName = fileName;
            _fileType = fileType;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[CloseFileCommand] Dosya kapatılıyor: {_fileName} (Tür: {_fileType})");

                // Dosya türüne göre hangi uygulamaları kontrol edeceğimizi belirle
                string[] processNames = DetermineProcessNames(_fileType);

                bool foundAndClosed = false;
                int closedCount = 0;

                foreach (var processName in processNames)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(processName);
                        Debug.WriteLine($"[CloseFileCommand] {processName} process'lerinde {processes.Length} adet bulundu");

                        foreach (var process in processes)
                        {
                            try
                            {
                                // Window title kontrolü
                                if (string.IsNullOrEmpty(process.MainWindowTitle))
                                {
                                    Debug.WriteLine($"[CloseFileCommand] Process {process.Id} - Window title boş, atlanıyor");
                                    continue;
                                }

                                Debug.WriteLine($"[CloseFileCommand] Process {process.Id} - Window title: '{process.MainWindowTitle}'");

                                // Dosya adı window title içinde var mı?
                                if (WindowTitleContainsFileName(process.MainWindowTitle, _fileName))
                                {
                                    Debug.WriteLine($"[CloseFileCommand] ✓ Eşleşen pencere bulundu: {process.MainWindowTitle} (PID: {process.Id})");

                                    // Güvenli kapatma: CloseMainWindow → Wait(10s) → Kill
                                    bool closed = await CloseProcessSafely(process);

                                    if (closed)
                                    {
                                        closedCount++;
                                        foundAndClosed = true;
                                        Debug.WriteLine($"[CloseFileCommand] Dosya başarıyla kapatıldı");
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine($"[CloseFileCommand] ✗ Eşleşme yok: '{_fileName}' bulunamadı '{process.MainWindowTitle}' içinde");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[CloseFileCommand] Process kontrolü hatası: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CloseFileCommand] Process enumeration hatası ({processName}): {ex.Message}");
                    }
                }

                if (foundAndClosed)
                {
                    string message = closedCount == 1
                        ? $"{_fileName} dosyası kapatıldı"
                        : $"{closedCount} adet {_fileName} dosyası kapatıldı";

                    await TextToSpeechService.SpeakTextAsync(message);
                    return true;
                }
                else
                {
                    await TextToSpeechService.SpeakTextAsync($"{_fileName} dosyası açık değil");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CloseFileCommand] Hata: {ex.Message}");
                await TextToSpeechService.SpeakTextAsync("Dosya kapatılırken hata oluştu");
                return false;
            }
        }

        /// <summary>
        /// Window title'ın dosya adını içerip içermediğini kontrol eder
        /// </summary>
        private bool WindowTitleContainsFileName(string windowTitle, string fileName)
        {
            if (string.IsNullOrWhiteSpace(windowTitle) || string.IsNullOrWhiteSpace(fileName))
                return false;

            // Türkçe karakter normalizasyonu
            var normalizedTitle = NormalizeTurkish(windowTitle.ToLowerInvariant());
            var normalizedFileName = NormalizeTurkish(fileName.ToLowerInvariant());

            // Basit contains kontrolü
            return normalizedTitle.Contains(normalizedFileName);
        }

        /// <summary>
        /// Process'i güvenli şekilde kapatır: CloseMainWindow → Wait(10s) → Kill
        /// </summary>
        private async Task<bool> CloseProcessSafely(Process process)
        {
            try
            {
                // Adım 1: Güvenli kapatma dene (CloseMainWindow)
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    process.CloseMainWindow();

                    // Adım 2: 10 saniye bekle (unsaved changes için)
                    await Task.Run(() =>
                    {
                        if (process.WaitForExit(10000)) // 10 saniye timeout
                        {
                            return true;
                        }
                        return false;
                    });

                    if (process.HasExited)
                    {
                        Debug.WriteLine($"[CloseFileCommand] Process gracefully closed (PID: {process.Id})");
                        return true;
                    }
                }

                // Adım 3: Hala çalışıyorsa zorla kapat
                if (!process.HasExited)
                {
                    Debug.WriteLine($"[CloseFileCommand] Timeout, zorla kapatılıyor (PID: {process.Id})");
                    process.Kill();

                    // Kill sonrası kısa bir bekle
                    await Task.Run(() => process.WaitForExit(2000));

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CloseFileCommand] CloseProcessSafely hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Dosya türüne göre hangi process'leri kontrol edeceğimizi belirler
        /// </summary>
        private string[] DetermineProcessNames(string fileType)
        {
            // Dosya türü belirtilmişse ona göre, yoksa tüm Office uygulamalarını kontrol et
            return fileType.ToLowerInvariant() switch
            {
                "excel" => new[] { "EXCEL" },
                "word" => new[] { "WINWORD" },
                "powerpoint" => new[] { "POWERPNT" },
                "pdf" => new[] { "AcroRd32", "Acrobat", "FoxitReader", "msedge", "chrome" },
                "notepad" or "metin" => new[] { "notepad", "notepad++" },
                _ => new[] { "EXCEL", "WINWORD", "POWERPNT", "notepad", "AcroRd32", "Acrobat" } // Tümünü dene
            };
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
