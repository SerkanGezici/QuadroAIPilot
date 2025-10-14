# InstallTurkishVoices.ps1
# Windows Turkce TTS seslerini ve dil paketlerini kurar
# QuadroAIPilot icin Turkce ses destegi saglar

$logFile = "$env:TEMP\QuadroAI_TurkishVoices.log"
function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "[$timestamp] $Message" | Out-File -FilePath $logFile -Append
    Write-Host $Message
}

Write-Log "==============================================="
Write-Log "Turkce Ses Paketi Kurulum Kontrolu"
Write-Log "==============================================="

$script:turkishVoiceInstalled = $false
$script:turkishLanguageInstalled = $false
$script:speechPlatformInstalled = $false

# Windows sürümünü kontrol et
function Get-WindowsVersion {
    $version = [System.Environment]::OSVersion.Version
    $build = (Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion" -Name CurrentBuild).CurrentBuild
    return @{
        Major = $version.Major
        Minor = $version.Minor
        Build = [int]$build
        IsWindows11 = [int]$build -ge 22000
        IsWindows10 = $version.Major -eq 10 -and [int]$build -lt 22000
    }
}

# Türkçe dil paketi kurulu mu kontrol et
function Test-TurkishLanguagePack {
    Write-Host "`nTürkçe dil paketi kontrol ediliyor..." -ForegroundColor Yellow
    
    try {
        $languages = Get-WinUserLanguageList
        $turkish = $languages | Where-Object { $_.LanguageTag -like "tr-*" -or $_.LanguageTag -eq "tr" }
        
        if ($turkish) {
            Write-Log "[OK] Turkce dil destegi mevcut: $($turkish.LanguageTag)"
            $script:turkishLanguageInstalled = $true
            return $true
        }
        
        # Alternatif kontrol
        $installedLanguages = Get-WindowsCapability -Online | Where-Object { 
            $_.Name -like "*Language.Basic*tr-tr*" -and $_.State -eq "Installed" 
        }
        
        if ($installedLanguages) {
            Write-Log "[OK] Turkce dil paketi kurulu"
            $script:turkishLanguageInstalled = $true
            return $true
        }
    } catch {
        Write-Host "⚠ Dil paketi kontrolünde hata: $_" -ForegroundColor Yellow
    }
    
    Write-Log "[HATA] Turkce dil paketi kurulu degil"
    return $false
}

# Türkçe TTS seslerini kontrol et
function Test-TurkishTTSVoices {
    Write-Host "`nTürkçe TTS sesleri kontrol ediliyor..." -ForegroundColor Yellow
    
    try {
        Add-Type -AssemblyName System.Speech
        $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
        $voices = $synth.GetInstalledVoices()
        
        $turkishVoices = @()
        foreach ($voice in $voices) {
            if ($voice.VoiceInfo.Culture.Name -like "tr-*") {
                $turkishVoices += $voice.VoiceInfo.Name
                Write-Host "  ✓ Ses bulundu: $($voice.VoiceInfo.Name)" -ForegroundColor Green
            }
        }
        
        if ($turkishVoices.Count -gt 0) {
            Write-Log "[OK] Toplam $($turkishVoices.Count) Turkce ses bulundu"
            $script:turkishVoiceInstalled = $true
            
            # Tolga sesini özel olarak kontrol et
            if ($turkishVoices -match "Tolga") {
                Write-Host "  ✓ Tolga sesi mevcut!" -ForegroundColor Green
            }
            return $true
        }
    } catch {
        Write-Host "⚠ TTS ses kontrolünde hata: $_" -ForegroundColor Yellow
    }
    
    Write-Log "[HATA] Turkce TTS sesi bulunamadi"
    return $false
}

# Speech Platform Runtime kontrolü
function Test-SpeechPlatform {
    Write-Host "`nSpeech Platform Runtime kontrol ediliyor..." -ForegroundColor Yellow
    
    $speechPlatformKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Speech\Voices\Tokens",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Speech\Voices\Tokens",
        "HKLM:\SOFTWARE\Microsoft\Speech Server\v11.0\Voices\Tokens"
    )
    
    foreach ($key in $speechPlatformKeys) {
        if (Test-Path $key) {
            $voices = Get-ChildItem $key -ErrorAction SilentlyContinue
            $turkishVoice = $voices | Where-Object { 
                $_.GetValue("Language") -eq "41F" -or  # Turkish LCID
                $_.GetValue("") -match "tr-TR" -or
                $_.Name -match "Tolga"
            }
            
            if ($turkishVoice) {
                Write-Host "✓ Speech Platform Türkçe ses bulundu" -ForegroundColor Green
                $script:speechPlatformInstalled = $true
                return $true
            }
        }
    }
    
    Write-Host "✗ Speech Platform Türkçe ses yok" -ForegroundColor Red
    return $false
}

# Türkçe dil paketini kur
function Install-TurkishLanguagePack {
    Write-Host "`nTürkçe dil paketi kuruluyor..." -ForegroundColor Yellow
    
    try {
        # Windows 10/11 için Language Pack kurulumu
        $capabilities = Get-WindowsCapability -Online | Where-Object { 
            $_.Name -like "*Language.Basic*tr-tr*" -or
            $_.Name -like "*Language.Handwriting*tr-tr*" -or
            $_.Name -like "*Language.OCR*tr-tr*" -or
            $_.Name -like "*Language.Speech*tr-tr*" -or
            $_.Name -like "*Language.TextToSpeech*tr-tr*"
        }
        
        foreach ($capability in $capabilities) {
            if ($capability.State -ne "Installed") {
                Write-Host "  Kuruluyor: $($capability.Name)" -ForegroundColor Yellow
                Add-WindowsCapability -Online -Name $capability.Name
            }
        }
        
        # Dil listesine Türkçe ekle
        $languages = Get-WinUserLanguageList
        $turkish = New-WinUserLanguage tr-TR
        if ($languages.LanguageTag -notcontains "tr-TR") {
            $languages.Add($turkish)
            Set-WinUserLanguageList $languages -Force
            Write-Host "✓ Türkçe dil listesine eklendi" -ForegroundColor Green
        }
        
        $script:turkishLanguageInstalled = $true
        return $true
    } catch {
        Write-Host "✗ Türkçe dil paketi kurulumu başarısız: $_" -ForegroundColor Red
        return $false
    }
}

# Speech özelliklerini etkinleştir
function Enable-SpeechFeatures {
    Write-Host "`nKonuşma özellikleri etkinleştiriliyor..." -ForegroundColor Yellow
    
    try {
        # Windows özellikleri
        $features = @(
            "Media.WindowsMediaPlayer",
            "Media.MediaFeaturePack"
        )
        
        foreach ($feature in $features) {
            $state = Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction SilentlyContinue
            if ($state -and $state.State -ne "Enabled") {
                Write-Host "  Etkinleştiriliyor: $feature" -ForegroundColor Yellow
                Enable-WindowsOptionalFeature -Online -FeatureName $feature -NoRestart -ErrorAction SilentlyContinue
            }
        }
        
        # Speech Recognition capability
        $speechCaps = Get-WindowsCapability -Online | Where-Object { 
            $_.Name -like "*Speech*" -and $_.State -ne "Installed" 
        }
        
        foreach ($cap in $speechCaps) {
            Write-Host "  Kuruluyor: $($cap.Name)" -ForegroundColor Yellow
            Add-WindowsCapability -Online -Name $cap.Name -ErrorAction SilentlyContinue
        }
        
        Write-Host "✓ Konuşma özellikleri etkinleştirildi" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "⚠ Bazı özellikler etkinleştirilemedi: $_" -ForegroundColor Yellow
        return $false
    }
}

# Registry ayarlarını yap
function Configure-TTSRegistry {
    Write-Host "`nTTS Registry ayarları yapılandırılıyor..." -ForegroundColor Yellow
    
    try {
        # TTS varsayılan ses ayarı
        $ttsKey = "HKCU:\SOFTWARE\Microsoft\Speech\Voices"
        if (-not (Test-Path $ttsKey)) {
            New-Item -Path $ttsKey -Force | Out-Null
        }
        
        # DefaultTTSRate - Konuşma hızı
        Set-ItemProperty -Path $ttsKey -Name "DefaultTTSRate" -Value 0 -Type DWord -Force
        
        # Edge TTS için registry ayarları
        $edgeKey = "HKCU:\SOFTWARE\Microsoft\Edge\TextToSpeech"
        if (-not (Test-Path $edgeKey)) {
            New-Item -Path $edgeKey -Force | Out-Null
        }
        
        # Türkçe ses tercihini ayarla
        Set-ItemProperty -Path $edgeKey -Name "PreferredVoice" -Value "tr-TR-EmelNeural" -Type String -Force
        Set-ItemProperty -Path $edgeKey -Name "FallbackVoice" -Value "tr-TR-AhmetNeural" -Type String -Force
        
        Write-Host "✓ Registry ayarları yapılandırıldı" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "⚠ Registry ayarlarında hata: $_" -ForegroundColor Yellow
        return $false
    }
}

# Microsoft Speech Platform Runtime'ı indir ve kur
function Install-SpeechPlatformRuntime {
    Write-Host "`nMicrosoft Speech Platform Runtime kuruluyor..." -ForegroundColor Yellow
    
    # URLs for Speech Platform Runtime v11
    $speechPlatformUrl = "https://download.microsoft.com/download/A/6/4/A64012D6-D56F-4E58-85E3-531E56ABC0E6/x64_SpeechPlatformRuntime.msi"
    $turkishVoiceUrl = "https://download.microsoft.com/download/4/0/D/40D31225-F759-4B56-B2B6-31FCCB46213F/MSSpeech_TTS_tr-TR_Tolga.msi"
    
    $tempDir = "$env:TEMP\QuadroAI_Speech"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    try {
        # Speech Platform Runtime'ı indir ve kur
        $platformInstaller = "$tempDir\SpeechPlatformRuntime.msi"
        if (-not (Test-Path $platformInstaller)) {
            Write-Host "  Speech Platform Runtime indiriliyor..." -ForegroundColor Yellow
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $speechPlatformUrl -OutFile $platformInstaller -UseBasicParsing
        }
        
        Write-Host "  Speech Platform Runtime kuruluyor..." -ForegroundColor Yellow
        Start-Process msiexec.exe -ArgumentList "/i `"$platformInstaller`" /quiet /norestart" -Wait
        
        # Türkçe Tolga sesini indir ve kur
        $voiceInstaller = "$tempDir\MSSpeech_TTS_tr-TR_Tolga.msi"
        if (-not (Test-Path $voiceInstaller)) {
            Write-Host "  Tolga sesi indiriliyor..." -ForegroundColor Yellow
            Invoke-WebRequest -Uri $turkishVoiceUrl -OutFile $voiceInstaller -UseBasicParsing
        }
        
        Write-Host "  Tolga sesi kuruluyor..." -ForegroundColor Yellow
        Start-Process msiexec.exe -ArgumentList "/i `"$voiceInstaller`" /quiet /norestart" -Wait
        
        Write-Host "✓ Speech Platform Runtime ve Tolga sesi kuruldu" -ForegroundColor Green
        $script:speechPlatformInstalled = $true
        
        # Temp dosyaları temizle
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        
        return $true
    } catch {
        Write-Host "✗ Speech Platform kurulumu başarısız: $_" -ForegroundColor Red
        return $false
    }
}

# Ana kurulum akışı
$winVer = Get-WindowsVersion
Write-Host "`nSistem: Windows $($winVer.Major) Build $($winVer.Build)" -ForegroundColor Cyan
if ($winVer.IsWindows11) {
    Write-Host "✓ Windows 11 algılandı" -ForegroundColor Green
} elseif ($winVer.IsWindows10) {
    Write-Host "✓ Windows 10 algılandı" -ForegroundColor Green
} else {
    Write-Host "⚠ Desteklenmeyen Windows sürümü" -ForegroundColor Yellow
}

# Kontroller
$langOK = Test-TurkishLanguagePack
$voiceOK = Test-TurkishTTSVoices
$platformOK = Test-SpeechPlatform

# Eksikleri kur
if (-not $langOK) {
    Install-TurkishLanguagePack | Out-Null
}

if (-not $voiceOK -and -not $platformOK) {
    Enable-SpeechFeatures | Out-Null
    Install-SpeechPlatformRuntime | Out-Null
    
    # Tekrar kontrol et
    $voiceOK = Test-TurkishTTSVoices
    $platformOK = Test-SpeechPlatform
}

# Registry ayarlarını her zaman yap
Configure-TTSRegistry | Out-Null

# Sonuç raporu
Write-Host "`n===============================================" -ForegroundColor Cyan
Write-Host "Kurulum Özeti:" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

if ($script:turkishLanguageInstalled) {
    Write-Host "✓ Türkçe Dil Paketi: KURULU" -ForegroundColor Green
} else {
    Write-Host "⚠ Türkçe Dil Paketi: KURULU DEĞİL" -ForegroundColor Yellow
}

if ($script:turkishVoiceInstalled) {
    Write-Host "✓ Türkçe TTS Sesleri: KURULU" -ForegroundColor Green
} else {
    Write-Host "⚠ Türkçe TTS Sesleri: KURULU DEĞİL" -ForegroundColor Yellow
}

if ($script:speechPlatformInstalled) {
    Write-Host "✓ Speech Platform: KURULU" -ForegroundColor Green
} else {
    Write-Host "⚠ Speech Platform: KURULU DEĞİL" -ForegroundColor Yellow
}

# Not
if (-not $script:turkishVoiceInstalled) {
    Write-Host "`n📌 NOT: Türkçe ses kurulumu için sistem yeniden başlatma gerekebilir." -ForegroundColor Yellow
    Write-Host "   QuadroAIPilot yine de WebSpeech API ve Edge TTS kullanarak çalışacaktır." -ForegroundColor Yellow
}

# Çıkış kodu
if ($script:turkishVoiceInstalled -or $script:speechPlatformInstalled) {
    Write-Host "`n✓ TÜRKÇE SES DESTEĞİ HAZIR" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠ TÜRKÇE SES KISMİ OLARAK HAZIR" -ForegroundColor Yellow
    exit 0  # Hata olsa da kuruluma devam et
}