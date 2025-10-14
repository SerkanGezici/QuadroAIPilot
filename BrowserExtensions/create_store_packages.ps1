# Store için extension paketlerini oluştur

Write-Host "Store paketleri oluşturuluyor..." -ForegroundColor Green

# Output klasörü
$outputDir = ".\StorePackages"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Chrome paketi
Write-Host "`nChrome paketi hazırlanıyor..."
$chromeDir = ".\Chrome"
$chromeOutput = "$outputDir\QuadroAI_Chrome_Store.zip"

# Geçici klasör oluştur
$tempChromeDir = "$outputDir\temp_chrome"
if (Test-Path $tempChromeDir) {
    Remove-Item -Path $tempChromeDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempChromeDir -Force | Out-Null

# Dosyaları kopyala (key içermeyen manifest kullan)
Copy-Item -Path "$chromeDir\manifest_store.json" -Destination "$tempChromeDir\manifest.json"
Copy-Item -Path "$chromeDir\background.js" -Destination $tempChromeDir
Copy-Item -Path "$chromeDir\icon*.png" -Destination $tempChromeDir
Copy-Item -Path "$chromeDir\README.md" -Destination $tempChromeDir

# ZIP oluştur
Compress-Archive -Path "$tempChromeDir\*" -DestinationPath $chromeOutput -Force
Write-Host "Chrome paketi oluşturuldu: $chromeOutput" -ForegroundColor Green

# Edge paketi
Write-Host "`nEdge paketi hazırlanıyor..."
$edgeDir = ".\Edge"
$edgeOutput = "$outputDir\QuadroAI_Edge_Store.zip"

# Geçici klasör oluştur
$tempEdgeDir = "$outputDir\temp_edge"
if (Test-Path $tempEdgeDir) {
    Remove-Item -Path $tempEdgeDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempEdgeDir -Force | Out-Null

# Dosyaları kopyala (key içermeyen manifest kullan)
Copy-Item -Path "$edgeDir\manifest_store.json" -Destination "$tempEdgeDir\manifest.json"
Copy-Item -Path "$edgeDir\background.js" -Destination $tempEdgeDir
Copy-Item -Path "$edgeDir\icon*.png" -Destination $tempEdgeDir
Copy-Item -Path "$edgeDir\README.md" -Destination $tempEdgeDir

# ZIP oluştur
Compress-Archive -Path "$tempEdgeDir\*" -DestinationPath $edgeOutput -Force
Write-Host "Edge paketi oluşturuldu: $edgeOutput" -ForegroundColor Green

# Geçici klasörleri temizle
Remove-Item -Path $tempChromeDir -Recurse -Force
Remove-Item -Path $tempEdgeDir -Recurse -Force

# Store görselleri klasörü
$assetsDir = "$outputDir\StoreAssets"
if (-not (Test-Path $assetsDir)) {
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
}

Write-Host "`n=== STORE PAKETLERİ HAZIR ===" -ForegroundColor Cyan
Write-Host "Chrome Paketi: $chromeOutput" -ForegroundColor Yellow
Write-Host "Edge Paketi: $edgeOutput" -ForegroundColor Yellow
Write-Host "`nSonraki adımlar:" -ForegroundColor Green
Write-Host "1. Store görsellerini hazırlayın (440x280, 920x680, 1280x800)"
Write-Host "2. Chrome Web Store Developer Console'a gidin"
Write-Host "3. Microsoft Partner Center'a gidin"
Write-Host "4. Paketleri yükleyin ve bilgileri doldurun"
Write-Host "`nDokümantasyon:" -ForegroundColor Yellow
Write-Host "- STORE_DESCRIPTIONS.md - Açıklamalar"
Write-Host "- PRIVACY_POLICY.md - Gizlilik politikası"
Write-Host "- STORE_PREPARATION.md - Detaylı talimatlar"