using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuadroAIPilot.Models
{
    /// <summary>
    /// Kullanıcının kişisel profil bilgileri
    /// </summary>
    public class PersonalProfile
    {
        /// <summary>
        /// Profil benzersiz kimliği
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Kullanıcının adı (ZORUNLU)
        /// </summary>
        [Required(ErrorMessage = "Ad alanı zorunludur")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Ad 2-50 karakter arasında olmalıdır")]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Kullanıcının soyadı (ZORUNLU)
        /// </summary>
        [Required(ErrorMessage = "Soyad alanı zorunludur")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Soyad 2-50 karakter arasında olmalıdır")]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Tam ad (FirstName + LastName)
        /// </summary>
        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>
        /// Email adresi (ZORUNLU)
        /// </summary>
        [Required(ErrorMessage = "Email adresi zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Telefon numarası (opsiyonel)
        /// </summary>
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz")]
        public string? Phone { get; set; }

        /// <summary>
        /// Doğum tarihi (opsiyonel)
        /// </summary>
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// Yaş (doğum tarihinden hesaplanır)
        /// </summary>
        public int? Age
        {
            get
            {
                if (!BirthDate.HasValue) return null;
                var today = DateTime.Today;
                var age = today.Year - BirthDate.Value.Year;
                if (BirthDate.Value.Date > today.AddYears(-age)) age--;
                return age;
            }
        }

        /// <summary>
        /// Cinsiyet (opsiyonel)
        /// </summary>
        public string? Gender { get; set; }

        /// <summary>
        /// Ülke (opsiyonel)
        /// </summary>
        [StringLength(50)]
        public string? Country { get; set; }

        /// <summary>
        /// Şehir (opsiyonel)
        /// </summary>
        [StringLength(50)]
        public string? City { get; set; }

        /// <summary>
        /// Sosyal medya hesapları
        /// Key: Platform adı (Twitter, LinkedIn, Instagram, vb.)
        /// Value: Kullanıcı adı veya profil URL'i
        /// </summary>
        public Dictionary<string, string> SocialMediaAccounts { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Profil fotoğrafı dosya yolu (opsiyonel)
        /// </summary>
        public string? ProfilePhotoPath { get; set; }

        /// <summary>
        /// Profil oluşturulma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Son güncelleme tarihi
        /// </summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// GDPR onay tarihi
        /// </summary>
        public DateTime? GdprConsentDate { get; set; }

        /// <summary>
        /// Veri işleme onayı
        /// </summary>
        public bool HasGdprConsent { get; set; } = false;

        /// <summary>
        /// Profil aktif mi?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Tüm zorunlu alanların dolu olup olmadığını kontrol eder
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(FirstName) && 
                   !string.IsNullOrWhiteSpace(LastName) && 
                   !string.IsNullOrWhiteSpace(Email);
        }

        /// <summary>
        /// Bugün doğum günü mü?
        /// </summary>
        public bool IsBirthdayToday()
        {
            if (!BirthDate.HasValue) return false;
            var today = DateTime.Today;
            return BirthDate.Value.Month == today.Month && BirthDate.Value.Day == today.Day;
        }

        /// <summary>
        /// Sosyal medya hesabı ekle/güncelle
        /// </summary>
        public void SetSocialMediaAccount(string platform, string account)
        {
            if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(account))
                return;

            SocialMediaAccounts[platform] = account;
            LastUpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Sosyal medya hesabı sil
        /// </summary>
        public void RemoveSocialMediaAccount(string platform)
        {
            if (SocialMediaAccounts.ContainsKey(platform))
            {
                SocialMediaAccounts.Remove(platform);
                LastUpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// GDPR uyumlu veri anonimleştirme
        /// </summary>
        public void AnonymizeData()
        {
            FirstName = "Anonim";
            LastName = "Kullanıcı";
            Email = $"anonim_{Id}@example.com";
            Phone = null;
            BirthDate = null;
            Gender = null;
            Country = null;
            City = null;
            SocialMediaAccounts.Clear();
            ProfilePhotoPath = null;
            IsActive = false;
            LastUpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Cinsiyet seçenekleri
    /// </summary>
    public enum Gender
    {
        [Display(Name = "Erkek")]
        Male,
        
        [Display(Name = "Kadın")]
        Female,
        
        [Display(Name = "Diğer")]
        Other,
        
        [Display(Name = "Belirtmek istemiyorum")]
        PreferNotToSay
    }

    /// <summary>
    /// Desteklenen sosyal medya platformları
    /// </summary>
    public static class SocialMediaPlatforms
    {
        public const string Twitter = "Twitter";
        public const string LinkedIn = "LinkedIn";
        public const string Instagram = "Instagram";
        public const string Facebook = "Facebook";
        public const string GitHub = "GitHub";
        public const string YouTube = "YouTube";
        public const string TikTok = "TikTok";
        public const string Medium = "Medium";

        public static readonly List<string> All = new List<string>
        {
            Twitter, LinkedIn, Instagram, Facebook, 
            GitHub, YouTube, TikTok, Medium
        };
    }
}