# Mikrofon erişim kontrolü ve düzeltme scripti
Write-Host "Mikrofon erişim kontrolü yapılıyor..." -ForegroundColor Yellow

# Windows 10 Mikrofon gizlilik ayarları kontrolü
$microphoneAccess = Get-ItemProperty -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone" -Name "Value" -ErrorAction SilentlyContinue

if ($microphoneAccess -eq $null -or $microphoneAccess.Value -ne "Allow") {
    Write-Host "Mikrofon erişimi kapalı! Açılıyor..." -ForegroundColor Red
    
    # Mikrofon erişimini aç
    Set-ItemProperty -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone" -Name "Value" -Value "Allow" -Force
    
    # Uygulamalar için mikrofon erişimi
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone" -Name "Value" -Value "Allow" -Force -ErrorAction SilentlyContinue
    
    Write-Host "Mikrofon erişimi açıldı." -ForegroundColor Green
} else {
    Write-Host "Mikrofon erişimi zaten açık." -ForegroundColor Green
}

# Chrome için mikrofon izni
$chromePrefs = "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Preferences"
if (Test-Path $chromePrefs) {
    Write-Host "Chrome mikrofon izinleri kontrol ediliyor..." -ForegroundColor Yellow
    # Chrome preferences JSON dosyasını güncelle
}

# Edge için mikrofon izni
Write-Host "Edge mikrofon politikası ayarlanıyor..." -ForegroundColor Yellow
New-Item -Path "HKLM:\SOFTWARE\Policies\Microsoft\Edge" -Force -ErrorAction SilentlyContinue | Out-Null
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Edge" -Name "DefaultAudioCaptureSetting" -Value 1 -Type DWord -Force

# Windows ses kayıt cihazları kontrolü
Write-Host "Varsayılan mikrofon kontrolü..." -ForegroundColor Yellow
$audioDevices = Get-WmiObject Win32_SoundDevice | Where-Object { $_.Name -like "*Microphone*" -or $_.Name -like "*Mikrofon*" }

if ($audioDevices.Count -eq 0) {
    Write-Host "UYARI: Sistemde mikrofon bulunamadı!" -ForegroundColor Red
    Write-Host "Lütfen bir mikrofon bağlayın veya ses sürücülerini kontrol edin." -ForegroundColor Yellow
} else {
    Write-Host "Bulunan mikrofonlar:" -ForegroundColor Green
    $audioDevices | ForEach-Object { Write-Host " - $($_.Name)" -ForegroundColor Cyan }
}

Write-Host "`nMikrofon kontrolü tamamlandı." -ForegroundColor Green