using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Basit email hesap yönetimi - başlangıç için
    /// </summary>
    public class SimpleEmailAccountManager
    {
        private List<SimpleManagedEmailAccount> _accounts = new();
        private readonly SimpleWindowsCredentialService _credentialService;
        
        public SimpleEmailAccountManager()
        {
            _credentialService = new SimpleWindowsCredentialService();
        }
        
        public async Task<bool> InitializeAccountsAsync()
        {
            try
            {
                // Windows'dan hesapları keşfet
                var discoveredAccounts = _credentialService.DiscoverEmailAccounts();
                
                foreach (var discovered in discoveredAccounts)
                {
                    var managedAccount = new SimpleManagedEmailAccount
                    {
                        Id = Guid.NewGuid().ToString(),
                        EmailAddress = discovered.EmailAddress,
                        FriendlyName = discovered.GetFriendlyName(),
                        AccountType = discovered.AccountType,
                        Protocol = discovered.Protocol,
                        IsEnabled = true,
                        IsAuthenticated = false
                    };
                    
                    _accounts.Add(managedAccount);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleEmailAccountManager] Init hatası: {ex.Message}");
                return false;
            }
        }
        
        public List<SimpleManagedEmailAccount> GetActiveAccounts()
        {
            return _accounts.Where(a => a.IsEnabled).ToList();
        }
        
        public SimpleManagedEmailAccount? MatchAccountFromVoiceCommand(string voiceCommand)
        {
            // Şimdilik ilk hesabı döndür
            return _accounts.FirstOrDefault();
        }
    }
    
    public class SimpleManagedEmailAccount
    {
        public string Id { get; set; } = "";
        public string EmailAddress { get; set; } = "";
        public string FriendlyName { get; set; } = "";
        public EmailAccountType AccountType { get; set; }
        public EmailProtocol Protocol { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsAuthenticated { get; set; } = false;
        public DateTime? LastAccessed { get; set; }
        
        public string GetSummary()
        {
            var status = IsAuthenticated ? "✓" : "○";
            return $"{status} {FriendlyName} ({EmailAddress})";
        }
    }
}