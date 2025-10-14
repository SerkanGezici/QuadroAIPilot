using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services.MAPI
{
    /// <summary>
    /// MAPI Profile management service
    /// Outlook profillerini yönetir ve session'ları koordine eder
    /// </summary>
    public class MAPIProfileManager : IDisposable
    {
        private readonly NativeMAPIService _nativeService;
        private readonly Dictionary<string, MAPISession> _activeSessions = new();
        private readonly Dictionary<string, List<MAPIStore>> _profileStores = new();
        private List<MAPIProfile> _availableProfiles = new();
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        
        public event EventHandler<ProfileEventArgs>? ProfileConnected;
        public event EventHandler<ProfileEventArgs>? ProfileDisconnected;
        public event EventHandler<ProfileErrorEventArgs>? ProfileError;
        
        public MAPIProfileManager(NativeMAPIService nativeService)
        {
            _nativeService = nativeService ?? throw new ArgumentNullException(nameof(nativeService));
            Debug.WriteLine("[MAPIProfileManager] Profile manager oluşturuldu");
        }
        
        /// <summary>
        /// Profile manager'ı başlatır ve profilleri keşfeder
        /// </summary>
        public async Task<MAPIResult<bool>> InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[MAPIProfileManager] Profil yöneticisi başlatılıyor...");
                
                // Native MAPI service'i başlat
                var initResult = await _nativeService.InitializeAsync();
                if (!initResult.Success)
                {
                    return MAPIResult<bool>.Fail($"MAPI initialization failed: {initResult.ErrorMessage}");
                }
                
                // Profilleri keşfet
                var profilesResult = await DiscoverProfilesAsync();
                if (!profilesResult.Success)
                {
                    return MAPIResult<bool>.Fail($"Profile discovery failed: {profilesResult.ErrorMessage}");
                }
                
                _availableProfiles = profilesResult.Data ?? new List<MAPIProfile>();
                _isInitialized = true;
                
                Debug.WriteLine($"[MAPIProfileManager] {_availableProfiles.Count} profil keşfedildi, manager başlatıldı");
                return MAPIResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIProfileManager] Initialization hatası: {ex.Message}");
                return MAPIResult<bool>.Fail("Profile manager initialization failed", ex);
            }
        }
        
        /// <summary>
        /// Tüm kullanılabilir profilleri keşfeder
        /// </summary>
        public async Task<MAPIResult<List<MAPIProfile>>> DiscoverProfilesAsync()
        {
            try
            {
                Debug.WriteLine("[MAPIProfileManager] Profiller keşfediliyor...");
                
                // Native service'den profilleri al
                var nativeProfilesResult = await _nativeService.GetProfilesAsync();
                if (!nativeProfilesResult.Success)
                {
                    return nativeProfilesResult;
                }
                
                var profiles = nativeProfilesResult.Data ?? new List<MAPIProfile>();
                
                // Her profil için ek bilgileri topla
                foreach (var profile in profiles)
                {
                    await EnrichProfileInfoAsync(profile);
                }
                
                Debug.WriteLine($"[MAPIProfileManager] {profiles.Count} profil başarıyla keşfedildi");
                return MAPIResult<List<MAPIProfile>>.Ok(profiles);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIProfileManager] Profile discovery hatası: {ex.Message}");
                return MAPIResult<List<MAPIProfile>>.Fail("Profile discovery failed", ex);
            }
        }
        
        /// <summary>
        /// Profil bilgilerini zenginleştirir
        /// </summary>
        private async Task EnrichProfileInfoAsync(MAPIProfile profile)
        {
            try
            {
                // Windows Registry'den ek bilgileri al
                var registryService = new WindowsOutlookProfileService();
                var outlookProfiles = registryService.DiscoverOutlookProfiles();
                
                var matchingProfile = outlookProfiles.FirstOrDefault(p => 
                    p.ProfileName.Equals(profile.ProfileName, StringComparison.OrdinalIgnoreCase));
                
                if (matchingProfile != null)
                {
                    profile.DisplayName = $"{matchingProfile.ProfileName} ({matchingProfile.EmailAccounts.Count} hesap)";
                    
                    // Email hesap sayısını profil flag'ine ekle
                    profile.ProfileFlags = (uint)matchingProfile.EmailAccounts.Count;
                    
                    Debug.WriteLine($"[MAPIProfileManager] Profil zenginleştirildi: {profile.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIProfileManager] Profil zenginleştirme hatası ({profile.ProfileName}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// Belirtilen profile bağlanır
        /// </summary>
        public async Task<MAPIResult<MAPISession>> ConnectToProfileAsync(string profileName)
        {
            try
            {
                Debug.WriteLine($"[MAPIProfileManager] Profile bağlanılıyor: {profileName}");
                
                if (!_isInitialized)
                {
                    return MAPIResult<MAPISession>.Fail("Profile manager not initialized");
                }
                
                // Zaten bağlı mı kontrol et
                if (_activeSessions.ContainsKey(profileName))
                {
                    var existingSession = _activeSessions[profileName];
                    if (existingSession.IsConnected)
                    {
                        Debug.WriteLine($"[MAPIProfileManager] Mevcut session döndürülüyor: {profileName}");
                        return MAPIResult<MAPISession>.Ok(existingSession);
                    }
                }
                
                // Profil mevcut mu kontrol et
                var profile = _availableProfiles.FirstOrDefault(p => 
                    p.ProfileName.Equals(profileName, StringComparison.OrdinalIgnoreCase));
                
                if (profile.ProfileName == null)
                {
                    return MAPIResult<MAPISession>.Fail($"Profile not found: {profileName}");
                }
                
                // Native service'den session oluştur
                var sessionResult = await _nativeService.CreateSessionAsync(profileName);
                if (!sessionResult.Success)
                {
                    ProfileError?.Invoke(this, new ProfileErrorEventArgs
                    {
                        ProfileName = profileName,
                        ErrorMessage = sessionResult.ErrorMessage,
                        ErrorCode = sessionResult.ErrorCode
                    });
                    
                    return sessionResult;
                }
                
                var session = sessionResult.Data!;
                _activeSessions[profileName] = session;
                
                Debug.WriteLine($"[MAPIProfileManager] Session _activeSessions'a eklendi. Toplam active: {_activeSessions.Count}");
                Debug.WriteLine($"[MAPIProfileManager] Active profile names: {string.Join(", ", _activeSessions.Keys)}");
                
                // Profile stores'ları keşfet
                await DiscoverProfileStoresAsync(profileName, session);
                
                ProfileConnected?.Invoke(this, new ProfileEventArgs
                {
                    ProfileName = profileName,
                    Session = session
                });
                
                Debug.WriteLine($"[MAPIProfileManager] Profile başarıyla bağlandı: {profileName}");
                return MAPIResult<MAPISession>.Ok(session);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIProfileManager] Profile bağlantı hatası ({profileName}): {ex.Message}");
                
                ProfileError?.Invoke(this, new ProfileErrorEventArgs
                {
                    ProfileName = profileName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                
                return MAPIResult<MAPISession>.Fail($"Failed to connect to profile: {profileName}", ex);
            }
        }
        
        /// <summary>
        /// Profile'daki message store'ları keşfeder
        /// </summary>
        private async Task DiscoverProfileStoresAsync(string profileName, MAPISession session)
        {
            try
            {
                Debug.WriteLine($"[MAPIProfileManager] Profile stores keşfediliyor: {profileName}");
                
                // Bu noktada gerçek MAPI store enumeration yapılacak
                // IMAPISession::GetMsgStoresTable() kullanılacak
                
                // Şimdilik mock store'lar oluşturuyoruz
                var stores = new List<MAPIStore>();
                
                // Default store (primary mailbox)
                var defaultStore = new MAPIStore
                {
                    StorePtr = new IntPtr(3000 + profileName.GetHashCode()),
                    StoreName = $"{profileName} - Primary Store",
                    ProviderName = "Microsoft Exchange",
                    IsDefault = true,
                    StoreFlags = 0x00000001 // MAPI_DEFAULT_STORE
                };
                
                stores.Add(defaultStore);
                _profileStores[profileName] = stores;
                
                Debug.WriteLine($"[MAPIProfileManager] {stores.Count} store bulundu: {profileName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIProfileManager] Store discovery hatası ({profileName}): {ex.Message}");
            }
        }
        
        /// <summary>
        /// Profile bağlantısını kapatır
        /// </summary>
        public async Task<MAPIResult<bool>> DisconnectFromProfileAsync(string profileName)
        {
            try
            {
                Debug.WriteLine($"[MAPIProfileManager] Profile bağlantısı kapatılıyor: {profileName}");
                
                if (!_activeSessions.ContainsKey(profileName))
                {
                    return MAPIResult<bool>.Ok(true); // Already disconnected
                }
                
                var session = _activeSessions[profileName];
                
                // Native service'den session'ı kapat
                var closeResult = await _nativeService.CloseSessionAsync(profileName);
                
                // Local state'i temizle
                _activeSessions.Remove(profileName);
                _profileStores.Remove(profileName);
                
                ProfileDisconnected?.Invoke(this, new ProfileEventArgs
                {
                    ProfileName = profileName,
                    Session = session
                });
                
                Debug.WriteLine($"[MAPIProfileManager] Profile bağlantısı kapatıldı: {profileName}");
                return closeResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIProfileManager] Profile disconnect hatası ({profileName}): {ex.Message}");
                return MAPIResult<bool>.Fail($"Failed to disconnect from profile: {profileName}", ex);
            }
        }
        
        /// <summary>
        /// Tüm aktif session'ları kapatır
        /// </summary>
        public async Task<MAPIResult<bool>> DisconnectAllAsync()
        {
            try
            {
                Debug.WriteLine("[MAPIProfileManager] Tüm profile bağlantıları kapatılıyor...");
                
                var profileNames = _activeSessions.Keys.ToList();
                
                foreach (var profileName in profileNames)
                {
                    await DisconnectFromProfileAsync(profileName);
                }
                
                Debug.WriteLine("[MAPIProfileManager] Tüm bağlantılar kapatıldı");
                return MAPIResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIProfileManager] Disconnect all hatası: {ex.Message}");
                return MAPIResult<bool>.Fail("Failed to disconnect all profiles", ex);
            }
        }
        
        /// <summary>
        /// Aktif profilleri listeler
        /// </summary>
        public List<string> GetActiveProfileNames()
        {
            var activeNames = _activeSessions.Keys.ToList();
            Debug.WriteLine($"[MAPIProfileManager] GetActiveProfileNames çağrıldı. Active count: {activeNames.Count}");
            Debug.WriteLine($"[MAPIProfileManager] Active names: [{string.Join(", ", activeNames)}]");
            return activeNames;
        }
        
        /// <summary>
        /// Kullanılabilir profilleri listeler
        /// </summary>
        public List<MAPIProfile> GetAvailableProfiles()
        {
            return new List<MAPIProfile>(_availableProfiles);
        }
        
        /// <summary>
        /// Belirtilen profile ait session'ı döndürür
        /// </summary>
        public MAPISession? GetSession(string profileName)
        {
            return _activeSessions.GetValueOrDefault(profileName);
        }
        
        /// <summary>
        /// Belirtilen profile ait store'ları döndürür
        /// </summary>
        public List<MAPIStore> GetProfileStores(string profileName)
        {
            return _profileStores.GetValueOrDefault(profileName) ?? new List<MAPIStore>();
        }
        
        /// <summary>
        /// Default profile adını döndürür
        /// </summary>
        public string? GetDefaultProfileName()
        {
            var defaultProfile = _availableProfiles.FirstOrDefault(p => p.IsDefault);
            return string.IsNullOrEmpty(defaultProfile.ProfileName) ? null : defaultProfile.ProfileName;
        }
        
        /// <summary>
        /// Profile durumunu kontrol eder
        /// </summary>
        public bool IsProfileConnected(string profileName)
        {
            return _activeSessions.ContainsKey(profileName) && 
                   _activeSessions[profileName].IsConnected;
        }
        
        /// <summary>
        /// Profile health check yapar
        /// </summary>
        public async Task<MAPIResult<ProfileHealthInfo>> CheckProfileHealthAsync(string profileName)
        {
            try
            {
                if (!IsProfileConnected(profileName))
                {
                    return MAPIResult<ProfileHealthInfo>.Fail("Profile not connected");
                }
                
                var session = _activeSessions[profileName];
                var stores = GetProfileStores(profileName);
                
                // Session health kontrolü
                bool isHealthy = session.IsConnected && session.SessionPtr != IntPtr.Zero;
                if (!isHealthy)
                {
                    Debug.WriteLine($"[MAPIProfileManager] Session unhealthy: {profileName}");
                    // Session'ı temizle
                    _activeSessions.Remove(profileName);
                    _profileStores.Remove(profileName);
                }
                
                var healthInfo = new ProfileHealthInfo
                {
                    ProfileName = profileName,
                    IsConnected = isHealthy,
                    SessionUptime = DateTime.Now - session.ConnectedTime,
                    StoreCount = stores.Count,
                    LastCheckTime = DateTime.Now
                };
                
                return MAPIResult<ProfileHealthInfo>.Ok(healthInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIProfileManager] Profile health check hatası ({profileName}): {ex.Message}");
                return MAPIResult<ProfileHealthInfo>.Fail($"Health check failed for profile: {profileName}", ex);
            }
        }
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Managed resources cleanup
                    try
                    {
                        DisconnectAllAsync().Wait();
                        _availableProfiles.Clear();
                        _activeSessions.Clear();
                        _profileStores.Clear();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MAPIProfileManager] Dispose hatası: {ex.Message}");
                    }
                }
                
                _isDisposed = true;
                Debug.WriteLine("[MAPIProfileManager] Profile manager disposed");
            }
        }
        
        #endregion
    }
    
    #region Event Args Classes
    
    public class ProfileEventArgs : EventArgs
    {
        public string ProfileName { get; set; } = "";
        public MAPISession? Session { get; set; }
    }
    
    public class ProfileErrorEventArgs : EventArgs
    {
        public string ProfileName { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public uint ErrorCode { get; set; }
        public Exception? Exception { get; set; }
    }
    
    #endregion
    
    #region Helper Classes
    
    public class ProfileHealthInfo
    {
        public string ProfileName { get; set; } = "";
        public bool IsConnected { get; set; }
        public TimeSpan SessionUptime { get; set; }
        public int StoreCount { get; set; }
        public DateTime LastCheckTime { get; set; }
        public string HealthStatus => IsConnected ? "Healthy" : "Disconnected";
    }
    
    #endregion
}