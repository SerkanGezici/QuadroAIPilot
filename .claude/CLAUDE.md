# 🚀 Quadro Pilot AI - Akıllı Asistan

## Proje Tanımı
C# WinUI 3 masaüstü uygulaması - AI destekli sohbet asistanı interface.

## Teknik Detaylar
- **Platform:** C# .NET 8.0 (net8.0-windows)
- **UI:** WinUI 3 (Windows App SDK 1.7)
- **Build:** MSBuild
- **Yapı:** Desktop application + Claude API entegrasyonu
- **Target OS:** Windows 10 (19041) ve üzeri
- **Modern Features:** Acrylic/Mica backdrop, native WebView2, modern XAML controls

## Çalışma Kuralları
- C# kod tabanı
- WinUI 3 XAML UI bileşenleri (Microsoft.UI.Xaml namespace)
- MSBuild ile derleme
- Visual Studio 2022 Community
- Platform: x64 (x86, ARM64 destekli)

## Tool Kullanımı
- Read/Write/Edit araçları için .cs ve .xaml dosyaları
- Bash tool için MSBuild komutları
- Derleme: `/mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe`

## Build Komutu
```bash
"/mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" QuadroAIPilot.csproj -p:Configuration=Debug -p:Platform=x64
```

## Önemli Notlar
- Bu proje C# WinUI 3 desktop uygulamasıdır (WPF DEĞİL!)
- .NET 8.0 ve Windows App SDK kullanır
- Modern Windows 11 UI/UX özellikleri desteklenir
- Claude Codex (Node.js projesi) ile KARIŞTIRILMAMALI
- Kod değişikliklerinde projeyi derle ve test et
- Browser Extensions klasöründe tarayıcı eklentisi kodları var

## WinUI 3 vs WPF Farkları
- **Namespace:** Microsoft.UI.Xaml (WPF'de System.Windows)
- **Modern UI:** Mica, Acrylic backdrop efektleri
- **WebView2:** Native kontrol (WPF'de eklenti)
- **Performans:** Daha iyi GPU hızlandırma
- **Windows 11:** Native design language desteği
