# QuadroAIPilot Test Stratejisi ve Eksiklik Raporu

**Tarih**: 2025-10-13
**Proje**: QuadroAIPilot - Windows AI Asistan
**Platform**: Windows 10/11, .NET 8.0, WinUI 3
**Hazırlayan**: Tester Agent

---

## ÖZET

QuadroAIPilot projesinde **hiç test altyapısı bulunmuyor**. Bu ciddi bir kalite riski oluşturuyor.

### Kritik Bulgular
- ❌ **Test Coverage**: %0 (hiç test yok)
- ❌ **Test Framework**: Kurulmamış (xUnit/NUnit/MSTest yok)
- ❌ **CI/CD Pipeline**: Otomatik test yok
- ❌ **Integration Tests**: Yok
- ❌ **UI Tests**: Yok
- ⚠️ **Manual Test Kılavuzu**: Mevcut ama otomasyon yok

---

## 1. UNIT TESTS: ❌ KRİTİK EKSİKLİK

### 1.1 Mevcut Durum
- **Toplam Test Dosyası**: 0
- **Test Framework**: Kurulmamış
- **Mock Library**: Yok
- **Test Coverage Tool**: Yok

### 1.2 Test Edilmesi Gereken Core Services

#### A. FileSearchService.cs (1380 satır) - YÜKSEK ÖNCELİK
**Dosya Yolu**: `/mnt/c/Users/serkan/source/repos/QuadroAIPilot setup so so outlook not setup deneme2/Services/FileSearchService.cs`

**Kritik Metodlar**:
```csharp
// Test edilmeli:
- FindFileAsync(string fileName, string extension)
- FindFileAsyncContains(string fileName, string extension)
- FindFileAsyncFuzzy(string fileName, string extension)
- FindMultipleFilesAsync(string fileName, string extension, int maxResults)
- FindMultipleFoldersAsync(string folderName, int maxResults)
- CalculateSimilarity(string source, string target) // Levenshtein
- LevenshteinDistance(string source, string target)
- OpenFileAsync(string filePath)
```

**Test Senaryoları (Önerilen):**
1. **Tam Eşleşme Testleri**
   - ✓ Exact file name match
   - ✓ Case insensitive match
   - ✓ Türkçe karakter normalizasyonu (ş→s, ç→c)

2. **Fuzzy Matching Testleri**
   - ✓ %70 benzerlik threshold
   - ✓ Levenshtein distance calculation
   - ✓ Kelime bazlı eşleşme

3. **Edge Cases**
   - ✓ Boş dosya adı
   - ✓ Null/empty extension
   - ✓ Uzun dosya yolu (>260 karakter)
   - ✓ Permission denied klasörler
   - ✓ Timeout senaryosu (8000ms)

4. **MRU/Recent Items Testleri**
   - ✓ Windows Recent Items parsing
   - ✓ Office MRU registry okuma
   - ✓ Shortcut (.lnk) resolution

**Mock Gereksinimleri**:
```csharp
// Mocking interfaces (oluşturulmalı):
- IFileSystem (File.Exists, Directory.GetFiles vb.)
- IStoragePermissions (MRU list erişimi)
- IRegistryAccess (Office MRU kayıtları)
```

---

#### B. CommandProcessor.cs (1636 satır) - KRİTİK ÖNCELİK
**Dosya Yolu**: `/mnt/c/Users/serkan/source/repos/QuadroAIPilot setup so so outlook not setup deneme2/Commands/CommandProcessor.cs`

**Kritik Metodlar**:
```csharp
// Test edilmeli:
- ProcessCommandAsync(string raw)
- ShouldProcessText(string text)
- DetermineExtension(string fileType)
- ExtractFolderName(string command)
- FuzzyMatchFolder(string folderName, string searchTerm)
- CalculateSimilarity(string source, string target)
```

**Test Senaryoları (Önerilen):**
1. **Komut Pipeline Testleri**
   - ✓ Valid command detection
   - ✓ Intent classification
   - ✓ CommandFactory routing
   - ✓ Execution result handling

2. **Mod Switching Testleri**
   - ✓ "yazı moduna geç" → Writing mode
   - ✓ "komut moduna geç" → Command mode
   - ✓ Özel komutlar: "yaz kızım", "yaz oğlum"
   - ✓ Mod değişikliği sırasında pending command

3. **Dosya/Klasör İşlemleri**
   - ✓ "excel dosyasını aç" parsing
   - ✓ "belgeler klasörünü aç" execution
   - ✓ Fuzzy folder matching
   - ✓ Extension determination logic

4. **Web Komut Yönlendirme**
   - ✓ "haberleri oku" → WebInfoCommand
   - ✓ "wikipedia" → Web search
   - ✓ CommandRegistry lookup

5. **Edge Cases**
   - ✓ Empty/null input
   - ✓ Çok uzun komut (>1000 karakter)
   - ✓ Concurrent command execution
   - ✓ Timeout handling
   - ✓ Exception propagation

**Mock Gereksinimleri**:
```csharp
- ICommandExecutor
- IApplicationService
- IFileSearchService
- ILocalIntentDetector
- IWebViewManager
- IModeManager
```

---

#### C. DictationManager.cs (1061 satır) - KRİTİK ÖNCELİK
**Dosya Yolu**: `/mnt/c/Users/serkan/source/repos/QuadroAIPilot setup so so outlook not setup deneme2/Managers/DictationManager.cs`

**Kritik Metodlar**:
```csharp
// Test edilmeli:
- ProcessTextChanged(string text)
- ShouldProcessText(string text)
- TTSOutputFilter.IsTTSOutput(string text)
- TTSOutputFilter.UpdateTTSText(string text)
- StartAsync(bool forceRestart)
- Stop()
- ToggleDictation()
```

**Test Senaryoları (Önerilen):**
1. **TTS Echo Filtering**
   - ✓ Exact match filtering
   - ✓ Partial match filtering
   - ✓ %70 similarity threshold
   - ✓ Time window validation (5 saniye)
   - ✓ History buffer logic (son 5 TTS)

2. **Komut Algılama**
   - ✓ VolumeRegex matching
   - ✓ MailRegex matching
   - ✓ Single word commands
   - ✓ Verb detection
   - ✓ Special short commands

3. **Debounce Logic**
   - ✓ Pending text accumulation
   - ✓ Timer reset on new input
   - ✓ Timer elapsed processing

4. **Mod Kontrolü**
   - ✓ Writing mode text routing
   - ✓ Command mode processing
   - ✓ "komut moduna geç" detection

5. **Edge Cases**
   - ✓ Rapid-fire text input
   - ✓ TTS konuşurken interrupt
   - ✓ Concurrent processing prevention
   - ✓ State reset on mode change

**Mock Gereksinimleri**:
```csharp
- IModeManager
- IWebViewManager
- IWebSpeechBridge
- ITextToSpeechService
- IVoiceActivityDetector
```

---

### 1.3 Diğer Test Edilmesi Gereken Sınıflar

#### AI/Intent Services
- **LocalIntentDetector.cs**: Pattern matching, synonym expansion
- **UserLearningService.cs**: Command history, success tracking
- **IntentPatterns.cs**: Regex pattern validation

#### Core Services
- **TextToSpeechService.cs**: Speech synthesis, queue management
- **ApplicationService.cs**: App launching, process control
- **SecurityValidator.cs**: Path validation, whitelist checking
- **WebContentService.cs**: RSS parsing, caching
- **NewsMemoryService.cs**: New content detection

#### Managers
- **ModeManager.cs**: Mode switching logic
- **EventCoordinator.cs**: Event routing
- **WindowController.cs**: Window management

---

## 2. INTEGRATION TESTS: ❌ YOK

### 2.1 Test Edilmesi Gereken Entegrasyonlar

#### A. MAPI/Outlook Entegrasyonu
**Servisler:**
- RealOutlookReader
- MAPIService
- OutlookStatsCommand
- LocalOutlookCommand

**Test Senaryoları:**
- ✓ Outlook açık/kapalı durumu
- ✓ Profil okuma
- ✓ Mail listesi çekme
- ✓ Folder navigation
- ✓ Mail filtering
- ✓ Error handling (Outlook not installed)

#### B. WebView2 Entegrasyonu
**Servisler:**
- WebViewManager
- WebSpeechBridge
- BrowserIntegrationService

**Test Senaryoları:**
- ✓ WebView initialization
- ✓ JavaScript interop (C# ↔ JS)
- ✓ HTML content injection
- ✓ Command message passing
- ✓ State synchronization
- ✓ WebView crash handling

#### C. Windows API Entegrasyonu
**Servisler:**
- WindowsApiService
- HotkeySender
- VolumeController

**Test Senaryoları:**
- ✓ Hotkey registration (Ctrl+Shift+Q)
- ✓ Foreground window detection
- ✓ SendKeys simulation
- ✓ Volume control
- ✓ Process enumeration
- ✓ UAC elevation handling

#### D. External Web Services
**Servisler:**
- RSSProvider
- WikipediaProvider
- TwitterTrendsProvider
- WebScraperProvider

**Test Senaryoları:**
- ✓ HTTP request handling
- ✓ XML/JSON parsing
- ✓ Cache hit/miss
- ✓ Provider failover
- ✓ Timeout handling
- ✓ Network error recovery

---

## 3. TEST COVERAGE ANALYSIS

### 3.1 Mevcut Coverage: %0

**Coverage Hedefleri:**
```
Minimum Target: 80% overall

Services/:        0% → 85%+  (FileSearchService, TextToSpeechService vb.)
Commands/:        0% → 75%+  (CommandProcessor, FindFileCommand vb.)
Managers/:        0% → 70%+  (DictationManager, ModeManager vb.)
AI/:              0% → 90%+  (LocalIntentDetector, UserLearningService)
Infrastructure/: 0% → 80%+  (ServiceContainer, GlobalExceptionHandler)
Modes/:           0% → 75%+  (CommandMode, WritingMode)
```

### 3.2 Coverage Tool Önerisi
```xml
<PackageReference Include="coverlet.collector" Version="6.0.0" />
<PackageReference Include="ReportGenerator" Version="5.2.0" />
```

**Kullanım:**
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

reportgenerator \
  -reports:"./TestResults/**/coverage.cobertura.xml" \
  -targetdir:"./CoverageReport" \
  -reporttypes:"Html;Badges"
```

---

## 4. CRITICAL PATHS - Test Öncelikleri

### Path 1: Voice Command Pipeline (EN YÜKSEK ÖNCELİK)
```
User Speech → DictationManager.ProcessTextChanged()
→ ShouldProcessText()
→ StartProcessing()
→ ModeManager.RouteSpeech()
→ CommandProcessor.ProcessCommandAsync()
→ CommandFactory.CreateCommand()
→ ICommand.ExecuteAsync()
```

**Test Senaryoları:**
- ✓ End-to-end happy path
- ✓ TTS echo filtering
- ✓ Mod switching
- ✓ Error propagation
- ✓ Timeout scenarios
- ✓ Concurrent command prevention

---

### Path 2: File Search Pipeline
```
"excel dosyasını aç"
→ CommandProcessor (parsing)
→ FindFileCommand
→ FileSearchService.FindFileAsync()
→ [Recent Items → MRU → Office Registry → File System]
→ OpenFileAsync()
```

**Test Senaryoları:**
- ✓ Exact match success
- ✓ Fuzzy match fallback
- ✓ Multiple results handling
- ✓ File not found
- ✓ Permission denied
- ✓ Timeout handling

---

### Path 3: TTS Echo Prevention
```
TTS Start (SpeechStarted event)
→ TTSOutputFilter.UpdateTTSText()
→ User speaks same text
→ TTSOutputFilter.IsTTSOutput()
→ Reject if match
```

**Test Senaryoları:**
- ✓ Exact match blocking
- ✓ Partial match blocking
- ✓ Time window expiry
- ✓ History buffer logic
- ✓ Interrupt commands allowed

---

### Path 4: Web Content Retrieval
```
"haberleri oku"
→ WebInfoCommand.ExecuteAsync()
→ RSSProvider.GetLatestNewsAsync()
→ ContentCacheService (check cache)
→ Parse RSS
→ Format HTML
→ WebViewManager.AppendOutput()
```

**Test Senaryoları:**
- ✓ Cache hit scenario
- ✓ Cache miss & fetch
- ✓ Multiple RSS sources
- ✓ Parsing errors
- ✓ Network failures
- ✓ HTML sanitization

---

## 5. EDGE CASES & BOUNDARY CONDITIONS

### 5.1 Dosya Arama Edge Cases

| Senaryo | Input | Beklenen Davranış |
|---------|-------|-------------------|
| Boş dosya adı | `""` | Return null, log warning |
| Null input | `null` | Return null, no crash |
| Çok uzun dosya adı | `"a".Repeat(300)` | Handle gracefully |
| Özel karakterler | `"şçğüöı.xlsx"` | Türkçe normalization |
| Birden fazla nokta | `"v2.0.final.xlsx"` | Correct extension parse |
| Uzantısız dosya | `"README"` | Search without extension |
| Permission denied | Access to `C:\System32` | Catch exception, skip |
| Network drive | `\\server\share\file.xlsx` | Timeout handling |
| Symlink | Junction point | Resolve or skip |

### 5.2 Komut İşleme Edge Cases

| Senaryo | Input | Beklenen Davranış |
|---------|-------|-------------------|
| Empty string | `""` | Ignore, no processing |
| Whitespace only | `"   "` | Ignore after trim |
| Çok uzun komut | `"aç".Repeat(500)` | Truncate or reject |
| Rapid commands | 10 komut/saniye | Debounce queue |
| Concurrent execution | Parallel komutlar | Lock & serialize |
| TTS konuşurken | User interrupts | Allow if interrupt cmd |
| Mod geçişi sırasında | Command arrives | Queue & process after |
| Invalid characters | `"<script>alert()</script>"` | Sanitize input |

### 5.3 TTS Filtering Edge Cases

| Senaryo | TTS Output | User Input | Filtrelenmeli mi? |
|---------|------------|------------|-------------------|
| Exact match | "Dosya açıldı" | "Dosya açıldı" | ✓ Evet |
| Partial start | "Toplam 5 haber bulundu" | "Toplam 5" | ✓ Evet |
| Partial middle | "İşleminiz tamamlandı" | "işleminiz" | ✓ Evet |
| %70 similarity | "Komut başarılı" | "Komut başardı" | ✓ Evet |
| Time expired | TTS 6 saniye önce | "Dosya açıldı" | ✗ Hayır |
| Different text | "Merhaba" | "Nasılsın" | ✗ Hayır |
| Interrupt command | "Haberleri okuyor..." | "dur" | ✗ Hayır (allow) |

---

## 6. MOCK USAGE STRATEGIES

### 6.1 Mock Edilmesi Gereken Interface'ler

```csharp
// File System Mocking
public interface IFileSystem
{
    Task<string[]> GetFilesAsync(string path, string pattern, SearchOption option);
    Task<string[]> GetDirectoriesAsync(string path);
    Task<bool> FileExistsAsync(string path);
    Task<bool> DirectoryExistsAsync(string path);
    Task<StorageFile> GetFileFromPathAsync(string path);
}

// Windows API Mocking
public interface IWindowsApi
{
    IntPtr GetForegroundWindow();
    bool SetForegroundWindow(IntPtr handle);
    bool PostMessage(IntPtr handle, uint msg, IntPtr wParam, IntPtr lParam);
    bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint vk);
    bool UnregisterHotKey(IntPtr handle, int id);
}

// Speech Service Mocking
public interface ISpeechService
{
    Task SpeakAsync(string text);
    bool IsSpeaking { get; }
    void Stop();
    event EventHandler SpeechStarted;
    event EventHandler SpeechCompleted;
    event EventHandler SpeechCancelled;
}

// Time Provider (Testability)
public interface ITimeProvider
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}

// WebView Manager Mocking
public interface IWebViewManager
{
    Task LoadHtmlContentAsync(string html);
    Task AppendOutputAsync(string text);
    Task<string> ExecuteScriptAsync(string script);
    void UpdateDictationState(bool isActive);
}
```

### 6.2 Mock Library: Moq

**Örnek Kullanım:**
```csharp
[Fact]
public async Task FindFileAsync_ExactMatch_ReturnsFile()
{
    // Arrange
    var mockFileSystem = new Mock<IFileSystem>();
    mockFileSystem
        .Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), "*.xlsx", SearchOption.TopDirectoryOnly))
        .ReturnsAsync(new[] { @"C:\Users\test\Documents\rapor.xlsx" });

    var service = new FileSearchService(mockFileSystem.Object);

    // Act
    var result = await service.FindFileAsync("rapor", "xlsx");

    // Assert
    result.Should().Be(@"C:\Users\test\Documents\rapor.xlsx");
}
```

---

## 7. TEST FRAMEWORK SETUP

### 7.1 Önerilen Test Stack

```xml
<!-- QuadroAIPilot.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Test Framework -->
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />

    <!-- Mocking -->
    <PackageReference Include="Moq" Version="4.20.70" />

    <!-- Assertions -->
    <PackageReference Include="FluentAssertions" Version="6.12.0" />

    <!-- Coverage -->
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="ReportGenerator" Version="5.2.0" />

    <!-- UI Testing (WinAppDriver) -->
    <PackageReference Include="Appium.WebDriver" Version="5.0.0-rc.1" />

    <!-- Fixtures & Builders -->
    <PackageReference Include="AutoFixture" Version="4.18.1" />
    <PackageReference Include="Bogus" Version="35.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\QuadroAIPilot\QuadroAIPilot.csproj" />
  </ItemGroup>
</Project>
```

### 7.2 Test Proje Yapısı

```
QuadroAIPilot.Tests/
├── Unit/
│   ├── Services/
│   │   ├── FileSearchServiceTests.cs           (✓ En Yüksek Öncelik)
│   │   ├── TextToSpeechServiceTests.cs
│   │   ├── SecurityValidatorTests.cs
│   │   ├── ApplicationServiceTests.cs
│   │   ├── WebContentServiceTests.cs
│   │   └── NewsMemoryServiceTests.cs
│   ├── Commands/
│   │   ├── CommandProcessorTests.cs            (✓ Kritik)
│   │   ├── FindFileCommandTests.cs
│   │   ├── FindFolderCommandTests.cs
│   │   ├── WebInfoCommandTests.cs
│   │   ├── OpenApplicationCommandTests.cs
│   │   └── SystemCommandTests.cs
│   ├── Managers/
│   │   ├── DictationManagerTests.cs            (✓ Kritik)
│   │   ├── TTSOutputFilterTests.cs
│   │   ├── ModeManagerTests.cs
│   │   ├── EventCoordinatorTests.cs
│   │   └── WindowControllerTests.cs
│   ├── AI/
│   │   ├── LocalIntentDetectorTests.cs
│   │   ├── UserLearningServiceTests.cs
│   │   ├── IntentPatternsTests.cs
│   │   └── SynonymDictionaryTests.cs
│   └── Infrastructure/
│       ├── ServiceContainerTests.cs
│       ├── GlobalExceptionHandlerTests.cs
│       └── SecurityValidatorTests.cs
│
├── Integration/
│   ├── Outlook/
│   │   ├── RealOutlookReaderTests.cs
│   │   ├── MAPIServiceTests.cs
│   │   └── OutlookCommandsTests.cs
│   ├── WebView/
│   │   ├── WebViewManagerTests.cs
│   │   ├── WebSpeechBridgeTests.cs
│   │   └── JavaScriptInteropTests.cs
│   ├── WindowsAPI/
│   │   ├── WindowsApiServiceTests.cs
│   │   ├── HotkeySenderTests.cs
│   │   └── VolumeControllerTests.cs
│   ├── FileSystem/
│   │   ├── FileSystemIntegrationTests.cs
│   │   └── PermissionTests.cs
│   └── EndToEnd/
│       ├── VoiceCommandFlowTests.cs            (✓ E2E Kritik)
│       ├── FileSearchFlowTests.cs
│       └── WebContentFlowTests.cs
│
├── UI/
│   ├── MainWindowTests.cs                      (WinAppDriver)
│   ├── SettingsDialogTests.cs
│   └── WebViewUITests.cs
│
├── Performance/
│   ├── FileSearchBenchmarks.cs
│   ├── CommandProcessingBenchmarks.cs
│   └── TTSFilteringBenchmarks.cs
│
├── Helpers/
│   ├── Mocks/
│   │   ├── MockFileSystem.cs
│   │   ├── MockWindowsApi.cs
│   │   ├── MockSpeechService.cs
│   │   ├── MockWebViewManager.cs
│   │   └── MockTimeProvider.cs
│   ├── Builders/
│   │   ├── CommandTestDataBuilder.cs
│   │   ├── FileInfoTestDataBuilder.cs
│   │   └── IntentResultBuilder.cs
│   └── Fixtures/
│       ├── FileSystemFixture.cs
│       └── TestDataFixture.cs
│
└── TestData/
    ├── SampleFiles/
    │   ├── test.xlsx
    │   ├── rapor.docx
    │   └── sunum.pptx
    ├── RSSFeeds/
    │   └── sample-rss.xml
    └── MockData/
        └── test-mail-list.json
```

---

## 8. CI/CD TESTING PIPELINE

### 8.1 GitHub Actions Workflow

**Dosya**: `.github/workflows/test-pipeline.yml`

```yaml
name: QuadroAIPilot Test Pipeline

on:
  push:
    branches: [ master, develop ]
  pull_request:
    branches: [ master ]

jobs:
  build-and-test:
    runs-on: windows-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v3

      - name: Setup .NET 8.0
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x

      - name: Restore dependencies
        run: dotnet restore

      - name: Build solution
        run: dotnet build --no-restore --configuration Release

      - name: Run Unit Tests
        run: |
          dotnet test `
            --no-build `
            --configuration Release `
            --logger "trx;LogFileName=unit-tests.trx" `
            --collect:"XPlat Code Coverage" `
            --filter "Category!=Integration&Category!=UI"

      - name: Run Integration Tests
        run: |
          dotnet test `
            --no-build `
            --configuration Release `
            --logger "trx;LogFileName=integration-tests.trx" `
            --filter "Category=Integration"

      - name: Generate Coverage Report
        run: |
          dotnet tool install --global dotnet-reportgenerator-globaltool
          reportgenerator `
            -reports:"**/coverage.cobertura.xml" `
            -targetdir:"CoverageReport" `
            -reporttypes:"Html;Badges;Cobertura"

      - name: Upload Coverage to Codecov
        uses: codecov/codecov-action@v3
        with:
          files: ./CoverageReport/Cobertura.xml
          fail_ci_if_error: true
          verbose: true

      - name: Publish Test Results
        uses: EnricoMi/publish-unit-test-result-action/composite@v2
        if: always()
        with:
          files: "**/*.trx"

      - name: Upload Test Artifacts
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: |
            **/*.trx
            CoverageReport/

      - name: Check Coverage Threshold
        run: |
          $coverage = [xml](Get-Content ./CoverageReport/Cobertura.xml)
          $lineRate = [double]$coverage.coverage.'line-rate' * 100
          Write-Host "Current Coverage: $lineRate%"
          if ($lineRate -lt 80) {
            Write-Error "Coverage ($lineRate%) is below threshold (80%)"
            exit 1
          }
```

### 8.2 Build Status Badge

README.md'ye eklenebilir:
```markdown
[![Build Status](https://github.com/[user]/QuadroAIPilot/actions/workflows/test-pipeline.yml/badge.svg)](https://github.com/[user]/QuadroAIPilot/actions)
[![Code Coverage](https://codecov.io/gh/[user]/QuadroAIPilot/branch/master/graph/badge.svg)](https://codecov.io/gh/[user]/QuadroAIPilot)
```

---

## 9. TEST IMPLEMENTATION ROADMAP

### Sprint 1 (Hafta 1-2): Foundation Setup
- [ ] Test projesi oluştur (xUnit)
- [ ] Package'leri kur (Moq, FluentAssertions, Coverlet)
- [ ] Mock interface'leri oluştur
- [ ] İlk 5 unit test yaz (FileSearchService)
- [ ] CI/CD pipeline kur
- **Hedef**: %10 coverage

### Sprint 2 (Hafta 3-4): Core Services Testing
- [ ] FileSearchService tüm metodlar (30 test)
- [ ] CommandProcessor ana metodlar (25 test)
- [ ] DictationManager & TTSFilter (20 test)
- **Hedef**: %30 coverage

### Sprint 3 (Hafta 5-6): AI & Integration Tests
- [ ] LocalIntentDetector (15 test)
- [ ] UserLearningService (10 test)
- [ ] WebView2 integration (10 test)
- [ ] MAPI integration (10 test)
- **Hedef**: %50 coverage

### Sprint 4 (Hafta 7-8): Commands & Edge Cases
- [ ] Tüm Command sınıfları (40 test)
- [ ] Edge case scenarios (30 test)
- [ ] Boundary condition tests (20 test)
- **Hedef**: %70 coverage

### Sprint 5 (Hafta 9-10): Full Coverage & E2E
- [ ] Kalan service testleri (30 test)
- [ ] UI tests (WinAppDriver) (15 test)
- [ ] End-to-end flow tests (10 test)
- [ ] Performance benchmarks
- **Hedef**: %80+ coverage

---

## 10. ÖRNEK TEST İMPLEMENTASYONU

### 10.1 FileSearchService Unit Test

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Tests.Unit.Services
{
    public class FileSearchServiceTests
    {
        private readonly Mock<IFileSystem> _mockFileSystem;
        private readonly FileSearchService _sut; // System Under Test

        public FileSearchServiceTests()
        {
            _mockFileSystem = new Mock<IFileSystem>();
            _sut = new FileSearchService(_mockFileSystem.Object);
        }

        [Fact]
        public async Task FindFileAsync_ExactMatch_ReturnsCorrectFile()
        {
            // Arrange
            var expectedPath = @"C:\Users\test\Documents\rapor.xlsx";
            _mockFileSystem
                .Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), "*.xlsx", SearchOption.TopDirectoryOnly))
                .ReturnsAsync(new[] { expectedPath });

            // Act
            var result = await _sut.FindFileAsync("rapor", "xlsx");

            // Assert
            result.Should().Be(expectedPath);
            _mockFileSystem.Verify(fs =>
                fs.GetFilesAsync(It.IsAny<string>(), "*.xlsx", SearchOption.TopDirectoryOnly),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task FindFileAsync_NoMatch_ReturnsNull()
        {
            // Arrange
            _mockFileSystem
                .Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .ReturnsAsync(Array.Empty<string>());

            // Act
            var result = await _sut.FindFileAsync("nonexistent", "xlsx");

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("", "xlsx")] // Empty filename
        [InlineData(null, "xlsx")] // Null filename
        [InlineData("test", "")] // Empty extension
        public async Task FindFileAsync_InvalidInput_ReturnsNull(string fileName, string extension)
        {
            // Act
            var result = await _sut.FindFileAsync(fileName, extension);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task FindFileAsync_TurkishCharacters_NormalizesCorrectly()
        {
            // Arrange
            _mockFileSystem
                .Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), "*.txt", SearchOption.TopDirectoryOnly))
                .ReturnsAsync(new[] { @"C:\test\calisma.txt" });

            // Act (Türkçe karakterlerle ara)
            var result = await _sut.FindFileAsync("çalışma", "txt");

            // Assert (Normalizasyon sonrası bulmalı)
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task CalculateSimilarity_ExactMatch_Returns100Percent()
        {
            // Arrange
            var text1 = "rapor";
            var text2 = "rapor";

            // Act
            var similarity = FileSearchService.CalculateSimilarity(text1, text2);

            // Assert
            similarity.Should().Be(1.0);
        }

        [Theory]
        [InlineData("rapor", "raporr", 0.83)] // 1 karakter fark
        [InlineData("excel", "exel", 0.80)]   // 1 harf eksik
        [InlineData("test", "best", 0.75)]    // 1 harf farklı
        public void CalculateSimilarity_SimilarStrings_ReturnsExpectedValue(
            string text1, string text2, double expected)
        {
            // Act
            var similarity = FileSearchService.CalculateSimilarity(text1, text2);

            // Assert
            similarity.Should().BeApproximately(expected, 0.05);
        }

        [Fact]
        public async Task FindMultipleFilesAsync_ReturnsOrderedByPriority()
        {
            // Arrange
            _mockFileSystem
                .Setup(fs => fs.GetFilesAsync(It.IsAny<string>(), "*.xlsx", It.IsAny<SearchOption>()))
                .ReturnsAsync(new[] {
                    @"C:\Users\test\Desktop\rapor.xlsx",      // Desktop (priority 1)
                    @"C:\Users\test\Recent\rapor.xlsx",       // Recent (priority 3)
                    @"C:\Users\test\Documents\rapor.xlsx"     // Documents (priority 2)
                });

            // Act
            var results = await _sut.FindMultipleFilesAsync("rapor", "xlsx", maxResults: 3);

            // Assert
            results.Should().HaveCount(3);
            results[0].SearchPriority.Should().Be(3); // Recent önce gelmeli
            results[0].FilePath.Should().Contain("Recent");
        }
    }
}
```

### 10.2 CommandProcessor Integration Test

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using QuadroAIPilot.Commands;

namespace QuadroAIPilot.Tests.Integration
{
    public class CommandProcessorIntegrationTests : IAsyncLifetime
    {
        private CommandProcessor _processor;
        private Mock<IFileSearchService> _mockFileSearch;
        private Mock<IWebViewManager> _mockWebView;

        public async Task InitializeAsync()
        {
            _mockFileSearch = new Mock<IFileSearchService>();
            _mockWebView = new Mock<IWebViewManager>();

            var executor = new CommandExecutor(/* dependencies */);
            var appService = new ApplicationService();

            _processor = new CommandProcessor(
                executor,
                appService,
                _mockFileSearch.Object,
                webViewManager: _mockWebView.Object
            );
        }

        [Fact]
        public async Task ProcessCommandAsync_FileOpenCommand_ExecutesSuccessfully()
        {
            // Arrange
            var command = "rapor excel dosyasını aç";
            _mockFileSearch
                .Setup(fs => fs.FindFileAsync("rapor", "xls,xlsx,csv,xlsm"))
                .ReturnsAsync(@"C:\test\rapor.xlsx");
            _mockFileSearch
                .Setup(fs => fs.OpenFileAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _processor.ProcessCommandAsync(command);

            // Assert
            result.Should().BeTrue();
            _mockFileSearch.Verify(fs =>
                fs.OpenFileAsync(@"C:\test\rapor.xlsx"), Times.Once);
        }

        [Fact]
        public async Task ProcessCommandAsync_ModeSwitch_UpdatesMode()
        {
            // Arrange
            var command = "yazı moduna geç";

            // Act
            var result = await _processor.ProcessCommandAsync(command);

            // Assert
            result.Should().BeTrue();
            AppState.CurrentMode.Should().Be(AppState.UserMode.Writing);

            // WebView'a bildirim gitmeli
            _mockWebView.Verify(wv =>
                wv.ExecuteScriptAsync(It.Is<string>(s => s.Contains("setCurrentMode('writing')"))),
                Times.Once);
        }

        [Theory]
        [InlineData("haberleri oku")]
        [InlineData("spor haberleri")]
        [InlineData("ekonomi haberlerini göster")]
        public async Task ProcessCommandAsync_NewsCommands_RoutesToWebInfoCommand(string command)
        {
            // Act
            var result = await _processor.ProcessCommandAsync(command);

            // Assert
            result.Should().BeTrue();
            _mockWebView.Verify(wv =>
                wv.AppendOutputAsync(It.IsAny<string>()), Times.AtLeastOnce);
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
```

### 10.3 DictationManager TTS Filtering Test

```csharp
using Xunit;
using FluentAssertions;
using QuadroAIPilot.Managers;

namespace QuadroAIPilot.Tests.Unit.Managers
{
    public class TTSOutputFilterTests
    {
        private readonly TTSOutputFilter _filter;

        public TTSOutputFilterTests()
        {
            _filter = new TTSOutputFilter();
        }

        [Fact]
        public void IsTTSOutput_ExactMatch_ReturnsTrue()
        {
            // Arrange
            var ttsText = "Dosya başarıyla açıldı";
            _filter.UpdateTTSText(ttsText);

            // Act
            var result = _filter.IsTTSOutput(ttsText);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("Dosya başarıyla açıldı", "Dosya başarıyla", true)]  // Partial start
        [InlineData("Toplam 5 haber bulundu", "5 haber", true)]          // Partial middle
        [InlineData("İşlem tamamlandı", "işlem", true)]                  // Case insensitive
        public void IsTTSOutput_PartialMatch_ReturnsExpected(
            string ttsText, string userInput, bool shouldFilter)
        {
            // Arrange
            _filter.UpdateTTSText(ttsText);

            // Act
            var result = _filter.IsTTSOutput(userInput);

            // Assert
            result.Should().Be(shouldFilter);
        }

        [Fact]
        public void IsTTSOutput_SimilarText_Above70Percent_ReturnsTrue()
        {
            // Arrange
            _filter.UpdateTTSText("Komut başarılı");

            // Act
            var result = _filter.IsTTSOutput("Komut başardı"); // %75 benzerlik

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsTTSOutput_TimeWindowExpired_ReturnsFalse()
        {
            // Arrange
            _filter.UpdateTTSText("Eski mesaj");
            Task.Delay(6000).Wait(); // 6 saniye bekle (window 5 saniye)

            // Act
            var result = _filter.IsTTSOutput("Eski mesaj");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsTTSOutput_InterruptCommand_AllowsThrough()
        {
            // Arrange
            _filter.UpdateTTSText("Haberleri okuyorum, lütfen bekleyin");

            // Act (Interrupt komutu)
            var result = _filter.IsTTSOutput("dur");

            // Assert
            result.Should().BeFalse(); // Interrupt'a izin ver
        }

        [Fact]
        public void Clear_RemovesAllHistory()
        {
            // Arrange
            _filter.UpdateTTSText("Test 1");
            _filter.UpdateTTSText("Test 2");

            // Act
            _filter.Clear();
            var result = _filter.IsTTSOutput("Test 1");

            // Assert
            result.Should().BeFalse();
        }
    }
}
```

---

## 11. PERFORMANCE TESTING

### 11.1 Benchmark Tests (BenchmarkDotNet)

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace QuadroAIPilot.Tests.Performance
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class FileSearchBenchmarks
    {
        private FileSearchService _service;

        [GlobalSetup]
        public void Setup()
        {
            _service = new FileSearchService();
        }

        [Benchmark]
        public async Task<string> FindFile_ExactMatch()
        {
            return await _service.FindFileAsync("test", "xlsx");
        }

        [Benchmark]
        public async Task<string> FindFile_FuzzyMatch()
        {
            return await _service.FindFileAsyncFuzzy("tset", "xlsx"); // Typo
        }

        [Benchmark]
        public double CalculateSimilarity_ShortStrings()
        {
            return FileSearchService.CalculateSimilarity("test", "best");
        }

        [Benchmark]
        public double CalculateSimilarity_LongStrings()
        {
            var str1 = "Bu çok uzun bir dosya adı örneğidir";
            var str2 = "Bu çok uzun bir dosya adi ornegidir"; // Türkçe hatasız
            return FileSearchService.CalculateSimilarity(str1, str2);
        }
    }
}
```

**Çalıştırma:**
```bash
dotnet run -c Release --project QuadroAIPilot.Tests.Performance
```

---

## 12. ÖNCELİKLİ AKSIYON PLANI

### Hafta 1: Acil Aksiyonlar
1. **Test projesi oluştur**
   ```bash
   dotnet new xunit -n QuadroAIPilot.Tests
   dotnet add package Moq
   dotnet add package FluentAssertions
   dotnet add package coverlet.collector
   ```

2. **İlk 10 critical test yaz**
   - FileSearchService.FindFileAsync (3 test)
   - CommandProcessor.ProcessCommandAsync (3 test)
   - TTSOutputFilter.IsTTSOutput (4 test)

3. **CI/CD pipeline ekle**
   - GitHub Actions workflow oluştur
   - Code coverage raporu ekle

### Hafta 2-4: Core Coverage
4. **FileSearchService full coverage** (30 test)
5. **CommandProcessor critical paths** (25 test)
6. **DictationManager logic** (20 test)
7. **%30 coverage hedefi**

### Hafta 5-8: Integration & Edge Cases
8. **MAPI integration** (10 test)
9. **WebView2 integration** (10 test)
10. **Edge case scenarios** (30 test)
11. **%60 coverage hedefi**

### Hafta 9-12: Full Coverage
12. **Kalan services** (40 test)
13. **UI tests (WinAppDriver)** (15 test)
14. **E2E tests** (10 test)
15. **%80+ coverage hedefi**

---

## 13. SONUÇ VE ÖNERİLER

### 13.1 Kritik Bulgular

1. **❌ Hiç test yok** - Bu ciddi bir kalite riski
2. **❌ Test framework kurulmamış** - Hemen kurulmalı
3. **❌ CI/CD pipeline yok** - Otomatik test gerekli
4. **⚠️ Karmaşık logic** - FileSearch, CommandProcessor test edilmeli
5. **⚠️ TTS feedback loop** - TTSOutputFilter kritik, test şart

### 13.2 Riskler

| Risk | Etki | Olasılık | Önlem |
|------|------|----------|-------|
| Production bug | Yüksek | Yüksek | Unit tests ekle |
| Regression | Yüksek | Orta | CI/CD pipeline |
| Performance degradation | Orta | Orta | Benchmark tests |
| Security vulnerability | Yüksek | Düşük | Security tests |

### 13.3 İyileştirme Önerileri

1. **Öncelik 1 (Hemen):**
   - Test framework kur (xUnit + Moq)
   - İlk 10 critical test yaz
   - CI/CD pipeline ekle

2. **Öncelik 2 (2 hafta içinde):**
   - Core services tam coverage (%90+)
   - Integration tests (MAPI, WebView)
   - Edge case scenarios

3. **Öncelik 3 (1 ay içinde):**
   - UI tests (WinAppDriver)
   - E2E tests
   - Performance benchmarks
   - %80+ overall coverage

### 13.4 Başarı Kriterleri

✅ **Sprint 1**: Test framework kurulu, ilk 10 test, CI/CD çalışıyor
✅ **Sprint 2**: %30 coverage, core services tested
✅ **Sprint 3**: %50 coverage, integration tests
✅ **Sprint 4**: %70 coverage, edge cases
✅ **Sprint 5**: %80+ coverage, E2E tests, production-ready

---

## 14. KAYNAKLAR VE REFERANSLAR

### Test Framework Dokumanları
- xUnit: https://xunit.net/
- Moq: https://github.com/moq/moq4
- FluentAssertions: https://fluentassertions.com/
- Coverlet: https://github.com/coverlet-coverage/coverlet

### CI/CD
- GitHub Actions: https://docs.github.com/en/actions
- Codecov: https://about.codecov.io/

### Windows Testing
- WinAppDriver: https://github.com/microsoft/WinAppDriver
- Appium: https://appium.io/

### Best Practices
- Unit Testing Best Practices: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices
- Test-Driven Development (TDD): https://martinfowler.com/bliki/TestDrivenDevelopment.html

---

**Rapor Sonu**

Bu rapor QuadroAIPilot projesinin test durumunu detaylı olarak analiz etmiştir. Acil aksiyon alınması önerilir.

---

**İletişim:**
Tester Agent - QuadroAIPilot Test Stratejisi
Tarih: 2025-10-13
