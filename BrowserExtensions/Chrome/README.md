# QuadroAI Chrome Uzantısı

## Kurulum

### Chrome
1. Chrome'da `chrome://extensions/` adresine gidin
2. Sağ üstte "Geliştirici modu"nu açın
3. "Paketlenmemiş uzantı yükle" butonuna tıklayın
4. Bu klasörü seçin

### Edge (Alternatif)
Edge kullanıcıları için özel optimize edilmiş versiyon için `../Edge` klasörünü kullanın.

## Kullanım

1. QuadroAI Pilot uygulamasının açık olduğundan emin olun
2. Herhangi bir web sayfasında metin seçin
3. Sağ tık yapın ve "QuadroAI ile Oku" seçeneğini tıklayın
4. Metin otomatik olarak Türkçe'ye çevrilip okunacaktır

## Özellikler

- ✅ Manifest V3 desteği
- ✅ Seçili metni sağ tık menüsünden okutma
- ✅ Otomatik clipboard kopyalama
- ✅ HTTP trigger ile QuadroAI Pilot entegrasyonu
- ✅ Chrome ve Edge uyumluluğu

## İkon Gereksinimleri

Bu klasörde aşağıdaki PNG ikonları bulunmalıdır:
- icon16.png (16x16 piksel)
- icon48.png (48x48 piksel)
- icon128.png (128x128 piksel)

### İkon Oluşturma
PNG ikonları oluşturmak için `create_simple_icon.py` script'ini kullanabilirsiniz:
```bash
python3 create_simple_icon.py
```

## Sorun Giderme

### "QuadroAI Pilot uygulaması bulunamadı" hatası
- QuadroAI Pilot uygulamasının çalıştığından emin olun
- Windows Güvenlik Duvarı'nda 19741 portuna izin verin
- Antivirüs yazılımının bağlantıyı engellemediğinden emin olun

### Uzantı yüklenmiyor
- Chrome'un güncel olduğundan emin olun
- Geliştirici modunun açık olduğunu kontrol edin
- Klasörde tüm gerekli dosyaların bulunduğundan emin olun

## Güvenlik Notları

Bu uzantı yalnızca localhost (127.0.0.1:19741) adresine bağlanır ve dış kaynaklara veri göndermez.