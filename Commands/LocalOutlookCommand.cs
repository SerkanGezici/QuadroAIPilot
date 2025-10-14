using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using QuadroAIPilot.Services;
using QuadroAIPilot.Helpers;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Local Outlook MAPI erişimi - RealOutlookReader ile gerçek mailler
    /// </summary>
    public class LocalOutlookCommand : ICommand
    {
        private string _currentCommandText = "";
        private readonly ResponseLearningService _responseLearningService;
        private static RealOutlookReader.RealEmailInfo _lastSelectedEmail;
        private static List<RealOutlookReader.RealEmailInfo> _lastEmailList; // Son gösterilen email listesi
        private readonly SmartMailCommandParser _smartParser;
        private readonly SmartMailFilter _smartFilter;

        public string CommandText => _currentCommandText;

        public LocalOutlookCommand()
        {
            // RealOutlookReader ile direkt gerçek maillere erişim
            _responseLearningService = ResponseLearningService.Instance;
            _smartParser = new SmartMailCommandParser();
            _smartFilter = new SmartMailFilter();
        }

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
            Debug.WriteLine($"[LocalOutlookCommand] ExecuteAsync başladı: '{text}'");
            try
            {
                _currentCommandText = text;
                // Tırnak işaretlerini temizle
                char[] trimChars = { '"', '\'', '\u201C', '\u201D', '\u2018', '\u2019' }; // Normal ve fancy tırnaklar
                text = text.TrimEnd(trimChars);
                text = text.ToLowerInvariant().Trim();
                Debug.WriteLine($"[LocalOutlookCommand] Normalize edilmiş komut: '{text}'");
                
                // RealOutlookReader ile direkt bağlan
                var realOutlookReader = new RealOutlookReader();
                Debug.WriteLine("[LocalOutlookCommand] RealOutlookReader oluşturuldu, bağlanıyor...");
                
                bool connected = await realOutlookReader.ConnectAsync();
                Debug.WriteLine($"[LocalOutlookCommand] Outlook bağlantı sonucu: {connected}");
                
                if (!connected)
                {
                    Debug.WriteLine("[LocalOutlookCommand] Outlook bağlantısı başarısız!");
                    await TextToSpeechService.SpeakTextAsync("Outlook'a bağlanılamadı. Outlook açık olduğundan emin olun.");
                    return false;
                }
                
                // RealOutlookReader ile komutları çalıştır
                Debug.WriteLine("[LocalOutlookCommand] ExecuteWithRealOutlook çağrılıyor...");
                return await ExecuteWithRealOutlook(text, realOutlookReader);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalOutlookCommand] ExecuteAsync hatası: {ex.Message}");
                Debug.WriteLine($"[LocalOutlookCommand] Stack trace: {ex.StackTrace}");
                await TextToSpeechService.SpeakTextAsync("E posta işleminde hata oluştu.");
                return false;
            }
        }

        /// <summary>
        /// RealOutlookReader ile komutları çalıştırır (gerçek maillerle)
        /// </summary>
        private async Task<bool> ExecuteWithRealOutlook(string text, RealOutlookReader reader)
        {
            Debug.WriteLine($"[LocalOutlookCommand] ExecuteWithRealOutlook başladı: '{text}'");
            try
            {
                // 1. ÖNCE SMART MAIL COMMAND KONTROLİ
                Debug.WriteLine("[LocalOutlookCommand] Smart command kontrolü yapılıyor...");
                if (_smartParser.IsSmartMailCommand(text))
                {
                    Debug.WriteLine("[LocalOutlookCommand] Smart mail command algılandı");
                    return await ExecuteSmartMailCommand(text, reader);
                }
                
                // 2. Yeni spesifik tam komut analizi - EN SPESİFİK KOMUTLAR ÖNCE
                Debug.WriteLine("[LocalOutlookCommand] Spesifik komut kontrolü yapılıyor...");
                
                if (text.Contains("okunmamış maillerimi göster") ||
                    text.Contains("okunmamış maillerini göster") ||
                    text.Contains("okunmamış mailleri göster") ||
                    text.Contains("okunmamış maillerimi oku") ||
                    text.Contains("okunmamış maillerini oku") ||
                    text.Contains("okunmamış mailleri oku"))
                {
                    Debug.WriteLine("[LocalOutlookCommand] 'okunmamış mail göster' komutu algılandı");
                    return await ShowUnreadMailsWithGrouping(reader);
                }
                else if (text.Contains("gönderilmiş mailleri göster") || 
                         text.Contains("gönderilmiş maillerini göster") ||
                         text.Contains("gönderilmiş maillerimi göster") ||
                         text.Contains("gönderilen mailleri göster") ||
                         text.Contains("gönderilen maillerini göster") ||
                         text.Contains("gönderilen maillerimi göster"))
                {
                    return await ShowSentMailsWithGrouping(reader);
                }
                else if (text.Contains("maillerimi göster") ||
                         text.Contains("maillerini göster") ||
                         text.Contains("mailleri göster"))
                {
                    return await ShowRecentMailsWithGrouping(reader);
                }
                else if (text.Contains("detaylı oku"))
                {
                    int mailIndex = ExtractMailIndexFromCommand(text, 1);
                    return await DetailedMailRead(reader, mailIndex);
                }
                else if (text.Contains("bugünkü toplantılarım neler") ||
                         text.Contains("bugünkü toplantıların neler") ||
                         text.Contains("bugün toplantı"))
                {
                    return await GetTodayMeetings(reader);
                }
                else if (text.Contains("bu haftaki toplantılarım neler") ||
                         text.Contains("bu hafta toplantı"))
                {
                    return await GetWeekMeetings(reader);
                }
                
                // Eski komutlarla uyumluluk için
                else if (text.Contains("mail") && text.Contains("özetle"))
                {
                    return await SmartMailSummary(reader);
                }
                else if (text.Contains("local") && text.Contains("test"))
                {
                    return await TestRealOutlookConnection(reader);
                }
                else if (text.Contains("cevap") && text.Contains("yaz"))
                {
                    return await HandleResponseWriting(reader, text);
                }
                else if (text.Contains("cevap") && text.Contains("öğren"))
                {
                    return await HandleResponseLearning(text);
                }
                else if (text.Contains("takvim") || text.Contains("toplantı") || text.Contains("randevu"))
                {
                    return await HandleCalendarCommands(reader, text);
                }
                else if (text.Contains("not") && (text.Contains("oluştur") || text.Contains("yaz") || text.Contains("ekle")))
                {
                    return await HandleNoteCommands(reader, text);
                }
                else if (text.Contains("görev") && (text.Contains("oluştur") || text.Contains("ekle") || text.Contains("listele")))
                {
                    return await HandleTaskCommands(reader, text);
                }

                Debug.WriteLine($"[LocalOutlookCommand] Hiçbir komut eşleşmedi: '{text}'");
                await TextToSpeechService.SpeakTextAsync("Gerçek Outlook komutu anlaşılamadı.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalOutlookCommand] ExecuteWithRealOutlook hatası: {ex.Message}");
                Debug.WriteLine($"[LocalOutlookCommand] Stack trace: {ex.StackTrace}");
                throw; // Exception'ı yukarı fırlat
            }
            finally
            {
                Debug.WriteLine("[LocalOutlookCommand] RealOutlookReader bağlantısı kapatılıyor...");
                reader.Disconnect();
            }
        }


        /// <summary>
        /// Gerçek Outlook bağlantısını test eder
        /// </summary>
        private async Task<bool> TestRealOutlookConnection(RealOutlookReader reader)
        {
            try
            {

                await TextToSpeechService.SpeakTextAsync("Gerçek Outlook bağlantısı test ediliyor...");

                var testEmails = await reader.GetRecentEmailsAsync(5);
                
                if (testEmails.Any())
                {
                    await TextToSpeechService.SpeakTextAsync($"Outlook başarılı! {testEmails.Count} e posta bulundu.");
                    TextToSpeechService.SendToOutput($"✅ Gerçek Outlook Test Başarılı");
                    TextToSpeechService.SendToOutput(CleanForOutput($"📧 {testEmails.Count} gerçek e posta erişilebilir"));
                    
                    foreach (var email in testEmails.Take(3))
                    {
                        TextToSpeechService.SendToOutput($"• {email.SenderName}: {email.Subject} ({email.AccountName})");
                    }
                }
                else
                {
                    await TextToSpeechService.SpeakTextAsync("Outlook'a bağlandı ama e posta bulunamadı.");
                    TextToSpeechService.SendToOutput($"⚠️ Gerçek Outlook Bağlandı - E Posta Yok");
                }

                return true;
            }
            catch (Exception ex)
            {
                await TextToSpeechService.SpeakTextAsync("Gerçek Outlook test başarısız.");
                TextToSpeechService.SendToOutput($"❌ Gerçek Outlook Test Başarısız: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Akıllı Mail Özeti - Kısa preview + öncelik
        /// </summary>
        private async Task<bool> SmartMailSummary(RealOutlookReader reader)
        {
            try
            {
                
                // Edge TTS'i zorla ve Emel sesini kullan
                TextToSpeechService.UseEdgeTTS = true;
                TextToSpeechService.CurrentEdgeVoice = "tr-TR-EmelNeural";
                await TextToSpeechService.SpeakTextAsync("E postalarınızı analiz ediyorum...");
                
                // OPTIMIZASYON: Paralel olarak recent ve unread mailleri al
                var recentTask = reader.GetRecentEmailsAsync(10);
                var unreadTask = reader.GetUnreadEmailsAsync(10);
                
                await Task.WhenAll(recentTask, unreadTask);
                
                var recentEmails = await recentTask;
                var unreadEmails = await unreadTask;
                
                if (!recentEmails.Any() && !unreadEmails.Any())
                {
                    // Edge TTS'i zorla ve Emel sesini kullan
                    TextToSpeechService.UseEdgeTTS = true;
                    TextToSpeechService.CurrentEdgeVoice = "tr-TR-EmelNeural";
                    await TextToSpeechService.SpeakTextAsync("E posta bulunamadı.");
                    return true;
                }
                
                // OPTIMIZASYON: Hızlı öncelik analizi
                var prioritizedEmails = AnalyzeEmailPriority(recentEmails, unreadEmails);
                
                // OPTIMIZASYON: Kısa sesli özet ver
                string voiceSummary = CreateVoiceSummary(prioritizedEmails);
                
                // OPTIMIZASYON: TTS başlat ama bekleme, detaylı listeyi hazırla
                // Edge TTS'i zorla ve Emel sesini kullan
                TextToSpeechService.UseEdgeTTS = true;
                TextToSpeechService.CurrentEdgeVoice = "tr-TR-EmelNeural";
                var ttsTask = TextToSpeechService.SpeakTextAsync(voiceSummary);
                
                
                // Paralel olarak detaylı listeyi hazırla
                string summary = CleanForOutput($"📧 {prioritizedEmails.Count} E Posta Özeti:\n");
                
                for (int i = 0; i < Math.Min(5, prioritizedEmails.Count); i++)
                {
                    var email = prioritizedEmails[i];
                    string priority = GetPriorityIcon(email);
                    string status = email.IsRead ? "✓" : "○";
                    
                    summary += $"{i + 1}. {priority} {status} {email.SenderName}: {TruncateSubject(email.Subject)}\n";
                }
                
                // TTS'in tamamlanmasını bekle
                await ttsTask;
                
                // TTS bittikten SONRA detaylı liste göster
                TextToSpeechService.SendToOutput(CleanForOutput("📧 Akıllı E Posta Özeti"));
                foreach (var line in summary.Split('\n'))
                {
                    if (!string.IsNullOrEmpty(line))
                        TextToSpeechService.SendToOutput(line);
                }
                
                TextToSpeechService.SendToOutput("\n💡 'detaylı oku [numara]' diyerek istediğiniz e postayı açabilirsiniz");
                
                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("E posta özeti hazırlanırken hata oluştu.");
                return false;
            }
        }
        
        /// <summary>
        /// Detaylı Mail Okuma ve Analiz - Email'i Outlook'ta açar
        /// </summary>
        private async Task<bool> DetailedMailRead(RealOutlookReader reader, int mailIndex)
        {
            try
            {
                // Son gösterilen listeden email'i al
                if (_lastEmailList == null || _lastEmailList.Count == 0)
                {
                    await TextToSpeechService.SpeakTextAsync("Henüz bir e-posta listesi gösterilmedi. Önce 'maillerimi göster' komutunu kullanın.");
                    return false;
                }
                
                if (mailIndex > _lastEmailList.Count || mailIndex < 1)
                {
                    await TextToSpeechService.SpeakTextAsync($"Geçersiz e-posta numarası. 1 ile {_lastEmailList.Count} arasında bir sayı söyleyin.");
                    return false;
                }
                
                var email = _lastEmailList[mailIndex - 1];
                
                // Son seçilen maili kaydet (cevap yazmak için)
                _lastSelectedEmail = email;
                
                // EntryID kullanarak Outlook'ta email'i aç
                if (!string.IsNullOrEmpty(email.EntryID))
                {
                    try
                    {
                        // Outlook'u aç ve e-postayı göster
                        Type outlookType = Type.GetTypeFromProgID("Outlook.Application");
                        if (outlookType != null)
                        {
                            dynamic outlookApp = Activator.CreateInstance(outlookType);
                            if (outlookApp != null)
                            {
                                dynamic nameSpace = outlookApp.GetNamespace("MAPI");
                                if (nameSpace != null)
                                {
                                    try
                                    {
                                        // EntryID ile mail'i bul ve göster
                                        dynamic mailItem = nameSpace.GetItemFromID(email.EntryID);
                                        if (mailItem != null)
                                        {
                                            mailItem.Display();
                                            await TextToSpeechService.SpeakTextAsync($"{mailIndex} numaralı e-posta Outlook'ta açıldı.");
                                            TextToSpeechService.SendToOutput($"\n📧 {email.Subject} - Outlook'ta açıldı");
                                            TextToSpeechService.SendToOutput("\n💬 'cevap yaz' diyerek yanıt verebilirsiniz");
                                            
                                            // COM nesnelerini temizle
                                            System.Runtime.InteropServices.Marshal.ReleaseComObject(mailItem);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        await TextToSpeechService.SpeakTextAsync("E-posta açılırken hata oluştu.");
                                        return false;
                                    }
                                    finally
                                    {
                                        if (nameSpace != null)
                                            System.Runtime.InteropServices.Marshal.ReleaseComObject(nameSpace);
                                        if (outlookApp != null)
                                            System.Runtime.InteropServices.Marshal.ReleaseComObject(outlookApp);
                                    }
                                }
                            }
                        }
                        else
                        {
                            await TextToSpeechService.SpeakTextAsync("Outlook bağlantısı kurulamadı.");
                            return false;
                        }
                    }
                    catch (Exception)
                    {
                        await TextToSpeechService.SpeakTextAsync("E-posta açılırken hata oluştu.");
                        return false;
                    }
                }
                else
                {
                    // EntryID yoksa eski yöntemi kullan (detaylı analiz)
                    await TextToSpeechService.SpeakTextAsync($"{mailIndex}. e-posta detaylı olarak okunuyor...");
                    
                    // Detaylı analiz
                    string detailedAnalysis = CreateDetailedEmailAnalysis(email);
                    
                    // Sesli özet
                    string voiceDetail = CreateVoiceDetailAnalysis(email);
                    await TextToSpeechService.SpeakTextAsync(voiceDetail);
                    
                    // Detaylı analiz output'a
                    TextToSpeechService.SendToOutput(CleanForOutput($"📧 DETAYLI E-POSTA ANALİZİ - #{mailIndex}"));
                    TextToSpeechService.SendToOutput("═══════════════════════════════════");
                    foreach (var line in detailedAnalysis.Split('\n'))
                    {
                        if (!string.IsNullOrEmpty(line))
                            TextToSpeechService.SendToOutput(line);
                    }
                    
                    TextToSpeechService.SendToOutput("\n💬 'cevap yaz' diyerek yanıt verebilirsiniz");
                }
                
                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("E-posta detaylı okunamadı.");
                return false;
            }
        }
        
        /// <summary>
        /// Mail öncelik analizi
        /// </summary>
        private List<RealOutlookReader.RealEmailInfo> AnalyzeEmailPriority(
            List<RealOutlookReader.RealEmailInfo> recent, 
            List<RealOutlookReader.RealEmailInfo> unread)
        {
            // ÇÖZÜM: E posta duplikasyonunu önle - ID ile distinct yap
            var allEmails = recent.Concat(unread)
                .GroupBy(e => e.Subject + "|" + e.SenderEmail + "|" + e.ReceivedTime.ToString("yyyyMMddHHmm"))
                .Select(g => g.First())
                .ToList();
            
            return allEmails.OrderByDescending(email => 
            {
                int score = 0;
                
                // Okunmamış e postalar öncelikli
                if (!email.IsRead) score += 50;
                
                // Önemli flagı
                if (email.Importance == "High") score += 30;
                
                // Ek dosya varsa
                if (email.HasAttachments) score += 20;
                
                // Acil kelimeler
                var urgentWords = new[] { "acil", "urgent", "asap", "hemen", "ivedi" };
                if (urgentWords.Any(word => 
                    email.Subject.ToLowerInvariant().Contains(word) || 
                    email.BodyPreview.ToLowerInvariant().Contains(word)))
                    score += 40;
                
                // Yeni e postalar (son 24 saat)
                if (email.ReceivedTime > DateTime.Now.AddDays(-1)) score += 10;
                
                return score;
            }).ToList();
        }
        
        /// <summary>
        /// HTML çıktısı için emoji'leri ve özel karakterleri temizler/dönüştürür
        /// </summary>
        private string CleanForOutput(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            return text
                // E posta prefix'lerini temizle (TTS için)
                .Replace("RE: ", "").Replace("Re: ", "")
                .Replace("FW: ", "").Replace("Fw: ", "")
                .Replace("Fwd: ", "").Replace("FWD: ", "")
                // E posta ve durum emojileri
                .Replace("📧", "E Posta:")
                .Replace("📩", "Gelen:")
                .Replace("📨", "Giden:")
                .Replace("🔴", "Okunmamış")
                .Replace("✅", "Okunmuş")
                .Replace("❌", "Hata")
                .Replace("⚠️", "Önemli")
                .Replace("📎", "Ekli")
                .Replace("⭐", "Yıldızlı")
                .Replace("ℹ️", "Bilgi")
                .Replace("💡", "İpucu:")
                // Takvim ve toplantı emojileri
                .Replace("📅", "Takvim:")
                .Replace("📊", "Grafik:")
                .Replace("📈", "Artış:")
                .Replace("📉", "Azalış:")
                .Replace("🎯", "Hedef:")
                .Replace("🚀", "Başlat:")
                .Replace("⚡", "Hızlı:")
                .Replace("🔥", "Acil:")
                .Replace("💰", "Para:")
                // Yön ve aksiyon emojileri - çoğunu kaldır
                .Replace("↩️", "")
                .Replace("↪️", "")
                .Replace("⬆️", "")
                .Replace("⬇️", "")
                .Replace("➡️", "")
                .Replace("⬅️", "")
                .Replace("🔄", "")
                .Replace("🔃", "")
                .Replace("↩", "")
                .Replace("⬅", "")
                .Replace("➡", "")
                .Replace("⬆", "")
                .Replace("⬇", "")
                // Diğer yaygın emojiler
                .Replace("👤", "Kişi:")
                .Replace("👥", "Grup:")
                .Replace("📍", "Konum:")
                .Replace("🕐", "Saat:")
                .Replace("📝", "Not:")
                .Replace("📋", "Liste:")
                .Replace("🔒", "Güvenli:")
                .Replace("🔓", "Açık:")
                .Trim();
        }

        /// <summary>
        /// Öncelik ikonunu belirler
        /// </summary>
        private string GetPriorityIcon(RealOutlookReader.RealEmailInfo email)
        {
            if (!email.IsRead && email.Importance == "High") return "⚠️";
            if (!email.IsRead) return "🔴";
            if (email.HasAttachments) return "📎";
            if (email.Importance == "High") return "⭐";
            return "ℹ️";
        }
        
        /// <summary>
        /// Konu başlığını kısaltır
        /// </summary>
        private string TruncateSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return "Konu yok";
            return subject.Length > 40 ? subject.Substring(0, 40) + "..." : subject;
        }
        
        /// <summary>
        /// Sesli özet oluşturur
        /// </summary>
        private string CreateVoiceSummary(List<RealOutlookReader.RealEmailInfo> emails)
        {
            if (!emails.Any()) return "E posta bulunamadı.";
            
            int unreadCount = emails.Count(e => !e.IsRead);
            int urgentCount = emails.Count(e => 
                e.Subject.ToLowerInvariant().Contains("acil") || 
                e.Importance == "High");
            
            string summary = $"{emails.Count} e postanız var. ";
            
            if (unreadCount > 0)
                summary += $"{unreadCount} tanesi okunmamış. ";
                
            if (urgentCount > 0)
                summary += $"{urgentCount} tanesi acil. ";
            
            // En önemli 3 e postayı sesli olarak söyle
            for (int i = 0; i < Math.Min(3, emails.Count); i++)
            {
                var email = emails[i];
                summary += $"{i + 1}. {TurkishGrammarHelper.CreateAblativePhrase(email.SenderName, email.Subject)}. ";
            }
            
            if (emails.Count > 3)
                summary += $"Ve {emails.Count - 3} e posta daha var. Detaylı okumak için numara söyleyin.";
            
            return summary;
        }
        
        /// <summary>
        /// Detaylı e posta analizi oluşturur
        /// </summary>
        private string CreateDetailedEmailAnalysis(RealOutlookReader.RealEmailInfo email)
        {
            var analysis = "";
            
            analysis += $"👤 GÖNDEREN: {email.SenderName}\n";
            if (!string.IsNullOrEmpty(email.SenderEmail))
                analysis += $"✉️  E-POSTA: {email.SenderEmail}\n";
            
            analysis += $"📅 TARİH: {email.ReceivedTime:dd.MM.yyyy HH:mm}\n";
            analysis += CleanForOutput($"📧 HESAP: {email.AccountName}\n");
            
            // Durum analizi
            string status = email.IsRead ? "Okunmuş" : "Okunmamış";
            analysis += $"👁️  DURUM: {status}\n";
            
            if (email.Importance != "Normal")
                analysis += $"⭐ ÖNCELİK: {email.Importance}\n";
                
            if (email.HasAttachments)
            {
                analysis += $"📎 EK DOSYALAR: {email.Attachments.Count} adet\n";
                foreach (var attachment in email.Attachments)
                {
                    analysis += $"   • {attachment.FileName} ({attachment.SizeFormatted}) - {attachment.FileType}\n";
                    analysis += $"     {attachment.ContentSummary}\n";
                }
            }
            
            analysis += $"\n📝 KONU: {email.Subject}\n";
            
            if (!string.IsNullOrEmpty(email.BodyPreview))
            {
                analysis += $"\n💬 ÖZET:\n{email.BodyPreview}\n";
            }
            
            // Akıllı analiz
            analysis += $"\n🎯 AKILLı ANALİZ:\n";
            analysis += AnalyzeEmailContent(email);
            
            return analysis;
        }
        
        /// <summary>
        /// Sesli detay analizi
        /// </summary>
        private string CreateVoiceDetailAnalysis(RealOutlookReader.RealEmailInfo email)
        {
            string voice;
            
            // Gönderilmiş e posta mı kontrol et
            if (email.IsSentMail)
            {
                voice = $"{TurkishGrammarHelper.CreateDativePhrase(email.RecipientName, "gönderilen e posta")}. ";
            }
            else
            {
                voice = $"{TurkishGrammarHelper.CreateAblativePhrase(email.SenderName, "gelen e posta")}. ";
            }
            
            voice += $"Konu: {email.Subject}. ";
            
            if (!email.IsRead)
                voice += "Henüz okunmamış. ";
                
            if (email.HasAttachments)
                voice += "Ek dosya var. ";
                
            if (email.Importance == "High")
                voice += "Yüksek öncelikli. ";
            
            // E posta içeriği özeti
            if (!string.IsNullOrEmpty(email.BodyPreview))
            {
                string shortPreview = email.BodyPreview.Length > 100 
                    ? email.BodyPreview.Substring(0, 100) + "..."
                    : email.BodyPreview;
                voice += $"İçerik özeti: {shortPreview}";
            }
            
            return voice;
        }
        
        /// <summary>
        /// E posta içeriği akıllı analizi
        /// </summary>
        private string AnalyzeEmailContent(RealOutlookReader.RealEmailInfo email)
        {
            var analysis = "";
            var content = (email.Subject + " " + email.BodyPreview).ToLowerInvariant();
            
            // Eylem analizi
            var actionWords = new[] { "onay", "approval", "imza", "sign", "tamamla", "complete", 
                                    "gönder", "send", "hazırla", "prepare", "toplantı", "meeting" };
            
            if (actionWords.Any(word => content.Contains(word)))
            {
                analysis += "• Eylem gerektirebilir\n";
            }
            
            // Tarih/deadline analizi
            var timeWords = new[] { "bugün", "yarın", "pazartesi", "salı", "çarşamba", "perşembe", 
                                  "cuma", "hafta", "ay", "deadline", "son tarih" };
            
            if (timeWords.Any(word => content.Contains(word)))
            {
                analysis += "• Zaman bilgisi içeriyor\n";
            }
            
            // Önemli kelimeler
            var importantWords = new[] { "acil", "urgent", "önemli", "important", "kritik", "critical" };
            
            if (importantWords.Any(word => content.Contains(word)))
            {
                analysis += "• Acil/önemli içerik\n";
            }
            
            // Pozitif/negatif ton
            var positiveWords = new[] { "teşekkür", "thank", "harika", "great", "mükemmel", "excellent" };
            var negativeWords = new[] { "sorun", "problem", "hata", "error", "geç", "late", "iptal", "cancel" };
            
            if (positiveWords.Any(word => content.Contains(word)))
                analysis += "• Pozitif ton\n";
            else if (negativeWords.Any(word => content.Contains(word)))
                analysis += "• Dikkat gereken içerik\n";
            
            if (string.IsNullOrEmpty(analysis))
                analysis = "• Standart bilgilendirme e postası\n";
                
            return analysis;
        }
        
        /// <summary>
        /// Cevap yazma işlemini yönetir
        /// </summary>
        private async Task<bool> HandleResponseWriting(RealOutlookReader reader, string command)
        {
            try
            {
                
                if (_lastSelectedEmail == null)
                {
                    await TextToSpeechService.SpeakTextAsync("Önce bir e posta seçin. 'detaylı oku' komutu ile e posta açabilirsiniz.");
                    return false;
                }
                
                await TextToSpeechService.SpeakTextAsync("Yanıtınızı söyleyin. Ben sizin tarzınıza uygun şekilde düzenleyeceğim.");
                
                // Kullanıcıdan input al (bu şimdilik simüle edelim)
                TextToSpeechService.SendToOutput("💬 CEVAP YAZMA MODU");
                TextToSpeechService.SendToOutput(CleanForOutput($"📧 Yanıtlanacak E Posta: {_lastSelectedEmail.SenderName} - {_lastSelectedEmail.Subject}"));
                TextToSpeechService.SendToOutput("═══════════════════════════════════");
                TextToSpeechService.SendToOutput("🎤 Yanıtınızı söyleyin...");
                TextToSpeechService.SendToOutput("💡 Örnek: 'konuyu değerlendiriyoruz pazartesi dönüş yapacağız'");
                
                // Kullanıcı stilini göster
                string styleInfo = _responseLearningService.GetUserStyleSummary();
                TextToSpeechService.SendToOutput("\n" + styleInfo);
                
                TextToSpeechService.SendToOutput("\n📝 Önceki yanıtlarınızdan öğrendiğim stil ile yanıt hazırlayacağım.");
                TextToSpeechService.SendToOutput("🔄 'cevap öğren [metniniz]' diyerek stilinizi geliştirebilirsiniz.");
                
                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Cevap yazma işleminde hata oluştu.");
                return false;
            }
        }
        
        /// <summary>
        /// Kullanıcı stilini öğrenme komutu
        /// </summary>
        private async Task<bool> HandleResponseLearning(string userInput)
        {
            try
            {
                
                // "cevap öğren" kısmını çıkar
                string cleanInput = userInput.Replace("cevap öğren", "").Trim();
                
                if (string.IsNullOrEmpty(cleanInput))
                {
                    await TextToSpeechService.SpeakTextAsync("Öğrenmem için bir metin örneği verin. Örneğin: 'cevap öğren konuyu inceleyeceğiz size dönüş yapacağız'");
                    return false;
                }
                
                // Stili öğren
                await _responseLearningService.LearnFromUserResponse(cleanInput, _lastSelectedEmail?.Subject ?? "");
                
                // Öğrenilen stili göster
                string styleInfo = _responseLearningService.GetUserStyleSummary();
                
                await TextToSpeechService.SpeakTextAsync("Yazım stilinizi öğrendim ve güncelledim.");
                
                TextToSpeechService.SendToOutput("✅ STİL ÖĞRENİLDİ");
                TextToSpeechService.SendToOutput("═══════════════════════════════════");
                TextToSpeechService.SendToOutput($"📝 Öğrenilen metin: {cleanInput}");
                TextToSpeechService.SendToOutput("\n" + styleInfo);
                
                // Örnek cevap üret
                if (_lastSelectedEmail != null)
                {
                    string suggestion = _responseLearningService.GenerateResponseSuggestion(
                        cleanInput, 
                        _lastSelectedEmail.SenderName, 
                        _lastSelectedEmail.Subject);
                        
                    TextToSpeechService.SendToOutput("\n💡 ÖNERİLEN YANIT:");
                    TextToSpeechService.SendToOutput("═══════════════════════════════════");
                    TextToSpeechService.SendToOutput(suggestion);
                }
                
                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Stil öğrenme işleminde hata oluştu.");
                return false;
            }
        }

        /// <summary>
        /// Komuttan mail index çıkarır
        /// </summary>
        private int ExtractMailIndexFromCommand(string command, int defaultIndex)
        {
            var words = command.Split(' ');
            foreach (var word in words)
            {
                if (int.TryParse(word, out int index))
                {
                    return Math.Max(1, Math.Min(index, 10)); // 1-10 arası
                }
            }
            return defaultIndex;
        }

        /// <summary>
        /// Komuttan sayı çıkarır
        /// </summary>
        private int ExtractCountFromCommand(string command, int defaultCount)
        {
            var words = command.Split(' ');
            foreach (var word in words)
            {
                if (int.TryParse(word, out int count))
                {
                    return Math.Min(count, 50); // Max 50 e posta
                }
            }
            return defaultCount;
        }

        /// <summary>
        /// Takvim/Toplantı komutlarını işler
        /// </summary>
        private async Task<bool> HandleCalendarCommands(RealOutlookReader reader, string text)
        {
            try
            {

                if (text.Contains("bugün") && text.Contains("toplantı"))
                {
                    return await GetTodayMeetings(reader);
                }
                else if (text.Contains("yarın") && text.Contains("toplantı"))
                {
                    return await GetTomorrowMeetings(reader);
                }
                else if (text.Contains("bu hafta") && text.Contains("toplantı"))
                {
                    return await GetWeekMeetings(reader);
                }
                else if (text.Contains("takvim") && text.Contains("listele"))
                {
                    return await GetCalendarSummary(reader);
                }
                else if (text.Contains("toplantı") && text.Contains("oluştur"))
                {
                    return await CreateMeeting(reader, text);
                }

                await TextToSpeechService.SpeakTextAsync("Takvim komutu anlaşılamadı. 'bugün toplantı', 'yarın toplantı', 'bu hafta toplantı' veya 'takvim listele' diyebilirsiniz.");
                return false;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Takvim işleminde hata oluştu.");
                return false;
            }
        }

        /// <summary>
        /// Not komutlarını işler
        /// </summary>
        private async Task<bool> HandleNoteCommands(RealOutlookReader reader, string text)
        {
            try
            {

                if (text.Contains("not oluştur") || text.Contains("not yaz") || text.Contains("not ekle"))
                {
                    string noteContent = ExtractNoteContent(text);
                    return await CreateNote(reader, noteContent);
                }
                else if (text.Contains("notlar") && text.Contains("listele"))
                {
                    return await ListRecentNotes(reader);
                }

                await TextToSpeechService.SpeakTextAsync("Not komutu anlaşılamadı. 'not oluştur [içerik]' veya 'notlar listele' diyebilirsiniz.");
                return false;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Not işleminde hata oluştu.");
                return false;
            }
        }

        /// <summary>
        /// Görev komutlarını işler
        /// </summary>
        private async Task<bool> HandleTaskCommands(RealOutlookReader reader, string text)
        {
            try
            {

                if (text.Contains("görev oluştur") || text.Contains("görev ekle"))
                {
                    string taskContent = ExtractTaskContent(text);
                    return await CreateTask(reader, taskContent);
                }
                else if (text.Contains("görev") && text.Contains("listele"))
                {
                    return await ListPendingTasks(reader);
                }

                await TextToSpeechService.SpeakTextAsync("Görev komutu anlaşılamadı. 'görev oluştur [açıklama]' veya 'görev listele' diyebilirsiniz.");
                return false;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Görev işleminde hata oluştu.");
                return false;
            }
        }

        /// <summary>
        /// Bugünkü toplantıları getirir
        /// </summary>
        private async Task<bool> GetTodayMeetings(RealOutlookReader reader)
        {
            try
            {

                await TextToSpeechService.SpeakTextAsync("Bugünkü toplantılar kontrol ediliyor...");

                // GERÇEK VERİ: RealOutlookReader'dan bugünkü etkinlikleri al
                var todayAppointments = await reader.GetTodayAppointmentsAsync();

                if (!todayAppointments.Any())
                {
                    await TextToSpeechService.SpeakTextAsync("Bugün hiç toplantınız yok.");
                    TextToSpeechService.SendToOutput("📅 Bugünkü Toplantılar");
                    TextToSpeechService.SendToOutput("✅ Bugün hiç toplantınız yok - özgür bir gün!");
                    return true;
                }                // Sesli özet - konum ve katılımcı bilgileri dahil
                string summary = $"Bugün {todayAppointments.Count} toplantınız var. ";
                for (int i = 0; i < Math.Min(3, todayAppointments.Count); i++)
                {
                    var apt = todayAppointments[i];
                    string timeInfo = apt.IsAllDay ? "Tüm gün" : $"{apt.StartTime:HH:mm}'de";
                    string locationInfo = string.IsNullOrEmpty(apt.Location) ? "" : $", {apt.Location}'da";
                    string attendeeInfo = apt.Attendees.Any() ? $", {apt.Attendees.Count} katılımcı ile" : "";
                    
                    summary += $"{timeInfo} {apt.Subject}{locationInfo}{attendeeInfo}. ";
                }

                if (todayAppointments.Count > 3)
                {
                    summary += $"Ve {todayAppointments.Count - 3} toplantı daha.";
                }

                await TextToSpeechService.SpeakTextAsync(summary);

                // Detaylı liste - StringBuilder ile topla ve tek seferde gönder
                var output = new System.Text.StringBuilder();
                output.AppendLine("📅 Bugünkü Toplantılar");
                
                foreach (var appointment in todayAppointments)
                {
                    string location = string.IsNullOrEmpty(appointment.Location) ? "Yer belirtilmemiş" : appointment.Location;
                    string timeRange = appointment.IsAllDay 
                        ? "Tüm gün" 
                        : $"{appointment.StartTime:HH:mm} - {appointment.EndTime:HH:mm}";
                    
                    output.AppendLine($"• {timeRange} - {appointment.Subject}");
                    if (!string.IsNullOrEmpty(appointment.Location))
                    {
                        output.AppendLine($"  📍 {appointment.Location}");
                    }
                    if (appointment.Attendees.Any())
                    {
                        output.AppendLine($"  👥 {appointment.Attendees.Count} katılımcı");
                    }
                }
                
                // Tek seferde gönder
                TextToSpeechService.SendToOutput(output.ToString().TrimEnd());

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Bugünkü toplantılar alınamadı.");
                return false;
            }
        }

        /// <summary>
        /// Yarınki toplantıları getirir
        /// </summary>
        private async Task<bool> GetTomorrowMeetings(RealOutlookReader reader)
        {
            try
            {

                await TextToSpeechService.SpeakTextAsync("Yarınki toplantılar kontrol ediliyor...");

                // GERÇEK VERİ: RealOutlookReader'dan yarınki etkinlikleri al
                var tomorrowAppointments = await reader.GetTomorrowAppointmentsAsync();

                if (!tomorrowAppointments.Any())
                {
                    await TextToSpeechService.SpeakTextAsync("Yarın hiç toplantınız yok.");
                    TextToSpeechService.SendToOutput("📅 Yarınki Toplantılar");
                    TextToSpeechService.SendToOutput("✅ Yarın hiç toplantınız yok - rahat bir gün!");
                    return true;
                }                // Sesli özet - konum ve katılımcı bilgileri dahil
                string summary = $"Yarın {tomorrowAppointments.Count} toplantınız var. ";
                for (int i = 0; i < Math.Min(3, tomorrowAppointments.Count); i++)
                {
                    var apt = tomorrowAppointments[i];
                    string timeInfo = apt.IsAllDay ? "Tüm gün" : $"{apt.StartTime:HH:mm}'de";
                    string locationInfo = string.IsNullOrEmpty(apt.Location) ? "" : $", {apt.Location}'da";
                    string attendeeInfo = apt.Attendees.Any() ? $", {apt.Attendees.Count} katılımcı ile" : "";
                    
                    summary += $"{timeInfo} {apt.Subject}{locationInfo}{attendeeInfo}. ";
                }

                if (tomorrowAppointments.Count > 3)
                {
                    summary += $"Ve {tomorrowAppointments.Count - 3} toplantı daha.";
                }

                await TextToSpeechService.SpeakTextAsync(summary);

                // Detaylı liste
                TextToSpeechService.SendToOutput("📅 Yarınki Toplantılar");
                foreach (var appointment in tomorrowAppointments)
                {
                    string timeRange = appointment.IsAllDay 
                        ? "Tüm gün" 
                        : $"{appointment.StartTime:HH:mm} - {appointment.EndTime:HH:mm}";
                    
                    TextToSpeechService.SendToOutput($"• {timeRange} - {appointment.Subject}");
                    if (!string.IsNullOrEmpty(appointment.Location))
                    {
                        TextToSpeechService.SendToOutput($"  📍 {appointment.Location}");
                    }
                    if (appointment.Attendees.Any())
                    {
                        TextToSpeechService.SendToOutput($"  👥 {appointment.Attendees.Count} katılımcı");
                    }
                }

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Yarınki toplantılar alınamadı.");
                return false;
            }
        }

        /// <summary>
        /// Bu haftaki toplantıları getirir
        /// </summary>
        private async Task<bool> GetWeekMeetings(RealOutlookReader reader)
        {
            try
            {

                await TextToSpeechService.SpeakTextAsync("Bu haftaki toplantılar kontrol ediliyor...");

                // GERÇEK VERİ: RealOutlookReader'dan bu haftaki etkinlikleri al
                var weekAppointments = await reader.GetWeekAppointmentsAsync();

                if (!weekAppointments.Any())
                {
                    await TextToSpeechService.SpeakTextAsync("Bu hafta hiç toplantınız yok.");
                    TextToSpeechService.SendToOutput("📅 Bu Haftaki Toplantılar");
                    TextToSpeechService.SendToOutput("✅ Bu hafta hiç toplantınız yok - rahat bir hafta!");
                    return true;
                }

                // İstatistik hesaplama
                var todayCount = weekAppointments.Count(a => a.StartTime.Date == DateTime.Today);
                var tomorrowCount = weekAppointments.Count(a => a.StartTime.Date == DateTime.Today.AddDays(1));
                var recurringCount = weekAppointments.Count(a => a.IsRecurring);
                var longestMeeting = weekAppointments.OrderByDescending(a => (a.EndTime - a.StartTime).TotalHours).FirstOrDefault();

                // Sesli özet
                string summary = $"Bu hafta toplam {weekAppointments.Count} toplantınız var. ";
                if (todayCount > 0) summary += $"Bugün {todayCount}, ";
                if (tomorrowCount > 0) summary += $"yarın {tomorrowCount} toplantı. ";
                if (recurringCount > 0) summary += $"{recurringCount} tanesi düzenli toplantı. ";

                await TextToSpeechService.SpeakTextAsync(summary);

                // Detaylı özet - StringBuilder ile topla ve tek seferde gönder
                var output = new System.Text.StringBuilder();
                
                output.AppendLine("📅 Bu Haftaki Toplantılar Özeti");
                output.AppendLine("════════════════════════════════");
                output.AppendLine($"📊 Toplam: {weekAppointments.Count} toplantı");
                output.AppendLine($"📅 Bugün: {todayCount} toplantı");
                output.AppendLine($"📅 Yarın: {tomorrowCount} toplantı");
                output.AppendLine($"🔄 Düzenli: {recurringCount} toplantı");
                
                if (longestMeeting != null)
                {
                    output.AppendLine($"⏰ En uzun: {longestMeeting.Subject} ({longestMeeting.Duration})");
                }

                // Günlük dağılım
                output.AppendLine("\n📊 Günlük Dağılım:");
                var dayGroups = weekAppointments.GroupBy(a => a.StartTime.Date)
                    .OrderBy(g => g.Key)
                    .Take(7);

                foreach (var dayGroup in dayGroups)
                {
                    string dayName = dayGroup.Key.ToString("dddd", new System.Globalization.CultureInfo("tr-TR"));
                    output.AppendLine($"• {dayName}: {dayGroup.Count()} toplantı");
                }
                
                // Tek seferde gönder
                TextToSpeechService.SendToOutput(output.ToString().TrimEnd());

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Bu haftaki toplantılar alınamadı.");
                return false;
            }
        }

        /// <summary>
        /// Takvim özetini getirir
        /// </summary>
        private async Task<bool> GetCalendarSummary(RealOutlookReader reader)
        {
            try
            {

                await TextToSpeechService.SpeakTextAsync("Takvim özeti hazırlanıyor...");

                // Placeholder
                await TextToSpeechService.SpeakTextAsync("Takvim özetiniz: Bu hafta 8 toplantı, gelecek hafta 6 toplantı planlanmış. En yoğun gün Çarşamba.");

                TextToSpeechService.SendToOutput("📅 Takvim Özeti");
                TextToSpeechService.SendToOutput("════════════════════");
                TextToSpeechService.SendToOutput("📊 Bu Hafta: 8 toplantı");
                TextToSpeechService.SendToOutput("📊 Gelecek Hafta: 6 toplantı");
                TextToSpeechService.SendToOutput("🔥 En Yoğun Gün: Çarşamba (4 toplantı)");
                TextToSpeechService.SendToOutput("⏰ Ortalama Süre: 1.5 saat");

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Takvim özeti alınamadı.");
                return false;
            }
        }

        /// <summary>
        /// Toplantı oluşturur
        /// </summary>
        private async Task<bool> CreateMeeting(RealOutlookReader reader, string text)
        {
            try
            {

                await TextToSpeechService.SpeakTextAsync("Toplantı oluşturma moduna geçiyorum. Toplantı detaylarını söyleyin.");

                TextToSpeechService.SendToOutput("📝 Toplantı Oluşturma");
                TextToSpeechService.SendToOutput("════════════════════");
                TextToSpeechService.SendToOutput("🎤 Toplantı detaylarını söyleyin:");
                TextToSpeechService.SendToOutput("• Konu");
                TextToSpeechService.SendToOutput("• Tarih ve saat");
                TextToSpeechService.SendToOutput("• Katılımcılar");
                TextToSpeechService.SendToOutput("• Süre");

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Toplantı oluşturulamadı.");
                return false;
            }
        }

        /// <summary>
        /// Not oluşturur
        /// </summary>
        private async Task<bool> CreateNote(RealOutlookReader reader, string content)
        {
            try
            {

                if (string.IsNullOrEmpty(content))
                {
                    await TextToSpeechService.SpeakTextAsync("Not içeriği belirtilmedi. 'not oluştur' komutu ile birlikte not içeriğini söyleyin.");
                    return false;
                }

                await TextToSpeechService.SpeakTextAsync($"'{content}' içeriği ile not oluşturuldu.");

                TextToSpeechService.SendToOutput("📝 Not Oluşturuldu");
                TextToSpeechService.SendToOutput("════════════════");
                TextToSpeechService.SendToOutput($"📄 İçerik: {content}");
                TextToSpeechService.SendToOutput($"🕐 Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}");

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Not oluşturulamadı.");
                return false;
            }
        }

        /// <summary>
        /// Son notları listeler
        /// </summary>
        private async Task<bool> ListRecentNotes(RealOutlookReader reader)
        {
            try
            {

                await TextToSpeechService.SpeakTextAsync("Son notlarınız kontrol ediliyor...");

                // Placeholder
                await TextToSpeechService.SpeakTextAsync("Son 5 notunuz: Proje notları, toplantı özeti, yapılacaklar listesi, fikirler ve bug raporları.");

                TextToSpeechService.SendToOutput("📝 Son Notlar");
                TextToSpeechService.SendToOutput("═══════════");
                TextToSpeechService.SendToOutput("1. 📋 Proje Notları (Bugün 14:30)");
                TextToSpeechService.SendToOutput("2. 📊 Toplantı Özeti (Bugün 11:15)");
                TextToSpeechService.SendToOutput("3. ✅ Yapılacaklar Listesi (Dün 16:45)");
                TextToSpeechService.SendToOutput("4. 💡 Fikirler (Dün 09:20)");
                TextToSpeechService.SendToOutput("5. 🐛 Bug Raporları (2 gün önce)");

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Notlar listelenemedi.");
                return false;
            }
        }

        /// <summary>
        /// Görev oluşturur
        /// </summary>
        private async Task<bool> CreateTask(RealOutlookReader reader, string content)
        {
            try
            {

                if (string.IsNullOrEmpty(content))
                {
                    await TextToSpeechService.SpeakTextAsync("Görev açıklaması belirtilmedi. 'görev oluştur' komutu ile birlikte görev açıklamasını söyleyin.");
                    return false;
                }

                await TextToSpeechService.SpeakTextAsync($"'{content}' görev listesine eklendi.");

                TextToSpeechService.SendToOutput("✅ Görev Oluşturuldu");
                TextToSpeechService.SendToOutput("══════════════════");
                TextToSpeechService.SendToOutput($"📋 Görev: {content}");
                TextToSpeechService.SendToOutput($"🕐 Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}");
                TextToSpeechService.SendToOutput("📅 Durum: Beklemede");
                TextToSpeechService.SendToOutput("⚡ Öncelik: Normal");

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Görev oluşturulamadı.");
                return false;
            }
        }

        /// <summary>
        /// Bekleyen görevleri listeler
        /// </summary>
        private async Task<bool> ListPendingTasks(RealOutlookReader reader)
        {
            try
            {

                await TextToSpeechService.SpeakTextAsync("Bekleyen görevleriniz kontrol ediliyor...");

                // Placeholder
                await TextToSpeechService.SpeakTextAsync("4 bekleyen göreviniz var. 2 tanesi bugün, 1 tanesi yarın, 1 tanesi gelecek hafta için.");

                TextToSpeechService.SendToOutput("✅ Bekleyen Görevler");
                TextToSpeechService.SendToOutput("═══════════════════");
                TextToSpeechService.SendToOutput("🔥 Bugün (2 görev):");
                TextToSpeechService.SendToOutput("   • Proje raporunu tamamla");
                TextToSpeechService.SendToOutput("   • Client'a e posta gönder");
                TextToSpeechService.SendToOutput("📅 Yarın (1 görev):");
                TextToSpeechService.SendToOutput("   • Design review hazırlığı");
                TextToSpeechService.SendToOutput("📆 Gelecek Hafta (1 görev):");
                TextToSpeechService.SendToOutput("   • Quarterly planning toplantısı");

                return true;
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Görevler listelenemedi.");
                return false;
            }
        }

        /// <summary>
        /// Metinden not içeriğini çıkarır
        /// </summary>
        private string ExtractNoteContent(string text)
        {
            var words = text.Split(' ');
            var startIndex = -1;

            for (int i = 0; i < words.Length - 1; i++)
            {
                if ((words[i] == "not" && (words[i + 1] == "oluştur" || words[i + 1] == "yaz" || words[i + 1] == "ekle")))
                {
                    startIndex = i + 2;
                    break;
                }
            }

            if (startIndex >= 0 && startIndex < words.Length)
            {
                return string.Join(" ", words.Skip(startIndex));
            }

            return string.Empty;
        }

        /// <summary>
        /// Metinden görev içeriğini çıkarır
        /// </summary>
        private string ExtractTaskContent(string text)
        {
            var words = text.Split(' ');
            var startIndex = -1;

            for (int i = 0; i < words.Length - 1; i++)
            {
                if ((words[i] == "görev" && (words[i + 1] == "oluştur" || words[i + 1] == "ekle")))
                {
                    startIndex = i + 2;
                    break;
                }
            }

            if (startIndex >= 0 && startIndex < words.Length)
            {
                return string.Join(" ", words.Skip(startIndex));
            }

            return string.Empty;
        }

        /// <summary>
        /// Okunmamış mailleri gruplandırmalı TTS ile gösterir
        /// </summary>
        private async Task<bool> ShowUnreadMailsWithGrouping(RealOutlookReader reader)
        {
            Debug.WriteLine("[LocalOutlookCommand] ShowUnreadMailsWithGrouping başladı");
            try
            {
                Debug.WriteLine("[LocalOutlookCommand] Okunmamış mailler alınıyor...");
                var unreadEmails = await reader.GetUnreadEmailsAsync(20);
                Debug.WriteLine($"[LocalOutlookCommand] {unreadEmails.Count} okunmamış mail bulundu");
                
                if (!unreadEmails.Any())
                {
                    await TextToSpeechService.SpeakTextAsync("Okunmamış e postanız yok.");
                    TextToSpeechService.SendToOutput(CleanForOutput("📧 Okunmamış E Posta Yok"));
                    return true;
                }
                
                // CreateSmartMailSummary metodunu kullan
                string voiceSummary = CreateSmartMailSummary(unreadEmails, "okunmamış e posta");
                
                // Sesli feedback - sadece özet
                await TextToSpeechService.SpeakTextAsync(voiceSummary);
                
                // Output area'ya liste formatında göster
                var output = new System.Text.StringBuilder();
                output.AppendLine(CleanForOutput($"📧 Okunmamış E Postalar ({unreadEmails.Count} adet)"));
                output.AppendLine("═══════════════════════════════════");
                
                for (int i = 0; i < Math.Min(10, unreadEmails.Count); i++)
                {
                    var email = unreadEmails[i];
                    string priority = email.Importance == "High" ? "⭐ " : "";
                    string attachment = email.HasAttachments ? " 📎" : "";
                    output.AppendLine($"{i + 1}. {priority}{email.SenderName}: {TruncateSubject(email.Subject)}{attachment}");
                }
                
                if (unreadEmails.Count > 10)
                {
                    output.AppendLine($"\n... ve {unreadEmails.Count - 10} e posta daha");
                }
                
                output.AppendLine("\n💡 'detaylı oku [numara]' veya 'kişi adından gelen e postayı oku' diyerek okuyabilirsiniz.");
                
                // Tek seferde output'a gönder
                TextToSpeechService.SendToOutput(output.ToString());
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalOutlookCommand] ShowUnreadMailsWithGrouping hatası: {ex.Message}");
                Debug.WriteLine($"[LocalOutlookCommand] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Son mailleri gruplandırmalı TTS ile gösterir
        /// </summary>
        private async Task<bool> ShowRecentMailsWithGrouping(RealOutlookReader reader)
        {
            try
            {
                var recentEmails = await reader.GetRecentEmailsAsync(15);
                
                if (!recentEmails.Any())
                {
                    await TextToSpeechService.SpeakTextAsync("E posta bulunamadı.");
                    return true;
                }
                
                string groupedSummary = CreateSmartMailSummary(recentEmails, "e posta");
                await TextToSpeechService.SpeakTextAsync(groupedSummary);
                
                // Detaylı listeyi ekranda göster
                DisplayMailList(recentEmails, "📧 Son E Postalar");
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gönderilmiş mailleri gruplandırmalı TTS ile gösterir
        /// </summary>
        private async Task<bool> ShowSentMailsWithGrouping(RealOutlookReader reader)
        {
            try
            {
                // Gönderilmiş mailler için özel metod kullan
                var sentEmails = await reader.GetSentEmailsAsync(10);
                
                if (!sentEmails.Any())
                {
                    await TextToSpeechService.SpeakTextAsync("Gönderilmiş e posta bulunamadı.");
                    return true;
                }
                
                string groupedSummary = CreateSmartMailSummary(sentEmails, "gönderilmiş e posta");
                await TextToSpeechService.SpeakTextAsync(groupedSummary);
                
                // Detaylı listeyi ekranda göster
                DisplayMailList(sentEmails, "📧 Gönderilmiş E Postalar");
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        /// <summary>
        /// Akıllı e posta özeti oluşturur - gönderene/alıcıya göre gruplar
        /// </summary>
        private string CreateSmartMailSummary(List<RealOutlookReader.RealEmailInfo> emails, string mailType)
        {
            if (!emails.Any())
                return $"{mailType} bulunamadı.";
                
            // Gönderilmiş e postalar için farklı format kullan
            bool isSentMail = mailType.Contains("gönderilmiş");
            
            // Gönderene/alıcıya göre grupla
            var groupedBySender = emails
                .GroupBy(e => isSentMail ? e.RecipientName : e.SenderName)
                .OrderByDescending(g => g.Count())
                .ToList();
            
            var summary = $"{emails.Count} {mailType}nız var. ";
            var parts = new List<string>();
            
            // Her zaman maksimum 5 gönderici/alıcı göster
            int maxGroups = Math.Min(5, groupedBySender.Count);
            int currentGroup = 0;
            
            foreach (var group in groupedBySender)
            {
                if (group.Count() == 1)
                {
                    // Tek e posta
                    if (isSentMail)
                    {
                        parts.Add($"{TurkishGrammarHelper.CreateDativePhrase(group.Key, $"1 e posta: {TruncateSubject(CleanForOutput(group.First().Subject))}")}");
                    }
                    else
                    {
                        parts.Add($"{TurkishGrammarHelper.CreateAblativePhrase(group.Key, $"1 e posta: {TruncateSubject(CleanForOutput(group.First().Subject))}")}");
                    }
                }
                else
                {
                    // Çoklu e posta - sadece 2 konu göster
                    var subjects = group.Take(2).Select(e => TruncateSubject(CleanForOutput(e.Subject))).ToList();
                    if (group.Count() > 2)
                    {
                        if (isSentMail)
                        {
                            parts.Add($"{TurkishGrammarHelper.CreateDativePhrase(group.Key, $"{group.Count()} adet e posta: {string.Join(", ", subjects)} ve {group.Count() - 2} e posta daha")}");
                        }
                        else
                        {
                            parts.Add($"{TurkishGrammarHelper.CreateAblativePhrase(group.Key, $"{group.Count()} adet e posta: {string.Join(", ", subjects)} ve {group.Count() - 2} e posta daha")}");
                        }
                    }
                    else
                    {
                        if (isSentMail)
                        {
                            parts.Add($"{TurkishGrammarHelper.CreateDativePhrase(group.Key, $"{group.Count()} adet e posta: {string.Join(", ", subjects)}")}");
                        }
                        else
                        {
                            parts.Add($"{TurkishGrammarHelper.CreateAblativePhrase(group.Key, $"{group.Count()} adet e posta: {string.Join(", ", subjects)}")}");
                        }
                    }
                }
                
                currentGroup++;
                if (currentGroup >= maxGroups)
                {
                    // Kalan grupları say
                    int remainingGroups = groupedBySender.Count - maxGroups;
                    if (remainingGroups > 0)
                    {
                        int remainingEmails = groupedBySender.Skip(maxGroups).Sum(g => g.Count());
                        parts.Add($"ve {remainingGroups} kişiden {remainingEmails} e posta daha");
                    }
                    break;
                }
            }
            
            return summary + string.Join(". ", parts) + ".";
        }

        /// <summary>
        /// E posta listesini ekranda gösterir
        /// </summary>
        private async void DisplayMailList(List<RealOutlookReader.RealEmailInfo> emails, string title)
        {
            // Email listesini sakla
            _lastEmailList = emails.Take(10).ToList();
            
            // WebViewManager'ı al
            var app = Microsoft.UI.Xaml.Application.Current as App;
            var mainWindow = app?.MainWindow as MainWindow;
            var webViewManager = mainWindow?.WebViewManager;
            
            if (webViewManager == null)
            {
                // WebViewManager yoksa eski yöntemi kullan
                TextToSpeechService.SendToOutput(CleanForOutput(title));
                for (int i = 0; i < emails.Count && i < 10; i++)
                {
                    var email = emails[i];
                    string readStatus = email.IsRead ? "✅" : "🔴";
                    string displayName = email.IsSentMail ? email.RecipientName : email.SenderName;
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = email.IsSentMail ? "Alıcı bilinmiyor" : "Gönderen bilinmiyor";
                    }
                    TextToSpeechService.SendToOutput(CleanForOutput($"{i + 1}. {readStatus} {displayName}: {email.Subject}"));
                }
                return;
            }
            
            // Başlığı göster
            await webViewManager.AppendOutput($"<div style='font-weight: bold; margin-bottom: 10px;'>{title}</div>");
            
            // Her email'i tıklanabilir HTML olarak göster
            for (int i = 0; i < emails.Count && i < 10; i++)
            {
                var email = emails[i];
                string readStatus = email.IsRead ? "✅" : "🔴";
                
                // Gönderilmiş e postalar için alıcı adını, diğerleri için gönderen adını kullan
                string displayName = email.IsSentMail ? email.RecipientName : email.SenderName;
                
                // Eğer displayName boşsa fallback kullan
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = email.IsSentMail ? "Alıcı bilinmiyor" : "Gönderen bilinmiyor";
                }
                
                // EntryID'yi JavaScript'e güvenli hale getir
                string safeEntryId = System.Web.HttpUtility.HtmlEncode(email.EntryID);
                
                // Tıklanabilir email HTML'i oluştur
                string emailHtml = $@"
                    <div style='margin: 5px 0; padding: 8px; border: 1px solid #e0e0e0; border-radius: 4px; cursor: pointer; transition: background-color 0.2s;' 
                         onclick='window.chrome.webview.postMessage(JSON.stringify({{action: ""openEmail"", entryId: ""{safeEntryId}""}}));' 
                         onmouseover='this.style.backgroundColor=""#f5f5f5"";' 
                         onmouseout='this.style.backgroundColor=""white"";'>
                        <span style='font-size: 16px;'>{i + 1}. {readStatus}</span>
                        <strong>{System.Web.HttpUtility.HtmlEncode(displayName)}:</strong> 
                        {System.Web.HttpUtility.HtmlEncode(email.Subject)}
                    </div>";
                
                await webViewManager.AppendOutput(emailHtml);
            }
            
            if (emails.Count > 0)
            {
                await webViewManager.AppendOutput($"<div style='margin-top: 10px; color: #666;'>💡 E-postaya tıklayarak Outlook'ta açabilirsiniz</div>");
            }
        }

        /// <summary>
        /// Tarihten gün adını alır
        /// </summary>
        private string GetDayName(DateTime date)
        {
            return date.DayOfWeek switch
            {
                DayOfWeek.Monday => "Pazartesi",
                DayOfWeek.Tuesday => "Salı", 
                DayOfWeek.Wednesday => "Çarşamba",
                DayOfWeek.Thursday => "Perşembe",
                DayOfWeek.Friday => "Cuma",
                DayOfWeek.Saturday => "Cumartesi",
                DayOfWeek.Sunday => "Pazar",
                _ => date.ToString("dd.MM")
            };
        }

        /// <summary>
        /// Hızlı okunmamış mail sayısını döndürür (TTS olmadan)
        /// </summary>
        public async Task<int> GetUnreadCountAsync()
        {
            try
            {
                var reader = new RealOutlookReader();
                if (!await reader.ConnectAsync())
                    return -1;
                    
                try
                {
                    var unreadEmails = await reader.GetUnreadEmailsAsync(100);
                    return unreadEmails.Count;
                }
                finally
                {
                    reader.Disconnect();
                }
            }
            catch
            {
                return -1;
            }
        }
        
        /// <summary>
        /// Hızlı bugünkü toplantı sayısını döndürür (TTS olmadan)
        /// </summary>
        public async Task<int> GetTodayMeetingCountAsync()
        {
            try
            {
                var reader = new RealOutlookReader();
                if (!await reader.ConnectAsync())
                    return -1;
                    
                try
                {
                    var meetings = await reader.GetTodayAppointmentsAsync();
                    return meetings.Count;
                }
                finally
                {
                    reader.Disconnect();
                }
            }
            catch
            {
                return -1;
            }
        }

        public bool CanHandle(string text)
        {
            text = text.ToLowerInvariant();
            
            // 1. Önce smart e posta command kontrolü
            if (_smartParser.IsSmartMailCommand(text))
            {
                return true;
            }
            
            // 2. Ana komutlar (fuzzy matching için)
            var coreCommands = new[] {
                "maillerimi göster",
                "toplantılarım neler",
                "detaylı oku",
                "gönderilmiş mailleri göster"
            };
            
            // Bilinen varyasyonlar (exact matching için)
            var variations = new[] {
                "okunmamış maillerimi göster",
                "okunmamış maillerini göster",
                "okunmamış maillerimi oku",
                "okunmamış maillerini oku",
                "okunmamış mailleri oku", 
                "gönderilmiş maillerimi göster",
                "gönderilmiş maillerini göster",
                "gönderilmiş mailleri göster",
                "gönderilen maillerimi göster",
                "gönderilen maillerini göster",
                "gönderilen mailleri göster",
                "maillerini göster",
                "mailleri göster",
                "maillerimizi göster",
                "bugünkü toplantılarım neler",
                "bugünkü toplantıların neler",
                "toplantıları göster",
                "toplantılarınız neler",
                "bu haftaki toplantılarım neler",
                "yarınki toplantılarım neler"
            };
            
            // 1. Önce variations'da exact match ara
            foreach (var variation in variations)
            {
                if (text.Contains(variation))
                {
                    return true;
                }
            }
            
            // 2. Core commands'da fuzzy match ara (daha katı threshold)
            // Ancak özel kelimeler varsa fuzzy matching yapmayız
            if (!text.Contains("gönderilmiş") && !text.Contains("gönderilen") && !text.Contains("okunmamış"))
            {
                foreach (var cmd in coreCommands)
                {
                    if (FuzzyMatchCommand(text, cmd, 0.8)) // Threshold artırıldı: 0.7 -> 0.8
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Smart e posta command'ları işler
        /// Örnek: "serkan geziciye gönderdiğim son e postayı oku"
        /// </summary>
        private async Task<bool> ExecuteSmartMailCommand(string text, RealOutlookReader reader)
        {
            try
            {
                // Command'ı parse et
                var parsedCommand = _smartParser.ParseCommand(text);
                
                if (!parsedCommand.IsValid)
                {
                    await TextToSpeechService.SpeakTextAsync("Komut anlaşılamadı. Lütfen tekrar deneyin.");
                    return false;
                }
                
                // E posta havuzu seçimi - performans optimizasyonu
                var allMails = new List<RealOutlookReader.RealEmailInfo>();
                
                // 1. ÖNCE: Okunmamış e posta araması - sadece unread pool'da ara
                if (parsedCommand.IsUnread == true)
                {
                    var unreadMails = await reader.GetUnreadEmailsAsync(100);
                    allMails = unreadMails;
                }
                // 2. Gönderilmiş e posta araması - sadece sent pool'da ara
                else if (parsedCommand.IsSentMail == true)
                {
                    var sentMails = await reader.GetSentEmailsAsync(100);
                    allMails = sentMails;
                }
                // 3. Gelen e posta araması - sadece received pool'da ara
                else if (parsedCommand.IsSentMail == false)
                {
                    var receivedMails = await reader.GetRecentEmailsAsync(100);
                    allMails = receivedMails.Where(m => !m.IsSentMail).ToList();
                }
                // 4. Genel arama - yön belirtilmemişse her iki pool'da ara
                else
                {
                    var sentMails = await reader.GetSentEmailsAsync(50);
                    var receivedMails = await reader.GetRecentEmailsAsync(50);
                    allMails = sentMails.Concat(receivedMails.Where(m => !m.IsSentMail)).ToList();
                }
                
                // Filtreleme işlemi
                var filteredMails = _smartFilter.FilterMails(allMails, parsedCommand);
                
                if (!filteredMails.Any())
                {
                    var summary = _smartFilter.GenerateSearchSummary(filteredMails, parsedCommand);
                    await TextToSpeechService.SpeakTextAsync(summary);
                    return true;
                }
                
                // Aksiyona göre işlem yap
                // Eğer okunmamış e posta araması ise her zaman liste göster
                if (parsedCommand.IsUnread == true && string.IsNullOrEmpty(parsedCommand.PersonName) && 
                    string.IsNullOrEmpty(parsedCommand.DomainName) && string.IsNullOrEmpty(parsedCommand.Subject))
                {
                    // Sadece "okunmamış e postalarımı göster" gibi genel bir arama
                    return await ExecuteSmartMailList(filteredMails, parsedCommand);
                }
                
                switch (parsedCommand.Action.ToLowerInvariant())
                {
                    case "oku":
                        return await ExecuteSmartMailRead(filteredMails, parsedCommand);
                    
                    case "göster":
                    case "listele":
                    case "bul":
                        return await ExecuteSmartMailList(filteredMails, parsedCommand);
                    
                    default:
                        // Default: eğer spesifik bir arama ise oku, değilse listele
                        if (!string.IsNullOrEmpty(parsedCommand.PersonName) || 
                            !string.IsNullOrEmpty(parsedCommand.DomainName) || 
                            !string.IsNullOrEmpty(parsedCommand.Subject))
                        {
                            return await ExecuteSmartMailRead(filteredMails, parsedCommand);
                        }
                        else
                        {
                            return await ExecuteSmartMailList(filteredMails, parsedCommand);
                        }
                }
            }
            catch (Exception)
            {
                await TextToSpeechService.SpeakTextAsync("Akıllı e posta işleminde hata oluştu.");
                return false;
            }
        }

        /// <summary>
        /// Filtrelenmiş e postalardan ilkini detaylı okur
        /// </summary>
        private async Task<bool> ExecuteSmartMailRead(List<RealOutlookReader.RealEmailInfo> mails, 
            SmartMailCommandParser.ParsedMailCommand command)
        {
            try
            {
                var targetMail = mails.First();
                _lastSelectedEmail = targetMail;
                
                // Özet bilgi
                var summary = _smartFilter.GenerateSearchSummary(mails, command);
                await TextToSpeechService.SpeakTextAsync(summary + " İlk e posta detaylı okunuyor.");
                
                // Detaylı analiz
                string detailedAnalysis = CreateDetailedEmailAnalysis(targetMail);
                string voiceDetail = CreateVoiceDetailAnalysis(targetMail);
                
                await TextToSpeechService.SpeakTextAsync(voiceDetail);
                
                // HTML çıktı
                TextToSpeechService.SendToOutput(CleanForOutput($"🔍 AKILLI E POSTA ARAMA SONUCU"));
                TextToSpeechService.SendToOutput("═══════════════════════════════════");
                TextToSpeechService.SendToOutput(CleanForOutput($"📋 Arama: {command.OriginalCommand}"));
                TextToSpeechService.SendToOutput(CleanForOutput($"✅ Sonuç: {summary}"));
                TextToSpeechService.SendToOutput("─────────────────────────────────");
                
                foreach (var line in detailedAnalysis.Split('\n'))
                {
                    if (!string.IsNullOrEmpty(line))
                        TextToSpeechService.SendToOutput(line);
                }
                
                if (mails.Count > 1)
                {
                    TextToSpeechService.SendToOutput($"\n💡 {mails.Count - 1} ek e posta daha bulundu. 'smart e posta listele' diyerek tümünü görebilirsiniz.");
                }
                
                TextToSpeechService.SendToOutput("\n💬 'cevap yaz' diyerek yanıt verebilirsiniz");
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Filtrelenmiş e postaları listeler
        /// </summary>
        private async Task<bool> ExecuteSmartMailList(List<RealOutlookReader.RealEmailInfo> mails, 
            SmartMailCommandParser.ParsedMailCommand command)
        {
            try
            {
                // Okunmamış e postalar için CreateSmartMailSummary kullan
                string summary;
                if (command.IsUnread == true && string.IsNullOrEmpty(command.PersonName) && 
                    string.IsNullOrEmpty(command.DomainName) && string.IsNullOrEmpty(command.Subject))
                {
                    summary = CreateSmartMailSummary(mails, "okunmamış e posta");
                }
                else
                {
                    summary = _smartFilter.GenerateSearchSummary(mails, command);
                }
                
                await TextToSpeechService.SpeakTextAsync(summary);
                
                // İlk 10 e postayı göster
                var mailsToShow = mails.Take(10).ToList();
                DisplayMailList(mailsToShow, $"🔍 Smart Search: {command.OriginalCommand}");
                
                if (mails.Count > 10)
                {
                    TextToSpeechService.SendToOutput(CleanForOutput($"\n💡 {mails.Count - 10} ek e posta daha var. Daha spesifik arama yapabilirsiniz."));
                }
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        #region Fuzzy Matching Helpers
        
        private bool FuzzyMatchCommand(string input, string command, double threshold = 0.8)
        {
            // Normalize et
            input = NormalizeTurkish(input);
            command = NormalizeTurkish(command);
            
            // Kelimelere böl
            var inputWords = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var commandWords = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // En az %70 kelime eşleşmesi gerekli
            int matchedWords = 0;
            foreach (var cmdWord in commandWords)
            {
                foreach (var inWord in inputWords)
                {
                    double similarity = CalculateSimilarity(inWord, cmdWord);
                    if (similarity >= threshold)
                    {
                        matchedWords++;
                        break;
                    }
                }
            }
            
            double matchRatio = (double)matchedWords / commandWords.Length;
            return matchRatio >= threshold;
        }
        
        private string NormalizeTurkish(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            
            return input
                .Replace('ı', 'i').Replace('İ', 'I')
                .Replace('ğ', 'g').Replace('Ğ', 'G')
                .Replace('ü', 'u').Replace('Ü', 'U')
                .Replace('ş', 's').Replace('Ş', 'S')
                .Replace('ö', 'o').Replace('Ö', 'O')
                .Replace('ç', 'c').Replace('Ç', 'C');
        }
        
        private int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target)) return source.Length;

            int[,] distance = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                distance[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                distance[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[source.Length, target.Length];
        }
        
        private double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            string normalizedSource = NormalizeTurkish(source.ToLowerInvariant());
            string normalizedTarget = NormalizeTurkish(target.ToLowerInvariant());

            if (normalizedSource.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            int distance = LevenshteinDistance(normalizedSource, normalizedTarget);
            int maxLength = Math.Max(normalizedSource.Length, normalizedTarget.Length);
            
            if (maxLength == 0) return 1.0;
            
            return 1.0 - (double)distance / maxLength;
        }
        
        #endregion
    }
}