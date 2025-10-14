# QuadroAIPilot Installer

Bu klasör QuadroAIPilot için profesyonel installer oluşturmak için gerekli dosyaları içerir.

## Gereksinimler

1. **Inno Setup 6.2+** - Winget ile kolayca kurabilirsiniz:
   ```powershell
   winget install --id JRSoftware.InnoSetup -e -s winget -i
   ```
   Alternatif: https://jrsoftware.org/isdl.php adresinden manuel indirme

2. **PowerShell 5.0+** - Windows 10/11'de varsayılan olarak yüklü

3. **.NET 8 SDK** - Projeyi derlemek için:
   ```powershell
   winget install Microsoft.DotNet.SDK.8
   ```

## Klasör Yapısı

```
Setup/
├── QuadroAIPilot.iss      # Ana Inno Setup script dosyası
├── Scripts/               # PowerShell yapılandırma scriptleri
│   ├── EnableWindowsFeatures.ps1     # Windows özelliklerini etkinleştirir
│   ├── InstallTurkishLanguage.ps1    # Türkçe dil desteği yükler
│   ├── InstallTTSVoices.ps1          # Edge TTS yapılandırması
│   ├── ConfigureSecurity.ps1         # Güvenlik ayarları
│   ├── ConfigureEmail.ps1            # E-posta sistemi yapılandırması
│   └── InstallBrowserExtensions.ps1  # Tarayıcı eklentileri
├── Prerequisites/         # Offline kurulum için bağımlılıklar (opsiyonel)
├── Tools/                # Yardımcı araçlar
└── README.md            # Bu dosya
```

## Installer Oluşturma

### 1. Projeyi Release Modda Derleyin

```powershell
cd ..
dotnet build QuadroAIPilot.csproj -c Release -p:Platform=x64
```

### 2. Bağımlılıkları İndirin (Offline Installer için - Opsiyonel)

Prerequisites klasörüne şu dosyaları indirin:
- `dotnet-runtime-8.0.0-win-x64.exe` - https://dotnet.microsoft.com/download/dotnet/8.0
- `MicrosoftEdgeWebView2Setup.exe` - https://go.microsoft.com/fwlink/p/?LinkId=2124703
- `VC_redist.x64.exe` - https://aka.ms/vs/17/release/vc_redist.x64.exe
- `WindowsAppRuntimeInstall.exe` - https://aka.ms/windowsappruntimeinstall-x64

### 3. Inno Setup ile Derleyin

1. Inno Setup'ı açın
2. File > Open ile `QuadroAIPilot.iss` dosyasını açın
3. Build > Compile (Ctrl+F9) ile derleyin
4. Output klasöründe `QuadroAIPilot_Setup_1.0.0.exe` oluşacak

## Installer Özellikleri

### Otomatik Yüklenenler:
- .NET 8.0 Runtime
- WebView2 Runtime
- Visual C++ Redistributables
- Windows App SDK Runtime
- Türkçe dil paketi
- Windows ses tanıma özellikleri

### Yapılandırmalar:
- Windows Defender istisnası
- Güvenlik duvarı kuralları
- Mikrofon erişim izinleri
- Edge TTS politikaları
- MAPI e-posta sistemi

### Opsiyonel Bileşenler:
- Tarayıcı eklentileri (Chrome, Edge, Firefox)
- Başlangıçta çalıştırma
- Masaüstü kısayolu

## Test Etme

1. Sanal makine veya test bilgisayarında deneyin
2. Farklı Windows sürümlerinde test edin (10/11)
3. Hem online hem offline kurulumu test edin
4. Outlook yüklü/yüksüz sistemlerde test edin

## Notlar

- Installer yaklaşık 20MB (online) veya 400MB (offline)
- İlk çalıştırmada mikrofon testi yapılır
- E-posta özellikleri için Outlook önerilir ama zorunlu değil
- Tarayıcı eklentileri manuel yükleme gerektirir