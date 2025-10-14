# QuadroAIPilot Setup Oluşturma Kılavuzu - Windows 11

Bu dokümantasyon, QuadroAIPilot uygulaması için Windows 11 odaklı, minimal müdahale prensibine dayalı setup dosyası oluşturma sürecini içerir.

## 🎯 Özet
QuadroAIPilot, WinUI 3 tabanlı bir masaüstü uygulamasıdır ve **sadece Windows 11 64-bit** sistemlerde çalışır. Inno Setup kullanılarak kurulum dosyası oluşturulmuştur. Setup stratejisi: Windows 11'de varsayılan olarak gelen bileşenlere dokunmadan, sadece eksik olanları kurar.

## 🚨 Önemli: Windows 11 Odaklı Strateji
- **Sadece Windows 11 destekleniyor** (Windows 10 desteği kaldırıldı)
- **Minimal müdahale prensibi** - Sadece gerçekten eksik olan bileşenler kurulur
- **Self-contained deployment** - .NET 8 ve Windows App SDK uygulama içinde

## 📋 Önkoşullar

### 1. Gerekli Yazılımlar
- **Inno Setup 6.4.3+**: `C:\Users\serkan\AppData\Local\Programs\Inno Setup 6\`
- **.NET 8 SDK**: Proje derleme için
- **Visual Studio 2022** veya **dotnet CLI**

### 2. Proje Yapısı (Windows 11 İçin Güncellenmiş)
```
QuadroAIPilot/
├── Setup/
│   ├── QuadroAIPilot.iss (Inno Setup script - Windows 11)
│   ├── Prerequisites/
│   │   ├── MicrosoftEdgeWebView2Setup.exe ✅ (Kurulacak - Her Windows 11'de yok)
│   │   ├── VC_redist.x64.exe ✅ (Kurulacak - Garanti değil)
│   │   ├── chromedriver.exe (Selenium desteği)
│   │   └── msedgedriver.exe (Selenium desteği)
│   │   ❌ dotnet-runtime-8.0.0-win-x64.exe (KALDIRILDI - Self-contained)
│   │   ❌ WindowsAppRuntimeInstall.exe (KALDIRILDI - Self-contained)
│   └── Scripts/
│       ├── EnableWindowsFeatures.ps1
│       ├── ConfigureSecurity.ps1
│       ├── ConfigureEmail.ps1
│       ├── CheckMicrophoneAccess.ps1
│       └── InstallBrowserExtensions.ps1
│       ❌ InstallTurkishLanguage.ps1 (KALDIRILDI - Edge TTS kullanıyor)
│       ❌ InstallTTSVoices.ps1 (KALDIRILDI - Edge TTS kullanıyor)
├── Output/ (Setup dosyalarının çıktı dizini)
├── Assets/
│   └── index.html
└── QuadroAIPilot.csproj
```

## 🔧 Kritik Yapılandırmalar

### 1. Proje Dosyası (QuadroAIPilot.csproj)
```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <!-- KRİTİK: Windows App SDK'yı self-contained olarak paketle -->
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>false</SelfContained>
</PropertyGroup>
```

### 2. WebView2 Veri Dizini Sorunu ve Çözümü

#### Problem:
WebView2, default olarak exe dosyasının yanına (Program Files) veri dizini oluşturmaya çalışır ve yazma izni hatası verir:
```
Microsoft Edge. kendi veri dizinini okuyamaz veya üzerine yazamaz:
C:\Program Files\QuadroAIPilot\QuadroAIPilot.exe.WebView2\EBWebView
```

#### Çözüm:
MainWindow.xaml.cs dosyasında, InitializeComponent() çağrısından ÖNCE environment variable ayarla:

```csharp
public MainWindow()
{
    try
    {
        // WebView2 User Data Folder'ı ayarla - InitializeComponent'ten ÖNCE!
        string userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuadroAIPilot",
            "WebView2"
        );
        
        // Dizin yoksa oluştur
        Directory.CreateDirectory(userDataPath);
        
        // Environment variable'ı ayarla
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataPath);
        
        this.InitializeComponent();
    }
    catch (Exception ex)
    {
        // Hata işleme
    }
}
```

## 📝 Inno Setup Script (QuadroAIPilot.iss)

### Kritik Ayarlar (Windows 11):
```pascal
[Setup]
AppName=QuadroAIPilot
AppVersion=1.0.0
DefaultDirName={autopf}\{#AppName}
OutputDir=..\Output
OutputBaseFilename=QuadroAIPilot_Setup_{#AppVersion}_Windows11
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
MinVersion=10.0.22000  ; Windows 11 minimum build
```

### Önemli Bileşenler (Windows 11 İçin Sadeleştirilmiş):
```pascal
[Components]
Name: "main"; Description: "QuadroAIPilot Ana Uygulama"; Types: typical full custom; Flags: fixed
Name: "runtime\webview2"; Description: "Microsoft Edge WebView2"; Types: typical full custom; Flags: fixed
Name: "runtime\vcredist"; Description: "Visual C++ Redistributables"; Types: typical full custom; Flags: fixed
; .NET 8 ve Windows App SDK self-contained olduğu için kaldırıldı
; Edge Windows 11'de varsayılan olduğu için kurulum yok, sadece kontrol
```

### Dosya Kopyalama:
```pascal
[Files]
; Ana uygulama - Logs klasörünü hariç tut (dosya kilidi sorunu)
Source: "..\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\*"; 
DestDir: "{app}"; 
Flags: ignoreversion recursesubdirs createallsubdirs; 
Components: main; 
Excludes: "Logs\*"

; PowerShell scriptleri (Sadece gerekli olanlar)
Source: "Scripts\EnableWindowsFeatures.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
Source: "Scripts\ConfigureSecurity.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
Source: "Scripts\CheckMicrophoneAccess.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
Source: "Scripts\ConfigureEmail.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
Source: "Scripts\InstallBrowserExtensions.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main

; Prerequisite dosyaları (Sadece gerçekten gerekli olanlar)
Source: "Prerequisites\MicrosoftEdgeWebView2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Components: runtime\webview2
Source: "Prerequisites\VC_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Components: runtime\vcredist
; .NET 8 kaldırıldı - self-contained deployment
```

## 🚀 Setup Oluşturma Adımları

### 1. Projeyi Derle
```bash
cd C:\Users\serkan\source\repos\QuadroAIPilot
dotnet build QuadroAIPilot.csproj -c Release -p:Platform=x64
```

### 2. Setup'ı Derle
```bash
cd Setup
"C:\Users\serkan\AppData\Local\Programs\Inno Setup 6\ISCC.exe" QuadroAIPilot.iss
```

### 3. Çıktı
Setup dosyası `Output\QuadroAIPilot_Setup_1.0.0_v5.exe` olarak oluşturulur.

## ⚠️ Karşılaşılan Sorunlar ve Çözümleri

### 1. "Sınıf kaydedilmemiş (REGDB_E_CLASSNOTREG)" Hatası
**Sorun**: Windows App SDK runtime düzgün yüklenmemiş.
**Çözüm**: `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` ekle.

### 2. WebView2 Veri Dizini Yazma İzni
**Sorun**: Program Files'da yazma izni yok.
**Çözüm**: Environment variable ile LocalAppData kullan.

### 3. Logs Klasörü Dosya Kilidi
**Sorun**: Setup derlenirken log dosyaları kilitli.
**Çözüm**: Inno Setup'ta `Excludes: "Logs\*"` ekle.

### 4. Gereksiz Bileşen Kontrolleri
**Sorun**: EdgeTTS kullanılıyor ama lokal TTS bileşenleri kontrol ediliyor.
**Çözüm**: Türkçe dil paketi ve TTS seslerini opsiyonel yap.

## 📦 Prerequisite Dosyaları (Windows 11 İçin)

Bu dosyalar `Setup\Prerequisites` klasöründe bulunmalı:
1. **MicrosoftEdgeWebView2Setup.exe**: WebView2 Runtime (Her Windows 11'de yok)
2. **VC_redist.x64.exe**: Visual C++ 2015-2022 Redistributable (Garanti değil)
3. **chromedriver.exe**: Chrome tarayıcı otomasyon desteği
4. **msedgedriver.exe**: Edge tarayıcı otomasyon desteği

❌ **KALDIRILACAKLAR:**
- dotnet-runtime-8.0.0-win-x64.exe (Self-contained)
- WindowsAppRuntimeInstall.exe (Self-contained)

İndirme linkleri:
- WebView2: https://developer.microsoft.com/microsoft-edge/webview2/
- VC++ Redist: https://aka.ms/vs/17/release/vc_redist.x64.exe

## 🔍 Test Prosedürü

1. Setup'ı farklı bir bilgisayarda test et
2. Kontrol edilecekler:
   - Uygulama başlıyor mu?
   - WebView2 düzgün yükleniyor mu?
   - Ses tanıma çalışıyor mu?
   - E-posta entegrasyonu (Outlook varsa)

## 💡 İpuçları

1. **Versiyon Güncellemesi**: Her yeni setup için `OutputBaseFilename` içindeki versiyon numarasını değiştir (v5, v6, vb.)
2. **Debug için**: SimpleCrashLogger.cs kullanılıyor, loglar `%LocalAppData%\QuadroAIPilot\startup_crash.log` dosyasında
3. **Silent Install**: `/SILENT` veya `/VERYSILENT` parametreleri kullanılabilir

## 🎯 Özet Komutlar

```bash
# 1. Projeyi derle
cd C:\Users\serkan\source\repos\QuadroAIPilot
dotnet build QuadroAIPilot.csproj -c Release -p:Platform=x64

# 2. Setup oluştur
cd Setup
"C:\Users\serkan\AppData\Local\Programs\Inno Setup 6\ISCC.exe" QuadroAIPilot.iss

# 3. Setup dosyası hazır: Output\QuadroAIPilot_Setup_1.0.0_Windows11.exe
```

## ⚡ Windows 11 Setup Stratejisi Özeti

### ✅ SADECE KONTROL ET:
- Windows 11 64-bit mi?
- Microsoft Edge kurulu mu? (Her Windows 11'de var)
- Port 19741 boş mu?
- Mikrofon sistem izni var mı?

### 📦 YOKSA KUR:
- WebView2 Runtime (kontrol et, yoksa kur)
- Visual C++ Redistributables (kontrol et, yoksa kur)

### ❌ ASLA KURMA:
- Microsoft Edge (Windows 11'de varsayılan)
- .NET 8 Runtime (self-contained)
- Windows App SDK (self-contained)
- Türkçe dil paketi (Edge TTS kullanıyor)

---

**NOT**: Bu dokümantasyon Windows 11 odaklı minimal müdahale stratejisine göre güncellenmiştir. Setup sadece gerçekten eksik olan bileşenleri kurar.