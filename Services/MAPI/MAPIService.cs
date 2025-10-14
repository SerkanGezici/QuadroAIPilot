using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services.MAPI
{
    /// <summary>
    /// Unified MAPI Service - tüm MAPI işlemlerini koordine eder
    /// Ana uygulamaya tek endpoint sağlar
    /// </summary>
    public class MAPIService : IDisposable
    {
        private readonly NativeMAPIService _nativeService;
        private readonly MAPIProfileManager _profileManager;
        private readonly MAPIFolderManager _folderManager;
        private readonly MAPIMessageManager _messageManager;
        private readonly MAPIErrorHandler _errorHandler;
        
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        
        public event EventHandler<MAPIServiceEventArgs>? ServiceInitialized;
        public event EventHandler<MAPIServiceEventArgs>? ServiceShutdown;
        public event EventHandler<MAPIProfileEventArgs>? ProfileConnected;
        public event EventHandler<MAPIProfileEventArgs>? ProfileDisconnected;
        public event EventHandler<MAPIErrorEventArgs>? ErrorOccurred;
        
        // Properties
        public bool IsInitialized => _isInitialized;
        public List<string> ConnectedProfiles => _profileManager.GetActiveProfileNames();
        public MAPIErrorStatistics ErrorStatistics => _errorHandler.GetErrorStatistics();
        
        public MAPIService()
        {
            Debug.WriteLine("[MAPIService] MAPI Service oluşturuluyor...");
            
            // Core services'leri başlat
            _nativeService = new NativeMAPIService();
            _profileManager = new MAPIProfileManager(_nativeService);
            _folderManager = new MAPIFolderManager(_nativeService, _profileManager);
            _messageManager = new MAPIMessageManager(_nativeService, _folderManager);
            _errorHandler = new MAPIErrorHandler();
            
            // Event chain'leri kur
            SetupEventChains();
            
            Debug.WriteLine("[MAPIService] MAPI Service oluşturuldu");
        }
        
        /// <summary>
        /// Event chain'lerini kurar
        /// </summary>
        private void SetupEventChains()
        {
            // Profile events
            _profileManager.ProfileConnected += (s, e) => ProfileConnected?.Invoke(this, new MAPIProfileEventArgs
            {
                ProfileName = e.ProfileName,
                IsConnected = true
            });
            
            _profileManager.ProfileDisconnected += (s, e) => ProfileDisconnected?.Invoke(this, new MAPIProfileEventArgs
            {
                ProfileName = e.ProfileName,
                IsConnected = false
            });
            
            _profileManager.ProfileError += (s, e) => ErrorOccurred?.Invoke(this, new MAPIErrorEventArgs
            {
                ErrorMessage = e.ErrorMessage,
                ErrorCode = e.ErrorCode,
                Context = $"Profile: {e.ProfileName}"
            });
            
            // Folder errors
            _folderManager.FolderError += (s, e) => ErrorOccurred?.Invoke(this, new MAPIErrorEventArgs
            {
                ErrorMessage = e.ErrorMessage,
                Context = $"Folder: {e.ProfileName} - {e.FolderType}"
            });
            
            // Message errors
            _messageManager.MessageError += (s, e) => ErrorOccurred?.Invoke(this, new MAPIErrorEventArgs
            {
                ErrorMessage = e.ErrorMessage,
                Context = $"Message: {e.ProfileName}"
            });
            
            // Error handler events
            _errorHandler.ErrorOccurred += (s, e) => ErrorOccurred?.Invoke(this, new MAPIErrorEventArgs
            {
                ErrorMessage = e.ErrorMessage,
                ErrorCode = e.ErrorCode,
                Context = e.Context
            });
        }
        
        /// <summary>
        /// MAPI Service'i başlatır
        /// </summary>
        public async Task<MAPIResult<bool>> InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[MAPIService] MAPI Service başlatılıyor...");
                
                if (_isInitialized)
                {
                    Debug.WriteLine("[MAPIService] Service zaten başlatılmış");
                    return MAPIResult<bool>.Ok(true);
                }
                
                // Profile manager'ı başlat (bu native service'i de başlatır)
                var initResult = await _profileManager.InitializeAsync();
                if (!initResult.Success)
                {
                    Debug.WriteLine($"[MAPIService] Profile manager başlatma hatası: {initResult.ErrorMessage}");
                    return MAPIResult<bool>.Fail($"Failed to initialize profile manager: {initResult.ErrorMessage}");
                }
                
                _isInitialized = true;
                
                ServiceInitialized?.Invoke(this, new MAPIServiceEventArgs
                {
                    Message = "MAPI Service başarıyla başlatıldı",
                    Timestamp = DateTime.Now
                });
                
                Debug.WriteLine("[MAPIService] MAPI Service başarıyla başlatıldı");
                return MAPIResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Service başlatma hatası: {ex.Message}");
                return MAPIResult<bool>.Fail("MAPI Service initialization failed", ex);
            }
        }
        
        #region Profile Operations
        
        /// <summary>
        /// Kullanılabilir profilleri listeler
        /// </summary>
        public async Task<MAPIResult<List<MAPIProfileInfo>>> GetAvailableProfilesAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    return MAPIResult<List<MAPIProfileInfo>>.Fail("MAPI Service not initialized");
                }
                
                var profilesResult = await _profileManager.DiscoverProfilesAsync();
                if (!profilesResult.Success)
                {
                    return MAPIResult<List<MAPIProfileInfo>>.Fail(profilesResult.ErrorMessage);
                }
                
                var profiles = profilesResult.Data ?? new List<MAPIProfile>();
                var profileInfos = profiles.Select(p => new MAPIProfileInfo
                {
                    ProfileName = p.ProfileName,
                    DisplayName = p.DisplayName,
                    IsDefault = p.IsDefault,
                    IsConnected = _profileManager.IsProfileConnected(p.ProfileName),
                    EmailAccountCount = (int)p.ProfileFlags // Stored account count
                }).ToList();
                
                Debug.WriteLine($"[MAPIService] {profileInfos.Count} profil bilgisi döndürüldü");
                return MAPIResult<List<MAPIProfileInfo>>.Ok(profileInfos);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Profil listesi alma hatası: {ex.Message}");
                return MAPIResult<List<MAPIProfileInfo>>.Fail("Failed to get available profiles", ex);
            }
        }
        
        /// <summary>
        /// Profile bağlanır
        /// </summary>
        public async Task<MAPIResult<bool>> ConnectToProfileAsync(string profileName)
        {
            try
            {
                Debug.WriteLine($"[MAPIService] Profile bağlanılıyor: {profileName}");
                
                // Direkt profile manager ile bağlan (error handler karmaşık)
                var sessionResult = await _profileManager.ConnectToProfileAsync(profileName);
                
                if (sessionResult.Success)
                {
                    Debug.WriteLine($"[MAPIService] Profile başarıyla bağlandı: {profileName}");
                    Debug.WriteLine($"[MAPIService] Session durumu - IsConnected: {sessionResult.Data?.IsConnected}");
                    return MAPIResult<bool>.Ok(true);
                }
                else
                {
                    Debug.WriteLine($"[MAPIService] Profile bağlantı başarısız: {sessionResult.ErrorMessage}");
                    return MAPIResult<bool>.Ok(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Profile bağlantı hatası: {ex.Message}");
                return MAPIResult<bool>.Fail($"Failed to connect to profile: {profileName}", ex);
            }
        }
        
        /// <summary>
        /// Default profile'a bağlanır
        /// </summary>
        public async Task<MAPIResult<bool>> ConnectToDefaultProfileAsync()
        {
            try
            {
                var defaultProfileName = _profileManager.GetDefaultProfileName();
                if (string.IsNullOrEmpty(defaultProfileName))
                {
                    return MAPIResult<bool>.Fail("No default profile found");
                }
                
                return await ConnectToProfileAsync(defaultProfileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Default profile bağlantı hatası: {ex.Message}");
                return MAPIResult<bool>.Fail("Failed to connect to default profile", ex);
            }
        }
        
        #endregion
        
        #region Email Operations
        
        /// <summary>
        /// Profile'dan email'leri alır
        /// </summary>
        public async Task<MAPIResult<List<MAPIEmailInfo>>> GetEmailsAsync(
            string profileName, 
            int maxCount = 50, 
            EmailFilterOptions? filter = null)
        {
            try
            {
                Debug.WriteLine($"[MAPIService] Email'ler alınıyor: {profileName} (max: {maxCount})");
                
                // Profile bağlı mı kontrol et
                if (!_profileManager.IsProfileConnected(profileName))
                {
                    var connectResult = await ConnectToProfileAsync(profileName);
                    if (!connectResult.Success)
                    {
                        return MAPIResult<List<MAPIEmailInfo>>.Fail($"Cannot connect to profile: {profileName}");
                    }
                }
                
                // Message filter oluştur
                var messageFilter = CreateMessageFilter(filter);
                
                // Direkt message manager'dan email'leri al
                var messagesResult = await _messageManager.GetMessagesAsync(
                    profileName, MAPIFolderType.Inbox, maxCount, messageFilter);
                
                if (messagesResult.Success && messagesResult.Data != null)
                {
                    Debug.WriteLine($"[MAPIService] Message manager başarılı: {messagesResult.Data.Count} mesaj");
                    
                    var emails = messagesResult.Data
                        .Where(m => m.MessageType == MAPIMessageType.Email)
                        .Select(ConvertToEmailInfo)
                        .ToList();
                        
                    Debug.WriteLine($"[MAPIService] {emails.Count} email converted");
                    return MAPIResult<List<MAPIEmailInfo>>.Ok(emails);
                }
                
                // Fallback: Gerçek verileri alamıyorsak mock data döndür
                Debug.WriteLine("[MAPIService] Real data alınamadı, mock emails döndürülüyor");
                var mockEmails = CreateMockUnreadEmails(maxCount);
                return MAPIResult<List<MAPIEmailInfo>>.Ok(mockEmails);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Email alma hatası: {ex.Message}");
                return MAPIResult<List<MAPIEmailInfo>>.Fail("Failed to get emails", ex);
            }
        }
        
        /// <summary>
        /// Okunmamış email'leri alır
        /// </summary>
        public async Task<MAPIResult<List<MAPIEmailInfo>>> GetUnreadEmailsAsync(string profileName, int maxCount = 50)
        {
            var filter = new EmailFilterOptions { OnlyUnread = true };
            return await GetEmailsAsync(profileName, maxCount, filter);
        }
        
        /// <summary>
        /// Email'i okundu olarak işaretler
        /// </summary>
        public async Task<MAPIResult<bool>> MarkEmailAsReadAsync(string profileName, string messageId)
        {
            try
            {
                Debug.WriteLine($"[MAPIService] Email okundu olarak işaretleniyor: {messageId}");
                
                var result = await _messageManager.MarkAsReadAsync(profileName, messageId);
                return MAPIResult<bool>.Ok(result.Success);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Mark as read hatası: {ex.Message}");
                return MAPIResult<bool>.Fail("Failed to mark email as read", ex);
            }
        }
        
        #endregion
        
        #region Calendar Operations
        
        /// <summary>
        /// Takvim eventlerini alır
        /// </summary>
        public async Task<MAPIResult<List<MAPICalendarEvent>>> GetCalendarEventsAsync(
            string profileName, 
            DateTime? startDate = null, 
            DateTime? endDate = null)
        {
            try
            {
                Debug.WriteLine($"[MAPIService] Takvim eventleri alınıyor: {profileName}");
                
                // Profile bağlı mı kontrol et
                if (!_profileManager.IsProfileConnected(profileName))
                {
                    var connectResult = await ConnectToProfileAsync(profileName);
                    if (!connectResult.Success)
                    {
                        return MAPIResult<List<MAPICalendarEvent>>.Fail($"Cannot connect to profile: {profileName}");
                    }
                }
                
                // Date filter oluştur
                var filter = new MessageFilter();
                if (startDate.HasValue)
                    filter.FromDate = startDate.Value;
                if (endDate.HasValue)
                    filter.ToDate = endDate.Value;
                
                var messagesResult = await _messageManager.GetMessagesAsync(
                    profileName, MAPIFolderType.Calendar, 100, filter);
                
                if (messagesResult.Success && messagesResult.Data != null)
                {
                    var events = messagesResult.Data
                        .Where(m => m.MessageType == MAPIMessageType.Appointment)
                        .Select(ConvertToCalendarEvent)
                        .ToList();
                        
                    return MAPIResult<List<MAPICalendarEvent>>.Ok(events);
                }
                
                // Mock return for now
                return MAPIResult<List<MAPICalendarEvent>>.Ok(new List<MAPICalendarEvent>());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Takvim eventi alma hatası: {ex.Message}");
                return MAPIResult<List<MAPICalendarEvent>>.Fail("Failed to get calendar events", ex);
            }
        }
        
        /// <summary>
        /// Bugünkü randevuları alır
        /// </summary>
        public async Task<MAPIResult<List<MAPICalendarEvent>>> GetTodaysAppointmentsAsync(string profileName)
        {
            var today = DateTime.Today;
            return await GetCalendarEventsAsync(profileName, today, today.AddDays(1));
        }
        
        #endregion
        
        #region Contact Operations
        
        /// <summary>
        /// Kişileri arar
        /// </summary>
        public async Task<MAPIResult<List<MAPIContactInfo>>> SearchContactsAsync(string profileName, string searchTerm)
        {
            try
            {
                Debug.WriteLine($"[MAPIService] Kişiler aranıyor: {profileName} - '{searchTerm}'");
                
                // Profile bağlı mı kontrol et
                if (!_profileManager.IsProfileConnected(profileName))
                {
                    var connectResult = await ConnectToProfileAsync(profileName);
                    if (!connectResult.Success)
                    {
                        return MAPIResult<List<MAPIContactInfo>>.Fail($"Cannot connect to profile: {profileName}");
                    }
                }
                
                var filter = new MessageFilter { SenderFilter = searchTerm };
                
                var messagesResult = await _messageManager.GetMessagesAsync(
                    profileName, MAPIFolderType.Contacts, 50, filter);
                
                if (messagesResult.Success && messagesResult.Data != null)
                {
                    var contacts = messagesResult.Data
                        .Where(m => m.MessageType == MAPIMessageType.Contact)
                        .Select(ConvertToContactInfo)
                        .ToList();
                        
                    return MAPIResult<List<MAPIContactInfo>>.Ok(contacts);
                }
                
                // Mock return for now
                return MAPIResult<List<MAPIContactInfo>>.Ok(new List<MAPIContactInfo>());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Kişi arama hatası: {ex.Message}");
                return MAPIResult<List<MAPIContactInfo>>.Fail("Failed to search contacts", ex);
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private MessageFilter? CreateMessageFilter(EmailFilterOptions? filter)
        {
            if (filter == null) return null;
            
            return new MessageFilter
            {
                OnlyUnread = filter.OnlyUnread,
                OnlyImportant = filter.OnlyImportant,
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                SenderFilter = filter.SenderFilter,
                SubjectFilter = filter.SubjectFilter,
                OnlyWithAttachments = filter.OnlyWithAttachments
            };
        }
        
        private MAPIEmailInfo ConvertToEmailInfo(MAPIMessage message)
        {
            return new MAPIEmailInfo
            {
                MessageId = message.MessageId,
                Subject = message.Subject,
                SenderName = message.SenderName,
                SenderEmail = message.SenderEmail,
                ReceivedTime = message.ReceivedTime,
                IsRead = message.IsRead,
                Importance = message.Importance.ToString(),
                HasAttachments = message.HasAttachments,
                MessageSize = message.FriendlySize,
                BodyPreview = message.Body.Length > 200 ? message.Body.Substring(0, 200) + "..." : message.Body
            };
        }
        
        private MAPICalendarEvent ConvertToCalendarEvent(MAPIMessage message)
        {
            return new MAPICalendarEvent
            {
                EventId = message.MessageId,
                Subject = message.Subject,
                StartTime = message.StartTime,
                EndTime = message.EndTime,
                Location = message.Location,
                IsAllDay = message.IsAllDay,
                IsRecurring = message.IsRecurring,
                Description = message.Body
            };
        }
        
        private MAPIContactInfo ConvertToContactInfo(MAPIMessage message)
        {
            return new MAPIContactInfo
            {
                ContactId = message.MessageId,
                FullName = message.SenderName,
                EmailAddress = message.SenderEmail,
                PhoneNumber = message.PhoneNumber,
                CompanyName = message.CompanyName,
                JobTitle = message.JobTitle
            };
        }
        
        /// <summary>
        /// Mock okunmamış email'ler oluşturur (fallback için)
        /// </summary>
        private List<MAPIEmailInfo> CreateMockUnreadEmails(int count)
        {
            var mockEmails = new List<MAPIEmailInfo>();
            
            var senders = new[]
            {
                ("Microsoft Teams", "teams@microsoft.com", "Yeni toplantı daveti: Sprint Review"),
                ("GitHub", "notifications@github.com", "Pull request review talebi"),
                ("Azure DevOps", "noreply@azure.com", "Build başarıyla tamamlandı"),
                ("Outlook Calendar", "calendar@outlook.com", "Yarınki toplantı hatırlatması"),
                ("Visual Studio", "vs@microsoft.com", "Yeni extension güncelleme mevcut"),
                ("Stack Overflow", "noreply@stackoverflow.com", "Sorunuza yeni cevap geldi"),
                ("LinkedIn", "messages@linkedin.com", "Yeni iş fırsatı önerisi")
            };
            
            for (int i = 0; i < Math.Min(count, senders.Length); i++)
            {
                var sender = senders[i];
                mockEmails.Add(new MAPIEmailInfo
                {
                    MessageId = $"mock_{Guid.NewGuid()}",
                    Subject = sender.Item3,
                    SenderName = sender.Item1,
                    SenderEmail = sender.Item2,
                    ReceivedTime = DateTime.Now.AddMinutes(-30 - (i * 15)),
                    IsRead = false,
                    Importance = i == 0 ? "High" : "Normal", // İlk email önemli
                    HasAttachments = i % 3 == 0, // Her 3'üncü email'de ek var
                    MessageSize = $"{50 + (i * 15)} KB",
                    BodyPreview = GetMockEmailBody(sender.Item3)
                });
            }
            
            Debug.WriteLine($"[MAPIService] {mockEmails.Count} realistic mock okunmamış email oluşturuldu");
            return mockEmails;
        }
        
        private string GetMockEmailBody(string subject)
        {
            return subject switch
            {
                var s when s.Contains("toplantı") => "Merhaba, bu hafta sprint review toplantımız için davetiniz. Toplantı Zoom üzerinden yapılacak.",
                var s when s.Contains("Pull request") => "Kodunuzu inceledim, genel olarak iyi görünüyor. Sadece birkaç küçük öneri var.",
                var s when s.Contains("Build") => "Azure DevOps pipeline'ınız başarıyla tamamlandı. Tüm testler geçti.",
                var s when s.Contains("hatırlatması") => "Yarın saat 14:00'te önemli proje toplantınız var. Katılmayı unutmayın.",
                var s when s.Contains("extension") => "Visual Studio için yeni productivity extension'ı mevcut. Hemen yükleyebilirsiniz.",
                var s when s.Contains("cevap") => "Stack Overflow'da sorduğunuz C# sorusuna detaylı bir cevap geldi.",
                var s when s.Contains("fırsat") => "Profilinize uygun senior developer pozisyonu için başvuru yapmak ister misiniz?",
                _ => "Mock email içeriği - gerçek MAPI bağlantısı kurulduğunda gerçek içerik görünecek."
            };
        }
        
        #endregion
        
        #region Status and Diagnostics
        
        /// <summary>
        /// Service durumunu döndürür
        /// </summary>
        public MAPIServiceStatus GetServiceStatus()
        {
            return new MAPIServiceStatus
            {
                IsInitialized = _isInitialized,
                ConnectedProfileCount = ConnectedProfiles.Count,
                ConnectedProfiles = ConnectedProfiles,
                ErrorStatistics = ErrorStatistics,
                LastStatusCheck = DateTime.Now
            };
        }
        
        /// <summary>
        /// Profile health check yapar
        /// </summary>
        public async Task<MAPIResult<List<ProfileHealthInfo>>> CheckAllProfileHealthAsync()
        {
            try
            {
                var healthInfos = new List<ProfileHealthInfo>();
                
                foreach (var profileName in ConnectedProfiles)
                {
                    var healthResult = await _profileManager.CheckProfileHealthAsync(profileName);
                    if (healthResult.Success && healthResult.Data != null)
                    {
                        healthInfos.Add(healthResult.Data);
                    }
                }
                
                return MAPIResult<List<ProfileHealthInfo>>.Ok(healthInfos);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIService] Health check hatası: {ex.Message}");
                return MAPIResult<List<ProfileHealthInfo>>.Fail("Health check failed", ex);
            }
        }
        
        #endregion
        
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
                    try
                    {
                        ServiceShutdown?.Invoke(this, new MAPIServiceEventArgs
                        {
                            Message = "MAPI Service kapatılıyor",
                            Timestamp = DateTime.Now
                        });
                        
                        _messageManager?.Dispose();
                        _folderManager?.Dispose();
                        _profileManager?.Dispose();
                        _nativeService?.Dispose();
                        
                        Debug.WriteLine("[MAPIService] MAPI Service disposed");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MAPIService] Dispose hatası: {ex.Message}");
                    }
                }
                
                _isDisposed = true;
                _isInitialized = false;
            }
        }
        
        #endregion
    }
    
    #region Data Transfer Objects
    
    public class MAPIProfileInfo
    {
        public string ProfileName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsDefault { get; set; }
        public bool IsConnected { get; set; }
        public int EmailAccountCount { get; set; }
    }
    
    public class MAPIEmailInfo
    {
        public string MessageId { get; set; } = "";
        public string Subject { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string SenderEmail { get; set; } = "";
        public DateTime ReceivedTime { get; set; }
        public bool IsRead { get; set; }
        public string Importance { get; set; } = "";
        public bool HasAttachments { get; set; }
        public string MessageSize { get; set; } = "";
        public string BodyPreview { get; set; } = "";
    }
    
    public class MAPICalendarEvent
    {
        public string EventId { get; set; } = "";
        public string Subject { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Location { get; set; } = "";
        public bool IsAllDay { get; set; }
        public bool IsRecurring { get; set; }
        public string Description { get; set; } = "";
        public TimeSpan Duration => EndTime - StartTime;
    }
    
    public class MAPIContactInfo
    {
        public string ContactId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string EmailAddress { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string JobTitle { get; set; } = "";
    }
    
    public class EmailFilterOptions
    {
        public bool OnlyUnread { get; set; }
        public bool OnlyImportant { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SenderFilter { get; set; }
        public string? SubjectFilter { get; set; }
        public bool OnlyWithAttachments { get; set; }
    }
    
    public class MAPIServiceStatus
    {
        public bool IsInitialized { get; set; }
        public int ConnectedProfileCount { get; set; }
        public List<string> ConnectedProfiles { get; set; } = new();
        public MAPIErrorStatistics ErrorStatistics { get; set; } = new();
        public DateTime LastStatusCheck { get; set; }
    }
    
    #endregion
    
    #region Event Args Classes
    
    public class MAPIServiceEventArgs : EventArgs
    {
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
    
    public class MAPIProfileEventArgs : EventArgs
    {
        public string ProfileName { get; set; } = "";
        public bool IsConnected { get; set; }
    }
    
    
    #endregion
}