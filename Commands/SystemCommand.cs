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
        private readonly WindowsApiService _windowsApiService;

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

                // Win+H kaldırıldıktan sonra artık pencere değiştirme yapılmıyor
                Debug.WriteLine($"[SystemCommand][{stopwatch.ElapsedMilliseconds}ms] Komut doğrudan çalıştırılıyor: {_action}");

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
            // Nokta ve fazla boşlukları temizle
            var cleanAction = _action.ToLowerInvariant().TrimEnd('.', ' ', '!', '?');

            switch (cleanAction)
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
                case "bilgisayarımı kilitle":
                case "pc kilitle":
                case "pc yi kilitle":
                case "pc'yi kilitle":
                case "ekranı kilitle":
                case "ekranımı kilitle":
                case "screen lock":
                case "lock screen":
                case "kilitle":
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
                case "belgeleri aç":
                case "belgelerim aç":
                case "belgelerimi aç":
                case "belgelerim klasörünü aç":
                case "belgeler klasörünü aç":
                    HotkeySender.OpenSpecialFolder("belgeler");
                    break;
                case "resimler aç":
                case "resimleri aç":
                case "resimlerim aç":
                case "resimlerimi aç":
                case "resimlerim klasörünü aç":
                case "resimler klasörünü aç":
                    HotkeySender.OpenSpecialFolder("resimler");
                    break;
                case "müzik aç":
                case "müziği aç":
                case "müziğim aç":
                case "müziğimi aç":
                case "müziğim klasörünü aç":
                case "müzik klasörünü aç":
                    HotkeySender.OpenSpecialFolder("müzik");
                    break;
                case "videolar aç":
                case "videoları aç":
                case "videolarım aç":
                case "videolarımı aç":
                case "videolarım klasörünü aç":
                case "videolar klasörünü aç":
                    HotkeySender.OpenSpecialFolder("videolar");
                    break;
                case "indirilenler aç":
                case "indirilenleri aç":
                case "İndirilenler aç":
                case "downloads aç":
                case "indirilenler klasörünü aç":
                case "downloads klasörünü aç":
                    HotkeySender.OpenSpecialFolder("indirilenler");
                    break;
                case "masaüstü aç":
                case "masaüstünü aç":
                case "desktop aç":
                case "masaüstü klasörünü aç":
                case "desktop klasörünü aç":
                    HotkeySender.OpenSpecialFolder("masaüstü");
                    break;
                case "bilgisayarım aç":
                case "bilgisayarımı aç":
                case "bilgisayarım":
                case "bilgisayarımı":
                case "bu bilgisayar aç":
                case "bu bilgisayar":
                case "this pc":
                case "bilgisayarım klasörünü aç":
                case "bu bilgisayar klasörünü aç":
                    HotkeySender.OpenSpecialFolder("bilgisayarım");
                    break;
                case "dosya gezginini aç":
                    HotkeySender.OpenFolder("");
                    break;

                // Hesap Makinesi (custom command from CommandRegistry)
                case "custom_calculator":
                    Debug.WriteLine("[SystemCommand] Hesap makinesi açılıyor: calc.exe");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "calc.exe",
                        UseShellExecute = true
                    });
                    break;

                default:
                    Debug.WriteLine($"[SystemCommand] Bilinmeyen sistem komutu: {_action}");
                    return false;
            }

            return true;
        }
    }
}