# 🚀 GitHub Auto-Update Kurulum Rehberi

## ✅ TAMAMLANAN İŞLEMLER

- ✅ Git yapılandırıldı
- ✅ Tüm dosyalar commit edildi (221 dosya)
- ✅ GitHub remote eklendi
- ✅ Branch 'main' olarak ayarlandı
- ✅ Setup dosyası hazır (104.88 MB)

---

## 📋 ŞİMDİ YAPILACAKLAR (ADIM ADIM)

### **ADIM 1: GitHub Repository Oluştur**

1. **Tarayıcıda aç**: https://github.com/new

2. **Formu doldur**:
   - **Repository name**: `QuadroAIPilot` (tam olarak bu isim)
   - **Description**: "AI-powered voice assistant for Windows 11"
   - **Public** seç (✅ ücretsiz)
   - **Add a README file**: ❌ TIKLAMAVALIN (zaten var)
   - **Add .gitignore**: ❌ Seçme (zaten var)
   - **Choose a license**: MIT License seçebilirsin (opsiyonel)

3. **"Create repository"** butonuna tıkla

4. **Açılan sayfayı KAPATMA!** Orada komutlar göreceksin ama KULLANMA!

---

### **ADIM 2: Kodu GitHub'a Yükle**

#### **Yöntem A: PowerShell (Kolay)**

1. PowerShell'i **YÖNETİCİ OLARAK** aç

2. Proje klasörüne git:
```powershell
cd "C:\Users\serkan\source\repos\QuadroAIPilot setup so so outlook not setup deneme2"
```

3. Kodu yükle:
```powershell
git push -u origin main
```

4. **GitHub kullanıcı adı ve şifre sorarsa**:
   - Username: GitHub kullanıcı adın
   - Password: GitHub şifren (VEYA Personal Access Token - önerilen)

5. Yükleme başarılı olursa: **"Branch 'main' set up to track remote branch 'main' from 'origin'"** mesajını göreceksin.

#### **Yöntem B: GitHub Desktop (Daha Kolay)**

1. GitHub Desktop indir: https://desktop.github.com/

2. Uygulamayı aç ve GitHub hesabınla giriş yap

3. **File → Add Local Repository**

4. Proje klasörünü seç: `C:\Users\serkan\source\repos\QuadroAIPilot setup so so outlook not setup deneme2`

5. **Publish Repository** butonuna tıkla

---

### **ADIM 3: İlk Release Oluştur**

1. **Tarayıcıda aç**: https://github.com/quadroaipilot/QuadroAIPilot/releases/new

2. **Formu doldur**:
   - **Tag version**: `v1.2.0` (tam olarak bu!)
   - **Release title**: `QuadroAIPilot v1.2.0`
   - **Description**:
```markdown
# QuadroAIPilot v1.2.0 - İlk Release 🎉

## ✨ Yeni Özellikler

- 🔄 **Otomatik Güncelleme Sistemi**: GitHub Releases üzerinden otomatik güncelleme
- 🎤 Sesli komut tanıma
- 🤖 Claude AI entegrasyonu
- 📰 Haber agregasyonu
- 📧 Outlook entegrasyonu
- 🌐 Tarayıcı eklentileri (Chrome, Edge, Firefox)
- 🎨 Modern UI (4 farklı tema)

## 📥 Kurulum

1. Aşağıdaki setup dosyasını indir
2. QuadroAIPilot_Setup_1.2.0_Win11_Final_v10.exe'yi çalıştır
3. Kurulum talimatlarını takip et

## 📋 Gereksinimler

- Windows 11 (Build 22000+)
- .NET 8.0 Runtime
- Microsoft Edge WebView2

## 🔄 Otomatik Güncelleme

Bu sürümden itibaren uygulama otomatik olarak güncellemeleri kontrol eder.
Manuel kontrol için: Ayarlar → Güncellemeler → Güncellemeleri Kontrol Et
```

3. **"Attach binaries by dropping them here or selecting them"** kısmına:
   - Setup dosyasını sürükle: `Output\QuadroAIPilot_Setup_1.2.0_Win11_Final_v10.exe`
   - VEYA **"Choose files"** tıklayıp dosyayı seç

4. **"Publish release"** butonuna tıkla

---

### **ADIM 4: update.xml Dosyasını GitHub'a Yükle**

Release yayınlandıktan sonra:

1. PowerShell'de:
```powershell
cd "C:\Users\serkan\source\repos\QuadroAIPilot setup so so outlook not setup deneme2"
git add update.xml
git commit -m "Add update manifest for v1.2.0"
git push origin main
```

2. **ÖNEMLİ**: Release yayınladıktan sonra setup dosyasının gerçek URL'ini kontrol et:
   - GitHub'da release sayfasına git
   - Setup dosyasına sağ tıkla → "Copy link address"
   - URL şuna benzer olmalı:
     `https://github.com/quadroaipilot/QuadroAIPilot/releases/download/v1.2.0/QuadroAIPilot_Setup_1.2.0_Win11_Final_v10.exe`

3. Eğer URL farklıysa, `update.xml` dosyasındaki `<url>` etiketini güncelle.

---

### **ADIM 5: Test Et**

1. **Uygulamayı çalıştır**:
   - `bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\QuadroAIPilot.exe`

2. **Ayarlar → Güncellemeler**:
   - "Mevcut Versiyon: 1.2.0" görmeli
   - "Güncellemeleri Kontrol Et" butonuna tıkla

3. **Debug Output'u kontrol et**:
   - Visual Studio → View → Output
   - "[UpdateService]" log'larını ara
   - "Güncelleme kontrolü başlatılıyor..." mesajını görmeli

4. **İlk testte**:
   - "Güncelleme yok" mesajı almalısın (çünkü zaten 1.2.0 çalışıyor)
   - Bu normal ve doğru!

---

## 🔄 YENİ VERSİYON YAYINLAMA (İlerde)

### **1. Versiyon Numaralarını Güncelle**

```powershell
# Package.appxmanifest
# <Identity Version="1.2.0.0" /> → <Identity Version="1.3.0.0" />

# Setup/QuadroAIPilot.iss
# AppVersion "1.2.0" → AppVersion "1.3.0"
```

### **2. Build Al**

```powershell
.\BuildAndSetup.ps1
```

### **3. Git Commit ve Push**

```powershell
git add .
git commit -m "Release v1.3.0: [Değişiklik notları]"
git push origin main
```

### **4. GitHub'da Yeni Release Oluştur**

1. https://github.com/quadroaipilot/QuadroAIPilot/releases/new
2. Tag: `v1.3.0`
3. Title: `QuadroAIPilot v1.3.0`
4. Setup dosyasını yükle
5. Publish release

### **5. update.xml Güncelle**

```xml
<version>1.3.0</version>
<url>https://github.com/quadroaipilot/QuadroAIPilot/releases/download/v1.3.0/QuadroAIPilot_Setup_1.3.0_Win11_Final_v10.exe</url>
<changelog>https://github.com/quadroaipilot/QuadroAIPilot/releases/tag/v1.3.0</changelog>
```

```powershell
git add update.xml
git commit -m "Update manifest for v1.3.0"
git push origin main
```

### **6. Kullanıcılar Otomatik Bildirim Alır! 🎉**

Eski versiyonu kullanan kullanıcılar:
- Uygulama başlatıldığında (10 saniye sonra)
- "Yeni versiyon mevcut!" bildirimi alırlar
- İndirip kurarlar

---

## 🛠️ SORUN GİDERME

### **Sorun: "git push" hata veriyor**

**Çözüm**:
```powershell
# GitHub Personal Access Token oluştur
# 1. https://github.com/settings/tokens
# 2. "Generate new token" (classic)
# 3. Scope: repo (tümünü seç)
# 4. Token'ı kopyala

# Git'te token kullan
git remote set-url origin https://[TOKEN]@github.com/quadroaipilot/QuadroAIPilot.git
git push -u origin main
```

### **Sorun: "Setup dosyası bulunamıyor"**

**Çözüm**:
```powershell
# Build ve setup oluştur
.\BuildAndSetup.ps1

# Setup dosyası burada olmalı:
ls Output\QuadroAIPilot_Setup*.exe
```

### **Sorun: "Güncelleme bulunamadı" hatası**

**Çözüm**:
1. update.xml dosyası GitHub'da main branch'te mi? Kontrol et: https://github.com/quadroaipilot/QuadroAIPilot/blob/main/update.xml
2. URL doğru mu? Raw URL olmalı: `https://raw.githubusercontent.com/quadroaipilot/QuadroAIPilot/main/update.xml`
3. Internet bağlantısı var mı?

### **Sorun: "404 Not Found"**

**Çözüm**:
1. Repository public mi? (Settings → Danger Zone → Change visibility)
2. Release yayınlandı mı?
3. Setup dosyası release'e eklendi mi?

---

## 📊 İSTATİSTİKLER

- **Toplam Dosya**: 221 dosya
- **Kod Satırı**: ~63,000 satır
- **Setup Boyutu**: 104.88 MB
- **Build Süresi**: ~1-2 dakika
- **Maliyet**: ₺0 (Tamamen ücretsiz!)

---

## 🎯 ÖZET

✅ **TAMAMLANAN**:
- Auto-update sistemi kodlandı
- Git repository hazırlandı
- Setup dosyası oluşturuldu
- Tüm dosyalar commit edildi

📋 **YAPILACAK** (Sadece 5 dakika!):
1. GitHub'da repo oluştur (2 dk)
2. Git push (1 dk)
3. Release oluştur (2 dk)
4. Test et (30 sn)

🎉 **SONUÇ**:
Kullanıcılarınız otomatik güncelleme alacak!

---

## 💡 EK İPUÇLARI

### **GitHub CLI Kullanımı (Otomatik Release)**

```powershell
# GitHub CLI kur
winget install GitHub.cli

# Giriş yap
gh auth login

# Otomatik release oluştur
gh release create v1.2.0 `
  "Output\QuadroAIPilot_Setup_1.2.0_Win11_Final_v10.exe" `
  --title "QuadroAIPilot v1.2.0" `
  --notes "İlk release - Auto-update sistemi eklendi"
```

### **İstatistik Takibi**

- **İndirme sayıları**: GitHub Release sayfasında otomatik görünür
- **Kullanıcı sayısı**: Her release'in download count'u
- **Popüler versiyon**: En çok indirilen sürüm

### **Güvenlik**

- Setup dosyası hash'i otomatik kontrol edilir (AutoUpdater.NET)
- HTTPS zorunlu
- Digital signature ekleyebilirsin (opsiyonel)

---

**Başarılar! 🚀**
