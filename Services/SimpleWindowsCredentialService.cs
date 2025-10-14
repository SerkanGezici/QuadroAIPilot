using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Email hesap türleri
/// </summary>
public enum EmailAccountType
{
    Unknown,
    Personal,
    Corporate,
    Gmail,
    Yahoo
}

/// <summary>
/// Email protokol türleri
/// </summary>
public enum EmailProtocol
{
    MicrosoftGraph,
    IMAP,
    Exchange,
    POP3
}

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Windows credential service - Gerçek Outlook hesaplarını keşfeder
    /// </summary>
    public class SimpleWindowsCredentialService
    {
        private readonly WindowsOutlookProfileService _profileService;
        
        public SimpleWindowsCredentialService()
        {
            _profileService = new WindowsOutlookProfileService();
        }
        
        /// <summary>
        /// Gerçek Outlook hesaplarını Windows Registry'den keşfeder
        /// </summary>
        public List<SimpleEmailAccountInfo> DiscoverEmailAccounts()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[SimpleWindowsCredentialService] Gerçek Outlook hesapları keşfediliyor...");
                
                // Windows Registry'den gerçek Outlook hesaplarını al
                var realAccounts = _profileService.GetAllEmailAccounts();
                
                if (realAccounts.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"[SimpleWindowsCredentialService] {realAccounts.Count} gerçek hesap bulundu");
                    return realAccounts;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SimpleWindowsCredentialService] Gerçek hesap bulunamadı, fallback kullanılıyor");
                    
                    // Fallback: Eğer gerçek hesap bulunamazsa, kullanıcıya bilgi ver
                    return new List<SimpleEmailAccountInfo>
                    {
                        new SimpleEmailAccountInfo
                        {
                            EmailAddress = "outlook-hesabi-bulunamadi@example.com",
                            DisplayName = "⚠️ Outlook Hesabı Bulunamadı",
                            AccountType = EmailAccountType.Unknown,
                            Protocol = EmailProtocol.MicrosoftGraph,
                            Source = "Registry Scan Failed - No Outlook profiles found"
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleWindowsCredentialService] Hesap keşif hatası: {ex.Message}");
                
                // Hata durumunda fallback
                return new List<SimpleEmailAccountInfo>
                {
                    new SimpleEmailAccountInfo
                    {
                        EmailAddress = "registry-erisim-hatasi@example.com",
                        DisplayName = "⚠️ Registry Erişim Hatası",
                        AccountType = EmailAccountType.Unknown,
                        Protocol = EmailProtocol.MicrosoftGraph,
                        Source = $"Registry Access Error: {ex.Message}"
                    }
                };
            }
        }
        
        public EmailAccountType DetermineAccountType(string emailAddress)
        {
            if (string.IsNullOrEmpty(emailAddress))
                return EmailAccountType.Unknown;
                
            var domain = emailAddress.Split('@')[1].ToLowerInvariant();
            
            return domain switch
            {
                "outlook.com" or "hotmail.com" or "live.com" => EmailAccountType.Personal,
                "gmail.com" => EmailAccountType.Gmail,
                "yahoo.com" => EmailAccountType.Yahoo,
                _ => EmailAccountType.Corporate
            };
        }
        
        public string ExtractCompanyName(string emailAddress)
        {
            if (string.IsNullOrEmpty(emailAddress) || !emailAddress.Contains("@"))
                return "Bilinmeyen";
                
            var domain = emailAddress.Split('@')[1];
            
            return domain.ToLowerInvariant() switch
            {
                "gmail.com" => "Gmail",
                "outlook.com" => "Outlook",
                "hotmail.com" => "Hotmail",
                "yahoo.com" => "Yahoo",
                _ => FormatDomainAsCompanyName(domain)
            };
        }
        
        private string FormatDomainAsCompanyName(string domain)
        {
            var parts = domain.Split('.');
            var mainPart = parts.Length >= 2 ? parts[parts.Length - 2] : parts[0];
            
            if (mainPart.Length > 0)
            {
                return char.ToUpperInvariant(mainPart[0]) + mainPart.Substring(1).ToLowerInvariant();
            }
            
            return mainPart;
        }
    }
    
    public class SimpleEmailAccountInfo
    {
        public string EmailAddress { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public EmailAccountType AccountType { get; set; }
        public string Source { get; set; } = "";
        public EmailProtocol Protocol { get; set; }
        
        public string GetFriendlyName()
        {
            return AccountType switch
            {
                EmailAccountType.Personal => "Kişisel Hesap",
                EmailAccountType.Gmail => "Gmail Hesabı",
                EmailAccountType.Yahoo => "Yahoo Hesabı",
                EmailAccountType.Corporate => ExtractCompanyName() + " Hesabı",
                _ => DisplayName
            };
        }
        
        private string ExtractCompanyName()
        {
            if (EmailAddress.Contains("@"))
            {
                var domain = EmailAddress.Split('@')[1];
                var parts = domain.Split('.');
                var mainPart = parts.Length >= 2 ? parts[parts.Length - 2] : parts[0];
                return char.ToUpperInvariant(mainPart[0]) + mainPart.Substring(1).ToLowerInvariant();
            }
            return "Şirket";
        }
    }
}