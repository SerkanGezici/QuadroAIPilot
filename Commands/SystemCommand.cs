using QuadroAIPilot.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Sistem komutlarını (kopyala, yapıştır, kes vb.) yürüten sınıf
    /// </summary>
    public class SystemCommand : ICommand
    {
        private readonly string _action;
        public string CommandText { get; }
        private readonly WindowsApiService _windowsApiService;        // Alt+Tab gerektirmeyen komutlar listesi
        private static readonly HashSet<string> NoWindowSwitchCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sesi arttır",
            "sesi azalt",
            "sesi kapat",
            "sesi aç",
            "caps lock aç/kapat",
            "ekran görüntüsü al",
            "bilgisayarı kilitle",
            "masaüstünü göster",
            "görev görünümünü aç",
            "çalıştır penceresini aç",
            "dosya gezginini aç",
            "belgeler aç",
            "belgelerim aç",
            "resimler aç",
            "resimlerim aç",
            "müzik aç",
            "müziğim aç",
            "videolar aç",
            "videolarım aç",
            "indirilenler aç",
            "İndirilenler aç",
            "downloads aç",
            "masaüstü aç",
            "desktop aç"
        };

        /// <summary>
        /// Yeni bir sistem komutu oluşturur
        /// </summary>
        /// <param name="commandText">Tam komut metni</param>
        /// <param name="action">Yapılacak eylem (kopyala, yapıştır, kes)</param>
        /// <param name="windowsApiService">Windows API servisi</param>
        public SystemCommand(string commandText, string action, WindowsApiService windowsApiService = null)
        {
            CommandText = commandText;
            _action = action;
            _windowsApiService = windowsApiService ?? new WindowsApiService();
        }

        /// <summary>
        /// Komutun pencere değiştirme gerektirip gerektirmediğini kontrol eder
        /// </summary>
        private bool RequiresWindowSwitch()
        {
            // Komut Alt+Tab gerektirmeyen listede varsa false döner
            return !NoWindowSwitchCommands.Contains(_action);
        }

        /// <summary>
        /// Aktif uygulamaya göre komutun uygun olup olmadığını kontrol eder
        /// </summary>
        private bool IsCommandValidForActiveApp()
        {
            try
            {
                var activeWindowTitle = _windowsApiService.GetActiveWindowTitle();

                // Outlook komutları sadece Outlook açıkken çalışmalı
                if (_action.ToLowerInvariant().Contains("outlook") ||
                    _action.ToLowerInvariant().Contains("e-posta") ||
                    _action.ToLowerInvariant().Contains("e posta") ||
                    _action.ToLowerInvariant().Contains("mail"))
                {
                    if (!activeWindowTitle.Contains("Outlook", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[SystemCommand] Outlook komutu tespit edildi ancak Outlook açık değil: {_action}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemCommand] Uygulama doğrulama hatası: {ex.Message}");
                return true; // Hata durumunda komutu çalıştırmaya devam et
            }
        }

        /// <summary>
        /// Komutu çalıştırır - ilgili sistem eylemini gerçekleştirir
        /// </summary>
        /// <returns>İşlem sonucu</returns>
        public async Task<bool> ExecuteAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] ===== BAŞLANGIÇ: Sistem komutu çalıştırılıyor: {_action} =====");

                // Komutun geçerliliğini kontrol et
                if (!IsCommandValidForActiveApp())
                {
                    Debug.WriteLine($"[SystemCommand] Komut aktif uygulama için uygun değil: {_action}");
                    return false;
                }

                // Pencere değiştirme gerekip gerekmediğini kontrol et
                if (RequiresWindowSwitch())
                {
                    // Aktif pencere bilgilerini kaydet (P/Invoke ile doğrudan çağrı)
                    var activeWindowBefore = _windowsApiService.GetActiveWindow();
                    Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Alt+Tab ÖNCESI aktif pencere: {activeWindowBefore}");

                    // Önce Alt+Tab ile odağı bir önceki pencereye geçir
                    Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Alt+Tab gönderiliyor...");
                    HotkeySender.AltTab();

                    Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Alt+Tab gönderildi, 800ms bekleniyor...");
                    await Task.Delay(800); // Pencere değişimi için bekle

                    // Hedef pencereyi odakla - bu adım kritik!
                    Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Hedef pencereyi odaklama...");
                    HotkeySender.FocusTargetWindow();

                    // Odaklamanın gerçekleşmesi için ek bekleme
                    await Task.Delay(300);

                    // Alt+Tab sonrası aktif pencereyi kontrol et
                    var activeWindowAfter = _windowsApiService.GetActiveWindow();
                    Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Alt+Tab SONRASI aktif pencere: {activeWindowAfter}");
                    Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Pencere değişti mi? {(activeWindowBefore != activeWindowAfter ? "EVET" : "HAYIR")}");

                    // Pencere değişmediği durumda tekrar Alt+Tab yapma
                    if (activeWindowBefore == activeWindowAfter && _action.ToLowerInvariant() == "pencereyi kapat")
                    {
                        Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Pencere değişmedi! Tekrar Alt+Tab deneniyor...");
                        HotkeySender.AltTab();
                        await Task.Delay(800);
                        HotkeySender.FocusTargetWindow();
                        await Task.Delay(300);

                        // Son bir kontrol
                        activeWindowAfter = _windowsApiService.GetActiveWindow();
                        Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] İkinci Alt+Tab SONRASI aktif pencere: {activeWindowAfter}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Bu komut pencere değiştirme gerektirmiyor: {_action}");
                }

                // Komutu çalıştır
                bool result = await ExecuteCommandAction();

                // Komut yürütme sonrası log
                Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] {_action} komutu {(result ? "başarıyla" : "başarısız")} çalıştırıldı");

                // Son aktif pencereyi kontrol et
                var finalActiveWindow = _windowsApiService.GetActiveWindow();
                Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Komut SONRASI aktif pencere: {finalActiveWindow}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemCommand] Hata: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gerçek komut eylemini çalıştırır
        /// </summary>
        private async Task<bool> ExecuteCommandAction()
        {
            switch (_action.ToLowerInvariant())
            {                // "Kabul et" ve "Vazgeç" komutları                case "kabul et":
                case "onayla":
                case "tamam":
                case "kabul":
                case "onay":
                case "onaylıyorum":
                case "evet":
                case "okey":
                case "olumlu":
                    HotkeySender.SendAccept();
                    break;
                    
                case "vazgeç":
                case "iptal":
                case "iptal et":
                case "hayır":
                case "red":
                case "reddet":
                    HotkeySender.SendCancel();
                    break;

                // Dosya işlemleri
                case "dosya aç":
                    Debug.WriteLine("[SystemCommand] Dosya aç komutu algılandı, CommandProcessor'a yönlendiriliyor");
                    return true;

                // Bilgisayar kontrolleri
                case "bilgisayarı kilitle":
                    HotkeySender.LockComputer();
                    break;                case "masaüstünü göster":
                    HotkeySender.ShowDesktop();
                    break;
                case "ekran görüntüsü al":
                    HotkeySender.CaptureScreenshot();
                    break;
                case "uygulamayı kapat":
                case "pencereyi kapat":
                    // Güvenli pencere kapatma metodunu çağır
                    bool closeResult = HotkeySender.CloseApplicationSafely(_windowsApiService);
                    Debug.WriteLine($"[SystemCommand] Pencere kapatma sonucu: {(closeResult ? "Başarılı" : "Başarısız/Engellendi")}");
                    return closeResult;
                case "çalıştır penceresini aç":
                    HotkeySender.OpenRun();
                    break;
                case "görev görünümünü aç":
                    HotkeySender.OpenTaskView();
                    break;

                // Tarayıcı/Editör komutları
                case "sekmeyi kapat":
                    HotkeySender.CloseTab();
                    break;
                case "bul":
                    HotkeySender.Find();
                    break;
                case "yazdır":
                    HotkeySender.Print();
                    break;
                case "kaydet":
                    HotkeySender.Save();
                    break;
                case "kopyala":
                    HotkeySender.Copy();
                    break;
                case "kes":
                    HotkeySender.Cut();
                    break;
                case "yapıştır":
                    HotkeySender.Paste();
                    break;
                case "geri al":
                    HotkeySender.Undo();
                    break;
                case "ileri al":
                    HotkeySender.Redo();
                    break;
                case "tümünü seç":
                    HotkeySender.SelectAll();
                    break;
                case "sayfa başına git":
                    HotkeySender.GoToPageStart();
                    break;
                case "sayfa sonuna git":
                    HotkeySender.GoToPageEnd();
                    break;
                case "yenile":
                    HotkeySender.Refresh();
                    break;
                case "yeniden adlandır":
                    HotkeySender.Rename();
                    break;

                // Pencere düzenleme
                case "pencereyi sağa hizala":
                    HotkeySender.SnapWindowRight();
                    break;
                case "pencereyi sola hizala":
                    HotkeySender.SnapWindowLeft();
                    break;

                // Tarayıcı gezintisi
                case "tarayıcıda ileri git":
                    HotkeySender.BrowserForward();
                    break;
                case "tarayıcıda geri git":
                    HotkeySender.BrowserBack();
                    break;

                // Ses kontrolleri
                case "sesi arttır":
                    HotkeySender.VolumeUp();
                    break;
                case "sesi azalt":
                    HotkeySender.VolumeDown();
                    break;
                case "sesi kapat":
                case "sesi aç":
                    HotkeySender.VolumeMute();
                    break;

                // Sekme ve pencere oluşturma
                case "yeni sekme":
                    HotkeySender.NewTab();
                    break;
                case "yeni sayfa":
                    HotkeySender.NewPage();
                    break;
                case "yeni pencere":
                    HotkeySender.NewWindow();
                    break;                // Temel klavye tuşları
                case "enter":
                case "enter tuşu":
                    HotkeySender.SendEnter();
                    break;
                case "sayfayı aşağı kaydır":
                    HotkeySender.PageDown();
                    break;                case "sayfayı yukarı kaydır":
                    HotkeySender.PageUp();
                    break;
                case "caps lock aç/kapat":
                    HotkeySender.ToggleCapsLock();
                    break;
                case "boşluk":
                    HotkeySender.SendSpace();
                    break;
                case "sağ":
                    HotkeySender.PressRightArrow();
                    break;
                case "sol":
                    HotkeySender.PressLeftArrow();
                    break;
                case "yukarı":
                    HotkeySender.PressUpArrow();
                    break;
                case "aşağı":
                    HotkeySender.PressDownArrow();
                    break;
                case "sonraki":
                case "tab":
                    HotkeySender.SendTab();
                    break;
                case "önceki":
                case "geri git":
                    HotkeySender.PressShiftTab();
                    break;
                case "escape":
                case "esc":
                    HotkeySender.SendCancel();
                    break;

                // Outlook komutları
                case "yeni e-posta oluştur":
                case "yeni e-posta":
                case "yeni e posta":
                case "yeni mail":
                case "yeni sayfa aç":
                    HotkeySender.OutlookNewMail();
                    break;
                case "e-postayı yanıtla":
                case "yanıtla":
                    HotkeySender.OutlookReply();
                    break;
                case "herkese yanıtla":
                case "tümünü yanıtla":
                    HotkeySender.OutlookReplyAll();
                    break;
                case "e-postayı ilet":
                case "ilet":
                case "forward":
                    HotkeySender.OutlookForward();
                    break;
                case "e-postayı gönder":
                case "gönder":
                case "e posta gönder":
                case "mail gönder":
                    HotkeySender.OutlookSendMail();
                    break;
                case "posta kutusuna git":
                case "gelen kutusuna git":
                case "inbox":
                    HotkeySender.OutlookGoToInbox();
                    break;
                case "takvim aç":
                case "takvime git":
                case "calendar":
                    HotkeySender.OutlookGoToCalendar();
                    break;
                case "yeni toplantı":
                case "toplantı oluştur":
                case "toplantı ekle":
                    HotkeySender.OutlookNewMeeting();
                    break;
                case "kişilere git":
                case "kişiler":
                case "contacts":
                    HotkeySender.OutlookGoToContacts();
                    break;
                case "yapılacaklara git":
                case "görevler":
                case "tasks":
                    HotkeySender.OutlookGoToTasks();
                    break;
                case "okunmadı olarak işaretle":
                case "okunmadı yap":
                    HotkeySender.OutlookMarkAsUnread();
                    break;
                case "okundu olarak işaretle":
                case "okundu yap":
                    HotkeySender.OutlookMarkAsRead();
                    break;
                case "bayrak ekle":
                case "işaretle":
                case "bayrakla":
                    HotkeySender.OutlookFlagMail();
                    break;
                case "dosya ekle":
                case "ek ekle":
                case "ekle":
                    await HotkeySender.OutlookAttachFileAsync();
                    break;
                case "randevulara git":
                case "randevular":
                case "appointments":
                    await HotkeySender.OutlookGoToAppointmentsAsync();
                    break;
                case "postaları gönder/al":
                case "e-postaları al":
                case "e postaları kontrol et":
                case "mailleri kontrol et":
                case "check mail":
                    HotkeySender.OutlookSendReceive();
                    break;
                case "e-posta ara":
                case "e posta ara":
                case "mail ara":
                case "outlook ara":
                    HotkeySender.OutlookSearch();
                    break;
                case "filtreleme yap":
                case "e postaları filtrele":
                case "mailleri filtrele":
                    HotkeySender.OutlookFilter();
                    break;
                case "okunmamış postalar":
                case "okunmamış e postalar":
                case "okunmamış mailler":
                case "okunmamışlar":
                    await HotkeySender.OutlookGoToUnreadMailAsync();
                    break;
                case "taslaklara git":
                case "taslaklar":
                case "drafts":
                    HotkeySender.OutlookGoToDrafts();
                    break;                case "gönderilenler":
                case "gönderilenlere git":
                case "sent items":
                    HotkeySender.OutlookGoToSentItems();
                    break;
                case "önemli olarak işaretle":
                case "önemli yap":
                    HotkeySender.OutlookMarkAsImportant();
                    break;

                // Klasör açma komutları
                case "belgeler aç":
                case "belgelerim aç":
                    HotkeySender.OpenSpecialFolder("belgeler");
                    break;
                case "resimler aç":
                case "resimlerim aç":
                    HotkeySender.OpenSpecialFolder("resimler");
                    break;
                case "müzik aç":
                case "müziğim aç":
                    HotkeySender.OpenSpecialFolder("müzik");
                    break;
                case "videolar aç":
                case "videolarım aç":
                    HotkeySender.OpenSpecialFolder("videolar");
                    break;
                case "indirilenler aç":
                case "İndirilenler aç":
                case "downloads aç":
                    HotkeySender.OpenSpecialFolder("indirilenler");
                    break;
                case "masaüstü aç":
                case "desktop aç":
                    HotkeySender.OpenSpecialFolder("masaüstü");
                    break;
                case "dosya gezginini aç":
                    HotkeySender.OpenFolder("");
                    break;

                default:
                    Debug.WriteLine($"[SystemCommand] Bilinmeyen sistem komutu: {_action}");
                    return false;
            }

            return true;
        }
    }
}