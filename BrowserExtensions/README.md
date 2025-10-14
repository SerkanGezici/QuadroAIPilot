# QuadroAI Tarayıcı Uzantıları

Bu klasör QuadroAI Pilot için tarayıcı uzantılarını içerir.

## Özellikler

- ✅ Seçili metni sağ tık menüsünden QuadroAI'ya gönderme
- ✅ Otomatik kopyalama (Ctrl+C)
- ✅ HTTP trigger ile QuadroAI Pilot'u tetikleme
- ✅ Çeviri ve TTS için QuadroAI'nın Edge WebView2 özelliklerini kullanma

## Desteklenen Tarayıcılar

- **Chrome** (Manifest V3) ✅
- **Edge** (Özel optimize edilmiş versiyon - Manifest V3) ✅
- **Firefox** (Manifest V2) ⚠️ *Test edilmedi*

## Kurulum

Her tarayıcı için ilgili klasördeki README.md dosyasına bakın:
- [Chrome Kurulum](./Chrome/README.md)
- [Edge Kurulum](./Edge/README.md) - **Microsoft Edge için önerilir**
- [Firefox Kurulum](./Firefox/README.md)

## Kullanım Akışı

1. Kullanıcı web sayfasında metin seçer
2. Sağ tık → "QuadroAI ile Oku"
3. Uzantı metni panoya kopyalar (Ctrl+C)
4. QuadroAI Pilot'a HTTP isteği gönderir
5. QuadroAI:
   - Panodan metni alır
   - Edge WebView2'de Türkçe'ye çevirir
   - Edge TTS ile seslendirir

## Gereksinimler

- QuadroAI Pilot uygulamasının çalışır durumda olması
- Port 19741'in açık olması

## İkon Oluşturma

PNG ikonları otomatik oluşturmak için Chrome veya Edge klasöründe:
```bash
python3 create_simple_icon.py
```

Bu script şu boyutlarda PNG ikonları oluşturur:
- icon16.png (16x16)
- icon48.png (48x48)
- icon128.png (128x128)

## Geliştirme

Uzantılarda değişiklik yaptıktan sonra:
1. Tarayıcıda uzantıyı yeniden yükleyin
2. QuadroAI Pilot'u yeniden başlatın (gerekirse)