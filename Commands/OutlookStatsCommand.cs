using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Outlook istatistiklerini TTS olmadan hızlıca gösteren komut
    /// </summary>
    public class OutlookStatsCommand : ICommand
    {
        private string _commandText = "";

        public string CommandText => _commandText;

        public void SetCommandText(string text)
        {
            _commandText = text;
        }

        public async Task<bool> ExecuteAsync()
        {
            return await ExecuteAsync(_commandText);
        }

        public async Task<bool> ExecuteAsync(string text)
        {
            try
            {
                _commandText = text;
                
                // RealOutlookReader ile bağlan
                var reader = new RealOutlookReader();
                bool connected = await reader.ConnectAsync();
                
                if (!connected)
                {
                    TextToSpeechService.SendToOutput("❌ Outlook'a bağlanılamadı. Outlook açık olduğundan emin olun.");
                    return false;
                }
                
                try
                {
                    // Paralel olarak verileri al - performans için
                    var unreadTask = reader.GetUnreadEmailsAsync(100);
                    var todayMeetingsTask = reader.GetTodayAppointmentsAsync();
                    
                    await Task.WhenAll(unreadTask, todayMeetingsTask);
                    
                    var unreadEmails = await unreadTask;
                    var todayMeetings = await todayMeetingsTask;
                    
                    // İstatistikleri hazırla
                    var stats = new StringBuilder();
                    stats.AppendLine("📊 OUTLOOK DURUM RAPORU");
                    stats.AppendLine("════════════════════════");
                    stats.AppendLine($"📧 Okunmamış E-posta: {unreadEmails.Count}");
                    stats.AppendLine($"📅 Bugünkü Toplantı: {todayMeetings.Count}");
                    
                    // Okunmamış e-postalardan önemli olanları göster
                    var urgentEmails = unreadEmails
                        .Where(e => e.Importance == "High" || 
                                   e.Subject.ToLowerInvariant().Contains("acil") ||
                                   e.Subject.ToLowerInvariant().Contains("urgent"))
                        .Take(3)
                        .ToList();
                    
                    if (urgentEmails.Any())
                    {
                        stats.AppendLine("\n⚠️ ÖNEMLİ E-POSTALAR:");
                        foreach (var email in urgentEmails)
                        {
                            stats.AppendLine($"   • {email.SenderName}: {TruncateSubject(email.Subject)}");
                        }
                    }
                    
                    // Sonraki toplantıyı göster
                    if (todayMeetings.Any())
                    {
                        var now = DateTime.Now;
                        var nextMeeting = todayMeetings
                            .Where(m => m.StartTime > now)
                            .OrderBy(m => m.StartTime)
                            .FirstOrDefault();
                            
                        if (nextMeeting != null)
                        {
                            stats.AppendLine($"\n⏰ SONRAKİ TOPLANTI:");
                            stats.AppendLine($"   {nextMeeting.StartTime:HH:mm} - {nextMeeting.Subject}");
                            if (!string.IsNullOrEmpty(nextMeeting.Location))
                            {
                                stats.AppendLine($"   📍 {nextMeeting.Location}");
                            }
                            
                            // Toplantıya kalan süre
                            var timeUntil = nextMeeting.StartTime - now;
                            if (timeUntil.TotalMinutes < 60)
                            {
                                stats.AppendLine($"   ⏱️ {(int)timeUntil.TotalMinutes} dakika sonra");
                            }
                            else
                            {
                                stats.AppendLine($"   ⏱️ {(int)timeUntil.TotalHours} saat {timeUntil.Minutes} dakika sonra");
                            }
                        }
                        
                        // Devam eden toplantı varsa göster
                        var currentMeeting = todayMeetings
                            .Where(m => m.StartTime <= now && m.EndTime > now)
                            .FirstOrDefault();
                            
                        if (currentMeeting != null)
                        {
                            stats.AppendLine($"\n🔴 DEVAM EDEN TOPLANTI:");
                            stats.AppendLine($"   {currentMeeting.Subject} ({currentMeeting.StartTime:HH:mm}-{currentMeeting.EndTime:HH:mm})");
                        }
                    }
                    
                    // Hesap bazlı özet
                    var accountGroups = unreadEmails.GroupBy(e => e.AccountName);
                    if (accountGroups.Count() > 1)
                    {
                        stats.AppendLine("\n📬 HESAP BAZLI DAĞILIM:");
                        foreach (var group in accountGroups)
                        {
                            stats.AppendLine($"   • {group.Key}: {group.Count()} okunmamış");
                        }
                    }
                    
                    // Son güncelleme zamanı
                    stats.AppendLine($"\n🕐 Son güncelleme: {DateTime.Now:HH:mm:ss}");
                    
                    // Output'a gönder (TTS kullanmadan)
                    TextToSpeechService.SendToOutput(stats.ToString());
                    
                    return true;
                }
                finally
                {
                    reader.Disconnect();
                }
            }
            catch (Exception ex)
            {
                TextToSpeechService.SendToOutput($"❌ Outlook istatistikleri alınamadı: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Komutun işlenebileceğini kontrol eder
        /// </summary>
        public bool CanHandle(string text)
        {
            text = text.ToLowerInvariant();
            
            var triggers = new[] {
                "outlook durum",
                "outlook istatistik",
                "mail sayısı",
                "toplantı sayısı",
                "outlook özet",
                "hızlı durum",
                "outlook stats"
            };
            
            return triggers.Any(trigger => text.Contains(trigger));
        }
        
        /// <summary>
        /// Konu başlığını kısaltır
        /// </summary>
        private string TruncateSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return "Konu yok";
            return subject.Length > 50 ? subject.Substring(0, 50) + "..." : subject;
        }
    }
}