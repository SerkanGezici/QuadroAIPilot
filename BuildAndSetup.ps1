# QuadroAIPilot - Otomatik Build ve Setup Script'i
# Bu script clean build, publish, dil temizliği ve setup oluşturma işlemlerini otomatikleştirir

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "QuadroAIPilot Build & Setup Automation" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Proje kök dizini
$ProjectRoot = $PSScriptRoot
Set-Location $ProjectRoot

# 1. CLEAN BUILD
Write-Host "[1/6] Temizlik yapiliyor..." -ForegroundColor Yellow
dotnet clean
if ($LASTEXITCODE -ne 0) {
    Write-Host "HATA: Clean islemi basarisiz!" -ForegroundColor Red
    exit 1
}
Write-Host "OK: Temizlik tamamlandi" -ForegroundColor Green

# 2. RELEASE BUILD
Write-Host "`n[2/6] Release build yapiliyor..." -ForegroundColor Yellow
dotnet build -c Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    Write-Host "HATA: Build islemi basarisiz!" -ForegroundColor Red
    exit 1
}
Write-Host "OK: Build basarili" -ForegroundColor Green

# 3. SELF-CONTAINED PUBLISH
Write-Host "`n[3/6] Self-contained publish yapiliyor..." -ForegroundColor Yellow
dotnet publish -c Release -p:Platform=x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    Write-Host "HATA: Publish islemi basarisiz!" -ForegroundColor Red
    exit 1
}
Write-Host "OK: Publish tamamlandi" -ForegroundColor Green

# 4. DİL KLASÖRLERİNİ TEMİZLE
Write-Host "`n[4/6] Gereksiz dil klasorleri temizleniyor..." -ForegroundColor Yellow
$cleanScript = Join-Path $ProjectRoot "Scripts\CleanLanguageFolders.ps1"
if (Test-Path $cleanScript) {
    & $cleanScript -PublishPath "bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"
} else {
    Write-Host "UYARI: Temizlik script'i bulunamadi" -ForegroundColor Yellow
}

# 5. EDGE MANIFEST KONTROLÜ VE KOPYALAMA
Write-Host "`n[5/6] Edge manifest.json kontrol ediliyor..." -ForegroundColor Yellow

# Ana kaynak manifest'leri kontrol et
$edgeManifestSource = Join-Path $ProjectRoot "BrowserExtensions\Edge\manifest.json"

# Edge manifest'te CSP ve minimum_edge_version OLMADIĞINDAN emin ol (Manifest V3 için gerekli!)
if (Test-Path $edgeManifestSource) {
    $edgeContent = Get-Content $edgeManifestSource -Raw
    $hasChanges = $false

    # CSP kontrolü ve kaldırma
    if ($edgeContent -match "content_security_policy") {
        Write-Host "  UYARI: Edge manifest'ten CSP kaldiriliyor (Manifest V3)..." -ForegroundColor Yellow
        $edgeContent = $edgeContent -replace ',?\s*"content_security_policy"\s*:\s*\{[^}]*\}', ''
        $hasChanges = $true
    }

    # minimum_edge_version kontrolü ve kaldırma
    if ($edgeContent -match "minimum_edge_version") {
        Write-Host "  UYARI: Edge manifest'ten minimum_edge_version kaldiriliyor..." -ForegroundColor Yellow
        $edgeContent = $edgeContent -replace ',?\s*"minimum_edge_version"\s*:\s*"[^"]*"', ''
        $hasChanges = $true
    }

    if ($hasChanges) {
        Set-Content -Path $edgeManifestSource -Value $edgeContent -Force
        Write-Host "  OK: Edge manifest temizlendi" -ForegroundColor Green
    } else {
        Write-Host "  OK: Edge manifest temiz" -ForegroundColor Green
    }
}

# Publish'teki BrowserExtensions klasörünü güncelle
$publishBrowserExt = Join-Path $ProjectRoot "bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\BrowserExtensions"
if (Test-Path $publishBrowserExt) {
    Remove-Item -Path $publishBrowserExt -Recurse -Force -ErrorAction SilentlyContinue
}

# Yeni klasör oluştur ve dosyaları kopyala
New-Item -ItemType Directory -Path $publishBrowserExt -Force | Out-Null
New-Item -ItemType Directory -Path "$publishBrowserExt\Edge" -Force | Out-Null
New-Item -ItemType Directory -Path "$publishBrowserExt\Chrome" -Force | Out-Null

Copy-Item -Path "$ProjectRoot\BrowserExtensions\Edge\*" -Destination "$publishBrowserExt\Edge\" -Force
Copy-Item -Path "$ProjectRoot\BrowserExtensions\Chrome\*" -Destination "$publishBrowserExt\Chrome\" -Force

Write-Host "  OK: BrowserExtensions publish'e kopyalandi" -ForegroundColor Green

# 6. SETUP OLUŞTUR
Write-Host "`n[6/6] Setup dosyasi olusturuluyor..." -ForegroundColor Yellow
$innoSetupPath = "C:\Users\serkan\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
$issFile = Join-Path $ProjectRoot "Setup\QuadroAIPilot.iss"

if (Test-Path $innoSetupPath) {
    & $innoSetupPath /Q $issFile
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: Setup dosyasi basariyla olusturuldu!" -ForegroundColor Green

        # Output klasörünü göster
        $outputDir = Join-Path $ProjectRoot "Output"
        $setupFiles = Get-ChildItem -Path $outputDir -Filter "*.exe" | Sort-Object LastWriteTime -Descending

        Write-Host "`nOlusturulan Setup Dosyalari:" -ForegroundColor Cyan
        foreach ($file in $setupFiles | Select-Object -First 3) {
            $sizeMB = [math]::Round($file.Length / 1MB, 2)
            $fileName = $file.Name
            $fileTime = $file.LastWriteTime
            Write-Host "  - $fileName ($sizeMB MB) - $fileTime" -ForegroundColor White
        }
    } else {
        Write-Host "HATA: Setup olusturma basarisiz!" -ForegroundColor Red
    }
} else {
    Write-Host "HATA: Inno Setup bulunamadi!" -ForegroundColor Red
}

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "Islem Tamamlandi!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan