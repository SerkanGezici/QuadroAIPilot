using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Debug amaçlı: Outlook hesaplarını test eden komut
    /// "outlook hesaplarımı göster" komutu ile çalışır
    /// </summary>
    public class DebugOutlookAccountsCommand : ICommand
    {
        private string _currentCommandText = "";
        
        public string CommandText => _currentCommandText;
        
        public async Task<bool> ExecuteAsync()
        {
            return await ExecuteAsync(_currentCommandText);
        }
        
        public void SetCommandText(string text)
        {
            _currentCommandText = text;
        }
        
        public async Task<bool> ExecuteAsync(string text)
        {
            try
            {
                _currentCommandText = text;
                Debug.WriteLine($"[DebugOutlookAccountsCommand] Debug komutu çalışıyor: {text}");
                
                await TextToSpeechService.SpeakTextAsync("Outlook hesapları kontrol ediliyor...");
                
                // 1. SimpleEmailAccountManager test et
                var accountManager = new SimpleEmailAccountManager();
                await accountManager.InitializeAccountsAsync();
                var managedAccounts = accountManager.GetActiveAccounts();
                
                Debug.WriteLine($"[DebugOutlookAccountsCommand] SimpleEmailAccountManager: {managedAccounts.Count} hesap");
                
                // 2. WindowsOutlookProfileService direkt test et
                var profileService = new WindowsOutlookProfileService();
                var profiles = profileService.DiscoverOutlookProfiles();
                var allAccounts = profileService.GetAllEmailAccounts();
                
                Debug.WriteLine($"[DebugOutlookAccountsCommand] WindowsOutlookProfileService: {profiles.Count} profil, {allAccounts.Count} hesap");
                
                // 3. SimpleWindowsCredentialService test et
                var credentialService = new SimpleWindowsCredentialService();
                var discoveredAccounts = credentialService.DiscoverEmailAccounts();
                
                Debug.WriteLine($"[DebugOutlookAccountsCommand] SimpleWindowsCredentialService: {discoveredAccounts.Count} hesap");
                
                // Sonuçları raporla
                string report = $"Outlook hesap taraması tamamlandı. ";
                
                if (managedAccounts.Any())
                {
                    report += $"{managedAccounts.Count} aktif hesap bulundu. ";
                    
                    foreach (var account in managedAccounts.Take(3))
                    {
                        report += $"{account.FriendlyName}, ";
                        
                        // Outputa detaylı bilgi ekle
                        string accountDetail = $"📧 {account.FriendlyName}";
                        accountDetail += $"\n   ✉️  {account.EmailAddress}";
                        accountDetail += $"\n   🔗 {account.Protocol}";
                        accountDetail += $"\n   📊 {(account.IsAuthenticated ? "Bağlı" : "Bağlı Değil")}";
                        
                        TextToSpeechService.SendToOutput(accountDetail);
                    }
                    
                    if (managedAccounts.Count > 3)
                    {
                        report += $"ve {managedAccounts.Count - 3} hesap daha. ";
                    }
                }
                else
                {
                    report += "Hiç hesap bulunamadı. ";
                    
                    // Debug bilgilerini outputa ekle
                    TextToSpeechService.SendToOutput("🔍 DEBUG BİLGİLERİ:");
                    TextToSpeechService.SendToOutput($"📊 Profil sayısı: {profiles.Count}");
                    TextToSpeechService.SendToOutput($"📊 Toplam hesap: {allAccounts.Count}");
                    TextToSpeechService.SendToOutput($"📊 Keşfedilen hesap: {discoveredAccounts.Count}");
                    
                    if (discoveredAccounts.Any())
                    {
                        foreach (var account in discoveredAccounts)
                        {
                            TextToSpeechService.SendToOutput($"❓ {account.DisplayName} - {account.Source}");
                        }
                    }
                }
                
                await TextToSpeechService.SpeakTextAsync(report);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DebugOutlookAccountsCommand] Hata: {ex.Message}");
                await TextToSpeechService.SpeakTextAsync("Outlook hesap kontrolünde hata oluştu.");
                
                // Hata detayını outputa ekle
                TextToSpeechService.SendToOutput($"❌ HATA: {ex.Message}");
                TextToSpeechService.SendToOutput($"📍 Stack: {ex.StackTrace}");
                
                return false;
            }
        }
        
        public bool CanHandle(string text)
        {
            text = text.ToLowerInvariant();
            return text.Contains("outlook") && text.Contains("hesap") && 
                   (text.Contains("göster") || text.Contains("listele") || text.Contains("kontrol"));
        }
    }
}