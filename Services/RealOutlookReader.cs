using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Gerçek Outlook MAPI Reader - Late binding ile çalışır
    /// COM Interop çakışmasını önler
    /// </summary>
    public class RealOutlookReader
    {
        private dynamic _outlookApp;
        private dynamic _nameSpace;

        // Compiled regex for better performance (HTML tag removal)
        private static readonly Regex HtmlTagRegex = new Regex("<.*?>", RegexOptions.Compiled | RegexOptions.Singleline);
        
        public class RealEmailInfo
        {
            public string Subject { get; set; } = "";
            public string SenderName { get; set; } = "";
            public string SenderEmail { get; set; } = "";
            public string RecipientName { get; set; } = "";  // Gönderilmiş mailler için alıcı adı
            public string RecipientEmail { get; set; } = ""; // Gönderilmiş mailler için alıcı email
            public DateTime ReceivedTime { get; set; }
            public string BodyPreview { get; set; } = "";
            public bool IsRead { get; set; }
            public string Importance { get; set; } = "";
            public bool HasAttachments { get; set; }
            public string AccountName { get; set; } = "";
            public List<AttachmentInfo> Attachments { get; set; } = new List<AttachmentInfo>();
            public bool IsSentMail { get; set; } = false; // Mail gönderilmiş mi yoksa gelen mi
            public string EntryID { get; set; } = ""; // Outlook'ta mail'i açmak için unique ID
        }
        
        public class AttachmentInfo
        {
            public string FileName { get; set; } = "";
            public string FileType { get; set; } = "";
            public long FileSize { get; set; }
            public string SizeFormatted => FormatFileSize(FileSize);
            public string ContentSummary { get; set; } = "";
            
            private static string FormatFileSize(long bytes)
            {
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
                return $"{bytes / (1024 * 1024)} MB";
            }
        }
        
        /// <summary>
        /// Outlook'a late binding ile bağlan
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() => ConnectSyncWithTimeout());
        }
        
        private bool ConnectSyncWithTimeout()
        {
            try
            {
                Debug.WriteLine("[RealOutlookReader] Outlook'a bağlanılıyor...");
                
                // Outlook COM instance oluştur
                Type outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    Debug.WriteLine("[RealOutlookReader] Outlook.Application ProgID bulunamadı!");
                    return false;
                }
                
                Debug.WriteLine("[RealOutlookReader] Outlook instance oluşturuluyor (timeout ile)...");
                
                // Timeout ile CreateInstance
                bool instanceCreated = false;
                object outlookInstance = null;
                
                var createTask = Task.Run(() =>
                {
                    try
                    {
                        outlookInstance = Activator.CreateInstance(outlookType);
                        instanceCreated = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RealOutlookReader] CreateInstance hatası: {ex.Message}");
                    }
                });
                
                // 10 saniye timeout
                if (createTask.Wait(TimeSpan.FromSeconds(10)))
                {
                    if (instanceCreated && outlookInstance != null)
                    {
                        _outlookApp = outlookInstance;
                        Debug.WriteLine("[RealOutlookReader] Outlook instance oluşturuldu!");
                    }
                    else
                    {
                        Debug.WriteLine("[RealOutlookReader] Outlook instance oluşturulamadı!");
                        return false;
                    }
                }
                else
                {
                    Debug.WriteLine("[RealOutlookReader] Outlook instance oluşturma timeout!");
                    return false;
                }
                
                if (_outlookApp == null)
                {
                    Debug.WriteLine("[RealOutlookReader] Outlook instance null!");
                    return false;
                }
                
                Debug.WriteLine("[RealOutlookReader] MAPI namespace alınıyor...");
                
                // MAPI namespace'i de timeout ile al
                bool namespaceCreated = false;
                var namespaceTask = Task.Run(() =>
                {
                    try
                    {
                        _nameSpace = _outlookApp.GetNamespace("MAPI");
                        namespaceCreated = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RealOutlookReader] GetNamespace hatası: {ex.Message}");
                    }
                });
                
                if (namespaceTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    if (!namespaceCreated || _nameSpace == null)
                    {
                        Debug.WriteLine("[RealOutlookReader] MAPI namespace alınamadı!");
                        return false;
                    }
                }
                else
                {
                    Debug.WriteLine("[RealOutlookReader] MAPI namespace alma timeout!");
                    return false;
                }
                
                Debug.WriteLine("[RealOutlookReader] Outlook bağlantısı başarılı!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealOutlookReader] ConnectSyncWithTimeout hatası: {ex.Message}");
                Debug.WriteLine($"[RealOutlookReader] Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Okunmamış mailleri al
        /// </summary>
        public async Task<List<RealEmailInfo>> GetUnreadEmailsAsync(int maxCount = 20)
        {
            var emails = new List<RealEmailInfo>();
            
            Debug.WriteLine($"[RealOutlookReader] GetUnreadEmailsAsync başladı, maxCount: {maxCount}");
            
            try
            {
                if (_nameSpace == null)
                {
                    Debug.WriteLine("[RealOutlookReader] _nameSpace null, boş liste dönülüyor");
                    return emails;
                }
                
                Debug.WriteLine("[RealOutlookReader] Outlook Store'lar alınıyor...");
                
                // Tüm hesapları kontrol et
                var stores = _nameSpace.Stores;
                Debug.WriteLine($"[RealOutlookReader] {stores.Count} adet store bulundu");
                
                for (int i = 1; i <= stores.Count; i++)
                {
                    dynamic store = null;
                    try
                    {
                        store = stores[i];

                        // Timeout için task wrapper
                        var storeTask = Task.Run(() => {
                            dynamic folder = null;
                            try
                            {
                                // Inbox klasörünü al
                                folder = store.GetDefaultFolder(6); // olFolderInbox = 6
                                var accountEmails = GetUnreadEmailsFromFolder(folder, store.DisplayName, maxCount);

                                return accountEmails;
                            }
                            catch (Exception)
                            {
                                return new List<RealEmailInfo>();
                            }
                            finally
                            {
                                // COM cleanup - ALWAYS
                                if (folder != null)
                                {
                                    try { Marshal.ReleaseComObject(folder); } catch { }
                                }
                            }
                        });

                        // 10 saniye timeout (30'dan düşürüldü - performans için)
                        if (storeTask.Wait(10000))
                        {
                            emails.AddRange(storeTask.Result);
                        }
                        else
                        {
                            Debug.WriteLine($"[RealOutlookReader] Store {i} timeout - COM cleanup yapılacak");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RealOutlookReader] Store {i} error: {ex.Message}");
                    }
                    finally
                    {
                        // COM cleanup - ALWAYS
                        if (store != null)
                        {
                            try { Marshal.ReleaseComObject(store); } catch { }
                        }
                    }
                }
                
                Marshal.ReleaseComObject(stores);
                
                // En yeni mailleri önce getir
                emails = emails.OrderByDescending(e => e.ReceivedTime).Take(maxCount).ToList();
                
                return emails;
            }
            catch (Exception)
            {
                return emails;
            }
        }
        
        /// <summary>
        /// En son mailleri al
        /// </summary>
        public async Task<List<RealEmailInfo>> GetRecentEmailsAsync(int maxCount = 10)
        {
            var emails = new List<RealEmailInfo>();
            
            try
            {
                if (_nameSpace == null)
                {
                    return emails;
                }
                
                
                // Tüm hesapları kontrol et
                var stores = _nameSpace.Stores;
                
                for (int i = 1; i <= stores.Count; i++)
                {
                    dynamic store = null;
                    try
                    {
                        store = stores[i];

                        // Timeout için task wrapper
                        var storeTask = Task.Run(() => {
                            dynamic folder = null;
                            try
                            {
                                // Inbox klasörünü al
                                folder = store.GetDefaultFolder(6); // olFolderInbox = 6
                                var accountEmails = GetRecentEmailsFromFolder(folder, store.DisplayName, maxCount);

                                return accountEmails;
                            }
                            catch (Exception)
                            {
                                return new List<RealEmailInfo>();
                            }
                            finally
                            {
                                // COM cleanup - ALWAYS
                                if (folder != null)
                                {
                                    try { Marshal.ReleaseComObject(folder); } catch { }
                                }
                            }
                        });

                        // 10 saniye timeout (30'dan düşürüldü - performans için)
                        if (storeTask.Wait(10000))
                        {
                            emails.AddRange(storeTask.Result);
                        }
                        else
                        {
                            Debug.WriteLine($"[RealOutlookReader] GetRecent Store {i} timeout");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RealOutlookReader] GetRecent Store {i} error: {ex.Message}");
                    }
                    finally
                    {
                        // COM cleanup - ALWAYS
                        if (store != null)
                        {
                            try { Marshal.ReleaseComObject(store); } catch { }
                        }
                    }
                }
                
                Marshal.ReleaseComObject(stores);
                
                // En yeni mailleri önce getir
                emails = emails.OrderByDescending(e => e.ReceivedTime).Take(maxCount).ToList();
                
                return emails;
            }
            catch (Exception)
            {
                return emails;
            }
        }
        
        /// <summary>
        /// Gönderilmiş mailleri al
        /// </summary>
        public async Task<List<RealEmailInfo>> GetSentEmailsAsync(int maxCount = 10)
        {
            var emails = new List<RealEmailInfo>();
            
            try
            {
                if (_nameSpace == null)
                {
                    return emails;
                }
                
                
                // Tüm hesapları kontrol et
                var stores = _nameSpace.Stores;
                
                for (int i = 1; i <= stores.Count; i++)
                {
                    dynamic store = null;
                    try
                    {
                        store = stores[i];

                        // Timeout için task wrapper
                        var storeTask = Task.Run(() => {
                            dynamic folder = null;
                            try
                            {
                                // Sent Items klasörünü al
                                folder = store.GetDefaultFolder(5); // olFolderSentMail = 5
                                var accountEmails = GetSentEmailsFromFolder(folder, store.DisplayName, maxCount);

                                return accountEmails;
                            }
                            catch (Exception)
                            {
                                return new List<RealEmailInfo>();
                            }
                            finally
                            {
                                // COM cleanup - ALWAYS
                                if (folder != null)
                                {
                                    try { Marshal.ReleaseComObject(folder); } catch { }
                                }
                            }
                        });

                        // 10 saniye timeout (30'dan düşürüldü - performans için)
                        if (storeTask.Wait(10000))
                        {
                            emails.AddRange(storeTask.Result);
                        }
                        else
                        {
                            Debug.WriteLine($"[RealOutlookReader] GetSent Store {i} timeout");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RealOutlookReader] GetSent Store {i} error: {ex.Message}");
                    }
                    finally
                    {
                        // COM cleanup - ALWAYS
                        if (store != null)
                        {
                            try { Marshal.ReleaseComObject(store); } catch { }
                        }
                    }
                }
                
                Marshal.ReleaseComObject(stores);
                
                // En yeni mailleri önce getir
                emails = emails.OrderByDescending(e => e.ReceivedTime).Take(maxCount).ToList();
                
                return emails;
            }
            catch (Exception)
            {
                return emails;
            }
        }
        
        private List<RealEmailInfo> GetSentEmailsFromFolder(dynamic folder, string accountName, int maxCount)
        {
            var emails = new List<RealEmailInfo>();
            
            try
            {
                var items = folder.Items;
                
                
                // Timeout riski için sadece son 100 item'ı işle
                int totalCount = items.Count;
                int startIndex = Math.Max(1, totalCount - 100);
                
                // Sort kullanmadan performanslı çözüm
                int processedCount = 0;
                for (int i = totalCount; i >= startIndex && processedCount < maxCount; i--)
                {
                    try
                    {
                        var item = items[i];
                        
                        // Mail item mı kontrol et
                        if (item.Class == 43) // olMail = 43
                        {
                            emails.Add(ConvertToSentEmailInfo(item, accountName));
                            processedCount++;
                        }
                        
                        if (item != null) Marshal.ReleaseComObject(item);
                    }
                    catch (Exception)
                    {
                    }
                }
                
                
                Marshal.ReleaseComObject(items);
            }
            catch (Exception)
            {
            }
            
            return emails;
        }
        
        private List<RealEmailInfo> GetUnreadEmailsFromFolder(dynamic folder, string accountName, int maxCount)
        {
            var emails = new List<RealEmailInfo>();
            
            try
            {
                var items = folder.Items;
                
                
                // Timeout riski için sadece son 100 item'ı işle
                int totalCount = items.Count;
                int startIndex = Math.Max(1, totalCount - 100);
                
                // Önce tarihe göre sırala (en yeniler önce)
                items.Sort("[ReceivedTime]", true);
                
                int processedCount = 0;
                int checkedCount = 0;
                int maxChecks = Math.Min(items.Count, 200); // En fazla 200 mail kontrol et
                
                for (int i = 1; i <= items.Count && processedCount < maxCount && checkedCount < maxChecks; i++)
                {
                    try
                    {
                        var item = items[i];
                        checkedCount++;
                        
                        // Mail item mı kontrol et
                        if (item.Class == 43) // olMail = 43
                        {
                            // Okunmamış mı kontrol et
                            if (item.UnRead == true)
                            {
                                emails.Add(ConvertToRealEmailInfo(item, accountName));
                                processedCount++;
                                Debug.WriteLine($"[RealOutlookReader] Okunmamış mail bulundu: {item.Subject}");
                            }
                        }
                        
                        if (item != null) Marshal.ReleaseComObject(item);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RealOutlookReader] Mail işleme hatası: {ex.Message}");
                    }
                }
                
                Debug.WriteLine($"[RealOutlookReader] {checkedCount} mail kontrol edildi, {processedCount} okunmamış bulundu");
                Marshal.ReleaseComObject(items);
            }
            catch (Exception)
            {
            }
            
            return emails;
        }
        
        private List<RealEmailInfo> GetRecentEmailsFromFolder(dynamic folder, string accountName, int maxCount)
        {
            var emails = new List<RealEmailInfo>();
            
            try
            {
                var items = folder.Items;
                
                
                // Timeout riski için sadece son 100 item'ı işle
                int totalCount = items.Count;
                int startIndex = Math.Max(1, totalCount - 100);
                
                // Sort kullanmadan performanslı çözüm
                int processedCount = 0;
                for (int i = totalCount; i >= startIndex && processedCount < maxCount; i--)
                {
                    try
                    {
                        var item = items[i];
                        
                        // Mail item mı kontrol et
                        if (item.Class == 43) // olMail = 43
                        {
                            emails.Add(ConvertToRealEmailInfo(item, accountName));
                            processedCount++;
                        }
                        
                        if (item != null) Marshal.ReleaseComObject(item);
                    }
                    catch (Exception)
                    {
                    }
                }
                
                
                Marshal.ReleaseComObject(items);
            }
            catch (Exception)
            {
            }
            
            return emails;
        }
        
        private RealEmailInfo ConvertToRealEmailInfo(dynamic mailItem, string accountName)
        {
            try
            {
                var bodyPreview = "";
                try
                {
                    string body = mailItem.Body ?? "";
                    if (!string.IsNullOrEmpty(body))
                    {
                        // HTML taglarını temizle ve ilk 200 karakteri al (compiled regex kullan)
                        bodyPreview = HtmlTagRegex.Replace(body, "");
                        if (bodyPreview.Length > 200)
                            bodyPreview = bodyPreview.Substring(0, 200) + "...";
                    }
                }
                catch { }
                
                // Ek dosya analizi
                var attachments = new List<AttachmentInfo>();
                if (mailItem.Attachments.Count > 0)
                {
                    attachments = ExtractAttachmentInfo(mailItem.Attachments);
                }

                // Sender email'i düzgün almak için
                string senderEmail = "";
                try
                {
                    senderEmail = mailItem.SenderEmailAddress ?? "";
                    
                    // Exchange adresi ise SMTP'ye çevirmeyi dene
                    if (senderEmail.StartsWith("/o=") || senderEmail.StartsWith("/O="))
                    {
                        try
                        {
                            var sender = mailItem.Sender;
                            if (sender != null)
                            {
                                var exchangeUser = sender.GetExchangeUser();
                                if (exchangeUser != null)
                                {
                                    senderEmail = exchangeUser.PrimarySmtpAddress ?? senderEmail;
                                    Marshal.ReleaseComObject(exchangeUser);
                                }
                                Marshal.ReleaseComObject(sender);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    
                }
                catch (Exception)
                {
                }

                return new RealEmailInfo
                {
                    Subject = mailItem.Subject ?? "Konu yok",
                    SenderName = mailItem.SenderName ?? "Bilinmeyen",
                    SenderEmail = senderEmail,
                    ReceivedTime = mailItem.ReceivedTime,
                    BodyPreview = bodyPreview.Trim(),
                    IsRead = !mailItem.UnRead,
                    Importance = mailItem.Importance.ToString(),
                    HasAttachments = mailItem.Attachments.Count > 0,
                    AccountName = accountName,
                    Attachments = attachments,
                    EntryID = mailItem.EntryID ?? ""
                };
            }
            catch (Exception)
            {
                return new RealEmailInfo
                {
                    Subject = "Mail okuma hatası",
                    SenderName = "Hata",
                    AccountName = accountName
                };
            }
        }
        
        private RealEmailInfo ConvertToSentEmailInfo(dynamic mailItem, string accountName)
        {
            try
            {
                var bodyPreview = "";
                try
                {
                    string body = mailItem.Body ?? "";
                    if (!string.IsNullOrEmpty(body))
                    {
                        // HTML taglarını temizle ve ilk 200 karakteri al (compiled regex kullan)
                        bodyPreview = HtmlTagRegex.Replace(body, "");
                        if (bodyPreview.Length > 200)
                            bodyPreview = bodyPreview.Substring(0, 200) + "...";
                    }
                }
                catch { }
                
                // Ek dosya analizi
                var attachments = new List<AttachmentInfo>();
                if (mailItem.Attachments.Count > 0)
                {
                    attachments = ExtractAttachmentInfo(mailItem.Attachments);
                }

                // Gönderilmiş mail için alıcı bilgisini al
                string recipientName = "";
                string recipientEmail = "";
                try
                {
                    var recipients = mailItem.Recipients;
                    if (recipients.Count > 0)
                    {
                        var firstRecipient = recipients[1]; // COM index 1'den başlar
                        recipientName = firstRecipient.Name ?? "";
                        recipientEmail = firstRecipient.Address ?? "";
                        
                        // Exchange adresi ise SMTP'ye çevirmeyi dene
                        if (recipientEmail.StartsWith("/o=") || recipientEmail.StartsWith("/O="))
                        {
                            try
                            {
                                var addressEntry = firstRecipient.AddressEntry;
                                if (addressEntry != null)
                                {
                                    var exchangeUser = addressEntry.GetExchangeUser();
                                    if (exchangeUser != null)
                                    {
                                        recipientEmail = exchangeUser.PrimarySmtpAddress ?? recipientEmail;
                                        Marshal.ReleaseComObject(exchangeUser);
                                    }
                                    Marshal.ReleaseComObject(addressEntry);
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                        
                        
                        // Eğer birden fazla alıcı varsa, sadece ilkini al
                        if (recipients.Count > 1)
                        {
                            recipientName += $" (+{recipients.Count - 1} diğer)";
                        }
                        
                        Marshal.ReleaseComObject(firstRecipient);
                    }
                    Marshal.ReleaseComObject(recipients);
                }
                catch (Exception)
                {
                }

                // Sender email'i düzgün almak için (gönderilmiş mailede de lazım)
                string senderEmail = "";
                try
                {
                    senderEmail = mailItem.SenderEmailAddress ?? "";
                    
                    // Exchange adresi ise SMTP'ye çevirmeyi dene
                    if (senderEmail.StartsWith("/o=") || senderEmail.StartsWith("/O="))
                    {
                        try
                        {
                            var sender = mailItem.Sender;
                            if (sender != null)
                            {
                                var exchangeUser = sender.GetExchangeUser();
                                if (exchangeUser != null)
                                {
                                    senderEmail = exchangeUser.PrimarySmtpAddress ?? senderEmail;
                                    Marshal.ReleaseComObject(exchangeUser);
                                }
                                Marshal.ReleaseComObject(sender);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                catch (Exception)
                {
                }

                return new RealEmailInfo
                {
                    Subject = mailItem.Subject ?? "Konu yok",
                    SenderName = mailItem.SenderName ?? "Bilinmeyen", // Gönderen (kendisi)
                    SenderEmail = senderEmail,
                    RecipientName = recipientName, // Alıcı (kime gönderildi)
                    RecipientEmail = recipientEmail,
                    ReceivedTime = mailItem.SentOn ?? mailItem.ReceivedTime, // Gönderilme zamanı
                    BodyPreview = bodyPreview.Trim(),
                    IsRead = true, // Gönderilmiş mailler her zaman okunmuş sayılır
                    Importance = mailItem.Importance.ToString(),
                    HasAttachments = mailItem.Attachments.Count > 0,
                    AccountName = accountName,
                    Attachments = attachments,
                    IsSentMail = true, // Bu gönderilmiş mail
                    EntryID = mailItem.EntryID ?? ""
                };
            }
            catch (Exception)
            {
                return new RealEmailInfo
                {
                    Subject = "Mail okuma hatası",
                    SenderName = "Hata",
                    AccountName = accountName,
                    IsSentMail = true
                };
            }
        }
        
        /// <summary>
        /// Bağlantıyı kapat
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_nameSpace != null)
                {
                    Marshal.ReleaseComObject(_nameSpace);
                    _nameSpace = null;
                }
                
                if (_outlookApp != null)
                {
                    Marshal.ReleaseComObject(_outlookApp);
                    _outlookApp = null;
                }
                
            }
            catch (Exception)
            {
            }
        }
        
        /// <summary>
        /// Ek dosya bilgilerini çıkarır
        /// </summary>
        private List<AttachmentInfo> ExtractAttachmentInfo(dynamic attachments)
        {
            var attachmentList = new List<AttachmentInfo>();
            
            try
            {
                for (int i = 1; i <= attachments.Count; i++)
                {
                    try
                    {
                        var attachment = attachments[i];
                        
                        var attachInfo = new AttachmentInfo
                        {
                            FileName = attachment.FileName ?? "Bilinmeyen dosya",
                            FileSize = attachment.Size,
                            FileType = GetFileTypeFromExtension(attachment.FileName),
                            ContentSummary = AnalyzeAttachmentContent(attachment)
                        };
                        
                        attachmentList.Add(attachInfo);
                        
                        if (attachment != null) Marshal.ReleaseComObject(attachment);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return attachmentList;
        }

        #region Calendar/Appointment Methods

        /// <summary>
        /// Takvim etkinliği bilgisi
        /// </summary>
        public class CalendarEventInfo
        {
            public string Subject { get; set; } = "";
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string Location { get; set; } = "";
            public string Organizer { get; set; } = "";
            public string Body { get; set; } = "";
            public bool IsAllDay { get; set; }
            public string Duration => $"{(EndTime - StartTime).TotalHours:F1} saat";
            public bool IsRecurring { get; set; }
            public string Status { get; set; } = "";
            public List<string> Attendees { get; set; } = new List<string>();
        }

        /// <summary>
        /// Bugünkü takvim etkinliklerini getirir
        /// </summary>
        public async Task<List<CalendarEventInfo>> GetTodayAppointmentsAsync()
        {
            return await GetAppointmentsByDateAsync(DateTime.Today, DateTime.Today.AddDays(1));
        }

        /// <summary>
        /// Yarınki takvim etkinliklerini getirir
        /// </summary>
        public async Task<List<CalendarEventInfo>> GetTomorrowAppointmentsAsync()
        {
            var tomorrow = DateTime.Today.AddDays(1);
            return await GetAppointmentsByDateAsync(tomorrow, tomorrow.AddDays(1));
        }

        /// <summary>
        /// Bu haftaki takvim etkinliklerini getirir
        /// </summary>
        public async Task<List<CalendarEventInfo>> GetWeekAppointmentsAsync()
        {
            // Pazartesi'den başlayan hafta hesaplaması (Türkiye standardı)
            var today = DateTime.Today;
            int daysFromMonday = ((int)today.DayOfWeek + 6) % 7; // Pazartesi = 0, Pazar = 6
            var startOfWeek = today.AddDays(-daysFromMonday); // Bu haftanın Pazartesi'si
            var endOfWeek = startOfWeek.AddDays(7); // Gelecek haftanın Pazartesi'si
            
            return await GetAppointmentsByDateAsync(startOfWeek, endOfWeek);
        }        /// <summary>
        /// Belirtilen tarih aralığındaki takvim etkinliklerini getirir
        /// </summary>
        public async Task<List<CalendarEventInfo>> GetAppointmentsByDateAsync(DateTime startDate, DateTime endDate)
        {
            var appointments = new List<CalendarEventInfo>();


            try
            {

                if (_nameSpace == null)
                {
                    return appointments;
                }

                // TIMEOUT KONTROLÜ: 30 saniye timeout ekle
                using (var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    return await Task.Run(() => GetAppointmentsByDateSync(startDate, endDate), timeoutCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return appointments;
            }
            catch (Exception)
            {
                return appointments;
            }
        }

        private List<CalendarEventInfo> GetAppointmentsByDateSync(DateTime startDate, DateTime endDate)
        {
            var appointments = new List<CalendarEventInfo>();

            try
            {
                // ÇÖZÜM: Her adımda null check ekle
                dynamic calendarFolder = null;
                dynamic items = null;
                dynamic restrictedItems = null;

                try
                {
                    // Takvim klasörünü al (olDefaultFolderCalendar = 9)
                    calendarFolder = _nameSpace.GetDefaultFolder(9);
                    if (calendarFolder == null)
                    {
                        return appointments;
                    }

                    items = calendarFolder.Items;
                    if (items == null)
                    {
                        return appointments;
                    }

                    // Microsoft önerisi: ÖNCE SORT, SONRA IncludeRecurrences, SONRA RESTRICT
                    items.Sort("[Start]", false);
                    items.IncludeRecurrences = true;

                    // Tarih filtresi - Outlook DASL formatı
                    // Format: d/M/yyyy h:mm tt (Türkiye formatı: Gün/Ay/Yıl)
                    string startStr = startDate.ToString("d/M/yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
                    string endStr = endDate.ToString("d/M/yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

                    string filter = $"[Start] >= '{startStr}' AND [Start] < '{endStr}'";

                    LogService.LogInfo($"[RealOutlookReader] GetAppointmentsByDateSync - Tarih aralığı: {startStr} - {endStr}");
                    LogService.LogInfo($"[RealOutlookReader] GetAppointmentsByDateSync - Filter: {filter}");

                    restrictedItems = items.Restrict(filter);

                    LogService.LogInfo($"[RealOutlookReader] GetAppointmentsByDateSync - Restrict() sonucu: {(restrictedItems == null ? "NULL" : "SUCCESS")}");

                    if (restrictedItems != null)
                    {
                        try
                        {
                            int restrictCount = restrictedItems.Count;
                            LogService.LogInfo($"[RealOutlookReader] GetAppointmentsByDateSync - Restrict().Count = {restrictCount}");
                        }
                        catch (Exception countEx)
                        {
                            LogService.LogInfo($"[RealOutlookReader] GetAppointmentsByDateSync - Count property hatası: {countEx.Message}");
                        }
                    }

                    if (restrictedItems == null)
                    {
                        LogService.LogInfo("[RealOutlookReader] GetAppointmentsByDateSync - Restrict NULL döndü, MANUEL FİLTRELEME başlatılıyor...");

                        // FALLBACK: Manuel filtreleme
                        int totalItems = items.Count;
                        int maxCheck = Math.Min(totalItems, 500); // Maksimum 500 item kontrol et

                        LogService.LogInfo($"[RealOutlookReader] Manuel filtreleme: {totalItems} item içinden {maxCheck} tanesini kontrol ediliyor...");

                        for (int i = 1; i <= maxCheck; i++)
                        {
                            dynamic appt = null;
                            try
                            {
                                appt = items[i];
                                if (appt == null) continue;

                                DateTime start = appt.Start;

                                // Tarih aralığını kontrol et
                                if (start >= startDate && start < endDate)
                                {
                                    string subject = appt.Subject ?? "";
                                    string location = appt.Location ?? "";
                                    DateTime end = appt.End;
                                    bool isRecurring = appt.IsRecurring;
                                    bool isAllDay = appt.AllDayEvent;
                                    int attendeeCount = 0;

                                    try { attendeeCount = appt.Recipients.Count; } catch { }

                                    appointments.Add(new CalendarEventInfo
                                    {
                                        Subject = subject,
                                        Location = location,
                                        StartTime = start,
                                        EndTime = end,
                                        IsRecurring = isRecurring,
                                        IsAllDay = isAllDay
                                    });
                                }
                            }
                            catch (Exception itemEx)
                            {
                                LogService.LogInfo($"[RealOutlookReader] Manuel filtreleme - Item {i} işlenemedi: {itemEx.Message}");
                            }
                            finally
                            {
                                if (appt != null)
                                {
                                    try { Marshal.ReleaseComObject(appt); }
                                    catch { }
                                }
                            }
                        }

                        LogService.LogInfo($"[RealOutlookReader] Manuel filtreleme tamamlandı: {appointments.Count} sonuç");
                        Marshal.ReleaseComObject(items);
                        Marshal.ReleaseComObject(calendarFolder);

                        return appointments;
                    }

                    int itemCount = 0;
                    try
                    {
                        itemCount = restrictedItems.Count;
                        
                        
                        // GÜVENLIK: Count değeri mantıksızsa sınırla
                        if (itemCount > 1000 || itemCount < 0)
                        {
                            itemCount = Math.Min(itemCount, 100); // Maksimum 100 item
                        }
                    }
                    catch (Exception)
                    {
                        return appointments;
                    }


                    // GÜVENLIK: Maksimum 100 item ile sınırla
                    int maxItems = Math.Min(itemCount, 100);
                    for (int i = 1; i <= maxItems; i++)
                    {
                        dynamic appointment = null;
                        try
                        {
                            appointment = restrictedItems[i];
                            if (appointment == null)
                            {
                                continue;
                            }

                            // GÜVENLIK: Appointment'ın gerçek bir appointment olup olmadığını kontrol et
                            try
                            {
                                var testSubject = appointment.Subject; // Test etmek için Subject'e eriş
                                if (testSubject == null)
                                {
                                    continue;
                                }
                            }
                            catch
                            {
                                continue;
                            }

                            var appointmentInfo = ConvertToCalendarEventInfo(appointment);
                            if (appointmentInfo != null)
                            {
                                // MANUEL DOĞRULAMA: Tarih aralığını kontrol et
                                if (appointmentInfo.StartTime >= startDate && appointmentInfo.StartTime < endDate)
                                {
                                    appointments.Add(appointmentInfo);
                                }
                                else
                                {
                                    // Yanlış tarihli appointment, atla ve logla
                                    LogService.LogInfo($"[RealOutlookReader] ATLA: '{appointmentInfo.Subject}' - Tarih: {appointmentInfo.StartTime:yyyy-MM-dd} (aralık dışı: {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd})");
                                }
                            }
                        }
                        catch (Exception)
                        {
                        }
                        finally
                        {
                            if (appointment != null)
                            {
                                try { Marshal.ReleaseComObject(appointment); }
                                catch { }
                            }
                        }
                    }
                }
                finally
                {
                    // ÇÖZÜM: COM nesnelerini güvenli şekilde temizle
                    if (restrictedItems != null)
                    {
                        try { Marshal.ReleaseComObject(restrictedItems); }
                        catch { }
                    }
                    if (items != null)
                    {
                        try { Marshal.ReleaseComObject(items); }
                        catch { }
                    }
                    if (calendarFolder != null)
                    {
                        try { Marshal.ReleaseComObject(calendarFolder); }
                        catch { }
                    }
                }
            }
            catch (Exception)
            {
            }

                
            return appointments.OrderBy(a => a.StartTime).ToList();
        }

        /// <summary>
        /// Appointment nesnesini CalendarEventInfo'ya dönüştürür
        /// </summary>
        private CalendarEventInfo ConvertToCalendarEventInfo(dynamic appointment)
        {
            try
            {
                var attendeesList = new List<string>();

                // Katılımcıları al
                try
                {
                    var recipients = appointment.Recipients;
                    for (int i = 1; i <= recipients.Count; i++)
                    {
                        try
                        {
                            var recipient = recipients[i];
                            attendeesList.Add(recipient.Name ?? "Bilinmeyen");
                            if (recipient != null) Marshal.ReleaseComObject(recipient);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (recipients != null) Marshal.ReleaseComObject(recipients);
                }
                catch (Exception)
                {
                }

                return new CalendarEventInfo
                {
                    Subject = appointment.Subject ?? "Konu yok",
                    StartTime = appointment.Start,
                    EndTime = appointment.End,
                    Location = appointment.Location ?? "",
                    Organizer = appointment.Organizer ?? "",
                    Body = (appointment.Body ?? "").Length > 200 
                        ? appointment.Body.Substring(0, 200) + "..." 
                        : appointment.Body ?? "",
                    IsAllDay = appointment.AllDayEvent,
                    IsRecurring = appointment.IsRecurring,
                    Status = GetAppointmentStatus(appointment.MeetingStatus),
                    Attendees = attendeesList
                };
            }
            catch (Exception)
            {
                return new CalendarEventInfo
                {
                    Subject = "Etkinlik okuma hatası",
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddHours(1)
                };
            }
        }

        /// <summary>
        /// Toplantı durumunu anlamlı metin olarak döndürür
        /// </summary>
        private string GetAppointmentStatus(dynamic meetingStatus)
        {
            try
            {
                int status = meetingStatus;
                return status switch
                {
                    0 => "Normal", // olNonMeeting
                    1 => "Toplantı", // olMeeting
                    2 => "Alındı", // olMeetingReceived
                    3 => "İptal Edildi", // olMeetingCanceled
                    _ => "Bilinmiyor"
                };
            }
            catch
            {
                return "Bilinmiyor";
            }
        }

        #endregion

        /// <summary>
        /// Dosya uzantısından tür belirler
        /// </summary>
        private string GetFileTypeFromExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "Bilinmeyen";
            
            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            
            return extension switch
            {
                ".pdf" => "PDF Döküman",
                ".doc" or ".docx" => "Word Döküman",
                ".xls" or ".xlsx" => "Excel Tablosu",
                ".ppt" or ".pptx" => "PowerPoint Sunumu",
                ".txt" => "Metin Dosyası",
                ".jpg" or ".jpeg" or ".png" or ".gif" => "Resim Dosyası",
                ".zip" or ".rar" or ".7z" => "Sıkıştırılmış Dosya",
                ".mp4" or ".avi" or ".mov" => "Video Dosyası",
                ".mp3" or ".wav" => "Ses Dosyası",
                _ => "Dosya"
            };
        }
        
        /// <summary>
        /// Ek dosya içeriği analizi (gelecekte AI ile geliştirilebilir)
        /// </summary>
        private string AnalyzeAttachmentContent(dynamic attachment)
        {
            try
            {
                string fileName = attachment.FileName ?? "";
                string fileType = GetFileTypeFromExtension(fileName);
                long fileSize = attachment.Size;
                
                // Basit kural tabanlı analiz
                if (fileName.ToLowerInvariant().Contains("fatura"))
                    return "Muhtemelen fatura dökümanı";
                else if (fileName.ToLowerInvariant().Contains("rapor"))
                    return "Rapor dökümanı";
                else if (fileName.ToLowerInvariant().Contains("sözleşme"))
                    return "Sözleşme dökümanı";
                else if (fileName.ToLowerInvariant().Contains("teklif"))
                    return "Teklif dökümanı";
                else if (fileType.Contains("Resim"))
                    return "Görsel içerik";
                else if (fileSize > 10 * 1024 * 1024) // 10MB+
                    return "Büyük dosya - dikkatli inceleyin";
                else if (fileType.Contains("PDF"))
                    return "Okunabilir döküman";
                else if (fileType.Contains("Word"))
                    return "Düzenlenebilir döküman";
                else if (fileType.Contains("Excel"))
                    return "Tablo/hesaplama dökümanı";
                
                return $"{fileType} - İçerik analizi gerekebilir";
            }
            catch (Exception)
            {
                return "İçerik analiz edilemedi";
            }
        }

        /// <summary>
        /// Sadece okunmamış mail sayısını döndürür (TTS tetiklemez)
        /// </summary>
        public async Task<int> GetUnreadCountOnlyAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_nameSpace == null) return 0;

                    int totalUnreadCount = 0;
                    
                    // Tüm hesapları dolaş
                    dynamic folders = _nameSpace.Folders;
                    if (folders == null) return 0;
                    
                    int folderCount = folders.Count;
                    
                    for (int i = 1; i <= folderCount; i++)
                    {
                        dynamic accountFolder = null;
                        dynamic inbox = null;
                        dynamic items = null;
                        dynamic unreadItems = null;
                        
                        try
                        {
                            accountFolder = folders.Item(i);
                            string accountName = accountFolder.Name;
                            
                            // Her hesabın Inbox'ını bul
                            try
                            {
                                inbox = accountFolder.Folders.Item("Gelen Kutusu");
                            }
                            catch
                            {
                                try
                                {
                                    inbox = accountFolder.Folders.Item("Inbox");
                                }
                                catch
                                {
                                    // Bu hesapta inbox yok, devam et
                                    continue;
                                }
                            }
                            
                            if (inbox != null)
                            {
                                items = inbox.Items;
                                if (items != null)
                                {
                                    // Okunmamış mailler için filtre
                                    string filter = "[UnRead] = True";
                                    unreadItems = items.Restrict(filter);
                                    
                                    if (unreadItems != null)
                                    {
                                        int accountUnreadCount = unreadItems.Count;
                                        
                                        // Güvenlik kontrolü
                                        if (accountUnreadCount > 0 && accountUnreadCount < 10000)
                                        {
                                            totalUnreadCount += accountUnreadCount;
                                            LogService.LogInfo($"[RealOutlookReader] {accountName} hesabında {accountUnreadCount} okunmamış mail");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Bu hesapta hata oldu, diğer hesaplara devam et
                        }
                        finally
                        {
                            // COM nesnelerini temizle
                            if (unreadItems != null) Marshal.ReleaseComObject(unreadItems);
                            if (items != null) Marshal.ReleaseComObject(items);
                            if (inbox != null) Marshal.ReleaseComObject(inbox);
                            if (accountFolder != null) Marshal.ReleaseComObject(accountFolder);
                        }
                    }
                    
                    Marshal.ReleaseComObject(folders);
                    
                    LogService.LogInfo($"[RealOutlookReader] Toplam {totalUnreadCount} okunmamış mail");
                    return totalUnreadCount;
                }
                catch (Exception ex)
                {
                    LogService.LogInfo($"[RealOutlookReader] GetUnreadCountOnlyAsync hatası: {ex.Message}");
                    return 0;
                }
            });
        }

        /// <summary>
        /// Sadece bugünkü toplantı sayısını döndürür (TTS tetiklemez)
        /// </summary>
        public async Task<int> GetTodayMeetingCountOnlyAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    LogService.LogInfo("[RealOutlookReader] GetTodayMeetingCountOnlyAsync başladı");
                    
                    if (_nameSpace == null) 
                    {
                        LogService.LogInfo("[RealOutlookReader] GetTodayMeetingCountOnlyAsync - _nameSpace null");
                        return 0;
                    }

                    dynamic calendar = _nameSpace.GetDefaultFolder(9); // 9 = olFolderCalendar
                    if (calendar == null) 
                    {
                        LogService.LogInfo("[RealOutlookReader] GetTodayMeetingCountOnlyAsync - calendar null");
                        return 0;
                    }

                    dynamic items = calendar.Items;
                    if (items == null)
                    {
                        LogService.LogInfo("[RealOutlookReader] GetTodayMeetingCountOnlyAsync - items null");
                        return 0;
                    }

                    // Microsoft önerisi: ÖNCE SORT, SONRA IncludeRecurrences, SONRA RESTRICT
                    items.Sort("[Start]", false);
                    items.IncludeRecurrences = true;

                    // Bugün için tarih filtresi - Outlook COM API formatı
                    DateTime today = DateTime.Today;
                    DateTime tomorrow = today.AddDays(1);

                    // Outlook DASL formatı
                    // Format: d/M/yyyy h:mm tt (Türkiye formatı: Gün/Ay/Yıl)
                    string startStr = today.ToString("d/M/yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
                    string endStr = tomorrow.ToString("d/M/yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

                    string filter = $"[Start] >= '{startStr}' AND [Start] < '{endStr}'";

                    LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync - Today: {today:yyyy-MM-dd}");
                    LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync - Filter: {filter}");

                    dynamic todayItems = items.Restrict(filter);

                    LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync - Restrict() sonucu: {(todayItems == null ? "NULL" : "SUCCESS")}");

                    if (todayItems != null)
                    {
                        try
                        {
                            int restrictCount = todayItems.Count;
                            LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync - Restrict().Count = {restrictCount}");
                        }
                        catch (Exception countEx)
                        {
                            LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync - Count property hatası: {countEx.Message}");
                        }
                    }

                    if (todayItems == null)
                    {
                        LogService.LogInfo("[RealOutlookReader] GetTodayMeetingCountOnlyAsync - Restrict NULL döndü, MANUEL SAYIM başlatılıyor...");

                        // FALLBACK: Manuel sayım
                        int manualCount = 0;
                        int totalItems = items.Count;
                        int maxCheck = Math.Min(totalItems, 500); // Maksimum 500 item kontrol et

                        LogService.LogInfo($"[RealOutlookReader] Manuel sayım: {totalItems} item içinden {maxCheck} tanesini kontrol ediliyor...");

                        for (int i = 1; i <= maxCheck; i++)
                        {
                            dynamic appt = null;
                            try
                            {
                                appt = items[i];
                                if (appt == null) continue;

                                DateTime start = appt.Start;

                                // SADECE BUGÜNE AİT OLANLARI SAY
                                if (start.Date == today.Date)
                                {
                                    manualCount++;
                                    string subject = appt.Subject ?? "Başlık yok";
                                    LogService.LogInfo($"[RealOutlookReader] ✓ Bulundu #{manualCount}: '{subject}' - {start:yyyy-MM-dd HH:mm}");
                                }
                            }
                            catch (Exception itemEx)
                            {
                                LogService.LogInfo($"[RealOutlookReader] Manuel sayım - Item {i} kontrol edilemedi: {itemEx.Message}");
                            }
                            finally
                            {
                                if (appt != null)
                                {
                                    try { Marshal.ReleaseComObject(appt); }
                                    catch { }
                                }
                            }
                        }

                        LogService.LogInfo($"[RealOutlookReader] Manuel sayım tamamlandı: {manualCount} toplantı bulundu");
                        Marshal.ReleaseComObject(items);
                        Marshal.ReleaseComObject(calendar);

                        return manualCount;
                    }

                    // Count property güvenilir değil (Int32.MaxValue dönebilir), iteration yap
                    int count = 0;
                    try
                    {
                        LogService.LogInfo("[RealOutlookReader] GetTodayMeetingCountOnlyAsync - Manuel iteration başlıyor...");

                        foreach (dynamic appt in todayItems)
                        {
                            try
                            {
                                DateTime start = appt.Start;

                                // MANUEL DOĞRULAMA: Sadece bugüne ait olanları say
                                if (start.Date == today.Date)
                                {
                                    count++;
                                    string subject = appt.Subject ?? "Başlık yok";
                                    LogService.LogInfo($"[RealOutlookReader] ✓ #{count}: '{subject}' - {start:yyyy-MM-dd HH:mm}");
                                }
                                else
                                {
                                    string subject = appt.Subject ?? "Başlık yok";
                                    LogService.LogInfo($"[RealOutlookReader] ✗ ATLA: '{subject}' - {start:yyyy-MM-dd} (bugün değil)");
                                }

                                try { Marshal.ReleaseComObject(appt); } catch { }
                            }
                            catch (Exception itemEx)
                            {
                                LogService.LogInfo($"[RealOutlookReader] Item kontrol hatası: {itemEx.Message}");
                            }

                            // Güvenlik limiti
                            if (count > 100) break;
                        }

                        LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync - Manuel iteration tamamlandı: {count} toplantı");
                    }
                    catch (Exception iterEx)
                    {
                        LogService.LogInfo($"[RealOutlookReader] Iteration hatası: {iterEx.Message}");
                        count = 0;
                    }
                    
                    Marshal.ReleaseComObject(todayItems);
                    Marshal.ReleaseComObject(items);
                    Marshal.ReleaseComObject(calendar);
                    
                    LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync tamamlandı - Sonuç: {count}");
                    return count;
                }
                catch (Exception ex)
                {
                    LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync hatası: {ex.Message}");
                    LogService.LogInfo($"[RealOutlookReader] GetTodayMeetingCountOnlyAsync stack trace: {ex.StackTrace}");
                    return 0;
                }
            });
        }
    }
}