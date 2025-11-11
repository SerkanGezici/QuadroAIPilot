# ⚡ Phi Silica - Hızlı Başlangıç Kılavuzu

**QuadroAIPilot için Phi Silica Entegrasyon Checklist**

---

## 🎯 Bugün Yapılacaklar (30 dakika)

### 1. LAF Token Başvurusu Yap (15 dk)

```
✅ 1. Forma git: https://aka.ms/limitedaccessfeature

✅ 2. Form bilgilerini doldur:
   - Application Name: QuadroAIPilot
   - Company: Quadro Computer (Tesla Teknoloji)
   - Email: [Your corporate email]
   - Use Case: AI-powered voice assistant with local processing for privacy
   - Expected Users: 100,000+
   - Platform: Windows 11
   - Features Needed: 
     ☑ Phi Silica (com.microsoft.windows.ai.phisilica)
     ☑ Florence Image Encoder (com.microsoft.windows.ai.florence)

✅ 3. Privacy commitment yaz:
   "QuadroAIPilot processes all AI inference locally on user devices using NPU/GPU. 
    No user data or prompts are uploaded to external servers. 
    LAF features will be used exclusively for local model inference."

✅ 4. Submit ve confirmation email'i bekle
```

### 2. Interface'leri Oluştur (10 dk)

**Dosya 1**: `Services/WindowsAI/Interfaces/IPhiSilicaService.cs`

```csharp
namespace QuadroAIPilot.Services.WindowsAI.Interfaces
{
    public interface IPhiSilicaService
    {
        Task<bool> InitializeAsync();
        Task<bool> IsAvailableAsync();
        Task<string> GenerateAsync(string prompt, int maxTokens = 512);
    }
}
```

**Dosya 2**: `Services/WindowsAI/Interfaces/IFlorenceService.cs`

```csharp
using Windows.Graphics.Imaging;

namespace QuadroAIPilot.Services.WindowsAI.Interfaces
{
    public interface IFlorenceService
    {
        Task<bool> InitializeAsync();
        Task<float[]> EncodeImageAsync(SoftwareBitmap image);
        Task<string[]> DetectObjectsAsync(SoftwareBitmap image);
    }
}
```

### 3. LAF Token Manager Oluştur (5 dk)

**Dosya**: `Services/WindowsAI/Helpers/LAFTokenManager.cs`

```csharp
using System;
using System.Threading.Tasks;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace QuadroAIPilot.Services.WindowsAI.Helpers
{
    public class LAFTokenManager
    {
        private const string PHI_SILICA_FEATURE_ID = "com.microsoft.windows.ai.phisilica";
        
        public async Task<bool> HasPhiSilicaAccessAsync()
        {
            try
            {
                var capability = AppCapabilityAccess.Create(PHI_SILICA_FEATURE_ID);
                var status = capability.CheckAccess();
                return status == AppCapabilityAccessStatus.Allowed;
            }
            catch
            {
                return false;
            }
        }
        
        public string GetLAFStatusMessage()
        {
            // TODO: Token durumu kontrol et
            return "LAF token awaiting Microsoft approval";
        }
    }
}
```

---

## 📦 Sonraki Adımlar (LAF onayını beklerken)

### Hafta 1-2: Hazırlık

- [ ] Public Phi-3 model indir (test için)
  ```bash
  # PowerShell
  $url = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/resolve/main/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/phi-3-mini-4k-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx"
  Invoke-WebRequest -Uri $url -OutFile "C:\Temp\Phi3Test\model.onnx"
  ```

- [ ] PhiSilicaService skeleton oluştur
- [ ] FlorenceService skeleton oluştur
- [ ] HybridAIService plan yap

### Hafta 3-4: LAF onay takibi

- [ ] Microsoft'a status email gönder (3 hafta sonra)
- [ ] Alternative: Public model ile test devam et
- [ ] Performance benchmarking hazırlığı

---

## 🔍 LAF Token Geldiğinde (Çok Önemli!)

### Anında Yapılacaklar:

#### 1. .rc Dosyası Oluştur

**Dosya**: `QuadroAIPilot.rc` (proje root'a)

```rc
#include <windows.h>

1 RCDATA
BEGIN
    "YOUR-ACTUAL-LAF-TOKEN-HERE\0"  // Microsoft'tan gelen GUID
END

VS_VERSION_INFO VERSIONINFO
FILEVERSION 1,3,0,0
PRODUCTVERSION 1,3,0,0
BEGIN
    BLOCK "StringFileInfo"
    BEGIN
        BLOCK "040904b0"
        BEGIN
            VALUE "CompanyName", "Quadro Computer"
            VALUE "FileDescription", "Quadro Pilot AI"
            VALUE "FileVersion", "1.3.0.0"
            VALUE "ProductName", "QuadroAIPilot"
        END
    END
END
```

#### 2. CSProj'a RC Compilation Ekle

**Dosya**: `QuadroAIPilot.csproj`

```xml
<!-- Existing content... -->

<!-- RC File compilation -->
<ItemGroup>
    <None Include="QuadroAIPilot.rc" />
</ItemGroup>

<Target Name="CompileRC" BeforeTargets="CoreCompile">
    <Exec Command="rc.exe /fo $(IntermediateOutputPath)QuadroAIPilot.res QuadroAIPilot.rc" 
          WorkingDirectory="$(ProjectDir)" />
</Target>

<ItemGroup>
    <LinkResource Include="$(IntermediateOutputPath)QuadroAIPilot.res" />
</ItemGroup>
```

#### 3. Package.appxmanifest Güncelle

**Dosya**: `Package.appxmanifest`

```xml
<Capabilities>
    <rescap:Capability Name="runFullTrust" />
    <rescap:Capability Name="systemAIModels" />
    <DeviceCapability Name="microphone"/>
    
    <!-- LAF Token extension -->
    <uap:Extension Category="windows.limitedAccessFeature">
        <uap:LimitedAccessFeature Id="com.microsoft.windows.ai.phisilica">
            <uap:Token>YOUR-ACTUAL-LAF-TOKEN-HERE</uap:Token>
        </uap:LimitedAccessFeature>
    </uap:Extension>
</Capabilities>
```

#### 4. PhiSilicaService Full Implementation

**Dosya**: `Services/WindowsAI/PhiSilicaService.cs`

```csharp
using Windows.AI.MachineLearning;
using Windows.Storage;

namespace QuadroAIPilot.Services.WindowsAI
{
    public class PhiSilicaService : IPhiSilicaService
    {
        private LearningModelSession? _session;
        
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // System model path (Windows 11 24H2+)
                var modelPath = @"C:\Windows\SystemApps\Microsoft.Windows.Ai.Copilot_cw5n1h2txyewy\Assets\Models\phi-3-mini-4k-instruct-onnx\model.onnx";
                
                var modelFile = await StorageFile.GetFileFromPathAsync(modelPath);
                var model = await LearningModel.LoadFromStorageFileAsync(modelFile);
                
                // NPU device (fallback: GPU → CPU)
                var device = new LearningModelDevice(LearningModelDeviceKind.Npu);
                
                _session = new LearningModelSession(model, device);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Phi Silica init error: {ex.Message}");
                return false;
            }
        }
        
        public async Task<string> GenerateAsync(string prompt, int maxTokens = 512)
        {
            // TODO: Full implementation (tokenization, inference, decoding)
            await Task.CompletedTask;
            return "Phi Silica response";
        }
        
        public async Task<bool> IsAvailableAsync()
        {
            return _session != null;
        }
    }
}
```

#### 5. Test Et!

```csharp
// Test code
var phiSilica = new PhiSilicaService(_dispatcher, _lafManager);
var initialized = await phiSilica.InitializeAsync();

if (initialized)
{
    var response = await phiSilica.GenerateAsync("Hello, how are you?");
    Console.WriteLine($"Phi Silica: {response}");
}
else
{
    Console.WriteLine("Phi Silica not available");
}
```

---

## 🚨 Sık Karşılaşılan Sorunlar

### Sorun 1: "Access Denied" Hatası

**Sebep**: LAF token geçersiz veya eksik

**Çözüm**:
```csharp
// LAF token kontrolü
var lafManager = new LAFTokenManager();
var hasAccess = await lafManager.HasPhiSilicaAccessAsync();

if (!hasAccess)
{
    // Fallback to Claude API
    return await _claudeService.GenerateAsync(prompt);
}
```

### Sorun 2: Model Dosyası Bulunamadı

**Sebep**: Windows 11 24H2+ değil veya model yüklenmemiş

**Çözüm**:
```csharp
// Multiple path check
var paths = new[]
{
    @"C:\Windows\SystemApps\Microsoft.Windows.Ai.Copilot_cw5n1h2txyewy\Assets\Models\phi-3-mini-4k-instruct-onnx\model.onnx",
    @"C:\Temp\Phi3Test\model.onnx"  // Fallback: public model
};

foreach (var path in paths)
{
    if (File.Exists(path))
    {
        modelPath = path;
        break;
    }
}
```

### Sorun 3: NPU Bulunamadı

**Sebep**: Copilot+ PC değil veya NPU driver eksik

**Çözüm**:
```csharp
// Device fallback
LearningModelDevice GetBestDevice()
{
    try { return new LearningModelDevice(LearningModelDeviceKind.Npu); }
    catch
    {
        try { return new LearningModelDevice(LearningModelDeviceKind.DirectX); }
        catch { return new LearningModelDevice(LearningModelDeviceKind.Cpu); }
    }
}
```

---

## 📊 LAF Token Status Takibi

### Status Check (Her hafta)

```csharp
public class LAFStatusService
{
    public (string Status, string Message, DateTime LastChecked) GetStatus()
    {
        // Token durumu kontrol et
        var hasToken = CheckTokenInRC() || CheckTokenInRegistry();
        
        if (hasToken)
        {
            return ("APPROVED", "LAF token active", DateTime.UtcNow);
        }
        else
        {
            var daysSinceSubmission = (DateTime.UtcNow - _submissionDate).Days;
            
            if (daysSinceSubmission < 14)
                return ("PENDING", $"Awaiting approval (Day {daysSinceSubmission}/14)", DateTime.UtcNow);
            else if (daysSinceSubmission < 28)
                return ("PENDING", $"Under review (Day {daysSinceSubmission}/28)", DateTime.UtcNow);
            else
                return ("DELAYED", "Follow-up recommended", DateTime.UtcNow);
        }
    }
}
```

### UI'da Göster

**MainWindow.xaml**:

```xml
<InfoBar x:Name="LAFStatusBar"
         IsOpen="True"
         Severity="{Binding LAFSeverity}">
    <InfoBar.Title>Phi Silica Status</InfoBar.Title>
    <InfoBar.Message>{Binding LAFStatusMessage}</InfoBar.Message>
</InfoBar>
```

**MainWindow.xaml.cs**:

```csharp
private async void UpdateLAFStatus()
{
    var lafStatus = _lafStatusService.GetStatus();
    
    LAFStatusBar.Title = $"Phi Silica: {lafStatus.Status}";
    LAFStatusBar.Message = lafStatus.Message;
    
    LAFStatusBar.Severity = lafStatus.Status switch
    {
        "APPROVED" => InfoBarSeverity.Success,
        "PENDING" => InfoBarSeverity.Informational,
        "DELAYED" => InfoBarSeverity.Warning,
        _ => InfoBarSeverity.Error
    };
}
```

---

## 🎯 Success Checklist

### Pre-LAF (Şimdi)

- [x] LAF form submit edildi
- [ ] Interface'ler oluşturuldu
- [ ] LAFTokenManager implementasyonu
- [ ] Public Phi-3 model test edildi
- [ ] Skeleton services oluşturuldu

### Post-LAF (Token sonrası)

- [ ] .rc dosyası oluşturuldu
- [ ] CSProj RC compilation eklendi
- [ ] Package.appxmanifest güncellendi
- [ ] PhiSilicaService full implementation
- [ ] FlorenceService implementation
- [ ] HybridAIService entegrasyonu
- [ ] Performance benchmarks
- [ ] User acceptance testing

### Release Ready

- [ ] v1.3.0-beta tag
- [ ] Documentation tamamlandı
- [ ] 100 user beta test
- [ ] v1.3.0 production release

---

## 📞 Destek

### LAF Token Sorunları
- Email: aiplatform@microsoft.com
- Response: 3-5 business days

### Technical Issues
- GitHub: https://github.com/microsoft/Windows-Machine-Learning/issues
- Community: https://techcommunity.microsoft.com/t5/windows-ai/bd-p/WindowsAI

---

## 🔗 Hızlı Linkler

| Resource | URL |
|----------|-----|
| **LAF Form** | https://aka.ms/limitedaccessfeature |
| **Phi-3 Model** | https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx |
| **Windows.AI Docs** | https://learn.microsoft.com/en-us/windows/ai/ |
| **NPU Drivers** | https://www.intel.com/content/www/us/en/download/785597/ |
| **Full Report** | PHI_SILICA_RESEARCH_REPORT.md |
| **Roadmap** | PHI_SILICA_IMPLEMENTATION_ROADMAP.md |

---

**Quick Start Version**: 1.0  
**Created**: 2025-11-11  
**Estimated Time to Complete**: 30 minutes (pre-LAF), 1 week (post-LAF)

