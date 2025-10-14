# Profesyonel Mail Style Learning Engine Geliştirme Planı

## Özet
Kullanıcının mail yazma stilini otomatik öğrenen ve kişiye özel mail önerileri sunan profesyonel bir sistem.

## Mevcut Durum Analizi
- **ResponseLearningService**: Manuel öğrenme ile basit stil profili
- **RealOutlookReader**: COM API ile mail okuma (tek tek)
- **Eksikler**: 
  - Otomatik stil öğrenme yok
  - Kişi bazlı profil tutulmuyor
  - Geçmiş mail analizi yapılmıyor

## Önerilen Sistem Mimarisi

### 1. Yeni Servisler

#### MailStyleAnalyzer.cs
- Gönderilmiş mailleri toplu analiz
- Son 200 mail üzerinden genel profil
- Kişi bazlı son 20-50 mail analizi

#### RecipientProfileManager.cs
- Her alıcı için ayrı stil profili
- Profil güncelleme ve cache yönetimi
- Kişi-stil eşleştirme

#### MailContentAnalyzer.cs
- NLP tabanlı stil analizi
- Pattern recognition
- Sentiment analizi
- Formalite seviyesi tespiti

#### MailTemplateGenerator.cs
- Kişiye özel mail şablonları
- Dinamik öneri sistemi
- Bağlam-duyarlı cevaplar

#### SecureMailStyleStorage.cs
- Şifreli veri saklama
- GDPR uyumlu silme
- Veri anonimleştirme

### 2. Model Sınıfları

```csharp
public class RecipientStyleProfile
{
    public string RecipientEmail { get; set; }
    public string RecipientName { get; set; }
    public DateTime LastAnalyzed { get; set; }
    public int MailCount { get; set; }
    
    // Stil Metrikleri
    public FormalityLevel Formality { get; set; }
    public ToneType PreferredTone { get; set; }
    public double AverageSentenceLength { get; set; }
    public List<string> CommonGreetings { get; set; }
    public List<string> CommonClosings { get; set; }
    public Dictionary<string, int> FrequentPhrases { get; set; }
    
    // İlişki Metrikleri
    public RelationshipType Relationship { get; set; } // İş, Arkadaş, Aile
    public double ResponseTime { get; set; } // Ortalama cevap süresi
    public List<string> CommonTopics { get; set; }
}

public class MailAnalysisResult
{
    public string MailId { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public StyleMetrics Metrics { get; set; }
    public List<string> ExtractedPatterns { get; set; }
}

public class StyleMetrics
{
    public double FormalityScore { get; set; }
    public double PolitenessScore { get; set; }
    public double DirectnessScore { get; set; }
    public double EmotionalTone { get; set; }
    public List<string> KeyPhrases { get; set; }
}
```

### 3. Özellikler

#### Otomatik Öğrenme
1. İlk kurulumda son 200 gönderilmiş mail analizi
2. Her yeni mail gönderiminde profil güncelleme
3. Haftalık arka plan analizi

#### Kişiye Özel Profiller
1. Her alıcı için ayrı stil dosyası
2. İlişki tipi tespiti (iş/arkadaş/aile)
3. Konuya göre stil değişimi

#### Akıllı Öneri Sistemi
1. Alıcıya göre otomatik selamlama
2. Bağlam-duyarlı mail gövdesi
3. Uygun kapanış ve imza

### 4. Kullanım Senaryoları

#### Senaryo 1: CEO'ya Mail
```
Sistem tespit eder:
- Yüksek formalite
- "Sayın [İsim] Bey/Hanım" hitabı
- Kurumsal dil
- "Saygılarımla" kapanışı

Öneri:
"Sayın Mehmet Bey,
[Kullanıcı içeriği profesyonel dile çevrilir]
Saygılarımla,"
```

#### Senaryo 2: Takım Arkadaşına Mail
```
Sistem tespit eder:
- Orta formalite
- "Merhaba [İsim]" hitabı
- Samimi ama profesyonel
- "İyi çalışmalar" kapanışı
```

### 5. Teknik Detaylar

#### Veri Toplama
```csharp
// Batch mail okuma
var sentMails = await reader.GetSentEmailsAsync(200);
var recipientMails = sentMails
    .Where(m => m.RecipientEmail == targetEmail)
    .Take(50);
```

#### Stil Analizi
```csharp
// NLP analiz
var analyzer = new MailContentAnalyzer();
var metrics = analyzer.AnalyzeStyle(mailContent);
var patterns = analyzer.ExtractPatterns(mailContent);
```

#### Güvenlik
- AES-256 şifreleme
- Kullanıcı onayı sistemi
- Veri saklama süresi ayarları
- Anonimleştirme seçeneği

### 6. UI/UX Güncellemeleri

#### Stil Öğrenme Ekranı
- Progress bar ile analiz durumu
- "200 mail analiz ediliyor..."
- Atlama seçeneği

#### Kişi Profilleri Ekranı
- Kişi listesi ve stil özeti
- Detaylı stil metrikleri
- Manuel düzenleme imkanı

#### Mail Yazma Asistanı
- Gerçek zamanlı öneri
- Alternatif ifadeler
- Ton ayarlama slider'ı

### 7. Performans Optimizasyonu

- Asenkron mail okuma
- Batch processing
- Cache mekanizması
- Lazy loading
- Background worker pattern

### 8. Privacy ve Etik

- Açık kullanıcı onayı
- Veri silme hakkı
- Minimum veri saklama
- Lokal işleme (cloud yok)
- Audit log

### 9. Test Stratejisi

- Unit testler (her servis için)
- Integration testler
- Performance testler (1000+ mail)
- Memory leak testleri
- UI/UX testleri

### 10. Implementasyon Aşamaları

**Faz 1: Temel Altyapı (2 hafta)**
- Model sınıfları
- Veri erişim katmanı
- Basit analiz engine

**Faz 2: Analiz Motor (3 hafta)**
- NLP entegrasyonu
- Pattern recognition
- Metrik hesaplama

**Faz 3: UI Entegrasyonu (2 hafta)**
- Yeni ekranlar
- Progress göstergeleri
- Ayarlar paneli

**Faz 4: Optimizasyon (1 hafta)**
- Performance tuning
- Cache implementasyonu
- Bug fixing

**Faz 5: Test ve Polish (1 hafta)**
- Kapsamlı testler
- Kullanıcı geri bildirimleri
- Final düzenlemeler

## Beklenen Faydalar

1. **Zaman Tasarrufu**: %70 daha hızlı mail yazma
2. **Tutarlılık**: Her kişiye uygun ton
3. **Profesyonellik**: Doğru hitap ve üslup
4. **Öğrenme**: Sürekli gelişen sistem

## Notlar

- Bu plan daha sonra implementasyon için saklanmıştır
- Mevcut ResponseLearningService temel olarak kullanılabilir
- RealOutlookReader'a batch okuma özellikleri eklenecek
- Güvenlik ve privacy öncelikli olacak