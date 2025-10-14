using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace QuadroAIPilot.Services
{
    public class ApplicationRegistry
    {
        public class ApplicationInfo
        {
            public string Name { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string ExecutablePath { get; set; } = "";
            public string ProcessName { get; set; } = "";
            public List<string> Aliases { get; } = new();
            public bool IsSystemApp { get; set; }
            public DateTime LastScanTime { get; set; } = DateTime.Now;
            public int UsageCount { get; set; }
        }

        private static ApplicationRegistry? _inst;
        public static ApplicationRegistry Instance =>
            _inst ??= new ApplicationRegistry();

        private readonly Dictionary<string, ApplicationInfo> _apps =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _aliasLookup =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly System.Timers.Timer _scanTimer;
        private DateTime _lastFullScan = DateTime.MinValue;
        private readonly ILogger<ApplicationRegistry> _logger;

        private readonly Dictionary<string, string> _systemApps =
            new(StringComparer.OrdinalIgnoreCase)
        {
           { "denetim masası" , "control.exe"     },
           { "control panel"  , "control.exe"     },
           { "ayarlar"        , "ms-settings:"    },
           { "settings"       , "ms-settings:"    },
           { "görev yöneticisi","taskmgr.exe"     },
           { "task manager"   , "taskmgr.exe"     },
           { "dosya gezgini"  , "explorer.exe"    },
           { "file explorer"  , "explorer.exe"    },
           { "microsoft store","ms-windows-store:"},
           { "windows terminal","wt.exe"          },
           { "powershell"     , "powershell.exe"  },
           { "komut istemi"   , "cmd.exe"         },
           { "command prompt" , "cmd.exe"         },
           { "disk temizleme" , "cleanmgr.exe"    },
           { "disk cleanup"   , "cleanmgr.exe"    },
           { "sistem bilgisi" , "msinfo32.exe"    },
           { "system information","msinfo32.exe"  },
           { "bilgisayar yönetimi","compmgmt.msc" },
           { "whatsapp"       , "whatsapp:"       },
           { "wechat"         , "wechat:"         },
           { "skype"          , "skype:"          }
        };

        private ApplicationRegistry()
        {
            _logger = LoggingService.CreateLogger<ApplicationRegistry>();
            _logger.LogInformation("ApplicationRegistry başlatılıyor");
            
            InitializeSystemApps();

            _scanTimer = new System.Timers.Timer(TimeSpan.FromHours(12).TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = true
            };
            _scanTimer.Elapsed += (_, __) => _ = ScanInstalledApplicationsAsync();
            
            _logger.LogInformation("ApplicationRegistry başlatıldı, {AppCount} sistem uygulaması yüklendi", _apps.Count);
        }

        private void InitializeSystemApps()
        {
            foreach (var (name, path) in _systemApps)
            {
                string key = name.ToLowerInvariant();
                if (_apps.ContainsKey(key)) continue;

                _apps[key] = new ApplicationInfo
                {
                    Name = key,
                    DisplayName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key),
                    ExecutablePath = path,
                    ProcessName = Path.GetFileNameWithoutExtension(path),
                    IsSystemApp = true
                };
                _aliasLookup[key] = key;
                // Debug.WriteLine($"[ApplicationRegistry] Sistem uygulaması eklendi: {key}");
            }
        }

        public async Task InitializeScanAsync()
        {
            if (_lastFullScan == DateTime.MinValue ||
                (DateTime.Now - _lastFullScan).TotalHours >= 12)
            {
                await ScanInstalledApplicationsAsync();
            }
        }

        public ApplicationInfo? FindApplication(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var culture = new System.Globalization.CultureInfo("tr-TR");
            name = name.ToLower(culture);
            var searchWords = name.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);

            lock (_apps)
            {
                // 1. Tam eşleşme kontrolü
                if (_apps.TryGetValue(name, out var app)) return app;

                // 2. Alias eşleşme kontrolü
                if (_aliasLookup.TryGetValue(name, out var real) &&
                    _apps.TryGetValue(real, out var a2)) return a2;

                // 3. İçeren kelime araması
                var containsMatch = _apps.Values.FirstOrDefault(a =>
                {
                    var allNames = new List<string> { a.Name, a.DisplayName };
                    allNames.AddRange(a.Aliases);
                    return allNames.Any(candidate =>
                    {
                        var candidateLower = candidate.ToLower(culture);
                        return searchWords.All(word => candidateLower.Contains(word, StringComparison.OrdinalIgnoreCase));
                    });
                });
                
                if (containsMatch != null) return containsMatch;

                // 4. Fuzzy matching (Levenshtein distance)
                ApplicationInfo? bestMatch = null;
                double bestScore = 0.0;
                const double SIMILARITY_THRESHOLD = 0.7; // %70 benzerlik eşiği

                foreach (var appInfo in _apps.Values)
                {
                    var allNames = new List<string> { appInfo.Name, appInfo.DisplayName };
                    allNames.AddRange(appInfo.Aliases);

                    foreach (var candidate in allNames)
                    {
                        var similarity = CalculateSimilarity(name, candidate.ToLower(culture));
                        if (similarity > bestScore && similarity >= SIMILARITY_THRESHOLD)
                        {
                            bestScore = similarity;
                            bestMatch = appInfo;
                        }

                        // Ayrıca her kelimeyi ayrı ayrı kontrol et
                        foreach (var word in searchWords)
                        {
                            var wordSimilarity = CalculateSimilarity(word, candidate.ToLower(culture));
                            if (wordSimilarity > bestScore && wordSimilarity >= SIMILARITY_THRESHOLD)
                            {
                                bestScore = wordSimilarity;
                                bestMatch = appInfo;
                            }
                        }
                    }
                }

                if (bestMatch != null)
                {
                    _logger.LogInformation("Fuzzy match bulundu: '{Input}' -> '{Match}' (Skor: {Score:P})", 
                        name, bestMatch.DisplayName, bestScore);
                }

                return bestMatch;
            }
        }

        public void IncrementUsageCount(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_aliasLookup.TryGetValue(name, out var real)) name = real;

            if (_apps.TryGetValue(name, out var app)) app.UsageCount++;
        }

        public async Task ScanInstalledApplicationsAsync()
        {
            await Task.Run(() =>
            {
                lock (_apps)
                {
                    _lastFullScan = DateTime.Now;
                    // Debug.WriteLine($"[ApplicationRegistry] Uygulama taraması başlatılıyor...");
                    ScanStartMenu();
                    ScanProgramFiles();
                    ScanRegistry();
                }
            });

            // Debug.WriteLine($"[ApplicationRegistry] Tarama tamamlandı → {_apps.Count} uygulama bulundu.");
        }

        private void ScanStartMenu()
        {
            string allUsers = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
            string current = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

            ScanFolderShortcuts(allUsers);
            ScanFolderShortcuts(current);
        }

        private void ScanFolderShortcuts(string folder)
        {
            if (!Directory.Exists(folder)) return;

            foreach (var lnk in Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories))
            {
                try
                {
                    string target = ResolveShortcutTarget(lnk);
                    if (!IsExecutableFile(target)) continue;

                    string display = CleanName(Path.GetFileNameWithoutExtension(lnk));

                    if (display.ToLowerInvariant().Contains("uninstall") ||
                        display.ToLowerInvariant().Contains("kaldır"))
                        continue;

                    string key = display.ToLowerInvariant();
                    AddOrUpdate(key, display, target,
                                Path.GetFileNameWithoutExtension(target));
                }
                catch { /* Debug.WriteLine($"lnk hata: {ex.Message}"); */ }
            }
        }

        private void ScanProgramFiles()
        {
            ScanExeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            ScanExeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        }

        private void ScanExeDirectory(string root)
        {
            if (!Directory.Exists(root)) return;

            foreach (var dir in Directory.GetDirectories(root))
            {
                try
                {
                    string folderName = Path.GetFileName(dir);
                    string clean = CleanName(folderName);

                    if (clean.ToLowerInvariant().Contains("uninstall") ||
                        clean.ToLowerInvariant().Contains("kaldır"))
                        continue;

                    var exe = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                                       .FirstOrDefault();
                    if (exe == null) continue;

                    if (Path.GetFileNameWithoutExtension(exe).ToLowerInvariant().Contains("uninstall") ||
                        Path.GetFileNameWithoutExtension(exe).ToLowerInvariant().Contains("kaldır"))
                        continue;

                    AddOrUpdate(clean.ToLowerInvariant(), folderName, exe,
                                Path.GetFileNameWithoutExtension(exe));
                }
                catch { /* Debug.WriteLine($"exe tarama: {ex.Message}"); */ }
            }
        }

        private void ScanRegistry()
        {
            string[] roots =
            {
               @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
               @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
           };

            foreach (var root in roots)
                using (var k = Registry.LocalMachine.OpenSubKey(root))
                {
                    if (k == null) continue;

                    foreach (var sub in k.GetSubKeyNames())
                        using (var sk = k.OpenSubKey(sub))
                        {
                            try
                            {
                                string? display = sk?.GetValue("DisplayName") as string;
                                string? loc = sk?.GetValue("InstallLocation") as string;
                                if (string.IsNullOrWhiteSpace(display)) continue;

                                string clean = CleanName(display);
                                if (clean.Length < 2) continue;

                                if (clean.ToLowerInvariant().Contains("uninstall") ||
                                    clean.ToLowerInvariant().Contains("kaldır"))
                                    continue;

                                string? exe = null;
                                if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc))
                                    exe = Directory.GetFiles(loc, "*.exe", SearchOption.TopDirectoryOnly)
                                                    .FirstOrDefault(e =>
                                                        !Path.GetFileNameWithoutExtension(e).ToLowerInvariant().Contains("uninstall") &&
                                                        !Path.GetFileNameWithoutExtension(e).ToLowerInvariant().Contains("kaldır"));

                                if (exe != null)
                                    AddOrUpdate(clean.ToLowerInvariant(), display, exe,
                                                Path.GetFileNameWithoutExtension(exe));
                            }
                            catch { }
                        }
                }
        }

        private void AddOrUpdate(string key, string disp, string path, string proc)
        {
            if (string.IsNullOrWhiteSpace(key) || !File.Exists(path)) return;
            if (IsSystemUtilityFile(path)) return;

            if (key.Contains("uninstall") || key.Contains("kaldır") ||
                disp.ToLowerInvariant().Contains("uninstall") || disp.ToLowerInvariant().Contains("kaldır"))
                return;

            if (_apps.TryGetValue(key, out var app))
            {
                app.ExecutablePath = path; app.ProcessName = proc;
                app.LastScanTime = DateTime.Now;
            }
            else
            {
                app = new ApplicationInfo
                {
                    Name = key,
                    DisplayName = disp,
                    ExecutablePath = path,
                    ProcessName = proc,
                    LastScanTime = DateTime.Now
                };
                _apps[key] = app;
                _aliasLookup[key] = key;
                GenerateAliasesForApplication(app);
                // Uygulama ekleme logu - tek tek loglama gereksiz
            }
        }

        private string ResolveShortcutTarget(string lnk)
        {
            try
            {
                Type t = Type.GetTypeFromCLSID(
                    new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
                dynamic shell = Activator.CreateInstance(t);
                dynamic sc = shell.CreateShortcut(lnk);
                string target = sc.TargetPath;
                Marshal.FinalReleaseComObject(sc);
                Marshal.FinalReleaseComObject(shell);
                return target;
            }
            catch { return ""; }
        }

        private static bool IsExecutableFile(string p)
        {
            string ext = Path.GetExtension(p).ToLowerInvariant();
            return ext is ".exe" or ".com" or ".bat" or ".cmd";
        }

        private static string CleanName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            s = Regex.Replace(s, @"\(.*?\)|\[.*?\]", "");
            s = Regex.Replace(s, @"\d+(\.\d+)+", "");
            s = Regex.Replace(s, @"[vV]\d+(\.\d+)*", "");
            s = Regex.Replace(s, @"®|©|™", "");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        private static bool IsSystemUtilityFile(string exe)
        {
            string f = Path.GetFileName(exe).ToLowerInvariant();
            return f.Contains("install") || f.Contains("setup") ||
                   f.Contains("update") || f.Contains("uninst") ||
                   f.Contains("helper") || f.Contains("assistant") ||
                   f.Contains("service") || f.Contains("daemon") ||
                   f.Contains("agent") || f.Contains("monitor");
        }

        private void GenerateAliasesForApplication(ApplicationInfo app)
        {
            string disp = app.DisplayName.ToLowerInvariant();
            string[] parts = disp.Split(new[] { ' ', '-', '_', '.' },
                                        StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 1 && parts[0].Length > 1) AddAlias(app, parts[0]);

            if (disp.StartsWith("microsoft "))
                AddAlias(app, disp.Substring(10).Trim());

            AddSpecialAliases(app);
        }

        private void AddSpecialAliases(ApplicationInfo app)
        {            void A(string a) => AddAlias(app, a);
            string n = app.Name.ToLowerInvariant();

            if (n.Contains("word")) { A("word"); A("vörd"); A("world"); }
            else if (n.Contains("powerpoint")) { A("powerpoint"); A("sunum"); }

            else if (n.Contains("teams")) { A("teams"); }
            else if (n.Contains("calculator")) { A("hesap makinesi"); A("calculator"); }
            else if (n.Contains("photos")) { A("fotoğraflar"); }
            else if (n.Contains("paint")) { A("paint"); A("peyint"); A("peyint"); }

            else if (n.Contains("chrome")) { A("chrome"); A("krom"); A("tarayıcı"); }
            else if (n.Contains("edge")) { A("edge"); A("edc"); A("edj"); }

            else if (n.Contains("whatsapp")) { A("whatsapp"); A("vatsap"); A("vat sap"); A("vatzap"); }
            else if (n.Contains("wechat")) { A("wechat"); A("viçet"); A("we chat"); }
            else if (n.Contains("skype")) { A("skype"); A("es kayp"); }

            else if (n.Contains("spotify")) { A("spotify"); A("spotifi"); }
            else if (n.Contains("netflix")) { A("netflix"); A("netfliks"); A("netfiliks"); }
            else if (n.Contains("telegram")) { A("telegram"); }
            else if (n.Contains("zoom")) { A("zoom"); }
            else if (n.Contains("steam")) { A("steam"); A("stim"); }
            else if (n.Contains("discord")) { A("discord"); }
        }

        private void AddAlias(ApplicationInfo app, string alias)
        {
            if (string.IsNullOrWhiteSpace(alias) || alias.Length < 2) return;
            alias = alias.ToLowerInvariant();

            if (alias == app.Name || app.Aliases.Contains(alias)) return;
            if (_aliasLookup.ContainsKey(alias)) return;

            _aliasLookup[alias] = app.Name;
            app.Aliases.Add(alias);
        }

        /// <summary>
        /// İki kelime arasındaki benzerlik skorunu hesaplar (Levenshtein distance)
        /// </summary>
        private static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 1.0 : 0.0;
            if (string.IsNullOrEmpty(target)) return 0.0;
            
            source = source.ToLowerInvariant();
            target = target.ToLowerInvariant();
            
            if (source == target) return 1.0;
            
            int distance = LevenshteinDistance(source, target);
            int maxLength = Math.Max(source.Length, target.Length);
            
            return 1.0 - ((double)distance / maxLength);
        }
        
        private static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target)) return source.Length;
            
            int[,] distance = new int[source.Length + 1, target.Length + 1];
            
            for (int i = 0; i <= source.Length; i++)
                distance[i, 0] = i;
                
            for (int j = 0; j <= target.Length; j++)
                distance[0, j] = j;
                
            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost
                    );
                }
            }
            
            return distance[source.Length, target.Length];
        }

        public IEnumerable<ApplicationInfo> GetAllRegisteredApplications()
        {
            return _apps.Values.ToList();
        }

        public IEnumerable<ApplicationInfo> SearchApplications(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return GetAllRegisteredApplications();

            searchTerm = searchTerm.ToLowerInvariant();
            return _apps.Values.Where(app => 
                app.Name.ToLowerInvariant().Contains(searchTerm) ||
                app.DisplayName.ToLowerInvariant().Contains(searchTerm) ||
                app.Aliases.Any(alias => alias.Contains(searchTerm))
            ).ToList();
        }
    }
}
