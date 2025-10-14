# QuadroAI Pilot

QuadroAI Pilot, gelişmiş ses komutları ve yapay zeka entegrasyonu ile Windows uygulamalarını kontrol etmenizi sağlayan akıllı bir masaüstü asistanıdır.

## 🚀 Özellikler

### Ana Özellikler
- **Ses Komut Kontrolü**: Uygulamaları ses komutlarıyla açma ve kontrol etme
- **Outlook Entegrasyonu**: E-posta okuma, yazma ve yönetimi
- **Yapay Zeka Asistanı**: Akıllı komut işleme ve yanıt sistemi
- **Dosya Arama**: Gelişmiş dosya bulma ve açma özellikleri
- **Sistem Komutları**: Windows sistem işlemlerini ses ile kontrol

### Teknik Özellikler
- **WinUI 3** ile modern kullanıcı arayüzü
- **WebView2** entegrasyonu ile web teknolojileri desteği
- **Text-to-Speech (TTS)** ile sesli geri bildirim
- **Dependency Injection** ile modüler mimari
- **Structured Logging** ile detaylı sistem takibi
- **Global Exception Handling** ile güvenilir işletim

## 🛠️ Teknoloji Stack'i

### Frontend & UI
- **WinUI 3** - Modern Windows uygulaması framework'ü
- **XAML** - Kullanıcı arayüzü tanımlamaları
- **WebView2** - Web teknolojileri entegrasyonu
- **HTML/CSS/JavaScript** - Web tabanlı kontroller

### Backend & Services
- **.NET 8** - Ana framework
- **Microsoft.Extensions.DependencyInjection** - Bağımlılık enjeksiyonu
- **Serilog** - Yapılandırılmış loglama
- **System.Speech** - Ses sentezi (TTS)
- **Microsoft.Office.Interop.Outlook** - Outlook entegrasyonu

### Audio & Speech
- **NAudio** - Ses işleme
- **Windows Speech API** - Ses tanıma ve sentezi
- **Tolga TTS Voice** - Türkçe ses sentezi

### AI & ML
- **TorchSharp** - Makine öğrenmesi framework'ü
- **Custom AI Models** - Özelleştirilmiş komut işleme modelleri

## 🏗️ Mimari

### Katmanlar
```
┌─────────────────┐
│   Presentation  │  ← MainWindow, XAML, WebView2
├─────────────────┤
│   Application   │  ← Commands, Modes, Managers  
├─────────────────┤
│   Services      │  ← TTS, FileSearch, Outlook
├─────────────────┤
│   Infrastructure│  ← DI, Logging, Exception Handling
└─────────────────┘
```

### Tasarım Desenleri
- **Command Pattern** - Komut işleme sistemi
- **Manager Pattern** - Bileşen yönetimi
- **Singleton Pattern** - Uygulama kayıt defteri
- **Factory Pattern** - Komut fabrikası
- **Observer Pattern** - Olay koordinasyonu

## 🚦 Başlangıç

### Gereksinimler
- Windows 10/11
- .NET 8 Runtime
- Microsoft Outlook (e-posta özellikleri için)
- Windows Speech Platform

### Kurulum
1. Repository'yi klonlayın
2. Visual Studio 2022 ile açın
3. NuGet paketlerini restore edin
4. Solution'ı build edin
5. Uygulamayı çalıştırın

### İlk Kullanım
1. Uygulamayı başlatın
2. Mikrofon izinlerini verin
3. "Komut modu" ile ses komutlarını aktifleştirin
4. İlk komutunuzu deneyin: "not defteri aç"

## 🎮 Kullanım

### Temel Komutlar
- `"notepad aç"` - Not Defteri'ni açar
- `"calculater aç"` - Hesap Makinesi'ni açar  
- `"outlook aç"` - Microsoft Outlook'u açar
- `"dosya ara <dosya adı>"` - Dosya arar
- `"mail oku"` - E-postaları okur

### Modlar
- **Komut Modu**: Ses komutlarını işler
- **Okuma Modu**: Metinleri sesli okur
- **Yazma Modu**: Dikte ile metin girişi

## 🔧 Geliştirme

### Proje Yapısı
```
QuadroAIPilot/
├── Commands/           # Komut implementasyonları
├── Modes/             # Uygulama modları
├── Managers/          # Bileşen yöneticileri
├── Services/          # Servis katmanı
├── Infrastructure/    # Altyapı bileşenleri
├── Assets/           # UI assets (HTML, CSS, JS)
├── Tests/            # Unit testler
└── Properties/       # Uygulama özellikleri
```

### Yeni Komut Ekleme
1. `Commands/` klasöründe yeni komut sınıfı oluşturun
2. `ICommand` interface'ini implement edin
3. `CommandRegistry`'ye kaydedin
4. Unit testlerini yazın

### Test Çalıştırma
```bash
dotnet test QuadroAIPilot.Tests
```

### Logging
Uygulama, Serilog ile yapılandırılmış loglama kullanır:
- **Console Sink**: Geliştirme sırasında konsol çıktısı
- **File Sink**: Production ortamında dosya loglama
- **Structured Logging**: JSON formatında detaylı loglar

Log dosyaları: `./Logs/quadroai-{date}.log`

## 🛡️ Güvenlik

### Güvenlik Özellikleri
- **Input Validation**: Tüm kullanıcı girişleri doğrulanır
- **Command Sanitization**: Tehlikeli komutlar engellenir
- **Path Validation**: Dosya yolu güvenlik kontrolleri
- **Process Whitelisting**: Sadece güvenli uygulamalar çalıştırılır

### Güvenlik Politikaları
- Sistem dosyalarına erişim engellendi
- Script injection koruması
- Path traversal koruması
- Executable whitelist kontrolü

## 📊 İzleme ve Performans

### Metrikler
- Komut işleme süreleri
- TTS yanıt süreleri
- Bellek ve CPU kullanımı
- Hata oranları

### Performans Optimizasyonları
- Asenkron komut işleme
- Lazy loading servisleri
- Efficient memory management
- Background task optimization

## 🐛 Troubleshooting

### Yaygın Sorunlar

#### TTS Çalışmıyor
- Windows Speech API'nin yüklü olduğunu kontrol edin
- Tolga sesinin sistemde mevcut olduğunu doğrulayın
- Ses kartı sürücülerini güncelleyin

#### Outlook Entegrasyonu Sorunları  
- Outlook'un yüklü ve yapılandırılmış olduğunu kontrol edin
- MAPI istemcisinin etkin olduğunu doğrulayın
- Güvenlik ayarlarını kontrol edin

#### Mikrofon Tanımıyor
- Mikrofon izinlerini kontrol edin
- Windows ses tanıma ayarlarını doğrulayın
- Ses seviyesini ayarlayın

### Log Analizi
Sorun tespiti için log dosyalarını inceleyin:
```
./Logs/quadroai-{date}.log
```

## 🤝 Katkıda Bulunma

### Geliştirme Süreci
1. Issue oluşturun veya mevcut bir issue'yu seçin
2. Feature branch oluşturun
3. Kodlarınızı yazın ve test edin
4. Pull request gönderin

### Code Style
- C# coding conventions
- SOLID principles
- Clean code practices
- Comprehensive unit testing

## 📝 Changelog

### v1.0.0 (Mevcut)
- İlk stable release
- Temel ses komut özellikleri
- Outlook entegrasyonu
- TTS sistemi
- Güvenlik altyapısı

## 📄 Lisans

Bu proje özel kullanım için geliştirilmiştir. Ticari kullanım için izin gereklidir.

## 📞 İletişim

Sorularınız ve önerileriniz için:
- **Geliştirici**: Serkan
- **E-posta**: [İletişim bilgisi]
- **GitHub Issues**: [Repository link]/issues

---

**🎯 QuadroAI Pilot - Sesinizle Windows'u kontrol edin!**