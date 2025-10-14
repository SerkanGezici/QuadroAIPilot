using System;

namespace QuadroAIPilot.Models
{
    /// <summary>
    /// Güncelleme bilgilerini içeren model sınıfı
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// Yeni versiyon numarası (örn: 1.2.0)
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Setup dosyası indirme URL'i
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// Değişiklik notları URL'i (GitHub release notes)
        /// </summary>
        public string ChangelogUrl { get; set; } = string.Empty;

        /// <summary>
        /// Zorunlu güncelleme mi? (Kritik bug fix için true)
        /// </summary>
        public bool IsMandatory { get; set; }

        /// <summary>
        /// Setup dosyası boyutu (bytes)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Dosya hash'i (SHA256) - güvenlik doğrulaması için
        /// </summary>
        public string Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Release tarihi
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Release notları (kısa özet)
        /// </summary>
        public string ReleaseNotes { get; set; } = string.Empty;

        /// <summary>
        /// Minimum gerekli Windows sürümü (Build numarası)
        /// </summary>
        public int MinimumWindowsBuild { get; set; } = 22000; // Windows 11

        /// <summary>
        /// Yeni versiyon mevcut mu kontrolü
        /// </summary>
        /// <param name="currentVersion">Mevcut uygulama versiyonu</param>
        /// <returns>Yeni versiyon varsa true</returns>
        public bool IsNewerVersion(string currentVersion)
        {
            try
            {
                var current = System.Version.Parse(currentVersion);
                var newVersion = System.Version.Parse(Version);
                return newVersion > current;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Dosya boyutunu okunabilir formata çevirir
        /// </summary>
        public string GetReadableFileSize()
        {
            if (FileSize == 0) return "Bilinmiyor";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = FileSize;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
