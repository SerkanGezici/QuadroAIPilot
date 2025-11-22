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

### 2. Bağımlılıkları İndirin (ZORUNLU - AI Provider Desteği için)

Prerequisites klasörüne şu dosyaları indirin:

**Zorunlu (AI Provider'lar için):**
- `node-v20.11.1-x64.msi` - https://nodejs.org/dist/v20.11.1/node-v20.11.1-x64.msi (Claude CLI için)

**Opsiyonel (Gerekirse):**
- `MicrosoftEdgeWebView2Setup.exe` - https://go.microsoft.com/fwlink/p/?LinkId=2124703
- `VC_redist.x64.exe` - https://aka.ms/vs/17/release/vc_redist.x64.exe

**Not:** dotnet-runtime ve WindowsAppSDK artık gerekmiyor (self-contained deployment)

### 3. Inno Setup ile Derleyin

1. Inno Setup'ı açın
2. File > Open ile `QuadroAIPilot.iss` dosyasını açın
3. Build > Compile (Ctrl+F9) ile derleyin
4. Output klasöründe `QuadroAIPilot_Setup_1.0.0.exe` oluşacak

## Installer Özellikleri

### Otomatik Yüklenenler:
- **Python 3.11.7 embedded** (TTS ve AI Bridges için)
- **edge-tts** (Türkçe ses sentezleme)
- **playwright 1.40.0** (ChatGPT/Gemini browser automation)
- **websockets 12.0** (AI Bridge HTTP servisleri)
- **Playwright Chromium** (Headless browser)
- **Node.js 20.11.1 LTS** (Claude CLI için)
- **Claude CLI** (@anthropics/claude)
- **WebView2 Runtime** (UI için)
- **Visual C++ Redistributables** (Sistem bağımlılıkları)

### AI Provider Desteği:
- ✅ **ChatGPT** (Python Playwright bridge - localhost:8765)
- ✅ **Gemini** (Python Playwright bridge - localhost:8766)
- ✅ **Claude** (Node.js CLI - @anthropics/claude)

### Yapılandırmalar:
- Windows Defender istisnası
- Güvenlik duvarı kuralları
- Mikrofon erişim izinleri
- Edge TTS politikaları
- MAPI e-posta sistemi

### Opsiyonel Bileşenler:
- Başlangıçta çalıştırma
- Masaüstü kısayolu
- Sağ tık menü entegrasyonu

## Test Etme

1. Sanal makine veya test bilgisayarında deneyin
2. Farklı Windows sürümlerinde test edin (10/11)
3. Hem online hem offline kurulumu test edin
4. Outlook yüklü/yüksüz sistemlerde test edin

## Notlar

- **Installer boyutu:** ~400-500MB (Tüm AI provider'lar dahil)
- **Kurulum süresi:** ~7-10 dakika (İnternet gerekli)
- **Kurulum sonrası:** Tüm AI provider'lar kullanıma hazır
- **İlk çalıştırma:** ChatGPT ve Gemini bridge'leri otomatik başlar
- **Claude kullanımı:** API key gerekli (https://console.anthropic.com/)
- **E-posta özellikleri:** Outlook önerilir ama zorunlu değil

### Kurulum İçeriği:
1. Python 3.11.7 + pip → %LOCALAPPDATA%\QuadroAIPilot\Python
2. Playwright Chromium → ~200MB (Browser automation)
3. Node.js 20.11.1 → Program Files\nodejs
4. Claude CLI → npm global packages
5. WebView2 + VC++ Redist → Sistem geneli

### Yeni PC'de İlk Kurulum Kontrolü:
```powershell
# Python kontrolü
%LOCALAPPDATA%\QuadroAIPilot\Python\python.exe --version

# Playwright kontrolü
%LOCALAPPDATA%\QuadroAIPilot\Python\python.exe -c "import playwright; print('OK')"

# Node.js kontrolü
node --version

# Claude CLI kontrolü
claude --version
```
- Tarayıcı eklentileri manuel yükleme gerektirir