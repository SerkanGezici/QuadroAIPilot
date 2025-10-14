# QuadroAIPilot - Gereksiz Dil Klasörlerini Temizleme Script'i
# Bu script publish sonrası sadece tr-TR ve en-US dil klasörlerini bırakır

param(
    [string]$PublishPath = "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
)

Write-Host "Dil klasörleri temizleniyor..." -ForegroundColor Yellow

# Publish klasörünün varlığını kontrol et
if (-not (Test-Path $PublishPath)) {
    Write-Host "Publish klasörü bulunamadı: $PublishPath" -ForegroundColor Red
    exit 1
}

# Tüm dil klasörlerini bul
$languageFolders = Get-ChildItem -Path $PublishPath -Directory |
    Where-Object { $_.Name -match "^[a-z]{2}-[A-Z]{2}$" }

$removedCount = 0
$keptFolders = @()

foreach ($folder in $languageFolders) {
    if ($folder.Name -notmatch "^(tr-TR|en-US)$") {
        # Gereksiz dil klasörünü sil
        Remove-Item -Path $folder.FullName -Recurse -Force
        Write-Host "  Silindi: $($folder.Name)" -ForegroundColor Red
        $removedCount++
    } else {
        # Korunan klasör
        $keptFolders += $folder.Name
        Write-Host "  Korundu: $($folder.Name)" -ForegroundColor Green
    }
}

Write-Host "`nÖzet:" -ForegroundColor Cyan
Write-Host "  - $removedCount dil klasörü silindi" -ForegroundColor Yellow
Write-Host "  - Korunan klasörler: $($keptFolders -join ', ')" -ForegroundColor Green
Write-Host "Temizlik tamamlandı!" -ForegroundColor Green