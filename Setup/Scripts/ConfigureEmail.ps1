# ConfigureEmail.ps1
# E-posta sistemi (MAPI) yapılandırması
# QuadroAIPilot MAPI ve Outlook entegrasyonu kullanıyor

Write-Host "E-posta sistemi yapılandırılıyor..." -ForegroundColor Yellow

try {
    # MAPI32.dll kontrolü
    Write-Host "MAPI bileşenleri kontrol ediliyor..."
    $mapiPath = "${env:WINDIR}\System32\MAPI32.dll"
    if (-not (Test-Path $mapiPath)) {
        Write-Host "MAPI32.dll bulunamadı. Windows Mail uygulaması yüklenecek..." -ForegroundColor Yellow
        
        # Windows Mail uygulamasını yüklemeyi dene
        try {
            Get-AppxPackage -Name "microsoft.windowscommunicationsapps" -AllUsers | 
                ForEach-Object {Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml"}
        }
        catch {
            Write-Host "Windows Mail yüklenemedi: $_" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "MAPI32.dll bulundu." -ForegroundColor Green
    }
    
    # Outlook kontrolü
    Write-Host "`nMicrosoft Outlook kontrol ediliyor..."
    $outlookPaths = @(
        "${env:ProgramFiles}\Microsoft Office\root\Office16\OUTLOOK.EXE",
        "${env:ProgramFiles(x86)}\Microsoft Office\root\Office16\OUTLOOK.EXE",
        "${env:ProgramFiles}\Microsoft Office\Office16\OUTLOOK.EXE",
        "${env:ProgramFiles(x86)}\Microsoft Office\Office16\OUTLOOK.EXE"
    )
    
    $outlookFound = $false
    foreach ($path in $outlookPaths) {
        if (Test-Path $path) {
            $outlookFound = $true
            Write-Host "Microsoft Outlook bulundu: $path" -ForegroundColor Green
            break
        }
    }
    
    if (-not $outlookFound) {
        Write-Host "Microsoft Outlook bulunamadı." -ForegroundColor Yellow
        Write-Host "E-posta özellikleri sınırlı olacak. Tam özellikler için Outlook önerilir." -ForegroundColor Yellow
    }
    
    # Default MAPI client kontrolü
    Write-Host "`nVarsayılan e-posta istemcisi kontrol ediliyor..."
    $defaultMailClient = Get-ItemProperty -Path "HKLM:\SOFTWARE\Clients\Mail" -Name "(Default)" -ErrorAction SilentlyContinue
    
    if ($defaultMailClient) {
        Write-Host "Varsayılan e-posta istemcisi: $($defaultMailClient.'(Default)')" -ForegroundColor Green
    }
    else {
        Write-Host "Varsayılan e-posta istemcisi ayarlanmamış." -ForegroundColor Yellow
    }
    
    # MAPI profili kontrolü
    Write-Host "`nMAPI profilleri kontrol ediliyor..."
    $profilesPath = "HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows Messaging Subsystem\Profiles"
    
    if (Test-Path $profilesPath) {
        $profiles = Get-ChildItem $profilesPath -ErrorAction SilentlyContinue
        if ($profiles.Count -gt 0) {
            Write-Host "MAPI profilleri bulundu:" -ForegroundColor Green
            foreach ($profile in $profiles) {
                Write-Host "  - $(Split-Path $profile.Name -Leaf)" -ForegroundColor Green
            }
        }
        else {
            Write-Host "MAPI profili bulunamadı." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "MAPI profil yapılandırması bulunamadı." -ForegroundColor Yellow
    }
    
    # QuadroAIPilot için MAPI ayarları
    Write-Host "`nQuadroAIPilot MAPI ayarları yapılandırılıyor..."
    
    # SimpleMAPI kullanımı için gerekli registry ayarları
    $mapiRegPath = "HKLM:\SOFTWARE\Microsoft\Windows Messaging Subsystem"
    if (-not (Test-Path $mapiRegPath)) {
        New-Item -Path $mapiRegPath -Force | Out-Null
    }
    Set-ItemProperty -Path $mapiRegPath -Name "MAPI" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $mapiRegPath -Name "MAPIX" -Value 1 -Type DWord -Force
    
    # 64-bit sistem için WOW64 ayarları
    if ([Environment]::Is64BitOperatingSystem) {
        $mapiRegPath32 = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Messaging Subsystem"
        if (-not (Test-Path $mapiRegPath32)) {
            New-Item -Path $mapiRegPath32 -Force | Out-Null
        }
        Set-ItemProperty -Path $mapiRegPath32 -Name "MAPI" -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $mapiRegPath32 -Name "MAPIX" -Value 1 -Type DWord -Force
    }
    
    Write-Host "`nE-posta sistemi yapılandırması tamamlandı." -ForegroundColor Green
    
    if ($outlookFound) {
        Write-Host "`nOutlook kurulu - Tüm e-posta özellikleri kullanılabilir." -ForegroundColor Green
    }
    else {
        Write-Host "`nOutlook kurulu değil - Temel e-posta özellikleri kullanılabilir." -ForegroundColor Yellow
        Write-Host "Tam özellikler için Microsoft Outlook kurulumu önerilir." -ForegroundColor Yellow
    }
}
catch {
    Write-Host "E-posta sistemi yapılandırması sırasında hata oluştu: $_" -ForegroundColor Red
    # E-posta hatası kritik değil, kuruluma devam edilebilir
    Write-Host "E-posta özellikleri kısıtlı olabilir." -ForegroundColor Yellow
}