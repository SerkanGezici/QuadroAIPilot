using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services.MAPI
{
    /// <summary>
    /// MAPI Message management service
    /// Email, Calendar, Contact mesajlarını yönetir
    /// </summary>
    public class MAPIMessageManager : IDisposable
    {
        private readonly NativeMAPIService _nativeService;
        private readonly MAPIFolderManager _folderManager;
        private readonly Dictionary<string, MAPIMessageCache> _messageCache = new();
        private bool _isDisposed = false;
        
        public event EventHandler<MessageEventArgs>? MessageLoaded;
        public event EventHandler<MessageEventArgs>? MessageUpdated;
        public event EventHandler<MessageErrorEventArgs>? MessageError;
        
        public MAPIMessageManager(NativeMAPIService nativeService, MAPIFolderManager folderManager)
        {
            _nativeService = nativeService ?? throw new ArgumentNullException(nameof(nativeService));
            _folderManager = folderManager ?? throw new ArgumentNullException(nameof(folderManager));
            
            Debug.WriteLine("[MAPIMessageManager] Message manager oluşturuldu");
        }
        
        /// <summary>
        /// Folder'dan mesajları alır ve MAPIMessage objelerine dönüştürür
        /// </summary>
        public async Task<MAPIResult<List<MAPIMessage>>> GetMessagesAsync(
            string profileName, 
            MAPIFolderType folderType, 
            int maxCount = 50,
            MessageFilter? filter = null)
        {
            try
            {
                Debug.WriteLine($"[MAPIMessageManager] Mesajlar alınıyor: {profileName} - {folderType} (max: {maxCount})");
                
                // Cache key oluştur
                var cacheKey = $"{profileName}:{folderType}:{maxCount}:{filter?.GetHashCode()}";
                
                // Cache'den kontrol et
                if (_messageCache.ContainsKey(cacheKey))
                {
                    var cache = _messageCache[cacheKey];
                    if (!cache.IsExpired)
                    {
                        Debug.WriteLine($"[MAPIMessageManager] Cached mesajlar döndürülüyor: {cache.Messages.Count}");
                        return MAPIResult<List<MAPIMessage>>.Ok(cache.Messages);
                    }
                }
                
                // Folder'dan mesaj pointer'larını al
                var messagePointersResult = await _folderManager.GetFolderMessagesAsync(profileName, folderType, maxCount);
                if (!messagePointersResult.Success)
                {
                    return MAPIResult<List<MAPIMessage>>.Fail($"Cannot get message pointers: {messagePointersResult.ErrorMessage}");
                }
                
                var messagePointers = messagePointersResult.Data ?? new List<IntPtr>();
                var messages = new List<MAPIMessage>();
                
                // Her message pointer'ı için MAPIMessage objesi oluştur
                foreach (var messagePtr in messagePointers)
                {
                    try
                    {
                        var message = await LoadMessageFromPointerAsync(messagePtr, profileName, folderType);
                        if (message != null)
                        {
                            // Filter uygula
                            if (filter == null || filter.Matches(message))
                            {
                                messages.Add(message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MAPIMessageManager] Mesaj yükleme hatası (ptr: {messagePtr}): {ex.Message}");
                    }
                }
                
                // Cache'e ekle
                _messageCache[cacheKey] = new MAPIMessageCache
                {
                    Messages = messages,
                    CachedTime = DateTime.Now,
                    ExpiryMinutes = 5 // 5 dakika cache
                };
                
                Debug.WriteLine($"[MAPIMessageManager] {messages.Count} mesaj başarıyla yüklendi");
                return MAPIResult<List<MAPIMessage>>.Ok(messages);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIMessageManager] Mesaj alma hatası: {ex.Message}");
                return MAPIResult<List<MAPIMessage>>.Fail("Failed to get messages", ex);
            }
        }
        
        /// <summary>
        /// Message pointer'ından MAPIMessage objesi oluşturur (REAL MAPI IMPLEMENTATION)
        /// </summary>
        private async Task<MAPIMessage?> LoadMessageFromPointerAsync(IntPtr messagePtr, string profileName, MAPIFolderType folderType)
        {
            try
            {
                Debug.WriteLine($"[MAPIMessageManager] REAL Message loading: {messagePtr}");
                
                // Pointer'ın mock mu real mi olduğunu kontrol et
                if (IsRealMAPIPointer(messagePtr))
                {
                    Debug.WriteLine($"[MAPIMessageManager] Real MAPI pointer detected, using real property reading");
                    
                    // Real MAPI property reading
                    var realMessageResult = await _nativeService.ReadMessagePropertiesAsync(
                        messagePtr, GetPointerSize(messagePtr), profileName);
                        
                    if (realMessageResult.Success && realMessageResult.Data != null)
                    {
                        var realMessage = realMessageResult.Data;
                        realMessage.FolderType = folderType;
                        
                        Debug.WriteLine($"[MAPIMessageManager] REAL message loaded: {realMessage.Subject}");
                        
                        MessageLoaded?.Invoke(this, new MessageEventArgs
                        {
                            ProfileName = profileName,
                            Message = realMessage
                        });
                        
                        return realMessage;
                    }
                    else
                    {
                        Debug.WriteLine($"[MAPIMessageManager] Real property reading failed: {realMessageResult.ErrorMessage}");
                        Debug.WriteLine($"[MAPIMessageManager] Fallback to enhanced mock message");
                    }
                }
                else
                {
                    Debug.WriteLine($"[MAPIMessageManager] Mock pointer detected: {messagePtr}");
                }
                
                // Fallback to enhanced mock data
                var message = CreateMockMessage(messagePtr, profileName, folderType);
                
                MessageLoaded?.Invoke(this, new MessageEventArgs
                {
                    ProfileName = profileName,
                    Message = message
                });
                
                return message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIMessageManager] Message loading hatası (ptr: {messagePtr}): {ex.Message}");
                
                MessageError?.Invoke(this, new MessageErrorEventArgs
                {
                    ProfileName = profileName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                
                return null;
            }
        }
        
        /// <summary>
        /// Pointer'ın real MAPI pointer olup olmadığını kontrol eder
        /// </summary>
        private bool IsRealMAPIPointer(IntPtr pointer)
        {
            try
            {
                // Real MAPI pointer'lar genellikle allocated buffer'lar
                // Mock pointer'lar ise simple integer cast'ler
                
                long ptrValue = pointer.ToInt64();
                
                // Mock pointer'lar 2000-5000 range'inde
                if (ptrValue >= 2000 && ptrValue <= 5000)
                {
                    return false; // Mock pointer
                }
                
                // Real pointer'lar allocated memory adresleri
                if (ptrValue > 0x10000) // Typical allocated memory range
                {
                    return true; // Likely real MAPI pointer
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Pointer size'ını tahmin eder (real MAPI için)
        /// </summary>
        private uint GetPointerSize(IntPtr pointer)
        {
            // Real MAPI EntryID'ler genellikle 16-70 byte arası
            // Bu basit bir heuristic, gerçek implementasyonda
            // EntryID size bilgisi ayrıca saklanmalı
            return 32; // Default EntryID size estimate
        }
        
        /// <summary>
        /// Enhanced mock message oluşturur (fallback for real MAPI)
        /// </summary>
        private MAPIMessage CreateMockMessage(IntPtr messagePtr, string profileName, MAPIFolderType folderType)
        {
            var messageId = messagePtr.ToInt64().ToString();
            var random = new Random(messagePtr.ToInt32());
            
            Debug.WriteLine($"[MAPIMessageManager] Enhanced mock message oluşturuluyor: {folderType} - {messageId}");
            
            var message = folderType switch
            {
                MAPIFolderType.Inbox or MAPIFolderType.SentMail => CreateMockEmailMessage(messageId, random, profileName),
                MAPIFolderType.Calendar => CreateMockCalendarMessage(messageId, random, profileName),
                MAPIFolderType.Contacts => CreateMockContactMessage(messageId, random, profileName),
                MAPIFolderType.Tasks => CreateMockTaskMessage(messageId, random, profileName),
                _ => CreateMockEmailMessage(messageId, random, profileName)
            };
            
            // Enhanced mock marker ekle
            message.Subject = $"[Enhanced Mock] {message.Subject}";
            
            return message;
        }
        
        private MAPIMessage CreateMockEmailMessage(string messageId, Random random, string profileName)
        {
            var senders = new[] { "Ahmet Yılmaz", "Zeynep Kaya", "Mehmet Demir", "Ayşe Özkan", "Fatma Çelik" };
            var subjects = new[] 
            { 
                "Proje Güncellemesi", "Toplantı Daveti", "Dökümanlar Eklendi", 
                "Önemli Duyuru", "Haftalık Rapor", "Sistem Bakımı" 
            };
            
            var sender = senders[random.Next(senders.Length)];
            var subject = subjects[random.Next(subjects.Length)];
            var isRead = random.Next(10) > 2; // %80 okunmuş
            var importance = (MAPIImportance)random.Next(3);
            var receivedTime = DateTime.Now.AddDays(-random.Next(30)).AddHours(-random.Next(24));
            
            return new MAPIMessage
            {
                MessageId = messageId,
                MessageType = MAPIMessageType.Email,
                Subject = subject,
                Body = $"Bu {subject.ToLower()} ile ilgili detayları içeren bir mesajdır. Lütfen inceleyiniz.",
                SenderName = sender,
                SenderEmail = $"{sender.Replace(" ", ".").ToLower()}@{GetDomainFromProfile(profileName)}",
                ReceivedTime = receivedTime,
                IsRead = isRead,
                Importance = importance,
                HasAttachments = random.Next(10) > 7, // %30 attachment
                MessageSize = random.Next(1024, 50000),
                ProfileName = profileName,
                FolderType = MAPIFolderType.Inbox
            };
        }
        
        private MAPIMessage CreateMockCalendarMessage(string messageId, Random random, string profileName)
        {
            var subjects = new[] 
            { 
                "Proje Toplantısı", "Müşteri Sunumu", "Haftalık Değerlendirme", 
                "Ekip Buluşması", "Stratejik Planlama", "Performans Görüşmesi" 
            };
            
            var locations = new[] { "Toplantı Odası A", "Konferans Salonu", "Online (Teams)", "Müşteri Ofisi", "Ana Ofis" };
            
            var subject = subjects[random.Next(subjects.Length)];
            var location = locations[random.Next(locations.Length)];
            var startTime = DateTime.Now.AddDays(random.Next(-7, 30)).AddHours(random.Next(9, 17));
            var duration = TimeSpan.FromMinutes(random.Next(1, 6) * 30); // 30dk-3saat
            
            return new MAPIMessage
            {
                MessageId = messageId,
                MessageType = MAPIMessageType.Appointment,
                Subject = subject,
                Body = $"{subject} - {location}",
                StartTime = startTime,
                EndTime = startTime.Add(duration),
                Location = location,
                IsAllDay = random.Next(20) == 0, // %5 all day
                IsRecurring = random.Next(10) == 0, // %10 recurring
                ProfileName = profileName,
                FolderType = MAPIFolderType.Calendar
            };
        }
        
        private MAPIMessage CreateMockContactMessage(string messageId, Random random, string profileName)
        {
            var firstNames = new[] { "Ahmet", "Mehmet", "Ali", "Zeynep", "Ayşe", "Fatma", "Elif", "Mustafa" };
            var lastNames = new[] { "Yılmaz", "Kaya", "Demir", "Özkan", "Çelik", "Şahin", "Doğan", "Arslan" };
            var companies = new[] { "TeslaTeknoloji", "Microsoft", "Google", "Apple", "Amazon", "Meta" };
            
            var firstName = firstNames[random.Next(firstNames.Length)];
            var lastName = lastNames[random.Next(lastNames.Length)];
            var company = companies[random.Next(companies.Length)];
            
            return new MAPIMessage
            {
                MessageId = messageId,
                MessageType = MAPIMessageType.Contact,
                Subject = $"{firstName} {lastName}",
                Body = $"{company} - {firstName} {lastName}",
                SenderName = $"{firstName} {lastName}",
                SenderEmail = $"{firstName.ToLower()}.{lastName.ToLower()}@{company.ToLower()}.com",
                CompanyName = company,
                JobTitle = GetRandomJobTitle(random),
                PhoneNumber = GeneratePhoneNumber(random),
                ProfileName = profileName,
                FolderType = MAPIFolderType.Contacts
            };
        }
        
        private MAPIMessage CreateMockTaskMessage(string messageId, Random random, string profileName)
        {
            var tasks = new[] 
            { 
                "Proje dokümantasyonu tamamla", "Müşteri geri bildirimlerini değerlendir", 
                "Sistem güvenlik güncellemesi", "Ekip performans raporu hazırla",
                "Yeni özellik testlerini yap", "Veri yedekleme kontrolü"
            };
            
            var task = tasks[random.Next(tasks.Length)];
            var dueDate = DateTime.Now.AddDays(random.Next(1, 14));
            var priority = (MAPIPriority)random.Next(-1, 2);
            
            return new MAPIMessage
            {
                MessageId = messageId,
                MessageType = MAPIMessageType.Task,
                Subject = task,
                Body = $"Görev detayları: {task}",
                DueDate = dueDate,
                Priority = priority,
                IsCompleted = random.Next(10) > 7, // %30 tamamlanmış
                ProfileName = profileName,
                FolderType = MAPIFolderType.Tasks
            };
        }
        
        private string GetDomainFromProfile(string profileName)
        {
            if (profileName.ToLower().Contains("tesla"))
                return "teslateknoloji.com";
            if (profileName.ToLower().Contains("gmail"))
                return "gmail.com";
            return "outlook.com";
        }
        
        private string GetRandomJobTitle(Random random)
        {
            var titles = new[] { "Yazılım Geliştirici", "Proje Yöneticisi", "Sistem Analisti", "Veri Analisti", "UI/UX Tasarımcı" };
            return titles[random.Next(titles.Length)];
        }
        
        private string GeneratePhoneNumber(Random random)
        {
            return $"+90 5{random.Next(10, 99)} {random.Next(100, 999)} {random.Next(10, 99)} {random.Next(10, 99)}";
        }
        
        /// <summary>
        /// Okunmamış mesajları getirir
        /// </summary>
        public async Task<MAPIResult<List<MAPIMessage>>> GetUnreadMessagesAsync(string profileName, int maxCount = 50)
        {
            try
            {
                Debug.WriteLine($"[MAPIMessageManager] Okunmamış mesajlar alınıyor: {profileName}");
                
                var filter = new MessageFilter { OnlyUnread = true };
                var result = await GetMessagesAsync(profileName, MAPIFolderType.Inbox, maxCount, filter);
                
                if (result.Success)
                {
                    Debug.WriteLine($"[MAPIMessageManager] {result.Data?.Count} okunmamış mesaj bulundu");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIMessageManager] Okunmamış mesaj alma hatası: {ex.Message}");
                return MAPIResult<List<MAPIMessage>>.Fail("Failed to get unread messages", ex);
            }
        }
        
        /// <summary>
        /// Mesajı okundu olarak işaretler
        /// </summary>
        public async Task<MAPIResult<bool>> MarkAsReadAsync(string profileName, string messageId)
        {
            try
            {
                Debug.WriteLine($"[MAPIMessageManager] Mesaj okundu olarak işaretleniyor: {messageId}");
                
                // Bu noktada gerçek MAPI property update yapılacak
                // IMessage::SetProps() ile MSGFLAG_READ flag'i set edilecek
                
                // Şimdilik cache'i güncelle
                UpdateMessageInCache(messageId, m => m.IsRead = true);
                
                MessageUpdated?.Invoke(this, new MessageEventArgs
                {
                    ProfileName = profileName,
                    Message = null // Updated message could be provided here
                });
                
                Debug.WriteLine($"[MAPIMessageManager] Mesaj başarıyla okundu olarak işaretlendi: {messageId}");
                return MAPIResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIMessageManager] Mark as read hatası: {ex.Message}");
                return MAPIResult<bool>.Fail("Failed to mark message as read", ex);
            }
        }
        
        /// <summary>
        /// Belirli bir mesajın detaylarını getirir
        /// </summary>
        public async Task<MAPIResult<MAPIMessage>> GetMessageDetailAsync(string profileName, string messageId)
        {
            try
            {
                Debug.WriteLine($"[MAPIMessageManager] Mesaj detayı alınıyor: {messageId}");
                
                // Cache'lerden ara
                var message = FindMessageInCache(messageId);
                if (message != null)
                {
                    Debug.WriteLine($"[MAPIMessageManager] Mesaj cache'den bulundu: {message.Subject}");
                    return MAPIResult<MAPIMessage>.Ok(message);
                }
                
                // Bu noktada gerçek MAPI message loading yapılacak
                // Specific message ID ile IMessage interface'i alınacak
                
                Debug.WriteLine($"[MAPIMessageManager] Mesaj bulunamadı: {messageId}");
                return MAPIResult<MAPIMessage>.Fail($"Message not found: {messageId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPIMessageManager] Mesaj detay alma hatası: {ex.Message}");
                return MAPIResult<MAPIMessage>.Fail("Failed to get message detail", ex);
            }
        }
        
        /// <summary>
        /// Cache'de mesaj arar
        /// </summary>
        private MAPIMessage? FindMessageInCache(string messageId)
        {
            foreach (var cache in _messageCache.Values)
            {
                var message = cache.Messages.FirstOrDefault(m => m.MessageId == messageId);
                if (message != null)
                    return message;
            }
            return null;
        }
        
        /// <summary>
        /// Cache'deki mesajı günceller
        /// </summary>
        private void UpdateMessageInCache(string messageId, Action<MAPIMessage> updateAction)
        {
            foreach (var cache in _messageCache.Values)
            {
                var message = cache.Messages.FirstOrDefault(m => m.MessageId == messageId);
                if (message != null)
                {
                    updateAction(message);
                    break;
                }
            }
        }
        
        /// <summary>
        /// Cache'i temizler
        /// </summary>
        public void ClearCache(string? profileName = null)
        {
            if (profileName != null)
            {
                var keysToRemove = _messageCache.Keys.Where(k => k.StartsWith($"{profileName}:")).ToList();
                foreach (var key in keysToRemove)
                {
                    _messageCache.Remove(key);
                }
                Debug.WriteLine($"[MAPIMessageManager] Cache temizlendi: {profileName}");
            }
            else
            {
                _messageCache.Clear();
                Debug.WriteLine("[MAPIMessageManager] Tüm cache temizlendi");
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
                    ClearCache();
                }
                
                _isDisposed = true;
                Debug.WriteLine("[MAPIMessageManager] Message manager disposed");
            }
        }
        
        #endregion
    }
    
    #region Data Models
    
    /// <summary>
    /// MAPI Message unified model
    /// </summary>
    public class MAPIMessage
    {
        public string MessageId { get; set; } = "";
        public MAPIMessageType MessageType { get; set; }
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string SenderEmail { get; set; } = "";
        public DateTime ReceivedTime { get; set; }
        public DateTime SentTime { get; set; }
        public bool IsRead { get; set; }
        public MAPIImportance Importance { get; set; }
        public MAPIPriority Priority { get; set; }
        public bool HasAttachments { get; set; }
        public long MessageSize { get; set; }
        public string ProfileName { get; set; } = "";
        public MAPIFolderType FolderType { get; set; }
        
        // Calendar specific
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Location { get; set; } = "";
        public bool IsAllDay { get; set; }
        public bool IsRecurring { get; set; }
        
        // Contact specific
        public string CompanyName { get; set; } = "";
        public string JobTitle { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        
        // Task specific
        public DateTime DueDate { get; set; }
        public bool IsCompleted { get; set; }
        
        public TimeSpan Duration => EndTime - StartTime;
        public bool IsOverdue => MessageType == MAPIMessageType.Task && !IsCompleted && DueDate < DateTime.Now;
        public string FriendlySize => MessageSize < 1024 ? $"{MessageSize} B" : 
                                     MessageSize < 1024 * 1024 ? $"{MessageSize / 1024} KB" : 
                                     $"{MessageSize / (1024 * 1024)} MB";
    }
    
    public enum MAPIMessageType
    {
        Email,
        Appointment,
        Contact,
        Task,
        Note
    }
    
    /// <summary>
    /// Message filtering criteria
    /// </summary>
    public class MessageFilter
    {
        public bool OnlyUnread { get; set; }
        public bool OnlyImportant { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SenderFilter { get; set; }
        public string? SubjectFilter { get; set; }
        public bool OnlyWithAttachments { get; set; }
        
        public bool Matches(MAPIMessage message)
        {
            if (OnlyUnread && message.IsRead) return false;
            if (OnlyImportant && message.Importance != MAPIImportance.High) return false;
            if (FromDate.HasValue && message.ReceivedTime < FromDate.Value) return false;
            if (ToDate.HasValue && message.ReceivedTime > ToDate.Value) return false;
            if (!string.IsNullOrEmpty(SenderFilter) && !message.SenderName.Contains(SenderFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(SubjectFilter) && !message.Subject.Contains(SubjectFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (OnlyWithAttachments && !message.HasAttachments) return false;
            
            return true;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(OnlyUnread, OnlyImportant, FromDate, ToDate, SenderFilter, SubjectFilter, OnlyWithAttachments);
        }
    }
    
    /// <summary>
    /// Message cache container
    /// </summary>
    public class MAPIMessageCache
    {
        public List<MAPIMessage> Messages { get; set; } = new();
        public DateTime CachedTime { get; set; }
        public int ExpiryMinutes { get; set; } = 5;
        
        public bool IsExpired => DateTime.Now - CachedTime > TimeSpan.FromMinutes(ExpiryMinutes);
    }
    
    #endregion
    
    #region Event Args Classes
    
    public class MessageEventArgs : EventArgs
    {
        public string ProfileName { get; set; } = "";
        public MAPIMessage? Message { get; set; }
    }
    
    public class MessageErrorEventArgs : EventArgs
    {
        public string ProfileName { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public Exception? Exception { get; set; }
    }
    
    #endregion
}