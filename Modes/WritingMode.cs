using System;
using System.Diagnostics;
using System.Windows.Forms;
using QuadroAIPilot.Services;
using QuadroAIPilot.Infrastructure;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Modes
{
    public class WritingMode : IMode
    {
        public void Enter() 
        { 
            Debug.WriteLine("[WritingMode] Yazı moduna girildi");
        }
        
        public void Exit() 
        { 
            Debug.WriteLine("[WritingMode] Yazı modundan çıkıldı");
        }

        public bool HandleSpeech(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                // Özel komutları kontrol et
                string lowerText = text.ToLowerInvariant().Trim();
                
                // Yeni satır komutları
                if (lowerText == "yeni satır" || lowerText == "enter" || lowerText == "alt satır")
                {
                    SendKeys.SendWait("{ENTER}");
                    Debug.WriteLine("[WritingMode] Yeni satır eklendi");
                    return true;
                }
                
                // Silme komutları
                if (lowerText == "sil" || lowerText == "geri al")
                {
                    SendKeys.SendWait("{BACKSPACE}");
                    Debug.WriteLine("[WritingMode] Karakter silindi");
                    return true;
                }
                
                // Kelime silme
                if (lowerText == "kelimeyi sil" || lowerText == "son kelimeyi sil")
                {
                    SendKeys.SendWait("^{BACKSPACE}"); // Ctrl+Backspace
                    Debug.WriteLine("[WritingMode] Son kelime silindi");
                    return true;
                }
                
                // Tümünü seç ve sil
                if (lowerText == "hepsini sil" || lowerText == "tümünü sil")
                {
                    SendKeys.SendWait("^a"); // Ctrl+A
                    SendKeys.SendWait("{DELETE}");
                    Debug.WriteLine("[WritingMode] Tüm metin silindi");
                    return true;
                }
                
                // Tab ekleme
                if (lowerText == "tab" || lowerText == "sekme")
                {
                    SendKeys.SendWait("{TAB}");
                    Debug.WriteLine("[WritingMode] Tab eklendi");
                    return true;
                }
                
                // Boşluk ekleme (açık komut)
                if (lowerText == "boşluk")
                {
                    SendKeys.SendWait(" ");
                    Debug.WriteLine("[WritingMode] Boşluk eklendi");
                    return true;
                }
                
                // Yön tuşları
                if (lowerText == "yukarı" || lowerText == "yukarı git")
                {
                    SendKeys.SendWait("{UP}");
                    Debug.WriteLine("[WritingMode] Yukarı ok tuşu");
                    return true;
                }
                
                if (lowerText == "aşağı" || lowerText == "aşağı git" || lowerText == "aşağı geç")
                {
                    SendKeys.SendWait("{DOWN}");
                    Debug.WriteLine("[WritingMode] Aşağı ok tuşu");
                    return true;
                }
                
                if (lowerText == "sağ" || lowerText == "sağa" || lowerText == "sağa git")
                {
                    SendKeys.SendWait("{RIGHT}");
                    Debug.WriteLine("[WritingMode] Sağ ok tuşu");
                    return true;
                }
                
                if (lowerText == "sol" || lowerText == "sola" || lowerText == "sola git")
                {
                    SendKeys.SendWait("{LEFT}");
                    Debug.WriteLine("[WritingMode] Sol ok tuşu");
                    return true;
                }
                
                // Sayfa hareketleri
                if (lowerText == "sayfa aşağı" || lowerText == "sayfa aşağıya")
                {
                    SendKeys.SendWait("{PGDN}");
                    Debug.WriteLine("[WritingMode] Sayfa aşağı");
                    return true;
                }
                
                if (lowerText == "sayfa yukarı" || lowerText == "sayfa yukarıya")
                {
                    SendKeys.SendWait("{PGUP}");
                    Debug.WriteLine("[WritingMode] Sayfa yukarı");
                    return true;
                }
                
                // Başa ve sona gitme
                if (lowerText == "başa git" || lowerText == "başa" || lowerText == "satır başı")
                {
                    SendKeys.SendWait("{HOME}");
                    Debug.WriteLine("[WritingMode] Satır başı");
                    return true;
                }
                
                if (lowerText == "sona git" || lowerText == "sona" || lowerText == "satır sonu")
                {
                    SendKeys.SendWait("{END}");
                    Debug.WriteLine("[WritingMode] Satır sonu");
                    return true;
                }
                
                // Belge başı ve sonu
                if (lowerText == "belge başı" || lowerText == "dosya başı")
                {
                    SendKeys.SendWait("^{HOME}"); // Ctrl+Home
                    Debug.WriteLine("[WritingMode] Belge başı");
                    return true;
                }
                
                if (lowerText == "belge sonu" || lowerText == "dosya sonu")
                {
                    SendKeys.SendWait("^{END}"); // Ctrl+End
                    Debug.WriteLine("[WritingMode] Belge sonu");
                    return true;
                }
                
                // Normal metin - WindowsApiService kullanarak gönder
                var windowsApiService = ServiceContainer.GetService<IWindowsApiService>();
                
                // Metni gönder
                windowsApiService.SendTextToActiveWindow(text);
                Debug.WriteLine($"[WritingMode] Metin gönderildi: {text}");
                
                // Her zaman 1 boşluk ekle
                windowsApiService.SendTextToActiveWindow(" ");
                Debug.WriteLine($"[WritingMode] Boşluk eklendi");
                
                return true; // Metni işledik
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WritingMode] Hata: {ex.Message}");
                LogService.LogDebug($"[WritingMode] SendKeys hatası: {ex.Message}");
                return false;
            }
        }
    }
}