# Claude CLI Login/Logout Yönetimi - Uygulama Planı

**Tarih:** 24 Kasım 2025
**Durum:** Beklemede (Daha sonra uygulanacak)

---

## 📋 Problem Özeti

- Claude CLI'dan logout yapınca "Invalid API key - please run /login" hatası
- Uygulama hiçbir API key yönetimi yapmıyor
- `~/.claude/.credentials.json` dosyası logout ile siliniyor
- Kullanıcı her logout sonrası manuel login yapmak zorunda

---

## 💡 Çözüm Seçenekleri

### Seçenek 1️⃣: Basit Uyarı (5 dakika)
**Yapılacaklar:**
- Settings dialog'a bilgilendirme metni
- "Claude kullanmak için CMD'de `claude setup-token` çalıştırın" mesajı

**Avantajları:** Hızlı, düşük risk
**Dezavantajları:** Manuel işlem gerekir

---

### Seçenek 2️⃣: Otomatik Login Sistemi (50 dakika) - TAM ÇÖZÜM
**Yapılacaklar:**

#### 1. ClaudeCLIService.cs - 3 Yeni Metod
```csharp
// Login durumu kontrolü
public static bool IsClaudeLoggedIn()
{
    // claude --version çalıştır
    // Error output'ta "Invalid API key" kontrolü
    // Return: true/false
}

// API key ile setup
public static async Task<bool> SetupClaudeTokenAsync(string apiKey)
{
    // API key'i Windows DPAPI ile şifrele
    // ~/.claude/auth_token.txt dosyasına kaydet
    // claude setup-token komutunu çalıştır
    // Return: başarılı/başarısız
}

// Logout
public static async Task LogoutClaudeAsync()
{
    // ~/.claude/.credentials.json sil
    // ~/.claude/auth_token.txt sil
}
```

#### 2. SettingsDialog.xaml - Claude API Key Bölümü
```xml
<Expander Header="🔐 Claude API Key">
    <StackPanel>
        <!-- Login durumu -->
        <TextBlock x:Name="ClaudeLoginStatusText" Text="Durum: Kontrol ediliyor..."/>

        <!-- API Key giriş -->
        <PasswordBox x:Name="ClaudeApiKeyBox" Header="API Key"/>

        <!-- Butonlar -->
        <Button Content="Kaydet ve Giriş Yap" Click="SaveClaudeKey_Click"/>
        <Button Content="Test Et" Click="TestClaudeKey_Click"/>
        <Button Content="Çıkış Yap" Click="LogoutClaude_Click"/>

        <!-- Yardım -->
        <HyperlinkButton Content="🔗 API Key nasıl alınır?"
                         NavigateUri="https://console.anthropic.com/"/>
    </StackPanel>
</Expander>
```

#### 3. SettingsDialog.xaml.cs - Event Handlers
```csharp
private async void SaveClaudeKey_Click(object sender, RoutedEventArgs e)
{
    var apiKey = ClaudeApiKeyBox.Password;
    var success = await ClaudeCLIService.SetupClaudeTokenAsync(apiKey);
    ClaudeLoginStatusText.Text = success ? "✅ Giriş başarılı" : "❌ Hata";
}

private async void TestClaudeKey_Click(object sender, RoutedEventArgs e)
{
    var isLoggedIn = ClaudeCLIService.IsClaudeLoggedIn();
    ClaudeLoginStatusText.Text = isLoggedIn ? "✅ Aktif" : "❌ Login gerekli";
}

private async void LogoutClaude_Click(object sender, RoutedEventArgs e)
{
    await ClaudeCLIService.LogoutClaudeAsync();
    ClaudeLoginStatusText.Text = "⚠️ Çıkış yapıldı";
}
```

#### 4. AIMode.cs - Login Kontrolü
```csharp
public void Enter()
{
    // Mevcut kontroller...

    if (!ClaudeCLIService.IsClaudeLoggedIn())
    {
        LogService.LogWarning("[AIMode] Claude not logged in");

        await TextToSpeechService.SpeakTextAsync(
            "Claude kullanmak için API key gerekli. Ayarlardan yapılandırabilirsiniz.");

        SendToWebView("aiWarning", new
        {
            message = "⚠️ Claude API key gerekli (Ayarlar → Claude API Key)"
        });

        return; // ChatGPT/Gemini fallback devam eder
    }

    // Normal akış devam eder...
}
```

**Avantajları:** Tam otomatik, kullanıcı dostu
**Dezavantajları:** 150 satır kod, API key güvenliği

---

### Seçenek 3️⃣: Hybrid - Sadece Durum Kontrolü (20 dakika) ⭐ ÖNERİLEN
**Yapılacaklar:**

#### 1. ClaudeCLIService.cs
```csharp
// Sadece login durumu kontrolü
public static bool IsClaudeLoggedIn()
{
    // claude --version error output kontrolü
    // "Invalid API key" varsa logged out
}
```

#### 2. AIMode.cs
```csharp
// Login yoksa uyarı ver
if (!ClaudeCLIService.IsClaudeLoggedIn())
{
    await SpeakAsync("Claude login gerekli. Ayarlara gidin.");
}
```

#### 3. Settings'te Durum Göster
```
⚠️ Claude Durumu: Login Gerekli
💡 CMD'de şu komutu çalıştırın: claude setup-token
```

**Avantajları:** Logout tespiti, otomatik uyarı, az kod
**Dezavantajları:** Login yine manuel

---

## 📊 Karşılaştırma

| Özellik | Seçenek 1 | Seçenek 2 | Seçenek 3 |
|---------|-----------|-----------|-----------|
| Logout tespiti | ❌ | ✅ | ✅ |
| Otomatik uyarı | ❌ | ✅ | ✅ |
| Uygulama içi login | ❌ | ✅ | ❌ |
| API key yönetimi | ❌ | ✅ | ❌ |
| Süre | 5 dk | 50 dk | 20 dk |
| Kod | 10 satır | 150 satır | 50 satır |
| Risk | Çok düşük | Orta | Düşük |

---

## 🎯 Önerilen Çözüm

**Seçenek 3** - Şu anda en mantıklısı:
- Logout durumunu tespit eder
- Otomatik uyarı verir
- Az kod (~50 satır)
- Düşük risk
- API key güvenliği sorunu yok

---

## 🔒 Güvenlik Notları (Seçenek 2 için)

- API key **Windows DPAPI** ile şifreli
- Sadece current user okuyabilir
- Log'larda API key asla görünmez
- Memory'de plaintext tutulmaz

---

## ✅ Test Senaryoları

1. Yeni kurulumda API key girişi
2. Geçerli key ile giriş
3. Geçersiz key ile hata
4. Manuel logout sonrası uyarı
5. Fallback (ChatGPT/Gemini) çalışması
6. API key değişikliği

---

## 📝 Dosya Değişiklikleri

### Seçenek 2 (Tam Çözüm):
1. **ClaudeCLIService.cs** - 3 yeni metod (80 satır)
2. **SettingsDialog.xaml** - 1 yeni Expander (30 satır)
3. **SettingsDialog.xaml.cs** - 3 yeni event handler (30 satır)
4. **AIMode.cs** - Login kontrolü (10 satır)

**Toplam:** ~150 satır

### Seçenek 3 (Önerilen):
1. **ClaudeCLIService.cs** - 1 metod (30 satır)
2. **AIMode.cs** - Login kontrolü (10 satır)
3. **SettingsDialog.xaml** - Bilgi bölümü (10 satır)

**Toplam:** ~50 satır

---

## 📌 Sonraki Adımlar

1. Seçenek belirle (1, 2 veya 3)
2. Kod implementasyonu
3. Test (logout senaryoları)
4. Setup v41 oluştur
5. Diğer PC'de test

---

## 📞 Karar Soruları

1. **Hangi seçeneği tercih ediyorsun?**
   - Seçenek 1 (Basit uyarı)
   - Seçenek 2 (Tam otomatik)
   - Seçenek 3 (Hybrid - önerilen)

2. **Uygulama içinden API key girişi önemli mi?**
   - Evet → Seçenek 2
   - Hayır → Seçenek 3

3. **Zaman kısıtı var mı?**
   - Evet → Seçenek 1 veya 3
   - Hayır → Seçenek 2

---

**Not:** Bu plan daha sonra uygulanacak. Şimdilik mevcut sistem çalışıyor (login durumunda).
