using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Serilog;

namespace QuadroAIPilot.Helpers
{
    /// <summary>
    /// Build ve versiyon bilgilerini okur - Profesyonel SSOT (Single Source of Truth) sistemi
    /// Öncelik sırası: FileVersionInfo > Embedded Resource > Registry > Assembly
    /// </summary>
    public static class BuildInfoHelper
    {
        private static string? _cachedVersion;
        private static string? _cachedDisplayVersion;

        /// <summary>
        /// Tam versiyon bilgisini döndürür (örn: "1.2.1.68")
        /// </summary>
        public static string GetFullVersion()
        {
            if (_cachedVersion != null)
                return _cachedVersion;

            // 1. FileVersionInfo - En güvenilir kaynak (EXE dosyasının metadata'sı)
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var location = assembly.Location;

                // .NET 5+ single-file deployment kontrolü
                if (string.IsNullOrEmpty(location))
                {
                    // Process'in executable path'ini al
                    location = Environment.ProcessPath;
                }

                if (!string.IsNullOrEmpty(location) && File.Exists(location))
                {
                    var fileVersionInfo = FileVersionInfo.GetVersionInfo(location);

                    if (!string.IsNullOrEmpty(fileVersionInfo.FileVersion))
                    {
                        _cachedVersion = fileVersionInfo.FileVersion;
                        Log.Information("[BuildInfoHelper] Versiyon FileVersionInfo'dan okundu: {Version}", _cachedVersion);
                        return _cachedVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[BuildInfoHelper] FileVersionInfo okuma hatası, fallback'e geçiliyor");
            }

            // 2. Embedded resource'tan oku
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "QuadroAIPilot.Properties.BuildInfo.txt";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var version = reader.ReadToEnd().Trim();

                    if (!string.IsNullOrEmpty(version))
                    {
                        _cachedVersion = version;
                        Log.Information("[BuildInfoHelper] Versiyon embedded resource'tan okundu: {Version}", _cachedVersion);
                        return _cachedVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[BuildInfoHelper] Embedded resource okuma hatası, fallback'e geçiliyor");
            }

            // 3. Registry'den oku
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\QuadroAI\QuadroAIPilot");

                if (key != null)
                {
                    var buildNumber = key.GetValue("BuildNumber") as string;
                    var displayVersion = key.GetValue("DisplayVersion") as string ?? "1.2.1";

                    if (!string.IsNullOrEmpty(buildNumber))
                    {
                        _cachedVersion = $"{displayVersion}.{buildNumber}";
                        Log.Information("[BuildInfoHelper] Versiyon Registry'den okundu: {Version}", _cachedVersion);
                        return _cachedVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[BuildInfoHelper] Registry okuma hatası");
            }

            // 4. Assembly version (son çare)
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;

                if (version != null)
                {
                    _cachedVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
                    Log.Warning("[BuildInfoHelper] Versiyon Assembly'den okundu (fallback): {Version}", _cachedVersion);
                    return _cachedVersion;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[BuildInfoHelper] Assembly version okuma hatası");
            }

            // 5. En son fallback: Sabit değer
            _cachedVersion = "1.2.1.0";
            Log.Error("[BuildInfoHelper] Tüm versiyon okuma methodları başarısız, default değer kullanılıyor");
            return _cachedVersion;
        }

        /// <summary>
        /// Kullanıcı dostu versiyon formatı döndürür (örn: "1.2.1 (Build 68)")
        /// </summary>
        public static string GetDisplayVersion()
        {
            if (_cachedDisplayVersion != null)
                return _cachedDisplayVersion;

            var fullVersion = GetFullVersion();
            _cachedDisplayVersion = FormatVersionForDisplay(fullVersion);
            return _cachedDisplayVersion;
        }

        /// <summary>
        /// Versiyon string'ini kullanıcı dostu formata çevirir
        /// "1.2.1.68" -> "1.2.1 (Build 68)"
        /// </summary>
        public static string FormatVersionForDisplay(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "Bilinmiyor";

            var parts = version.Split('.');

            if (parts.Length >= 4)
            {
                // 4 parçalı: Major.Minor.Patch.Build
                return $"{parts[0]}.{parts[1]}.{parts[2]} (Build {parts[3]})";
            }
            else if (parts.Length == 3)
            {
                // 3 parçalı: Major.Minor.Patch
                return version;
            }
            else
            {
                return version;
            }
        }

        /// <summary>
        /// Sadece build numarasını döndürür
        /// </summary>
        public static string GetBuildNumber()
        {
            var fullVersion = GetFullVersion();
            var parts = fullVersion.Split('.');

            return parts.Length >= 4 ? parts[3] : "0";
        }

        /// <summary>
        /// Versiyon cache'ini temizle (test amaçlı)
        /// </summary>
        public static void ClearCache()
        {
            _cachedVersion = null;
            _cachedDisplayVersion = null;
        }
    }
}
