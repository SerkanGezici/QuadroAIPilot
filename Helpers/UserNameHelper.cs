using System;
using System.IO;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Helpers
{
    /// <summary>
    /// Kullanıcı adı yönetimi için yardımcı sınıf
    /// </summary>
    public static class UserNameHelper
    {
        private static string _cachedUserName = null;
        private static DateTime _lastCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Kullanıcı adını al (profil varsa FirstName kullan)
        /// </summary>
        public static string GetUserName()
        {
            try
            {
                // Cache kontrolü
                if (_cachedUserName != null && DateTime.Now - _lastCacheTime < CacheDuration)
                {
                    return _cachedUserName;
                }

                // PersonalProfileService'ten kullanıcı adını al
                // Not: PersonalProfileService logger gerektiriyor, basit bir çözüm için 
                // doğrudan dosyadan okuyalım
                var profilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "QuadroAIPilot",
                    "profile.json");
                
                if (File.Exists(profilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(profilePath);
                        var profile = System.Text.Json.JsonSerializer.Deserialize<QuadroAIPilot.Models.PersonalProfile>(json);
                        
                        if (profile != null && !string.IsNullOrWhiteSpace(profile.FirstName))
                        {
                            _cachedUserName = profile.FirstName.Trim();
                            _lastCacheTime = DateTime.Now;
                            return _cachedUserName;
                        }
                    }
                    catch
                    {
                        // Hata durumunda sessizce devam et
                    }
                }
                
                // Profil yoksa boş string döndür
                _cachedUserName = string.Empty;
                _lastCacheTime = DateTime.Now;
                return string.Empty;
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[UserNameHelper] Kullanıcı adı alınamadı: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Sesli geri bildirim mesajını kullanıcı adıyla formatla
        /// </summary>
        public static string FormatMessageWithUserName(string template, bool includeComma = true)
        {
            var userName = GetUserName();
            
            if (string.IsNullOrWhiteSpace(userName))
            {
                // Kullanıcı adı yoksa şablonu olduğu gibi döndür
                return template;
            }

            // Kullanıcı adını mesaja ekle
            if (includeComma)
            {
                // Örnek: "Serkan, ben bekleme moduna geçiyorum"
                return $"{userName}, {template.ToLowerInvariant()}";
            }
            else
            {
                // Örnek: "Buyrun efendim Serkan"
                return $"{template} {userName}";
            }
        }

        /// <summary>
        /// Cache'i temizle (profil güncellendiğinde kullanılır)
        /// </summary>
        public static void ClearCache()
        {
            _cachedUserName = null;
            _lastCacheTime = DateTime.MinValue;
        }
    }
}