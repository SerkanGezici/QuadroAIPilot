using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Güvenlik doğrulama ve temizleme işlemleri için yardımcı sınıf
    /// </summary>
    public static class SecurityValidator
    {
        // Güvenli olmayan karakterler ve desenler
        private static readonly Regex UnsafePathChars = new Regex(@"[<>:""|?*]", RegexOptions.Compiled);
        private static readonly Regex PathTraversalPattern = new Regex(@"(\.\.[\\/]|\.\.)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UncPathPattern = new Regex(@"^\\\\", RegexOptions.Compiled);
        
        // Komut enjeksiyonu için tehlikeli karakterler
        private static readonly char[] CommandInjectionChars = { ';', '|', '&', '$', '`', '\n', '\r', '>', '<', '(', ')' };
        
        // Güvenli dosya uzantıları listesi
        private static readonly HashSet<string> SafeFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", 
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".rtf",
            ".csv", ".xml", ".json", ".html", ".htm", ".md", ".log"
        };

        // Tehlikeli dosya uzantıları
        private static readonly HashSet<string> DangerousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".com", ".scr", ".vbs", ".vbe", 
            ".js", ".jse", ".ws", ".wsf", ".wsc", ".wsh", ".ps1", 
            ".psc1", ".msc", ".msi", ".dll", ".sys", ".reg"
        };

        /// <summary>
        /// Dosya yolunun güvenli olup olmadığını kontrol eder
        /// </summary>
        public static bool IsPathSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Path traversal kontrolü
                if (PathTraversalPattern.IsMatch(path))
                {
                    LoggingService.LogWarning($"Path traversal attempt detected: {path}");
                    return false;
                }

                // UNC path kontrolü (ağ paylaşımları)
                if (UncPathPattern.IsMatch(path))
                {
                    LoggingService.LogWarning($"UNC path detected: {path}");
                    return false;
                }

                // Güvenli olmayan karakterler
                if (UnsafePathChars.IsMatch(path))
                {
                    LoggingService.LogWarning($"Unsafe characters in path: {path}");
                    return false;
                }

                // Mutlak yol oluştur ve kontrol et
                string fullPath = Path.GetFullPath(path);
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Kullanıcı profili dışına çıkış kontrolü
                if (!fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
                {
                    // Sistem klasörlerine erişim kontrolü
                    string[] allowedSystemPaths = {
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)
                    };

                    bool isAllowedSystemPath = allowedSystemPaths.Any(p => 
                        fullPath.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                    if (!isAllowedSystemPath)
                    {
                        LoggingService.LogWarning($"Path outside allowed directories: {path}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Path validation error: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Dosya uzantısının güvenli olup olmadığını kontrol eder
        /// </summary>
        public static bool IsFileExtensionSafe(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string extension = Path.GetExtension(filePath);
            
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            // Tehlikeli uzantı kontrolü
            if (DangerousExtensions.Contains(extension))
            {
                LoggingService.LogWarning($"Dangerous file extension detected: {extension}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Komut enjeksiyonu için string'i temizler
        /// </summary>
        public static string SanitizeForCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Tehlikeli karakterleri kaldır
            string sanitized = input;
            foreach (char c in CommandInjectionChars)
            {
                sanitized = sanitized.Replace(c.ToString(), string.Empty);
            }

            // Çift/tek tırnak temizleme
            sanitized = sanitized.Replace("\"", string.Empty).Replace("'", string.Empty);

            // Birden fazla boşluğu tek boşluğa indir
            sanitized = Regex.Replace(sanitized, @"\s+", " ");

            return sanitized.Trim();
        }

        /// <summary>
        /// HTML/JavaScript için string'i temizler (XSS koruması)
        /// </summary>
        public static string SanitizeForHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // HTML encode
            return System.Net.WebUtility.HtmlEncode(input);
        }

        /// <summary>
        /// Dosya adını güvenli hale getirir
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            // Path.GetInvalidFileNameChars() kullanarak geçersiz karakterleri temizle
            string sanitized = fileName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, '_');
            }

            // Ek güvenlik kontrolleri
            sanitized = sanitized.Replace("..", "_");
            sanitized = sanitized.Trim('.', ' ');

            // Maksimum uzunluk kontrolü (Windows için 255 karakter)
            if (sanitized.Length > 255)
            {
                string extension = Path.GetExtension(sanitized);
                string nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
                sanitized = nameWithoutExt.Substring(0, 255 - extension.Length) + extension;
            }

            return sanitized;
        }

        /// <summary>
        /// URL'nin güvenli olup olmadığını kontrol eder
        /// </summary>
        public static bool IsUrlSafe(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                Uri uri = new Uri(url);

                // Sadece HTTP ve HTTPS protokollerine izin ver
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    LoggingService.LogWarning($"Unsafe URL scheme: {uri.Scheme}");
                    return false;
                }

                // Local file sistemine erişim kontrolü
                if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("127.0.0.1") || uri.Host.StartsWith("192.168.") || 
                    uri.Host.StartsWith("10.") || uri.Host.StartsWith("172."))
                {
                    LoggingService.LogWarning($"Local/Internal URL detected: {url}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"URL validation error: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Process başlatma için güvenli argüman listesi oluşturur
        /// </summary>
        public static string[] CreateSafeProcessArguments(string command, string arguments = null)
        {
            if (string.IsNullOrWhiteSpace(command))
                return Array.Empty<string>();

            List<string> safeArgs = new List<string>();

            // Komut güvenlik kontrolü
            string safeCommand = SanitizeForCommand(command);
            if (string.IsNullOrWhiteSpace(safeCommand))
                return Array.Empty<string>();

            safeArgs.Add(safeCommand);

            // Argümanlar varsa güvenli hale getir
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                // Argümanları parçala ve her birini temizle
                string[] argParts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (string arg in argParts)
                {
                    string safeArg = SanitizeForCommand(arg);
                    if (!string.IsNullOrWhiteSpace(safeArg))
                    {
                        safeArgs.Add(safeArg);
                    }
                }
            }

            return safeArgs.ToArray();
        }
    }
}