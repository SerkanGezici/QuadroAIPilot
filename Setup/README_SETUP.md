# QuadroAIPilot Setup Oluşturma Kılavuzu

## 🚀 Hızlı Başlangıç

Setup dosyası oluşturmak için:

```batch
cd Setup
build_setup.bat
```

Bu komut:
1. ✅ Projeyi temizler
2. ✅ Release modunda publish eder
3. ✅ Inno Setup ile installer oluşturur
4. ✅ Version numarasını otomatik artırır
5. ✅ Dosya boyutunu doğrular (115-120 MB olmalı)

---

## 📋 Gereksinimler

### 1. .NET SDK 8.0
```batch
dotnet --version
# 8.0.x görmeli
```

### 2. Inno Setup 6
Kurulum konumları:
- `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`
- `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`

İndirme: https://jrsoftware.org/isdl.php

---

## 📁 Dosya Yapısı

```
Setup/
├── QuadroAIPilot.iss          # Ana Inno Setup script
├── build_setup.bat            # Otomatik build script
├── build_version.txt          # Mevcut build numarası (örn: 24)
├── Scripts/
│   ├── InstallPythonOptimized.bat
│   ├── edge-tts-nossl.py      # ⚠️ KRİTİK: SSL bypass için
│   └── ...
└── Prerequisites/
    ├── MicrosoftEdgeWebView2Setup.exe
    └── VC_redist.x64.exe
```

---

## ⚠️ Önemli Notlar

### 1. edge-tts-nossl.py Dosyası
Bu dosya **mutlaka** Inno Setup script'ine eklenmiş olmalı:

```innosetup
[Files]
Source: "Scripts\edge-tts-nossl.py"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
```

**Yoksa TTS çalışmaz!**

### 2. Build Süresi
- Publish: ~1-2 dakika
- Inno Setup: ~2-3 dakika
- **Toplam: ~5 dakika**

### 3. Dosya Boyutu
- ✅ Normal: 115-120 MB
- ❌ Bozuk: <100 MB (timeout nedeniyle yarım kalmış)

Bozuk dosya çıkarsa:
```batch
# Bozuk dosyayı sil
del Output\QuadroAIPilot_Setup_*_v24.exe

# Tekrar derle
build_setup.bat
```

---

## 🔧 Manuel Setup Oluşturma

Eğer `build_setup.bat` çalışmazsa manuel:

### Adım 1: Temizlik
```batch
dotnet clean QuadroAIPilot.csproj -c Release -p:Platform=x64
```

### Adım 2: Publish
```batch
dotnet publish QuadroAIPilot.csproj -c Release -p:Platform=x64 --self-contained -r win-x64
```

### Adım 3: Inno Setup
```batch
cd Setup
"%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" QuadroAIPilot.iss
```

### Adım 4: Version Artır
```batch
cd Setup
set /p VER=<build_version.txt
set /a NEWVER=%VER%+1
echo %NEWVER%> build_version.txt
```

---

## 🐛 Sorun Giderme

### Hata: "Setup dosyası çok küçük"
**Neden:** Inno Setup timeout nedeniyle yarım kaldı

**Çözüm:**
```batch
# Bozuk dosyayı sil
del Output\QuadroAIPilot_Setup_*_vXX.exe

# build_setup.bat'ı tekrar çalıştır
build_setup.bat
```

### Hata: "Inno Setup bulunamadı"
**Çözüm:** Inno Setup 6 yükleyin
- İndirme: https://jrsoftware.org/isdl.php
- Kurulum: Standart kurulum yeterli

### Hata: "TTS çalışmıyor (kurulum sonrası)"
**Neden:** `edge-tts-nossl.py` Inno Setup'a eklenmemiş

**Kontrol:**
```batch
# QuadroAIPilot.iss dosyasında arayın:
findstr "edge-tts-nossl.py" Setup\QuadroAIPilot.iss
```

Görmüyorsanız ekleyin:
```innosetup
Source: "Scripts\edge-tts-nossl.py"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
```

---

## 📦 Çıktı Dosyaları

Setup başarılı olduğunda:

```
Output/
└── QuadroAIPilot_Setup_1.2.1_Win11_Final_v24.exe  (117 MB)

Setup/
└── setup_build_v24.txt  (Build log)
```

---

## ✅ Doğrulama Checklist

Setup oluşturduktan sonra:

- [ ] Dosya boyutu 115-120 MB arasında
- [ ] SHA256 hash hesaplandı
- [ ] Kurulum test edildi
- [ ] `C:\Program Files\QuadroAIPilot\Scripts\edge-tts-nossl.py` var
- [ ] `%LOCALAPPDATA%\QuadroAIPilot\Python\Scripts\edge-tts-nossl.py` kopyalandı
- [ ] TTS test edildi (ses çalıyor)
- [ ] Butonlar görünüyor

---

## 🔄 Version Numaraları

`build_version.txt` dosyası son build numarasını tutar:

```
24
```

Her `build_setup.bat` çalışmasında otomatik artar: 24 → 25 → 26...

Manuel değiştirmek:
```batch
echo 25> Setup\build_version.txt
```

---

## 📝 Değişiklik Süreci

Kod değiştirdikten sonra setup oluşturma:

1. Değişiklikleri test et (Visual Studio'da F5)
2. Git commit yap
3. `Setup\build_setup.bat` çalıştır
4. Setup'ı test et
5. Başarılıysa git push yap

---

## 🚨 Kritik Dosyalar (Asla Silme!)

- `Setup/QuadroAIPilot.iss` - Ana setup script
- `Setup/Scripts/edge-tts-nossl.py` - TTS için SSL bypass
- `Setup/Scripts/InstallPythonOptimized.bat` - Python kurulum
- `Setup/build_version.txt` - Version takibi

---

## 💡 İpuçları

1. **Build öncesi:** Eski bozuk setup'ları silin
   ```batch
   del Output\QuadroAIPilot_Setup_*_v*.exe
   ```

2. **Hızlı test:** Setup'ı silent mode'da kur
   ```batch
   QuadroAIPilot_Setup_v24.exe /VERYSILENT /SUPPRESSMSGBOXES
   ```

3. **Log takibi:** Build sırasında log dosyasını izleyin
   ```batch
   tail -f Setup/setup_build_v24.txt
   ```

---

## 📞 Destek

Sorun yaşarsanız:
1. `Setup/setup_build_vXX.txt` log dosyasını kontrol edin
2. Build script'ini verbose modda çalıştırın
3. Manuel adımları takip edin

---

**Son Güncelleme:** 2025-10-17
**Build Versiyonu:** v24
**Durum:** ✅ Çalışıyor (TTS + SSL bypass)
