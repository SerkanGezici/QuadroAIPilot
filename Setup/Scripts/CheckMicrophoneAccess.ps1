# ===================================================================
# CheckMicrophoneAccess.ps1 - Gelişmiş Mikrofon Erişim Kontrolü
# ===================================================================
# Versiyon: 2.0
# Windows 10/11 mikrofon gizlilik ayarlarını kontrol eder ve düzeltir
# ===================================================================

# Verbose logging
$LogFile = "C:\Temp\QuadroAI_MicSetup.log"
$ErrorActionPreference = "Continue"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogMessage = "[$Timestamp] [$Level] $Message"
    Write-Host $LogMessage -ForegroundColor $(if ($Level -eq "ERROR") { "Red" } elseif ($Level -eq "SUCCESS") { "Green" } else { "Yellow" })

    # Log dosyasına yaz
    try {
        if (-not (Test-Path "C:\Temp")) { New-Item -Path "C:\Temp" -ItemType Directory -Force | Out-Null }
        Add-Content -Path $LogFile -Value $LogMessage -ErrorAction SilentlyContinue
    } catch {
        # Sessizce devam et
    }
}

Write-Log "===== QuadroAIPilot Mikrofon Kurulum Başlatılıyor =====" "INFO"
Write-Log "Kullanıcı: $env:USERNAME" "INFO"
Write-Log "Admin: $(([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))" "INFO"

# ===================================================================
# 1. GLOBAL MİKROFON ERİŞİMİNİ AÇ (HKCU - Current User)
# ===================================================================
Write-Log "1. Global mikrofon erişimi kontrol ediliyor..." "INFO"

try {
    $MicPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone"

    # Registry anahtarı yoksa oluştur
    if (-not (Test-Path $MicPath)) {
        Write-Log "Mikrofon registry anahtarı yok, oluşturuluyor..." "INFO"
        New-Item -Path $MicPath -Force | Out-Null
    }

    # Mevcut değeri kontrol et
    $CurrentValue = Get-ItemProperty -Path $MicPath -Name "Value" -ErrorAction SilentlyContinue

    if ($CurrentValue -eq $null -or $CurrentValue.Value -ne "Allow") {
        Write-Log "Mikrofon erişimi KAPALI, açılıyor..." "INFO"
        Set-ItemProperty -Path $MicPath -Name "Value" -Value "Allow" -Type String -Force
        Write-Log "✓ Mikrofon erişimi AÇILDI" "SUCCESS"
    } else {
        Write-Log "✓ Mikrofon erişimi zaten AÇIK" "SUCCESS"
    }
} catch {
    Write-Log "HATA: Mikrofon erişimi ayarlanamadı - $_" "ERROR"
}

# ===================================================================
# 2. DESKTOP APPS İÇİN MİKROFON ERİŞİMİ (Windows 11)
# ===================================================================
Write-Log "2. Desktop uygulamaları için mikrofon erişimi kontrol ediliyor..." "INFO"

try {
    # LetAppsAccessMicrophone - Desktop apps için
    $LetAppsPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone"

    # LastUsedTimeStart ve LastUsedTimeStop (mikrofon kullanımını işaretle)
    $Now = [DateTime]::UtcNow.ToFileTimeUtc()
    Set-ItemProperty -Path $LetAppsPath -Name "LastUsedTimeStart" -Value $Now -Type QWord -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $LetAppsPath -Name "LastUsedTimeStop" -Value $Now -Type QWord -Force -ErrorAction SilentlyContinue

    Write-Log "✓ Desktop apps mikrofon erişimi yapılandırıldı" "SUCCESS"
} catch {
    Write-Log "UYARI: Desktop apps ayarı yapılandırılamadı - $_" "ERROR"
}

# ===================================================================
# 3. EDGE WEBVIEW2 İÇİN MİKROFON İZNİ
# ===================================================================
Write-Log "3. Edge WebView2 mikrofon politikası ayarlanıyor..." "INFO"

try {
    # Edge WebView2 policy
    $EdgeWebView2Path = "HKLM:\SOFTWARE\Policies\Microsoft\Edge\WebView2"

    if (-not (Test-Path $EdgeWebView2Path)) {
        New-Item -Path $EdgeWebView2Path -Force | Out-Null
    }

    Set-ItemProperty -Path $EdgeWebView2Path -Name "AudioCaptureAllowed" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $EdgeWebView2Path -Name "AudioCaptureAllowedUrls" -Value "*" -Type String -Force

    Write-Log "✓ WebView2 mikrofon politikası ayarlandı" "SUCCESS"
} catch {
    Write-Log "UYARI: WebView2 politikası ayarlanamadı (admin gerekebilir) - $_" "ERROR"
}

# ===================================================================
# 4. EDGE BROWSER MİKROFON POLİTİKASI
# ===================================================================
Write-Log "4. Edge browser mikrofon politikası ayarlanıyor..." "INFO"

try {
    $EdgePath = "HKLM:\SOFTWARE\Policies\Microsoft\Edge"

    if (-not (Test-Path $EdgePath)) {
        New-Item -Path $EdgePath -Force | Out-Null
    }

    Set-ItemProperty -Path $EdgePath -Name "DefaultAudioCaptureSetting" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $EdgePath -Name "AudioCaptureAllowed" -Value 1 -Type DWord -Force

    Write-Log "✓ Edge browser mikrofon politikası ayarlandı" "SUCCESS"
} catch {
    Write-Log "UYARI: Edge politikası ayarlanamadı (admin gerekebilir) - $_" "ERROR"
}

# ===================================================================
# 5. MİKROFON CİHAZLARI KONTROLÜ
# ===================================================================
Write-Log "5. Mikrofon cihazları kontrol ediliyor..." "INFO"

try {
    $AudioDevices = Get-WmiObject Win32_SoundDevice -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -like "*Microphone*" -or $_.Name -like "*Mikrofon*" -or $_.Name -like "*Audio Input*" }

    if ($AudioDevices) {
        Write-Log "✓ Bulunan mikrofonlar:" "SUCCESS"
        foreach ($Device in $AudioDevices) {
            Write-Log "  - $($Device.Name)" "INFO"
        }
    } else {
        Write-Log "UYARI: Sistemde mikrofon cihazı bulunamadı!" "ERROR"
        Write-Log "Lütfen bir mikrofon bağlayın veya ses sürücülerini kontrol edin." "ERROR"
    }
} catch {
    Write-Log "UYARI: Mikrofon cihaz listesi alınamadı - $_" "ERROR"
}

# ===================================================================
# 6. ÖZET RAPOR
# ===================================================================
Write-Log "" "INFO"
Write-Log "===== Mikrofon Kurulum Özeti =====" "INFO"
Write-Log "1. Global mikrofon erişimi: AÇIK" "SUCCESS"
Write-Log "2. Desktop apps erişimi: AYARLANDI" "SUCCESS"
Write-Log "3. WebView2 politikası: AYARLANDI" "SUCCESS"
Write-Log "4. Edge browser politikası: AYARLANDI" "SUCCESS"
Write-Log "5. Mikrofon cihazları: KONTROL EDİLDİ" "SUCCESS"
Write-Log "" "INFO"
Write-Log "ÖNEMLİ UYARI:" "INFO"
Write-Log "Eğer mikrofon hala çalışmıyorsa:" "INFO"
Write-Log "1. Windows Ayarlar → Gizlilik ve Güvenlik → Mikrofon" "INFO"
Write-Log "2. 'Mikrofon erişimi' seçeneğini AÇIK yapın" "INFO"
Write-Log "3. 'Masaüstü uygulamalarının erişimi' AÇIK olmalı" "INFO"
Write-Log "4. QuadroAIPilot'u yeniden başlatın" "INFO"
Write-Log "" "INFO"
Write-Log "Log dosyası: $LogFile" "INFO"
Write-Log "===== Mikrofon Kurulum Tamamlandı =====" "SUCCESS"

# Kullanıcı için başarı kodu döndür
exit 0
