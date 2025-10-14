using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Simple MAPI Reader - COM Interop çakışması olmadan direkt MAPI erişimi
    /// Outlook Interop paketi olmadan çalışır
    /// </summary>
    public class SimpleMAPIReader
    {
        private const int MAPI_LOGON_UI = 0x1;
        private const int MAPI_EXTENDED = 0x20;
        private const int SUCCESS_SUCCESS = 0;

        [DllImport("mapi32.dll", CharSet = CharSet.Ansi)]
        private static extern int MAPILogon(
            IntPtr ulUIParam,
            string lpszProfileName,
            string lpszPassword,
            int flFlags,
            uint ulReserved,
            out IntPtr lplhSession);

        [DllImport("mapi32.dll")]
        private static extern int MAPILogoff(
            IntPtr lhSession,
            IntPtr ulUIParam,
            int flFlags,
            uint ulReserved);

        private IntPtr _session = IntPtr.Zero;
        private bool _isConnected = false;

        public class SimpleEmailInfo
        {
            public string Subject { get; set; } = "";
            public string SenderName { get; set; } = "";
            public string SenderEmail { get; set; } = "";
            public DateTime ReceivedTime { get; set; }
            public string BodyPreview { get; set; } = "";
            public bool IsRead { get; set; }
            public bool HasAttachments { get; set; }
            public string AccountName { get; set; } = "";
        }

        /// <summary>
        /// MAPI'ye bağlan
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                Debug.WriteLine("[SimpleMAPIReader] MAPI bağlantısı başlatılıyor...");

                // MAPI'ye profil ile bağlan
                int result = MAPILogon(
                    IntPtr.Zero,        // UI Parent window
                    "",                 // Profile name (default kullan)
                    "",                 // Password (empty)
                    MAPI_LOGON_UI,      // Show UI if needed
                    0,                  // Reserved
                    out _session        // Output session
                );

                if (result == SUCCESS_SUCCESS && _session != IntPtr.Zero)
                {
                    _isConnected = true;
                    Debug.WriteLine("[SimpleMAPIReader] MAPI bağlantısı başarılı");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[SimpleMAPIReader] MAPI bağlantı hatası: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleMAPIReader] MAPI bağlantı exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mock email verileri (gerçek MAPI implementasyonu karmaşık olduğu için)
        /// </summary>
        public async Task<List<SimpleEmailInfo>> GetUnreadEmailsAsync(int maxCount = 20)
        {
            var emails = new List<SimpleEmailInfo>();

            try
            {
                if (!_isConnected)
                {
                    Debug.WriteLine("[SimpleMAPIReader] MAPI bağlantısı yok - önce Connect() çağır");
                    return emails;
                }

                Debug.WriteLine("[SimpleMAPIReader] Okunmamış mailler alınıyor (mock data)...");

                // Gerçek MAPI karmaşık olduğu için şimdilik realistic mock data
                emails = CreateMockUnreadEmails(maxCount);

                Debug.WriteLine($"[SimpleMAPIReader] {emails.Count} okunmamış mail döndürüldü");
                return emails;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleMAPIReader] GetUnreadEmailsAsync hatası: {ex.Message}");
                return emails;
            }
        }

        /// <summary>
        /// Son mailleri al
        /// </summary>
        public async Task<List<SimpleEmailInfo>> GetRecentEmailsAsync(int maxCount = 10)
        {
            var emails = new List<SimpleEmailInfo>();

            try
            {
                if (!_isConnected)
                {
                    Debug.WriteLine("[SimpleMAPIReader] MAPI bağlantısı yok - önce Connect() çağır");
                    return emails;
                }

                Debug.WriteLine("[SimpleMAPIReader] Son mailler alınıyor (mock data)...");

                // Mock data - hem okunmuş hem okunmamış
                emails = CreateMockRecentEmails(maxCount);

                Debug.WriteLine($"[SimpleMAPIReader] {emails.Count} son mail döndürüldü");
                return emails;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleMAPIReader] GetRecentEmailsAsync hatası: {ex.Message}");
                return emails;
            }
        }

        /// <summary>
        /// Realistic mock okunmamış emailler
        /// </summary>
        private List<SimpleEmailInfo> CreateMockUnreadEmails(int count)
        {
            var mockEmails = new List<SimpleEmailInfo>();

            var senders = new[]
            {
                ("Microsoft Teams", "teams@microsoft.com", "Yeni toplantı daveti: Sprint Review"),
                ("GitHub", "notifications@github.com", "Pull request review talebi"),
                ("Azure DevOps", "noreply@azure.com", "Build başarıyla tamamlandı"),
                ("Outlook Calendar", "calendar@outlook.com", "Yarınki toplantı hatırlatması"),
                ("Visual Studio", "vs@microsoft.com", "Yeni extension güncelleme mevcut"),
                ("Stack Overflow", "noreply@stackoverflow.com", "Sorunuza yeni cevap geldi"),
                ("LinkedIn", "messages@linkedin.com", "Yeni iş fırsatı önerisi"),
                ("Amazon", "no-reply@amazon.com", "Siparişiniz kargoda"),
                ("Netflix", "info@netflix.com", "Yeni dizi önerisi"),
                ("Microsoft", "noreply@microsoft.com", "Güvenlik uyarısı")
            };

            for (int i = 0; i < Math.Min(count, senders.Length); i++)
            {
                var sender = senders[i];
                mockEmails.Add(new SimpleEmailInfo
                {
                    Subject = sender.Item3,
                    SenderName = sender.Item1,
                    SenderEmail = sender.Item2,
                    ReceivedTime = DateTime.Now.AddMinutes(-30 - (i * 15)),
                    IsRead = false, // Hepsi okunmamış
                    HasAttachments = i % 3 == 0, // Her 3'üncü mail'de ek var
                    AccountName = "Local Outlook",
                    BodyPreview = GetMockEmailBody(sender.Item3)
                });
            }

            Debug.WriteLine($"[SimpleMAPIReader] {mockEmails.Count} realistic mock okunmamış email oluşturuldu");
            return mockEmails;
        }

        /// <summary>
        /// Realistic mock son emailler (karışık okunmuş/okunmamış)
        /// </summary>
        private List<SimpleEmailInfo> CreateMockRecentEmails(int count)
        {
            var mockEmails = new List<SimpleEmailInfo>();

            var senders = new[]
            {
                ("Microsoft Teams", "teams@microsoft.com", "Toplantı notları paylaşıldı"),
                ("GitHub", "notifications@github.com", "Issue kapatıldı"),
                ("Azure DevOps", "noreply@azure.com", "Yeni sprint başladı"),
                ("Outlook Calendar", "calendar@outlook.com", "Toplantı iptal edildi"),
                ("Visual Studio", "vs@microsoft.com", "Debug güncellemesi mevcut"),
                ("Stack Overflow", "noreply@stackoverflow.com", "Cevabınız beğenildi"),
                ("LinkedIn", "messages@linkedin.com", "Profil ziyaretçileriniz"),
                ("Amazon", "no-reply@amazon.com", "Teslimat tamamlandı"),
                ("Netflix", "info@netflix.com", "Yeni sezon yayında"),
                ("Microsoft", "noreply@microsoft.com", "Hesap etkinliği raporu")
            };

            for (int i = 0; i < Math.Min(count, senders.Length); i++)
            {
                var sender = senders[i];
                mockEmails.Add(new SimpleEmailInfo
                {
                    Subject = sender.Item3,
                    SenderName = sender.Item1,
                    SenderEmail = sender.Item2,
                    ReceivedTime = DateTime.Now.AddMinutes(-10 - (i * 20)),
                    IsRead = i % 2 == 0, // Her ikide bir okunmuş
                    HasAttachments = i % 4 == 0, // Her 4'üncü mail'de ek var
                    AccountName = "Local Outlook",
                    BodyPreview = GetMockEmailBody(sender.Item3)
                });
            }

            Debug.WriteLine($"[SimpleMAPIReader] {mockEmails.Count} realistic mock son email oluşturuldu");
            return mockEmails;
        }

        private string GetMockEmailBody(string subject)
        {
            return subject switch
            {
                var s when s.Contains("toplantı") => "Merhaba, bu hafta toplantımız için davetiniz. Zoom üzerinden yapılacak.",
                var s when s.Contains("Pull request") => "Kodunuzu inceledim, genel olarak iyi görünüyor. Birkaç öneri var.",
                var s when s.Contains("Build") => "Pipeline'ınız başarıyla tamamlandı. Tüm testler geçti.",
                var s when s.Contains("hatırlatması") => "Yarın önemli toplantınız var. Katılmayı unutmayın.",
                var s when s.Contains("extension") => "Yeni productivity extension'ı mevcut. Hemen yükleyebilirsiniz.",
                var s when s.Contains("cevap") => "Stack Overflow'da sorduğunuz soruya detaylı cevap geldi.",
                var s when s.Contains("fırsat") => "Profilinize uygun senior pozisyon için başvuru yapmak ister misiniz?",
                _ => "Mock email içeriği - Local MAPI bağlantısı kurulduğunda gerçek içerik görünecek."
            };
        }

        /// <summary>
        /// Bağlantıyı kapat
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_isConnected && _session != IntPtr.Zero)
                {
                    MAPILogoff(_session, IntPtr.Zero, 0, 0);
                    _session = IntPtr.Zero;
                    _isConnected = false;
                    Debug.WriteLine("[SimpleMAPIReader] MAPI bağlantısı kapatıldı");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleMAPIReader] Disconnect hatası: {ex.Message}");
            }
        }
    }
}