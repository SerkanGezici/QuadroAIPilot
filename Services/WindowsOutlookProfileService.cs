using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Windows Registry'den Outlook profillerini okur
    /// </summary>
    public class WindowsOutlookProfileService
    {
        private const string OUTLOOK_PROFILES_BASE = @"SOFTWARE\Microsoft\Office";
        private readonly string[] OFFICE_VERSIONS = { "16.0", "15.0", "14.0" }; // 2021, 2019, 2016, 2013, 2010
        
        public class OutlookProfile
        {
            public string ProfileName { get; set; } = "";
            public List<EmailAccountInfo> EmailAccounts { get; set; } = new();
            public bool IsDefault { get; set; }
            public string OfficeVersion { get; set; } = "";
        }
        
        public class EmailAccountInfo
        {
            public string EmailAddress { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string ServerName { get; set; } = "";
            public string AccountType { get; set; } = ""; // Exchange, IMAP, POP3
            public bool IsDefault { get; set; }
        }
        
        /// <summary>
        /// Kurulu Outlook profillerini keşfeder
        /// </summary>
        public List<OutlookProfile> DiscoverOutlookProfiles()
        {
            var profiles = new List<OutlookProfile>();
            
            try
            {
                foreach (var version in OFFICE_VERSIONS)
                {
                    var versionProfiles = GetProfilesForVersion(version);
                    profiles.AddRange(versionProfiles);
                }
                
                // {profiles.Count} Outlook profili bulundu
                return profiles;
            }
            catch (Exception)
            {
                // Profil keşif hatası
                return profiles;
            }
        }
        
        private List<OutlookProfile> GetProfilesForVersion(string officeVersion)
        {
            var profiles = new List<OutlookProfile>();
            
            try
            {
                var profilesKeyPath = $@"{OUTLOOK_PROFILES_BASE}\{officeVersion}\Outlook\Profiles";
                
                using var profilesKey = Registry.CurrentUser.OpenSubKey(profilesKeyPath);
                if (profilesKey == null)
                {
                    // Office {officeVersion} profilleri bulunamadı
                    return profiles;
                }
                
                var profileNames = profilesKey.GetSubKeyNames();
                // Office {officeVersion}: {profileNames.Length} profil bulundu
                
                // Default profili bul
                string? defaultProfile = GetDefaultProfile(officeVersion);
                
                foreach (var profileName in profileNames)
                {
                    var profile = ReadProfile(profilesKey, profileName, officeVersion);
                    if (profile != null)
                    {
                        profile.IsDefault = profileName.Equals(defaultProfile, StringComparison.OrdinalIgnoreCase);
                        profiles.Add(profile);
                    }
                }
            }
            catch (Exception)
            {
                // Office {officeVersion} okuma hatası
            }
            
            return profiles;
        }
        
        private string? GetDefaultProfile(string officeVersion)
        {
            try
            {
                var outlookKeyPath = $@"{OUTLOOK_PROFILES_BASE}\{officeVersion}\Outlook";
                using var outlookKey = Registry.CurrentUser.OpenSubKey(outlookKeyPath);
                return outlookKey?.GetValue("DefaultProfile") as string;
            }
            catch
            {
                return null;
            }
        }
        
        private OutlookProfile? ReadProfile(RegistryKey profilesKey, string profileName, string officeVersion)
        {
            try
            {
                using var profileKey = profilesKey.OpenSubKey(profileName);
                if (profileKey == null) return null;
                
                var profile = new OutlookProfile
                {
                    ProfileName = profileName,
                    OfficeVersion = officeVersion
                };
                
                // Email hesaplarını oku
                profile.EmailAccounts = ReadEmailAccountsFromProfile(profileKey);
                
                // Profil '{profileName}': {profile.EmailAccounts.Count} hesap
                
                return profile;
            }
            catch (Exception)
            {
                // Profil '{profileName}' okuma hatası
                return null;
            }
        }
        
        private List<EmailAccountInfo> ReadEmailAccountsFromProfile(RegistryKey profileKey)
        {
            var accounts = new List<EmailAccountInfo>();
            
            try
            {
                // Outlook profil yapısında hesaplar genellikle şu yollarda:
                // - 9375CFF0413111d3B88A00104B2A6676\00000001, 00000002, etc.
                // - Her hesap için Email Address, Display Name, Server bilgileri
                
                var subKeyNames = profileKey.GetSubKeyNames();
                
                foreach (var subKeyName in subKeyNames)
                {
                    // GUID formatındaki alt anahtarları kontrol et
                    if (subKeyName.Length == 32) // GUID without dashes
                    {
                        using var guidKey = profileKey.OpenSubKey(subKeyName);
                        if (guidKey != null)
                        {
                            var guidAccounts = ReadAccountsFromGuidKey(guidKey);
                            accounts.AddRange(guidAccounts);
                        }
                    }
                }
                
                // Alternatif yol: Service Provider anahtarları
                if (!accounts.Any())
                {
                    accounts.AddRange(ReadAccountsFromServiceProviders(profileKey));
                }
            }
            catch (Exception)
            {
                // Hesap okuma hatası
            }
            
            return accounts;
        }
        
        private List<EmailAccountInfo> ReadAccountsFromGuidKey(RegistryKey guidKey)
        {
            var accounts = new List<EmailAccountInfo>();
            
            try
            {
                var subKeys = guidKey.GetSubKeyNames();
                
                foreach (var subKey in subKeys)
                {
                    using var accountKey = guidKey.OpenSubKey(subKey);
                    if (accountKey == null) continue;
                    
                    var account = ExtractAccountInfo(accountKey);
                    if (account != null && !string.IsNullOrEmpty(account.EmailAddress))
                    {
                        accounts.Add(account);
                    }
                }
            }
            catch (Exception)
            {
                // GUID anahtar okuma hatası
            }
            
            return accounts;
        }
        
        private List<EmailAccountInfo> ReadAccountsFromServiceProviders(RegistryKey profileKey)
        {
            var accounts = new List<EmailAccountInfo>();
            
            try
            {
                // Service Provider tabanlı arama
                var serviceKeys = new[] { "Services", "Providers" };
                
                foreach (var serviceKeyName in serviceKeys)
                {
                    using var serviceKey = profileKey.OpenSubKey(serviceKeyName);
                    if (serviceKey != null)
                    {
                        var serviceAccounts = ScanServiceKey(serviceKey);
                        accounts.AddRange(serviceAccounts);
                    }
                }
            }
            catch (Exception)
            {
                // Service provider okuma hatası
            }
            
            return accounts;
        }
        
        private List<EmailAccountInfo> ScanServiceKey(RegistryKey serviceKey)
        {
            var accounts = new List<EmailAccountInfo>();
            
            try
            {
                var subKeys = serviceKey.GetSubKeyNames();
                
                foreach (var subKey in subKeys)
                {
                    using var key = serviceKey.OpenSubKey(subKey);
                    if (key == null) continue;
                    
                    var account = ExtractAccountInfo(key);
                    if (account != null && !string.IsNullOrEmpty(account.EmailAddress))
                    {
                        accounts.Add(account);
                    }
                }
            }
            catch (Exception)
            {
                // Service key tarama hatası
            }
            
            return accounts;
        }
        
        private EmailAccountInfo? ExtractAccountInfo(RegistryKey accountKey)
        {
            try
            {
                // Ortak alan adları
                var emailFields = new[] { "Email Address", "EmailAddress", "Email", "PrimarySmtpAddress" };
                var displayFields = new[] { "Display Name", "DisplayName", "MailboxDisplayName", "Account Name" };
                var serverFields = new[] { "Server", "ServerName", "ExchangeServerName", "IncomingServer" };
                var typeFields = new[] { "Account Type", "AccountType", "Service Type", "Provider" };
                
                string? emailAddress = GetFirstValidValue(accountKey, emailFields);
                string? displayName = GetFirstValidValue(accountKey, displayFields);
                string? serverName = GetFirstValidValue(accountKey, serverFields);
                string? accountType = GetFirstValidValue(accountKey, typeFields);
                
                // Email address yoksa hesap geçersiz
                if (string.IsNullOrEmpty(emailAddress))
                {
                    return null;
                }
                
                return new EmailAccountInfo
                {
                    EmailAddress = emailAddress,
                    DisplayName = displayName ?? emailAddress,
                    ServerName = serverName ?? "",
                    AccountType = DetermineAccountType(accountType, serverName, emailAddress),
                    IsDefault = false // Bu bilgi profil seviyesinde
                };
            }
            catch (Exception)
            {
                // Hesap bilgisi çıkarma hatası
                return null;
            }
        }
        
        private string? GetFirstValidValue(RegistryKey key, string[] fieldNames)
        {
            foreach (var fieldName in fieldNames)
            {
                var value = key.GetValue(fieldName);
                if (value != null)
                {
                    var stringValue = value.ToString();
                    if (!string.IsNullOrWhiteSpace(stringValue))
                    {
                        return stringValue;
                    }
                }
            }
            return null;
        }
        
        private string DetermineAccountType(string? registryType, string? serverName, string emailAddress)
        {
            // Registry'den gelen tip bilgisi
            if (!string.IsNullOrEmpty(registryType))
            {
                if (registryType.Contains("Exchange", StringComparison.OrdinalIgnoreCase))
                    return "Exchange";
                if (registryType.Contains("IMAP", StringComparison.OrdinalIgnoreCase))
                    return "IMAP";
                if (registryType.Contains("POP", StringComparison.OrdinalIgnoreCase))
                    return "POP3";
            }
            
            // Server adından tahmin et
            if (!string.IsNullOrEmpty(serverName))
            {
                if (serverName.Contains("outlook", StringComparison.OrdinalIgnoreCase) || 
                    serverName.Contains("office365", StringComparison.OrdinalIgnoreCase))
                    return "Exchange Online";
                if (serverName.Contains("gmail", StringComparison.OrdinalIgnoreCase))
                    return "Gmail IMAP";
                if (serverName.Contains("yahoo", StringComparison.OrdinalIgnoreCase))
                    return "Yahoo IMAP";
            }
            
            // Email adresinden tahmin et
            var domain = emailAddress.Split('@').LastOrDefault()?.ToLowerInvariant();
            return domain switch
            {
                "outlook.com" or "hotmail.com" or "live.com" => "Outlook.com",
                "gmail.com" => "Gmail",
                "yahoo.com" => "Yahoo",
                _ when domain?.Contains("onmicrosoft.com") == true => "Office 365",
                _ => "Unknown"
            };
        }
        
        /// <summary>
        /// Tüm email hesaplarını düz liste olarak döndürür
        /// </summary>
        public List<SimpleEmailAccountInfo> GetAllEmailAccounts()
        {
            var allAccounts = new List<SimpleEmailAccountInfo>();
            var profiles = DiscoverOutlookProfiles();
            
            foreach (var profile in profiles)
            {
                foreach (var account in profile.EmailAccounts)
                {
                    var simpleAccount = new SimpleEmailAccountInfo
                    {
                        EmailAddress = account.EmailAddress,
                        DisplayName = account.DisplayName,
                        AccountType = DetermineEmailAccountType(account.EmailAddress),
                        Protocol = DetermineProtocol(account.AccountType),
                        Source = $"Outlook Profile: {profile.ProfileName} (Office {profile.OfficeVersion})"
                    };
                    
                    allAccounts.Add(simpleAccount);
                }
            }
            
            // Toplam {allAccounts.Count} email hesabı bulundu
            return allAccounts;
        }
        
        private EmailAccountType DetermineEmailAccountType(string emailAddress)
        {
            var domain = emailAddress.Split('@').LastOrDefault()?.ToLowerInvariant();
            return domain switch
            {
                "outlook.com" or "hotmail.com" or "live.com" => EmailAccountType.Personal,
                "gmail.com" => EmailAccountType.Gmail,
                "yahoo.com" => EmailAccountType.Yahoo,
                _ => EmailAccountType.Corporate
            };
        }
        
        private EmailProtocol DetermineProtocol(string accountType)
        {
            return accountType.ToLowerInvariant() switch
            {
                var t when t.Contains("exchange") => EmailProtocol.Exchange,
                var t when t.Contains("imap") => EmailProtocol.IMAP,
                var t when t.Contains("pop") => EmailProtocol.POP3,
                var t when t.Contains("outlook.com") || t.Contains("office") => EmailProtocol.MicrosoftGraph,
                _ => EmailProtocol.IMAP
            };
        }
    }
}