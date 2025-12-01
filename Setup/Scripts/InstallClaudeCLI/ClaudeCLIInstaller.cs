using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroAIPilot.Setup
{
    /// <summary>
    /// Claude CLI kurulum yöneticisi - Ana installer logic
    /// </summary>
    public class ClaudeCLIInstaller
    {
        private readonly Logger _logger;
        private readonly ProcessRunner _processRunner;

        public ClaudeCLIInstaller(Logger logger)
        {
            _logger = logger;
            _processRunner = new ProcessRunner(logger);
        }

        /// <summary>
        /// Claude CLI kurulumunu başlatır (ana entry point)
        /// </summary>
        public async Task<bool> InstallAsync()
        {
            _logger.Log("============================================");
            _logger.Log("Claude CLI Kurulum Başlıyor (v62)");
            _logger.Log("============================================");
            _logger.LogSeparator();

            try
            {
                // v62: PATH'leri proaktif olarak yükle
                _logger.Log("[v62] PATH environment güncelleniyor...");
                RefreshEnvironmentPaths();
                _logger.LogSeparator();

                // 1. Git kontrolü (Claude CLI dependency)
                _logger.Log("1. Git for Windows kontrolü yapılıyor...");
                if (!await CheckGitAsync())
                {
                    _logger.Log("UYARI: Git bulunamadı ama devam ediliyor...", LogLevel.Warning);
                    _logger.Log("Git kurulumu InstallGit.bat tarafından yapılmış olmalıydı.");
                    _logger.Log("Claude CLI için Git gereklidir!");

                    // v62: Git fallback kontrolü
                    if (!await TryFixGitPath())
                    {
                        _logger.Log("Git PATH düzeltme başarısız, ancak devam ediliyor...", LogLevel.Warning);
                    }
                }
                else
                {
                    _logger.Log("✓ Git DOĞRULANDI", LogLevel.Success);
                }
                _logger.LogSeparator();

                // 2. Node.js kontrolü
                _logger.Log("2. Node.js kontrolü yapılıyor...");
                if (!await CheckNodeJsAsync())
                {
                    _logger.Log("Node.js PATH'te bulunamadı, fallback kontrol...", LogLevel.Warning);

                    // v62: Node.js fallback
                    if (!await TryFixNodePath())
                    {
                        _logger.Log("KRITIK HATA: Node.js bulunamadı!", LogLevel.Error);
                        _logger.LogSeparator();
                        _logger.Log("Node.js kurulumu setup sırasında yapılmış olmalıydı.");
                        _logger.Log("Bu bir SETUP HATASIDIR!");
                        _logger.LogSeparator();
                        _logger.Log("Lütfen GitHub'da issue açın:");
                        _logger.Log("https://github.com/anthropics/quadroaipilot/issues");
                        return false;
                    }
                }
                else
                {
                    _logger.Log("✓ Node.js DOĞRULANDI", LogLevel.Success);
                }
                _logger.LogSeparator();

                // 3. npm kontrolü
                _logger.Log("3. npm kontrolü yapılıyor...");
                if (!await CheckNpmAsync())
                {
                    _logger.Log("HATA: npm bulunamadı veya çalışmıyor!", LogLevel.Error);
                    _logger.Log("Node.js kurulu ama npm eksik - bu olmamalıydı!");
                    return false;
                }
                else
                {
                    _logger.Log("✓ npm DOĞRULANDI", LogLevel.Success);
                }
                _logger.LogSeparator();

                // 4. Claude CLI kurulu mu kontrol et
                _logger.Log("4. Claude CLI kontrol ediliyor...");
                if (await IsClaudeInstalledAsync())
                {
                    _logger.Log("Claude CLI zaten kurulu!", LogLevel.Success);
                    _logger.LogSeparator();
                    return true;
                }
                _logger.Log("Claude CLI kurulu değil, kurulum başlatılıyor...");
                _logger.LogSeparator();

                // 5. Claude CLI kurulumu
                _logger.Log("5. Claude CLI kuruluyor (1-2 dakika sürebilir)...");
                _logger.Log("Bu işlem internet gerektirir, lütfen bekleyin...");
                _logger.LogSeparator();

                bool installSuccess = await InstallClaudeAsync();

                if (!installSuccess)
                {
                    _logger.Log("Claude CLI kurulumu BAŞARISIZ!", LogLevel.Error);
                    return false;
                }

                // 6. Claude OAuth token kontrolü (v58)
                _logger.Log("6. Claude OAuth token kontrolü yapılıyor...");
                await CheckClaudeAuthAsync();
                _logger.LogSeparator();

                // 7. Kurulum doğrulama
                _logger.Log("7. Kurulum doğrulanıyor...");
                if (await IsClaudeInstalledAsync())
                {
                    _logger.Log("============================================", LogLevel.Success);
                    _logger.Log("KURULUM BAŞARILI!", LogLevel.Success);
                    _logger.Log("============================================", LogLevel.Success);
                    _logger.Log("Claude CLI kullanıma hazır.");
                    _logger.LogSeparator();
                    _logger.Log("NOT: Claude CLI kullanmak için API key gereklidir.");
                    _logger.Log("API key: https://console.anthropic.com/");
                    return true;
                }
                else
                {
                    _logger.Log("Kurulum doğrulama BAŞARISIZ!", LogLevel.Error);
                    _logger.Log("npm install başarılı göründü ama claude komutu bulunamadı.");
                    _logger.Log("Lütfen bilgisayarı yeniden başlatın veya yeni terminal açın.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Log("Beklenmeyen hata!", LogLevel.Error);
                _logger.Log($"Exception: {ex.Message}");
                _logger.Log($"StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Git for Windows kurulu mu kontrol eder
        /// </summary>
        private async Task<bool> CheckGitAsync()
        {
            // where git ile kontrol et
            var result = await _processRunner.RunAsync("where", "git", timeoutSeconds: 10);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
            {
                // Git PATH'te bulundu
                var gitPath = result.Output.Split('\n').FirstOrDefault()?.Trim();
                _logger.Log($"Git bulundu: {gitPath}", LogLevel.Success);

                // Version bilgisini al
                var versionResult = await _processRunner.RunAsync("git", "--version", timeoutSeconds: 10);
                if (versionResult.Success)
                {
                    _logger.Log($"Git versiyonu: {versionResult.Output.Trim()}", LogLevel.Success);
                }

                return true;
            }

            // Git bulunamadı
            _logger.Log("Git PATH'te bulunamadı!", LogLevel.Warning);
            return false;
        }

        /// <summary>
        /// Claude OAuth token mevcut mu kontrol eder (v58)
        /// </summary>
        private async Task<bool> CheckClaudeAuthAsync()
        {
            // Credentials dosyası yolunu belirle
            var credPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".claude", ".credentials.json");

            _logger.Log($"OAuth token kontrol ediliyor: {credPath}");

            if (File.Exists(credPath))
            {
                _logger.Log("Claude OAuth token bulundu!", LogLevel.Success);
                _logger.Log($"Token dosyası: {credPath}", LogLevel.Success);

                // claude whoami ile token'ı doğrula
                _logger.Log("Token doğrulanıyor (claude whoami)...");
                var result = await _processRunner.RunAsync("claude", "whoami", timeoutSeconds: 15);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
                {
                    _logger.Log($"Claude auth BAŞARILI: {result.Output.Trim()}", LogLevel.Success);
                    _logger.Log("OAuth token geçerli, Claude CLI kullanıma hazır!", LogLevel.Success);
                    return true;
                }
                else
                {
                    _logger.Log("Token doğrulama başarısız (whoami hatası)", LogLevel.Warning);
                    _logger.Log($"Hata: {result.Error}", LogLevel.Warning);
                }
            }
            else
            {
                _logger.Log("OAuth token bulunamadı!", LogLevel.Warning);
                _logger.Log("Manuel login gerekli: claude login", LogLevel.Warning);
            }

            _logger.Log("NOT: Claude CLI çalışmak için OAuth login gerektirir.");
            _logger.Log("Kurulumdan sonra 'claude login' komutu ile giriş yapın.");
            return false;
        }

        /// <summary>
        /// Node.js kurulu mu kontrol eder
        /// </summary>
        private async Task<bool> CheckNodeJsAsync()
        {
            // Önce where node ile kontrol et
            var result = await _processRunner.RunAsync("where", "node", timeoutSeconds: 10);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
            {
                // Node.js PATH'te bulundu
                var nodePath = result.Output.Split('\n').FirstOrDefault()?.Trim();
                _logger.Log($"Node.js bulundu: {nodePath}", LogLevel.Success);

                // Version bilgisini al
                var versionResult = await _processRunner.RunAsync("node", "--version", timeoutSeconds: 10);
                if (versionResult.Success)
                {
                    _logger.Log($"Node.js versiyonu: {versionResult.Output.Trim()}", LogLevel.Success);
                }

                return true;
            }

            // where node başarısız, direkt path ile dene
            _logger.Log("Node.js PATH'te bulunamadı, standart path denenecek...");

            string standardNodePath = @"C:\Program Files\nodejs\node.exe";
            if (File.Exists(standardNodePath))
            {
                _logger.Log($"Node.js direkt path ile bulundu: {standardNodePath}", LogLevel.Success);

                // PATH'e ekle
                var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
                var nodeDir = Path.GetDirectoryName(standardNodePath);
                Environment.SetEnvironmentVariable("PATH", $"{currentPath};{nodeDir}", EnvironmentVariableTarget.Process);

                _logger.Log($"Node.js PATH'e eklendi: {nodeDir}");

                return true;
            }

            // Node.js hiçbir yerde bulunamadı
            _logger.Log("Node.js hiçbir yerde bulunamadı!", LogLevel.Error);
            return false;
        }

        /// <summary>
        /// npm çalışıyor mu kontrol eder
        /// </summary>
        private async Task<bool> CheckNpmAsync()
        {
            var result = await _processRunner.RunAsync("npm", "--version", timeoutSeconds: 30);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
            {
                _logger.Log($"npm bulundu: v{result.Output.Trim()}", LogLevel.Success);
                return true;
            }

            _logger.Log("npm bulunamadı veya çalışmıyor!", LogLevel.Error);
            return false;
        }

        /// <summary>
        /// Claude CLI kurulu mu kontrol eder (npm modül kontrolü)
        /// </summary>
        private async Task<bool> IsClaudeInstalledAsync()
        {
            // npm list ile modül kontrolü (daha güvenilir)
            var listResult = await _processRunner.RunAsync(
                "npm", "list -g @anthropic-ai/claude-code", timeoutSeconds: 30);

            if (listResult.Success || listResult.Output.Contains("@anthropic-ai/claude-code@"))
            {
                _logger.Log("Claude CLI npm modülü bulundu", LogLevel.Success);

                // Version bilgisini al
                var versionResult = await _processRunner.RunAsync("claude", "--version", timeoutSeconds: 10);
                if (versionResult.Success)
                {
                    _logger.Log($"Claude CLI versiyonu: {versionResult.Output.Trim()}", LogLevel.Success);
                }

                return true;
            }

            // npm modülü yok ama binary var mı? (eski/bozuk kurulum)
            var whereResult = await _processRunner.RunAsync("where", "claude", timeoutSeconds: 10);
            if (whereResult.Success && !string.IsNullOrWhiteSpace(whereResult.Output))
            {
                _logger.Log("Claude binary mevcut ama npm modülü yok!", LogLevel.Warning);
                _logger.Log("Eski/bozuk kurulum tespit edildi, temizlenecek...");

                // Eski kurulumu temizle
                var uninstallResult = await _processRunner.RunAsync(
                    "npm", "uninstall -g @anthropic-ai/claude-code", timeoutSeconds: 60);

                if (uninstallResult.Success)
                {
                    _logger.Log("Eski kurulum temizlendi", LogLevel.Success);
                }

                return false; // Yeniden kurulum gerekli
            }

            _logger.Log("Claude CLI kurulu değil");
            return false;
        }

        /// <summary>
        /// Claude CLI'yi npm ile global kurar (retry logic ile)
        /// </summary>
        private async Task<bool> InstallClaudeAsync()
        {
            const int maxRetries = 3;
            const int timeoutSeconds = 240; // 4 dakika timeout

            // npm ayarlarını optimize et
            _logger.Log("npm ayarları optimize ediliyor...");
            await _processRunner.RunAsync("npm", "config set registry https://registry.npmjs.org/", timeoutSeconds: 10);
            await _processRunner.RunAsync("npm", "config set fetch-retries 5", timeoutSeconds: 10);
            await _processRunner.RunAsync("npm", "config set fetch-retry-mintimeout 20000", timeoutSeconds: 10);
            await _processRunner.RunAsync("npm", "config set fetch-retry-maxtimeout 120000", timeoutSeconds: 10);
            _logger.LogSeparator();

            // Retry loop
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                _logger.Log($"=== DENEME {attempt}/{maxRetries} ===");

                if (attempt > 1)
                {
                    _logger.Log("Önceki deneme başarısız oldu, tekrar deneniyor...", LogLevel.Warning);
                    _logger.Log("npm cache temizleniyor...");

                    var cacheClean = await _processRunner.RunAsync(
                        "npm", "cache clean --force", timeoutSeconds: 60);

                    if (cacheClean.Success)
                    {
                        _logger.Log("Cache temizlendi", LogLevel.Success);
                    }

                    _logger.LogSeparator();
                }

                // npm install -g @anthropic-ai/claude-code
                _logger.Log("npm install -g @anthropic-ai/claude-code çalıştırılıyor...");

                var installResult = await _processRunner.RunAsync(
                    "npm", "install -g @anthropic-ai/claude-code", timeoutSeconds: timeoutSeconds);

                _logger.LogSeparator();
                _logger.Log("=== npm install çıktısı ===");
                _logger.Log(installResult.Output);

                if (!string.IsNullOrWhiteSpace(installResult.Error))
                {
                    _logger.Log("=== npm install hataları ===", LogLevel.Warning);
                    _logger.Log(installResult.Error);
                }

                _logger.Log("=== npm install çıktısı sonu ===");
                _logger.LogSeparator();

                // Başarılı mı?
                if (installResult.Success)
                {
                    _logger.Log($"npm install BAŞARILI (Deneme {attempt}/{maxRetries})", LogLevel.Success);

                    // PATH'i güncelle (claude komutu npm global'e eklendi)
                    var npmGlobalPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm");

                    var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable("PATH", $"{currentPath};{npmGlobalPath}", EnvironmentVariableTarget.Process);

                    _logger.Log($"Claude PATH'e eklendi: {npmGlobalPath}");

                    return true;
                }

                // Başarısız
                _logger.Log($"npm install BAŞARISIZ (Exit Code: {installResult.ExitCode})", LogLevel.Error);

                if (attempt < maxRetries)
                {
                    _logger.Log("Tekrar deneniyor...");
                    await Task.Delay(3000); // 3 saniye bekle
                }
            }

            // Tüm denemeler başarısız
            _logger.LogSeparator();
            _logger.Log("============================================", LogLevel.Error);
            _logger.Log($"KRITIK HATA: {maxRetries} deneme başarısız!", LogLevel.Error);
            _logger.Log("============================================", LogLevel.Error);
            _logger.LogSeparator();
            _logger.Log("Olası sebepler:");
            _logger.Log("- İnternet bağlantısı kesildi veya çok yavaş");
            _logger.Log("- npm kayıt sunucusuna erişilemiyor (npmjs.com)");
            _logger.Log("- Güvenlik duvarı npm'i engelliyor");
            _logger.Log("- Proxy/firewall ayarları npm'i blokluyor");
            _logger.Log("- @anthropics/claude paketi npmjs.com'da bulunamadı");
            _logger.LogSeparator();
            _logger.Log("Çözüm:");
            _logger.Log("1. İnternet bağlantınızı kontrol edin");
            _logger.Log("2. Güvenlik duvarı ayarlarınızı kontrol edin");
            _logger.Log("3. Kurulum tamamlandıktan sonra manuel çalıştırın:");
            _logger.Log("   npm install -g @anthropics/claude");
            _logger.Log("4. Sorun devam ederse verbose log alın:");
            _logger.Log("   npm install -g @anthropics/claude --loglevel verbose");
            _logger.LogSeparator();
            _logger.Log("ÖNEMLİ: Claude AI özelliği bu hatayla çalışmaz!");
            _logger.Log("Uygulama diğer AI servisleri ile kullanılabilir.");

            return false;
        }

        /// <summary>
        /// Log dosyasının konumunu döndürür
        /// </summary>
        public string GetLogFilePath()
        {
            return _logger.GetLogFilePath();
        }

        // ============================================
        // v62: PATH FIX VE VALIDATION METODLARI
        // ============================================

        /// <summary>
        /// v62: Sistem ve user PATH'leri process environment'a yükler
        /// </summary>
        private void RefreshEnvironmentPaths()
        {
            try
            {
                // System PATH
                var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                // User PATH
                var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                // Mevcut process PATH
                var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);

                _logger.Log($"System PATH: {(string.IsNullOrEmpty(systemPath) ? "(yok)" : systemPath.Substring(0, Math.Min(100, systemPath.Length)) + "...")}");
                _logger.Log($"User PATH: {(string.IsNullOrEmpty(userPath) ? "(yok)" : userPath.Substring(0, Math.Min(100, userPath.Length)) + "...")}");

                // Tüm PATH'leri birleştir (önce user, sonra system, sonra current)
                var newPath = string.Join(";",
                    new[] { userPath, systemPath, currentPath }
                    .Where(p => !string.IsNullOrWhiteSpace(p)));

                // Process'e uygula
                Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);
                _logger.Log("PATH environment process'e yüklendi", LogLevel.Success);
            }
            catch (Exception ex)
            {
                _logger.Log($"PATH refresh hatası: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// v62: Git standart yollarında varsa PATH'e ekle
        /// </summary>
        private async Task<bool> TryFixGitPath()
        {
            var gitPaths = new[]
            {
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files (x86)\Git\cmd\git.exe",
                @"C:\Git\cmd\git.exe"
            };

            foreach (var gitPath in gitPaths)
            {
                if (File.Exists(gitPath))
                {
                    _logger.Log($"Git bulundu: {gitPath}", LogLevel.Success);

                    var gitDir = Path.GetDirectoryName(gitPath);
                    var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable("PATH", $"{currentPath};{gitDir}", EnvironmentVariableTarget.Process);

                    _logger.Log($"Git PATH'e eklendi: {gitDir}");

                    // Doğrula
                    if (await CheckGitAsync())
                    {
                        _logger.Log("✓ Git PATH düzeltmesi BAŞARILI", LogLevel.Success);
                        return true;
                    }
                }
            }

            _logger.Log("Git hiçbir standart yolda bulunamadı", LogLevel.Warning);
            return false;
        }

        /// <summary>
        /// v62: Node.js standart yollarında varsa PATH'e ekle
        /// </summary>
        private async Task<bool> TryFixNodePath()
        {
            var nodePaths = new[]
            {
                @"C:\Program Files\nodejs\node.exe",
                @"C:\Program Files (x86)\nodejs\node.exe"
            };

            foreach (var nodePath in nodePaths)
            {
                if (File.Exists(nodePath))
                {
                    _logger.Log($"Node.js bulundu: {nodePath}", LogLevel.Success);

                    var nodeDir = Path.GetDirectoryName(nodePath);
                    var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable("PATH", $"{currentPath};{nodeDir}", EnvironmentVariableTarget.Process);

                    _logger.Log($"Node.js PATH'e eklendi: {nodeDir}");

                    // Doğrula
                    if (await CheckNodeJsAsync())
                    {
                        _logger.Log("✓ Node.js PATH düzeltmesi BAŞARILI", LogLevel.Success);
                        return true;
                    }
                }
            }

            _logger.Log("Node.js hiçbir standart yolda bulunamadı", LogLevel.Error);
            return false;
        }
    }
}
