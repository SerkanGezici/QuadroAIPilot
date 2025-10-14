# 🚀 Quadro Pilot AI - Akıllı Asistan

## Proje Tanımı
C# WPF masaüstü uygulaması - AI destekli sohbet asistanı interface.

## Teknik Detaylar
- **Platform:** C# .NET Framework
- **UI:** WPF (Windows Presentation Foundation)
- **Build:** MSBuild
- **Yapı:** Desktop application + Claude API entegrasyonu

## Çalışma Kuralları
- C# kod tabanı
- WPF XAML UI bileşenleri
- MSBuild ile derleme
- Visual Studio 2022 Community
- Platform: x64

## Tool Kullanımı
- Read/Write/Edit araçları için .cs ve .xaml dosyaları
- Bash tool için MSBuild komutları
- Derleme: `/mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe`

## Build Komutu
```bash
"/mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" QuadroAIPilot.csproj -p:Configuration=Debug -p:Platform=x64
```

## Önemli Notlar
- Bu proje C# WPF desktop uygulamasıdır
- Claude Codex (Node.js projesi) ile KARIŞTIRILMAMALI
- Kod değişikliklerinde projeyi derle ve test et
- Browser Extensions klasöründe tarayıcı eklentisi kodları var
