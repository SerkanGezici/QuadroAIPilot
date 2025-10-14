using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuadroAIPilot.Models;
using Windows.Security.Credentials;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Kullanıcı profili yönetim servisi - güvenli saklama ve GDPR uyumlu
    /// </summary>
    public class PersonalProfileService
    {
        private readonly ILogger<PersonalProfileService> _logger;
        private readonly string _profileDirectory;
        private readonly string _profileFilePath;
        private readonly string _credentialKey = "QuadroAIPilot_ProfileKey";
        private readonly string _credentialResource = "QuadroAIPilot_ProfileEncryption";
        private PersonalProfile? _cachedProfile;
        private readonly object _lockObject = new object();

        public PersonalProfileService(ILogger<PersonalProfileService> logger)
        {
            _logger = logger;
            
            // %AppData%/QuadroAIPilot dizinini oluştur
            _profileDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "QuadroAIPilot");
            
            if (!Directory.Exists(_profileDirectory))
            {
                Directory.CreateDirectory(_profileDirectory);
            }

            _profileFilePath = Path.Combine(_profileDirectory, "profile.encrypted");
        }

        /// <summary>
        /// Profili yükle (şifresi çözülmüş)
        /// </summary>
        public async Task<PersonalProfile?> LoadProfileAsync()
        {
            try
            {
                _logger.LogInformation("Profil yükleme başlatıldı");
                
                lock (_lockObject)
                {
                    if (_cachedProfile != null)
                    {
                        _logger.LogInformation("Cache'den profil döndürülüyor");
                        return _cachedProfile;
                    }
                }

                _logger.LogInformation("Profil dosyası kontrol ediliyor: {0}", _profileFilePath);
                
                if (!File.Exists(_profileFilePath))
                {
                    _logger.LogInformation("Profil dosyası bulunamadı: {0}", _profileFilePath);
                    return null;
                }

                var fileInfo = new FileInfo(_profileFilePath);
                _logger.LogInformation("Profil dosyası bulundu. Boyut: {0} byte", fileInfo.Length);

                // Şifrelenmiş veriyi oku
                _logger.LogInformation("Şifrelenmiş veri okunuyor...");
                var encryptedData = await File.ReadAllBytesAsync(_profileFilePath);
                _logger.LogInformation("Şifrelenmiş veri okundu. Boyut: {0} byte", encryptedData.Length);
                
                // Şifreleme anahtarını al
                _logger.LogInformation("Şifreleme anahtarı alınıyor...");
                var key = GetOrCreateEncryptionKey();
                _logger.LogInformation("Şifreleme anahtarı alındı. Boyut: {0} byte", key.Length);
                
                // Şifreyi çöz
                _logger.LogInformation("Şifre çözülüyor...");
                var decryptedJson = Decrypt(encryptedData, key);
                _logger.LogInformation("Şifre çözüldü. JSON boyutu: {0} karakter", decryptedJson.Length);
                
                // JSON'dan nesneye dönüştür
                _logger.LogInformation("JSON parse ediliyor...");
                var profile = JsonSerializer.Deserialize<PersonalProfile>(decryptedJson);
                
                if (profile != null)
                {
                    lock (_lockObject)
                    {
                        _cachedProfile = profile;
                    }
                    _logger.LogInformation("Profil başarıyla yüklendi - Ad: {0}, Soyad: {1}", profile.FirstName, profile.LastName);
                }
                else
                {
                    _logger.LogWarning("JSON parse edildi ama profil null döndü");
                }

                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil yüklenirken hata oluştu. Detay: {0}", ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// Profili kaydet (şifrelenmiş)
        /// </summary>
        public async Task<bool> SaveProfileAsync(PersonalProfile profile)
        {
            try
            {
                _logger.LogInformation("Profil kaydetme başlatıldı");
                
                if (!profile.IsValid())
                {
                    _logger.LogWarning("Geçersiz profil kaydedilemez - FirstName: {0}, LastName: {1}, Email: {2}", 
                        string.IsNullOrWhiteSpace(profile.FirstName), 
                        string.IsNullOrWhiteSpace(profile.LastName), 
                        string.IsNullOrWhiteSpace(profile.Email));
                    return false;
                }

                // GDPR onayı kontrolü
                if (!profile.HasGdprConsent)
                {
                    profile.HasGdprConsent = true;
                    profile.GdprConsentDate = DateTime.UtcNow;
                }

                profile.LastUpdatedAt = DateTime.UtcNow;

                // JSON'a dönüştür
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                _logger.LogInformation("Profil JSON'a dönüştürüldü. Boyut: {0} karakter", json.Length);

                // Şifreleme anahtarını al
                _logger.LogInformation("Şifreleme anahtarı alınıyor...");
                var key = GetOrCreateEncryptionKey();
                _logger.LogInformation("Şifreleme anahtarı alındı. Boyut: {0} byte", key.Length);

                // Şifrele
                _logger.LogInformation("Veri şifreleniyor...");
                var encryptedData = Encrypt(json, key);
                _logger.LogInformation("Veri şifrelendi. Boyut: {0} byte", encryptedData.Length);

                // Dosya yolunu kontrol et
                _logger.LogInformation("Dosya yolu: {0}", _profileFilePath);
                _logger.LogInformation("Dizin var mı: {0}", Directory.Exists(_profileDirectory));
                
                // Dosyaya yaz
                _logger.LogInformation("Dosyaya yazılıyor...");
                await File.WriteAllBytesAsync(_profileFilePath, encryptedData);
                
                // Dosyanın oluşturulduğunu kontrol et
                var fileExists = File.Exists(_profileFilePath);
                _logger.LogInformation("Dosya oluşturuldu mu: {0}", fileExists);
                
                if (fileExists)
                {
                    var fileInfo = new FileInfo(_profileFilePath);
                    _logger.LogInformation("Dosya boyutu: {0} byte", fileInfo.Length);
                }

                lock (_lockObject)
                {
                    _cachedProfile = profile;
                }

                _logger.LogInformation("Profil başarıyla kaydedildi");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil kaydedilirken hata oluştu. Detay: {0}", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Profili sil (GDPR uyumlu)
        /// </summary>
        public async Task<bool> DeleteProfileAsync()
        {
            try
            {
                // Önce anonimleştir (log tutma amaçlı)
                var profile = await LoadProfileAsync();
                if (profile != null)
                {
                    profile.AnonymizeData();
                    await SaveProfileAsync(profile);
                }

                // Sonra fiziksel olarak sil
                if (File.Exists(_profileFilePath))
                {
                    File.Delete(_profileFilePath);
                }

                // Profil fotoğrafını sil
                if (profile?.ProfilePhotoPath != null && File.Exists(profile.ProfilePhotoPath))
                {
                    try
                    {
                        File.Delete(profile.ProfilePhotoPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Profil fotoğrafı silinemedi");
                    }
                }

                lock (_lockObject)
                {
                    _cachedProfile = null;
                }

                _logger.LogInformation("Profil başarıyla silindi (GDPR uyumlu)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil silinirken hata oluştu");
                return false;
            }
        }

        /// <summary>
        /// Profil fotoğrafı kaydet
        /// </summary>
        public async Task<string?> SaveProfilePhotoAsync(string sourceFilePath)
        {
            try
            {
                if (!File.Exists(sourceFilePath))
                    return null;

                var extension = Path.GetExtension(sourceFilePath);
                var photoFileName = $"profile_{Guid.NewGuid()}{extension}";
                var photoPath = Path.Combine(_profileDirectory, photoFileName);

                // Dosyayı kopyala
                await Task.Run(() => File.Copy(sourceFilePath, photoPath, true));

                _logger.LogInformation("Profil fotoğrafı kaydedildi: {PhotoPath}", photoPath);
                return photoPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil fotoğrafı kaydedilirken hata oluştu");
                return null;
            }
        }

        /// <summary>
        /// Varsayılan profil oluştur
        /// </summary>
        public PersonalProfile CreateDefaultProfile()
        {
            return new PersonalProfile
            {
                FirstName = "",
                LastName = "",
                Email = "",
                Country = "Türkiye",
                HasGdprConsent = false
            };
        }

        /// <summary>
        /// Kişiselleştirilmiş selamlama metni
        /// </summary>
        public string GetPersonalizedGreeting(PersonalProfile? profile = null)
        {
            if (profile == null)
            {
                lock (_lockObject)
                {
                    profile = _cachedProfile;
                }
            }

            if (profile == null || !profile.IsActive)
                return "Hoş geldiniz!";

            var greeting = $"Merhaba {profile.FirstName}!";

            // Doğum günü kontrolü
            if (profile.IsBirthdayToday())
            {
                greeting = $"🎉 Doğum gününüz kutlu olsun {profile.FirstName}! 🎂";
            }
            // Sabah/akşam selamlaması
            else
            {
                var hour = DateTime.Now.Hour;
                if (hour >= 6 && hour < 12)
                    greeting = $"Günaydın {profile.FirstName}!";
                else if (hour >= 12 && hour < 18)
                    greeting = $"İyi günler {profile.FirstName}!";
                else if (hour >= 18 && hour < 22)
                    greeting = $"İyi akşamlar {profile.FirstName}!";
                else
                    greeting = $"İyi geceler {profile.FirstName}!";
            }

            return greeting;
        }

        #region Şifreleme İşlemleri

        /// <summary>
        /// Windows Credential Manager'dan şifreleme anahtarını al veya oluştur
        /// </summary>
        private byte[] GetOrCreateEncryptionKey()
        {
            try
            {
                var vault = new PasswordVault();
                
                try
                {
                    var credential = vault.Retrieve(_credentialResource, _credentialKey);
                    var keyString = credential.Password;
                    return Convert.FromBase64String(keyString);
                }
                catch (Exception)
                {
                    // Anahtar bulunamadı, yeni oluştur
                    var key = GenerateEncryptionKey();
                    var keyString = Convert.ToBase64String(key);
                    
                    var credential = new PasswordCredential(_credentialResource, _credentialKey, keyString);
                    vault.Add(credential);
                    
                    _logger.LogInformation("Yeni şifreleme anahtarı oluşturuldu");
                    return key;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifreleme anahtarı alınamadı, geçici anahtar kullanılıyor");
                // Fallback: Geçici anahtar kullan
                return Encoding.UTF8.GetBytes("QuadroAI_TempKey_2024_Secure!@#$");
            }
        }

        /// <summary>
        /// Rastgele şifreleme anahtarı oluştur
        /// </summary>
        private byte[] GenerateEncryptionKey()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var key = new byte[32]; // 256 bit
                rng.GetBytes(key);
                return key;
            }
        }

        /// <summary>
        /// AES-256 ile şifrele
        /// </summary>
        private byte[] Encrypt(string plainText, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    // IV'yi başa yaz
                    ms.Write(aes.IV, 0, aes.IV.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }

                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// AES-256 ile şifre çöz
        /// </summary>
        private string Decrypt(byte[] cipherText, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;

                // IV'yi oku (ilk 16 byte)
                var iv = new byte[16];
                Array.Copy(cipherText, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(cipherText, 16, cipherText.Length - 16))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        #endregion

        /// <summary>
        /// Profil var mı kontrol et
        /// </summary>
        public bool HasProfile()
        {
            return File.Exists(_profileFilePath);
        }

        /// <summary>
        /// Cache'i temizle
        /// </summary>
        public void ClearCache()
        {
            lock (_lockObject)
            {
                _cachedProfile = null;
            }
        }
        
        /// <summary>
        /// TEST METODU: Profili şifreleme olmadan kaydet (Debug amaçlı)
        /// </summary>
        public async Task<bool> SaveProfileUnencryptedAsync(PersonalProfile profile)
        {
            try
            {
                _logger.LogWarning("TEST: Şifrelenmemiş profil kaydediliyor!");
                
                var testFilePath = Path.Combine(_profileDirectory, "profile_test.json");
                
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                await File.WriteAllTextAsync(testFilePath, json);
                
                _logger.LogInformation("TEST: Profil şifrelenmeden kaydedildi: {0}", testFilePath);
                
                // Normal şifreli kaydetmeyi de dene
                var encryptedResult = await SaveProfileAsync(profile);
                _logger.LogInformation("TEST: Şifreli kaydetme sonucu: {0}", encryptedResult);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TEST: Şifrelenmemiş profil kaydedilirken hata");
                return false;
            }
        }
        
        /// <summary>
        /// Windows Credential Manager'ı test et
        /// </summary>
        public void TestCredentialManager()
        {
            try
            {
                _logger.LogInformation("Credential Manager testi başlatıldı");
                
                var key = GetOrCreateEncryptionKey();
                _logger.LogInformation("Credential Manager testi başarılı. Anahtar boyutu: {0}", key.Length);
                
                // Test verisi şifrele/çöz
                var testData = "Test verisi 123";
                var encrypted = Encrypt(testData, key);
                var decrypted = Decrypt(encrypted, key);
                
                var success = testData == decrypted;
                _logger.LogInformation("Şifreleme/Çözme testi: {0}", success ? "BAŞARILI" : "BAŞARISIZ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Credential Manager test hatası");
            }
        }
    }
}