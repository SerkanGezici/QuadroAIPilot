using System;
using System.IO;
using System.Reflection;
using Serilog;

namespace QuadroAIPilot.Helpers
{
    /// <summary>
    /// Build ve versiyon bilgilerini embedded resource'tan okur
    /// Fallback olarak Registry'ye bakar
    /// </summary>
    public static class BuildInfoHelper
    {
        private static string? _cachedVersion;

        /// <summary>
        /// Tam versiyon bilgisini döndürür (örn: "1.2.1.31" veya "1.2.1 (Build 31)")
        /// </summary>
        public static string GetFullVersion()
        {
            if (_cachedVersion != null)
                return _cachedVersion;

            try
            {
                // Önce embedded resource'tan okumayı dene
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "QuadroAIPilot.Properties.BuildInfo.txt";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    _cachedVersion = reader.ReadToEnd().Trim();

                    if (!string.IsNullOrEmpty(_cachedVersion))
                    {
                        Log.Information("[BuildInfoHelper] Versiyon embedded resource'tan okundu: {Version}", _cachedVersion);
                        return _cachedVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[BuildInfoHelper] Embedded resource okuma hatası, fallback'e geçiliyor");
            }

            // Fallback: Registry'den oku
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\QuadroAI\QuadroAIPilot");

                if (key != null)
                {
                    var buildNumber = key.GetValue("BuildNumber") as string;
                    if (!string.IsNullOrEmpty(buildNumber))
                    {
                        var assembly = Assembly.GetExecutingAssembly();
                        var version = assembly.GetName().Version;

                        if (version != null)
                        {
                            _cachedVersion = $"{version.Major}.{version.Minor}.{version.Build} (Build {buildNumber})";
                            Log.Information("[BuildInfoHelper] Versiyon Registry'den okundu: {Version}", _cachedVersion);
                            return _cachedVersion;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[BuildInfoHelper] Registry okuma hatası");
            }

            // Son fallback: Assembly version
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;

                if (version != null)
                {
                    _cachedVersion = $"{version.Major}.{version.Minor}.{version.Build}";
                    Log.Warning("[BuildInfoHelper] Versiyon Assembly'den okundu (fallback): {Version}", _cachedVersion);
                    return _cachedVersion;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[BuildInfoHelper] Assembly version okuma hatası");
            }

            // En son fallback: Sabit değer
            _cachedVersion = "1.2.1 (Build Unknown)";
            Log.Error("[BuildInfoHelper] Tüm versiyon okuma methodları başarısız, default değer kullanılıyor");
            return _cachedVersion;
        }

        /// <summary>
        /// Versiyon cache'ini temizle (test amaçlı)
        /// </summary>
        public static void ClearCache()
        {
            _cachedVersion = null;
        }
    }
}
