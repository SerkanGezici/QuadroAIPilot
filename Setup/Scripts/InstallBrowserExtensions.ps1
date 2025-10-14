# InstallBrowserExtensions.ps1
# Tarayıcı eklentileri için geliştirilmiş kurulum yardımcısı

param(
    [Parameter(Mandatory=$true)]
    [string]$AppPath
)

Write-Host "`n=== QuadroAI Tarayıcı Eklenti Kurulum Yardımcısı ===" -ForegroundColor Cyan
Write-Host "Tarayıcı eklentileri yapılandırılıyor..." -ForegroundColor Yellow

$extensionsConfigured = 0

# Chrome ve Edge extension ID'leri (store'a yüklendiğinde güncellenecek)
$chromeExtensionId = "ohidfnbapogpbmeo"  # Local ID
$edgeExtensionId = "lfejjbajaoimefhm"    # Local ID

# Store URL'leri (extension'lar yayınlandığında güncellenecek)
$chromeStoreUrl = "https://chrome.google.com/webstore"  # Henüz yayınlanmadı
$edgeStoreUrl = "https://microsoftedge.microsoft.com/addons"  # Henüz yayınlanmadı

try {
    # Native Messaging Host manifest içeriği
    $nmhManifest = @{
        "name" = "com.quadroai.pilot"
        "description" = "QuadroAIPilot Native Messaging Host"
        "path" = "$AppPath\QuadroAIPilot.exe"
        "type" = "stdio"
        "allowed_origins" = @(
            "chrome-extension://$chromeExtensionId/",
            "chrome-extension://$edgeExtensionId/"
        )
    }
    
    # Chrome için yapılandırma
    Write-Host "`nGoogle Chrome kontrol ediliyor..." -ForegroundColor White
    $chromePath = "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe"
    $chromePath86 = "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
    
    if ((Test-Path $chromePath) -or (Test-Path $chromePath86)) {
        Write-Host "✓ Google Chrome bulundu." -ForegroundColor Green
        
        # HKCU kullanarak Native Messaging Host kaydı (admin gerekmez)
        $chromeNMHPath = "HKCU:\SOFTWARE\Google\Chrome\NativeMessagingHosts\com.quadroai.pilot"
        New-Item -Path $chromeNMHPath -Force | Out-Null
        
        # Chrome native messaging manifest
        $chromeManifestPath = "$AppPath\chrome_native_messaging.json"
        $nmhManifest | ConvertTo-Json -Depth 10 | Set-Content -Path $chromeManifestPath -Encoding UTF8
        Set-ItemProperty -Path $chromeNMHPath -Name "(Default)" -Value $chromeManifestPath
        
        Write-Host "✓ Chrome native messaging host yapılandırıldı." -ForegroundColor Green
        $extensionsConfigured++
    }
    else {
        Write-Host "- Google Chrome kurulu değil." -ForegroundColor Yellow
    }
    
    # Edge için yapılandırma
    Write-Host "`nMicrosoft Edge kontrol ediliyor..." -ForegroundColor White
    $edgePath = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
    $edgePath64 = "${env:ProgramFiles}\Microsoft\Edge\Application\msedge.exe"
    
    if ((Test-Path $edgePath) -or (Test-Path $edgePath64)) {
        Write-Host "✓ Microsoft Edge bulundu." -ForegroundColor Green
        
        # HKCU kullanarak Native Messaging Host kaydı (admin gerekmez)
        $edgeNMHPath = "HKCU:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\com.quadroai.pilot"
        New-Item -Path $edgeNMHPath -Force | Out-Null
        
        # Edge native messaging manifest
        $edgeManifestPath = "$AppPath\edge_native_messaging.json"
        $nmhManifest | ConvertTo-Json -Depth 10 | Set-Content -Path $edgeManifestPath -Encoding UTF8
        Set-ItemProperty -Path $edgeNMHPath -Name "(Default)" -Value $edgeManifestPath
        
        Write-Host "✓ Edge native messaging host yapılandırıldı." -ForegroundColor Green
        $extensionsConfigured++
    }
    else {
        Write-Host "- Microsoft Edge kurulu değil." -ForegroundColor Yellow
    }
    
    if ($extensionsConfigured -gt 0) {
        Write-Host "`n✓ Native messaging host yapılandırması tamamlandı!" -ForegroundColor Green
        
        # Kurulum seçenekleri
        Write-Host "`n=== TARAYICI EKLENTİSİ KURULUM SEÇENEKLERİ ===" -ForegroundColor Cyan
        
        Write-Host "`nSeçenek 1: KOLAY KURULUM (Önerilen)" -ForegroundColor Yellow
        Write-Host "Chrome Web Store veya Edge Add-ons'tan tek tıkla kurulum:" -ForegroundColor White
        Write-Host "• Chrome: $chromeStoreUrl" -ForegroundColor Gray
        Write-Host "• Edge: $edgeStoreUrl" -ForegroundColor Gray
        Write-Host "(Not: Eklentiler henüz mağazada yayınlanmadı)" -ForegroundColor DarkGray
        
        Write-Host "`nSeçenek 2: GELİŞTİRİCİ MODU İLE KURULUM" -ForegroundColor Yellow
        Write-Host "Local klasörden manuel kurulum için:" -ForegroundColor White
        
        # Chrome talimatları
        if (Test-Path "$AppPath\Extensions\Chrome") {
            Write-Host "`nChrome için:" -ForegroundColor Cyan
            Write-Host "1. Chrome'da chrome://extensions adresine gidin" -ForegroundColor White
            Write-Host "2. Sağ üstten 'Geliştirici modu' anahtarını açın" -ForegroundColor White
            Write-Host "3. 'Paketi açılmış öğe yükle' butonuna tıklayın" -ForegroundColor White
            Write-Host "4. Şu klasörü seçin:" -ForegroundColor White
            Write-Host "   $AppPath\Extensions\Chrome" -ForegroundColor Green
        }
        
        # Edge talimatları
        if (Test-Path "$AppPath\Extensions\Edge") {
            Write-Host "`nEdge için:" -ForegroundColor Cyan
            Write-Host "1. Edge'de edge://extensions adresine gidin" -ForegroundColor White
            Write-Host "2. Sol altta 'Geliştirici modu' anahtarını açın" -ForegroundColor White
            Write-Host "3. 'Paketi açılmış öğe yükle' butonuna tıklayın" -ForegroundColor White
            Write-Host "4. Şu klasörü seçin:" -ForegroundColor White
            Write-Host "   $AppPath\Extensions\Edge" -ForegroundColor Green
        }
        
        # Masaüstü kısayolu
        $desktopPath = [Environment]::GetFolderPath("Desktop")
        $shortcutPath = "$desktopPath\QuadroAI Eklentiler.lnk"
        
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = "$AppPath\Extensions"
        $shortcut.Description = "QuadroAI Tarayıcı Eklentileri"
        $shortcut.IconLocation = "$AppPath\QuadroAIPilot.exe,0"
        $shortcut.Save()
        
        Write-Host "`n✓ Masaüstünde 'QuadroAI Eklentiler' kısayolu oluşturuldu." -ForegroundColor Green
        
        # HTML kurulum rehberi oluştur
        $guideHtml = @"
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="UTF-8">
    <title>QuadroAI Tarayıcı Eklentisi Kurulum Rehberi</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; }
        h1 { color: #2196F3; }
        h2 { color: #1976D2; margin-top: 30px; }
        .browser-section { background: #f5f5f5; padding: 20px; margin: 20px 0; border-radius: 8px; }
        .chrome { border-left: 4px solid #4CAF50; }
        .edge { border-left: 4px solid #0078D4; }
        ol { line-height: 1.8; }
        .path { background: #e3f2fd; padding: 10px; font-family: monospace; border-radius: 4px; margin: 10px 0; }
        .note { background: #fff3cd; padding: 15px; border-radius: 4px; margin: 20px 0; }
        img { max-width: 100%; border: 1px solid #ddd; margin: 10px 0; }
    </style>
</head>
<body>
    <h1>QuadroAI Tarayıcı Eklentisi Kurulum Rehberi</h1>
    
    <div class="note">
        <strong>Not:</strong> Eklentiler yakında Chrome Web Store ve Edge Add-ons'ta yayınlanacak. 
        O zamana kadar geliştirici modu ile kurulum yapabilirsiniz.
    </div>
    
    <div class="browser-section chrome">
        <h2>🟢 Google Chrome</h2>
        <ol>
            <li>Chrome'u açın ve adres çubuğuna <strong>chrome://extensions</strong> yazın</li>
            <li>Sağ üst köşedeki <strong>Geliştirici modu</strong> anahtarını açın</li>
            <li><strong>Paketi açılmış öğe yükle</strong> butonuna tıklayın</li>
            <li>Açılan pencerede şu klasörü seçin:</li>
        </ol>
        <div class="path">$AppPath\Extensions\Chrome</div>
        <p>✅ Eklenti başarıyla yüklendi! Artık sağ tık menüsünden kullanabilirsiniz.</p>
    </div>
    
    <div class="browser-section edge">
        <h2>🔵 Microsoft Edge</h2>
        <ol>
            <li>Edge'i açın ve adres çubuğuna <strong>edge://extensions</strong> yazın</li>
            <li>Sol alt köşedeki <strong>Geliştirici modu</strong> anahtarını açın</li>
            <li><strong>Paketi açılmış öğe yükle</strong> butonuna tıklayın</li>
            <li>Açılan pencerede şu klasörü seçin:</li>
        </ol>
        <div class="path">$AppPath\Extensions\Edge</div>
        <p>✅ Eklenti başarıyla yüklendi! Artık sağ tık menüsünden kullanabilirsiniz.</p>
    </div>
    
    <h2>📖 Kullanım</h2>
    <ol>
        <li>Herhangi bir web sayfasında okumak istediğiniz metni seçin</li>
        <li>Sağ tıklayın</li>
        <li><strong>QuadroAI ile Oku</strong> seçeneğine tıklayın</li>
        <li>Metin otomatik olarak seslendirilecektir</li>
    </ol>
    
    <div class="note">
        <strong>Sorun mu yaşıyorsunuz?</strong><br>
        • QuadroAIPilot uygulamasının çalıştığından emin olun<br>
        • Mikrofon izinlerini kontrol edin<br>
        • Tarayıcıyı yeniden başlatmayı deneyin
    </div>
</body>
</html>
"@
        
        $guideHtml | Out-File -FilePath "$AppPath\Extensions\KurulumRehberi.html" -Encoding UTF8
        
        # Rehberi otomatik aç
        Start-Process "$AppPath\Extensions\KurulumRehberi.html"
        
        Write-Host "`n✓ Kurulum rehberi açıldı!" -ForegroundColor Green
    }
    else {
        Write-Host "`n⚠ Desteklenen tarayıcı bulunamadı." -ForegroundColor Yellow
        Write-Host "Chrome veya Edge kurulu değil." -ForegroundColor Yellow
    }
}
catch {
    Write-Host "`n❌ Hata oluştu: $_" -ForegroundColor Red
    Write-Host "Lütfen manuel kurulum yöntemini deneyin." -ForegroundColor Yellow
}

Write-Host "`n=== Kurulum yardımcısı tamamlandı ===" -ForegroundColor Cyan
Write-Host "Yardım için: support@quadroai.com" -ForegroundColor Gray