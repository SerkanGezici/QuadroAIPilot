using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// İsimlendirilmiş klasör oluşturma komutu
    /// "denemeler adında yeni klasör oluştur" gibi komutları işler
    /// </summary>
    public class CreateNamedFolderCommand : ICommand
    {
        public string CommandText { get; }
        private readonly string _folderName;

        public CreateNamedFolderCommand(string commandText, string folderName)
        {
            CommandText = commandText;
            _folderName = folderName;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Debug.WriteLine($"[CreateNamedFolderCommand] Klasör oluşturuluyor: {_folderName}");

                // Mevcut dizini al (Desktop varsayılan)
                string currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Yeni klasör yolu
                string newFolderPath = Path.Combine(currentPath, _folderName);

                // Aynı isimde klasör varsa sayı ekle
                int counter = 1;
                while (Directory.Exists(newFolderPath))
                {
                    newFolderPath = Path.Combine(currentPath, $"{_folderName} ({counter})");
                    counter++;
                }

                // Klasörü oluştur
                Directory.CreateDirectory(newFolderPath);

                Debug.WriteLine($"[CreateNamedFolderCommand] Klasör oluşturuldu: {newFolderPath}");

                // Başarı mesajı
                string finalName = Path.GetFileName(newFolderPath);
                await TextToSpeechService.SpeakTextAsync($"{finalName} klasörü oluşturuldu");

                // Explorer'da aç
                Process.Start("explorer.exe", newFolderPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CreateNamedFolderCommand] Hata: {ex.Message}");
                await TextToSpeechService.SpeakTextAsync("Klasör oluşturulurken hata oluştu");
                return false;
            }
        }
    }
}
