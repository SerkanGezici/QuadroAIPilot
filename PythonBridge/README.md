# ChatGPT Bridge - QuadroAIPilot

## 📌 Nedir?

ChatGPT Bridge, QuadroAIPilot'un ChatGPT ile iletişim kurmasını sağlayan Python tabanlı HTTP sunucusudur.

**✨ Otomatik Başlatma:** Uygulama açıldığında bridge otomatik başlar, kapandığında otomatik temizlenir.

## 🚀 Kurulum (Bir Kere Yapılır)

### 1. Python Gereksinimleri Yükleyin

```bash
cd PythonBridge
install_dependencies.bat
```

Bu komut şunları yükler:
- `playwright==1.40.0` (Browser automation)
- `websockets==12.0` (WebSocket desteği)
- Playwright Chromium browser

### 2. İlk Giriş (Sadece İlk Kullanımda)

**MANUEL BAŞLATMA (Sadece ilk giriş için):**

```bash
cd PythonBridge
python chatgpt_http_bridge.py
```

- **Headless Mode:** Arka planda çalışır, pencere görünmez
- **İlk Kullanım:** ChatGPT'ye giriş için headless=False yapın (satır 49)
- **Sonraki Kullanımlar:** Session kaydedilir, otomatik giriş yapar

**NOT:** İlk kez kullanıyorsanız:
1. `chatgpt_http_bridge.py` → Satır 49 → `headless=False` yapın
2. Script'i manuel çalıştırın: `python chatgpt_http_bridge.py`
3. Chrome penceresi açılacak → ChatGPT'ye giriş yapın
4. Giriş yaptıktan sonra script'i durdurun (Ctrl+C)
5. `headless=True` geri yapın (arka plan modu)
6. Artık QuadroAIPilot otomatik başlatacak

## 🔧 Nasıl Çalışır?

1. **Otomatik Başlatma:** QuadroAIPilot açılınca 3 saniye sonra bridge başlar
2. **HTTP Server:** Localhost:8765 portunda çalışır
3. **Playwright Headless:** Arka planda Chromium çalışır (pencere yok)
4. **Persistent Profile:** Chrome profili kaydedilir (her seferde giriş yapmaya gerek kalmaz)
5. **Otomatik Temizleme:** Uygulama kapanınca bridge temizlenir

## 🎯 QuadroAIPilot ile Kullanım

Bridge otomatik başladıktan sonra:

1. **Ayarlar** → **Varsayılan Yapay Zeka** → **ChatGPT** seçin
2. AI moduna geçin: "AI moduna geç"
3. Soru sorun: "ChatGPT, Python nedir?"

### Sesli Komutlar

- **"ChatGPT'ye geç"** → ChatGPT kullan
- **"Claude'a geç"** → Claude kullan (fallback)

### Smart Fallback

Bridge çalışmazsa otomatik Claude'a geçer:
```
[Kullanıcı] "ChatGPT, Python nedir?"
[Sistem] "ChatGPT erişilemiyor. Claude kullanılıyor." (sesli)
[Claude] Python hakkında yanıt verir
```

## 📡 API Endpointleri

### Health Check
```bash
GET http://localhost:8765/health
Response: {"status": "ok", "ready": true}
```

### Chat
```bash
POST http://localhost:8765/chat
Body: {"message": "Merhaba ChatGPT!"}
Response: {"error": false, "content": "...", "timestamp": "..."}
```

### Reset Session
```bash
POST http://localhost:8765/reset
Response: {"status": "ok"}
```


## ⚙️ Ayarlar

### Headless Mode (Görünürlük)

**Varsayılan:** `headless=True` (pencere yok, arka planda çalışır)

Eğer debugging için Chrome penceresini görmek isterseniz:

```python
# chatgpt_http_bridge.py - Satır 49
headless=False  # Chrome penceresi açılır
```

**Önerilen:** İlk giriş için `False`, sonra `True` yapın.

### Manuel Python Path (Opsiyonel)

Eğer system Python yerine özel Python kullanmak isterseniz:

```python
# chatgpt_http_bridge.py içinde
pythonPath = "C:\\Path\\To\\Python\\python.exe"
```

### Port Değiştirme

```python
# chatgpt_http_bridge.py içinde (satır 227)
server = HTTPServer(('127.0.0.1', 8765), ChatGPTHandler)
# 8765 yerine başka port kullanabilirsiniz
```

**NOT:** Port değiştirirseniz `ChatGPTBridgeService.cs` içinde de güncelleyin (satır 14).

## 🔍 Sorun Giderme

### Bridge başlamıyor

```bash
# Python kontrolü
python --version
# Python 3.8+ gerekli

# Paket kontrolü
python -m pip list | findstr playwright
python -m pip list | findstr websockets
```

### ChatGPT'ye erişilemiyor

1. Bridge çalışıyor mu? → `http://localhost:8765/health` kontrol edin
2. ChatGPT'ye giriş yaptınız mı? (İlk kullanımda manuel giriş gerekli)
3. Firewall/Antivirus engelliyor mu?
4. **Headless mode sorunu:** İlk kullanımda `headless=False` yapıp giriş yaptınız mı?

### Playwright hatası

```bash
# Playwright browser'ları yeniden yükle
python -m playwright install chromium
```

## 📂 Dosyalar

- `chatgpt_http_bridge.py` → Ana HTTP server
- `chatgpt_bridge.py` → WebSocket bridge (eski, kullanılmıyor)
- `requirements.txt` → Python dependencies
- `install_dependencies.bat` → Kurulum scripti
- `chrome-profile/` → Persistent Chrome profili (otomatik oluşur)
- `chatgpt_bridge.log` → Log dosyası

## 🔐 Güvenlik

- Bridge sadece **localhost (127.0.0.1)** üzerinden çalışır
- External erişim yoktur
- ChatGPT session bilgileri `chrome-profile/` klasöründe saklanır

## 💡 İpuçları

1. **İlk Kullanım:** Bridge'i manuel başlatın, ChatGPT'ye giriş yapın, sonra QuadroAIPilot'u kullanın
2. **Persistent Session:** Giriş bilgileri kaydedilir, her seferde giriş yapmaya gerek yoktur
3. **Fallback:** ChatGPT çalışmazsa otomatik Claude'a geçilir

## 📝 Loglar

Bridge logları:
- **Konsol:** Real-time output
- **Dosya:** `chatgpt_bridge.log`

QuadroAIPilot logları:
- `%LOCALAPPDATA%\QuadroAIPilot\Logs\`
