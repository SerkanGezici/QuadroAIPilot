# EnableWindowsFeatures.ps1
# Windows özelliklerini etkinleştirir

Write-Host "Windows özellikleri etkinleştiriliyor..." -ForegroundColor Yellow

try {
    # Media Feature Pack kontrolü ve kurulumu (N/KN versiyonlar için)
    $mediaFeatures = @(
        "MediaPlayback",
        "WindowsMediaPlayer"
    )
    
    foreach ($feature in $mediaFeatures) {
        $featureState = Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction SilentlyContinue
        if ($featureState -and $featureState.State -ne "Enabled") {
            Write-Host "Etkinleştiriliyor: $feature"
            Enable-WindowsOptionalFeature -Online -FeatureName $feature -NoRestart -ErrorAction SilentlyContinue
        }
    }
    
    # Speech Recognition özelliği
    Write-Host "Ses tanıma özellikleri kontrol ediliyor..."
    $speechFeature = Get-WindowsCapability -Online | Where-Object {$_.Name -like "*Speech*"}
    if ($speechFeature -and $speechFeature.State -ne "Installed") {
        Write-Host "Ses tanıma özellikleri yükleniyor..."
        Add-WindowsCapability -Online -Name $speechFeature.Name
    }
    
    # .NET Framework 3.5 (bazı eski bileşenler için gerekebilir)
    $netfx3 = Get-WindowsOptionalFeature -Online -FeatureName "NetFx3"
    if ($netfx3.State -ne "Enabled") {
        Write-Host ".NET Framework 3.5 etkinleştiriliyor..."
        Enable-WindowsOptionalFeature -Online -FeatureName "NetFx3" -NoRestart -ErrorAction SilentlyContinue
    }
    
    Write-Host "Windows özellikleri başarıyla etkinleştirildi." -ForegroundColor Green
}
catch {
    Write-Host "Windows özellikleri etkinleştirilirken hata oluştu: $_" -ForegroundColor Red
    exit 1
}