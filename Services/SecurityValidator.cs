using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Güvenlik doğrulama ve temizleme işlemleri için yardımcı sınıf
    /// SECURITY: OWASP Top 10 standartlarına uygun güvenlik kontrolleri
    /// </summary>
    public static class SecurityValidator
    {
        #region P/Invoke - Canonical Path Resolution

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(IntPtr hFile,
            StringBuilder lpszFilePath, int cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        #endregion

        // Güvenli olmayan karakterler ve desenler
        // NOT: Windows drive letter'da : kullanılır (C:), bu yüzden : sadece ikinci karakterden sonra yasak
        private static readonly Regex UnsafePathChars = new Regex(@"[<>""|?*]", RegexOptions.Compiled);
        private static readonly Regex InvalidColonUsage = new Regex(@"(?<!^[A-Za-z]):", RegexOptions.Compiled); // Drive letter haricinde : yasak
        private static readonly Regex PathTraversalPattern = new Regex(@"(\.\.[\\/]|\.\.)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UncPathPattern = new Regex(@"^\\\\", RegexOptions.Compiled);
        private static readonly Regex AlternateDataStreamPattern = new Regex(@":[^\\/:*?""<>|]+$", RegexOptions.Compiled); // NTFS ADS detection
        
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
        /// SECURITY FIX: Canonical path resolution, ADS detection, 8.3 short name handling
        /// </summary>
        public static bool IsPathSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // 1. Case-insensitive normalize
                path = path.ToLowerInvariant();

                // 2. NTFS Alternate Data Streams (ADS) detection
                // Örnek: "C:\safe\file.txt:hidden.exe" gibi path'ler engellenmelidir
                if (AlternateDataStreamPattern.IsMatch(path))
                {
                    LoggingService.LogWarning($"[SECURITY] Alternate Data Stream detected: {path}");
                    return false;
                }

                // 3. Path traversal kontrolü
                if (PathTraversalPattern.IsMatch(path))
                {
                    LoggingService.LogWarning($"[SECURITY] Path traversal attempt detected: {path}");
                    return false;
                }

                // 4. UNC path kontrolü (ağ paylaşımları)
                if (UncPathPattern.IsMatch(path))
                {
                    LoggingService.LogWarning($"[SECURITY] UNC path detected: {path}");
                    return false;
                }

                // 5. Güvenli olmayan karakterler
                if (UnsafePathChars.IsMatch(path))
                {
                    LoggingService.LogWarning($"[SECURITY] Unsafe characters in path: {path}");
                    return false;
                }

                // 6. Windows drive letter haricinde : karakteri kullanımını kontrol et
                if (InvalidColonUsage.IsMatch(path))
                {
                    LoggingService.LogWarning($"[SECURITY] Invalid colon usage in path (not drive letter): {path}");
                    return false;
                }

                // 7. Device path kontrolü (CON, PRN, AUX, NUL, COM1-9, LPT1-9)
                var deviceNames = new[] { "con", "prn", "aux", "nul" };
                var fileName = Path.GetFileNameWithoutExtension(path).ToLower();
                if (deviceNames.Any(d => fileName.StartsWith(d)))
                {
                    LoggingService.LogWarning($"[SECURITY] Device path detected: {path}");
                    return false;
                }

                // COM1-COM9, LPT1-LPT9 kontrolü
                if (Regex.IsMatch(fileName, @"^(com|lpt)[1-9]", RegexOptions.IgnoreCase))
                {
                    LoggingService.LogWarning($"[SECURITY] Device path (COM/LPT) detected: {path}");
                    return false;
                }

                // 8. Canonical path resolution (symlink, junction detection)
                string canonicalPath = GetCanonicalPath(path);
                if (!string.IsNullOrEmpty(canonicalPath))
                {
                    path = canonicalPath.ToLowerInvariant();
                }

                // 9. Mutlak yol oluştur
                string fullPath = Path.GetFullPath(path);

                // 10. Blacklist validation (kritik sistem dizinleri)
                var blacklistedPaths = new[]
                {
                    @"c:\windows\system32",
                    @"c:\windows\syswow64",
                    @"c:\boot",
                    @"c:\recovery",
                    @"c:\windows\winsxs"
                };

                if (blacklistedPaths.Any(b => fullPath.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
                {
                    LoggingService.LogWarning($"[SECURITY] Blacklisted system path: {fullPath}");
                    return false;
                }

                // 11. Whitelist validation
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Kullanıcı profili içinde ise güvenli
                if (fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Sistem klasörlerine sınırlı erişim
                string[] allowedSystemPaths = {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)
                };

                bool isAllowedSystemPath = allowedSystemPaths.Any(p =>
                    !string.IsNullOrEmpty(p) && fullPath.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                if (!isAllowedSystemPath)
                {
                    LoggingService.LogWarning($"[SECURITY] Path outside allowed directories: {fullPath}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[SECURITY] Path validation error: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Canonical path resolution - symlink ve junction point'leri resolve eder
        /// SECURITY: Symlink attack, junction exploitation önleme
        /// </summary>
        public static string GetCanonicalPath(string path)
        {
            try
            {
                // Dosya/dizin yoksa canonical resolution yapılamaz
                if (!File.Exists(path) && !Directory.Exists(path))
                    return path;

                IntPtr handle = CreateFile(
                    path,
                    GENERIC_READ,
                    FILE_SHARE_READ,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_FLAG_BACKUP_SEMANTICS,
                    IntPtr.Zero);

                if (handle == IntPtr.Zero || handle.ToInt32() == -1)
                    return path;

                try
                {
                    StringBuilder sb = new StringBuilder(260);
                    uint result = GetFinalPathNameByHandle(handle, sb, sb.Capacity, 0);

                    if (result > 0)
                    {
                        // Remove \\?\ prefix
                        string canonicalPath = sb.ToString();
                        if (canonicalPath.StartsWith(@"\\?\"))
                            canonicalPath = canonicalPath.Substring(4);

                        return canonicalPath;
                    }

                    return path;
                }
                finally
                {
                    CloseHandle(handle);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[SECURITY] Canonical path resolution failed: {ex.Message}");
                return path; // Fallback to original path
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
        /// JavaScript kodunun tehlikeli fonksiyonlar içerip içermediğini kontrol eder
        /// SECURITY: XSS ve Script Injection koruması
        /// </summary>
        public static bool IsScriptSafe(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                return false;

            try
            {
                // Script length validation (max 100KB for theme scripts)
                if (script.Length > 100000)
                {
                    LoggingService.LogWarning($"[SECURITY] Script too long: {script.Length} characters");
                    return false;
                }

                // WHITELIST: Trusted script patterns (internal application scripts)
                var trustedPatterns = new[]
                {
                    // Theme & UI State
                    "// F5 refresh kontrolü",                                    // ThemeManager tema scripts
                    "sessionStorage.getItem('pageRefreshed')",                   // Theme refresh check
                    "document.body.style.background =",                          // Theme background
                    "root.style.setProperty('--",                                // CSS variables
                    "document.body.classList.add('theme-",                       // Theme class
                    "document.body.classList.add('processing')",                 // UIManager processing indicator
                    "document.body.classList.remove('processing')",              // UIManager processing indicator
                    "window.dispatchEvent(new CustomEvent('themeChanged'",      // Theme event

                    // DOM Manipulation (Internal only)
                    "document.createElement('div')",                             // Widget creation
                    "document.createElement('style')",                           // Style injection
                    "document.querySelector('#txtCikti')",                       // Textarea selection
                    "greetingDiv.innerHTML = `",                                 // Personalized greeting

                    // Widget & Feedback
                    "appendFeedback(",                                           // Feedback messages
                    "updateMailCount(",                                          // Mail widget
                    "updateMeetingCount(",                                       // Meeting widget
                    "updateNewsWidget(",                                         // News widget

                    // User Actions
                    "toggleDikte()",                                             // Dictation control
                    "toggleFocusMode()",                                         // Focus mode
                    "executeCommand()",                                          // Command execution
                    "toggleCommandPalette()",                                    // Command palette
                    "closeAllModals()",                                          // Modal control
                    "executeCommandAction(",                                     // Command actions

                    // Textarea Operations
                    "textarea.click()",                                          // Focus textarea
                    "textarea.focus()",                                          // Focus textarea
                    "textarea.value = ''",                                       // Clear textarea
                    "textarea.setSelectionRange(",                               // Cursor position
                    "checkButtonStates()",                                       // UI state update

                    // Audio & Media
                    "const audioContext = new (window.AudioContext",             // Audio API
                    "window.speechSynthesis",                                    // TTS API
                    "const gl = canvas.getContext('webgl",                       // GPU check

                    // Web Audio API & Media Capture (Microphone Support)
                    "navigator.mediaDevices.getUserMedia",                       // Mikrofon erişimi
                    "navigator.mediaDevices.enumerateDevices",                   // Cihaz listesi
                    "new MediaRecorder(",                                        // Ses kaydı
                    "audioContext.createMediaStreamSource",                      // Stream işleme
                    "audioContext.createAnalyser",                               // Audio analysis
                    "audioContext.createScriptProcessor",                        // Custom processing
                    "audioContext.destination",                                  // Output routing
                    "audioContext.resume()",                                     // Context resume
                    "audioContext.suspend()",                                    // Context pause
                    "mediaRecorder.start()",                                     // Kayıt başlat
                    "mediaRecorder.stop()",                                      // Kayıt durdur
                    "mediaRecorder.ondataavailable",                             // Data handler
                    "new Blob(",                                                 // Audio blob
                    "URL.createObjectURL(",                                      // Blob URL
                    "new Audio(",                                                // Audio playback
                    "audio.play()",                                              // Play control
                    "audio.pause()",                                             // Pause control
                    "navigator.permissions.query",                               // Permission check
                    "{ name: 'microphone' }",                                    // Permission name
                    "async function(",                                           // Async function declaration

                    // Performance & Debugging
                    "window.performance && window.performance.navigation",       // Performance check
                    "console.log(",                                              // Debug logging
                    "console.error("                                             // Error logging
                };

                // If script contains any trusted pattern, allow it (internal application code)
                string scriptLower = script.ToLowerInvariant();
                foreach (var pattern in trustedPatterns)
                {
                    if (scriptLower.Contains(pattern.ToLowerInvariant()))
                    {
                        // Trusted internal script - skip dangerous function checks
                        return true;
                    }
                }

                // BLACKLIST: Tehlikeli JavaScript fonksiyonları (only for non-whitelisted scripts)

                // setTimeout/setInterval - Sadece audio context ile birlikte safe
                bool hasAudioContext = scriptLower.Contains("audiocontext") ||
                                       scriptLower.Contains("mediarecorder") ||
                                       scriptLower.Contains("getusermedia");

                if ((scriptLower.Contains("settimeout(") || scriptLower.Contains("setinterval(")) && !hasAudioContext)
                {
                    LoggingService.LogWarning($"[SECURITY] Dangerous timer function detected without audio context");
                    return false;
                }

                var dangerousFunctions = new[]
                {
                    "eval(",
                    "Function(",
                    "document.write(",
                    "document.writeln(",
                    "location.href",
                    "location.replace",
                    "location.assign",
                    "window.location",
                    "document.location",
                    "<script",
                    "</script>",
                    "javascript:",
                    "onerror=",
                    "onload=",
                    "onclick=",
                    "onfocus=",
                    "onblur="
                };

                // Case-insensitive check
                foreach (var func in dangerousFunctions)
                {
                    if (scriptLower.Contains(func.ToLowerInvariant()))
                    {
                        LoggingService.LogWarning($"[SECURITY] Dangerous function detected in script: {func}");
                        return false;
                    }
                }

                // Base64 encoded script detection (possible obfuscation)
                if (scriptLower.Contains("atob(") || scriptLower.Contains("btoa("))
                {
                    LoggingService.LogWarning($"[SECURITY] Base64 encoding detected in script (possible obfuscation)");
                    return false;
                }

                // External resource loading
                if (scriptLower.Contains("import ") || scriptLower.Contains("require("))
                {
                    LoggingService.LogWarning($"[SECURITY] External resource loading detected in script");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[SECURITY] Script validation error: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Tehlikeli pattern'lerin varlığını kontrol eder
        /// </summary>
        public static bool ContainsDangerousPatterns(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            try
            {
                // Command injection patterns
                var dangerousPatterns = new[]
                {
                    @"\.\./",           // Path traversal
                    @"\.\.\\",          // Path traversal (Windows)
                    @"[;&|]",           // Command chaining
                    @"`.*`",            // Command substitution
                    @"\$\(",            // Command substitution
                    @"<script",         // XSS
                    @"javascript:",     // JavaScript protocol
                    @"data:text/html",  // Data URI XSS
                    @"vbscript:",       // VBScript protocol
                    @"\x00"             // Null byte injection
                };

                foreach (var pattern in dangerousPatterns)
                {
                    if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                    {
                        LoggingService.LogWarning($"[SECURITY] Dangerous pattern detected: {pattern}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[SECURITY] Pattern detection error: {ex.Message}", ex);
                return true; // Hata durumunda güvenli olmadığını varsay
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