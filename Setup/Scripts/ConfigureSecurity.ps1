# ConfigureSecurity.ps1
# Güvenlik ayarlarını yapılandırır

param(
    [Parameter(Mandatory=$true)]
    [string]$AppPath
)

Write-Host "Güvenlik ayarları yapılandırılıyor..." -ForegroundColor Yellow

try {
    # Windows Defender istisnası ekle
    Write-Host "Windows Defender istisnası ekleniyor..."
    try {
        Add-MpPreference -ExclusionPath $AppPath -ErrorAction Stop
        Add-MpPreference -ExclusionProcess "$AppPath\QuadroAIPilot.exe" -ErrorAction Stop
        Write-Host "Windows Defender istisnası eklendi." -ForegroundColor Green
    }
    catch {
        Write-Host "Windows Defender istisnası eklenemedi: $_" -ForegroundColor Yellow
    }
    
    # Windows Güvenlik Duvarı kuralları
    Write-Host "Güvenlik duvarı kuralları oluşturuluyor..."
    
    # Gelen bağlantılar için kural
    $inboundRule = Get-NetFirewallRule -DisplayName "QuadroAIPilot Inbound" -ErrorAction SilentlyContinue
    if (-not $inboundRule) {
        New-NetFirewallRule -DisplayName "QuadroAIPilot Inbound" `
                           -Direction Inbound `
                           -Program "$AppPath\QuadroAIPilot.exe" `
                           -Action Allow `
                           -Profile Any `
                           -Description "QuadroAIPilot uygulaması için gelen bağlantılara izin ver"
        Write-Host "Gelen bağlantı kuralı oluşturuldu." -ForegroundColor Green
    }
    
    # Giden bağlantılar için kural
    $outboundRule = Get-NetFirewallRule -DisplayName "QuadroAIPilot Outbound" -ErrorAction SilentlyContinue
    if (-not $outboundRule) {
        New-NetFirewallRule -DisplayName "QuadroAIPilot Outbound" `
                           -Direction Outbound `
                           -Program "$AppPath\QuadroAIPilot.exe" `
                           -Action Allow `
                           -Profile Any `
                           -Description "QuadroAIPilot uygulaması için giden bağlantılara izin ver"
        Write-Host "Giden bağlantı kuralı oluşturuldu." -ForegroundColor Green
    }
    
    # WebView2 için güvenlik duvarı kuralı
    $webViewPath = "${env:ProgramFiles(x86)}\Microsoft\EdgeWebView\Application\*\msedgewebview2.exe"
    $webViewRule = Get-NetFirewallRule -DisplayName "QuadroAIPilot WebView2" -ErrorAction SilentlyContinue
    if (-not $webViewRule) {
        New-NetFirewallRule -DisplayName "QuadroAIPilot WebView2" `
                           -Direction Outbound `
                           -Program $webViewPath `
                           -Action Allow `
                           -Profile Any `
                           -Description "QuadroAIPilot WebView2 bileşeni için internet erişimi"
    }
    
    # UAC için manifest kontrolü
    Write-Host "UAC ayarları kontrol ediliyor..."
    $manifestPath = "$AppPath\QuadroAIPilot.exe.manifest"
    if (-not (Test-Path $manifestPath)) {
        # Basit bir manifest oluştur
        $manifestContent = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <assemblyIdentity version="1.0.0.0" name="QuadroAIPilot.app"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
'@
        Set-Content -Path $manifestPath -Value $manifestContent -Encoding UTF8
    }
    
    # Mikrofon erişimi için Privacy ayarları
    Write-Host "Mikrofon erişim ayarları yapılandırılıyor..."
    $microphonePath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone"
    if (-not (Test-Path $microphonePath)) {
        New-Item -Path $microphonePath -Force | Out-Null
    }
    Set-ItemProperty -Path $microphonePath -Name "Value" -Value "Allow" -Type String
    
    # QuadroAIPilot için özel mikrofon izni
    $appMicPath = "$microphonePath\QuadroAIPilot"
    if (-not (Test-Path $appMicPath)) {
        New-Item -Path $appMicPath -Force | Out-Null
    }
    Set-ItemProperty -Path $appMicPath -Name "Value" -Value "Allow" -Type String
    
    # Developer mode kontrolü (opsiyonel)
    Write-Host "Developer mode durumu kontrol ediliyor..."
    $devModePath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
    $devModeEnabled = Get-ItemProperty -Path $devModePath -Name "AllowDevelopmentWithoutDevLicense" -ErrorAction SilentlyContinue
    
    if ($devModeEnabled.AllowDevelopmentWithoutDevLicense -ne 1) {
        Write-Host "Developer mode etkin değil. MSIX paketleri için gerekli olabilir." -ForegroundColor Yellow
    }
    
    # SmartScreen için güvenilir uygulama olarak işaretle
    Write-Host "SmartScreen ayarları yapılandırılıyor..."
    $smartScreenPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SmartScreen\AppReputation\FileRules"
    if (-not (Test-Path $smartScreenPath)) {
        New-Item -Path $smartScreenPath -Force | Out-Null
    }
    
    # Uygulama için güvenilir işareti
    $appHash = (Get-FileHash -Path "$AppPath\QuadroAIPilot.exe" -Algorithm SHA256).Hash
    $ruleName = "QuadroAIPilot_$appHash"
    New-ItemProperty -Path $smartScreenPath -Name $ruleName -Value "$AppPath\QuadroAIPilot.exe" -PropertyType String -Force | Out-Null
    
    Write-Host "Güvenlik ayarları başarıyla yapılandırıldı." -ForegroundColor Green
}
catch {
    Write-Host "Güvenlik ayarları yapılandırılırken hata oluştu: $_" -ForegroundColor Red
    exit 1
}