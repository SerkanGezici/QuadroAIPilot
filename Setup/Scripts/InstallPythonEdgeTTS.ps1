# InstallPythonEdgeTTS.ps1
# Python ve edge-tts kurulum scripti
# QuadroAIPilot TTS sistemi icin gerekli

# Hata durumunda bile devam et
$ErrorActionPreference = "Continue"

# Basit log - ilk satir
$startTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"[$startTime] Script baslatildi" | Out-File "$env:TEMP\QuadroAI_PythonInstall.log" -Force

# Transcript basla
Start-Transcript -Path "$env:TEMP\QuadroAI_PythonInstall_Transcript.log" -Force

$logFile = "$env:TEMP\QuadroAI_PythonInstall.log"
function Write-Log {
    param([string]$Message)
    try {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        "[$timestamp] $Message" | Out-File -FilePath $logFile -Append -Force
        Write-Host $Message
    } catch {
        Write-Host "LOG YAZMA HATASI: $_"
    }
}

Write-Log "==============================================="
Write-Log "Python ve Edge-TTS Kurulum Kontrolu"
Write-Log "==============================================="
Write-Log "Script dizini: $PSScriptRoot"
Write-Log "Kullanici: $env:USERNAME"
Write-Log "Temp dizini: $env:TEMP"

$script:pythonInstalled = $false
$script:edgeTTSInstalled = $false
$script:pythonPath = ""

# Python yollarini kontrol et
function Test-PythonInstallation {
    Write-Log "`nPython kurulumu kontrol ediliyor..."
    
    # Farkli Python konumlarini kontrol et
    $pythonPaths = @(
        "python",
        "python3",
        "py",
        "$env:LOCALAPPDATA\Programs\Python\Python*\python.exe",
        "$env:ProgramFiles\Python*\python.exe",
        "$env:ProgramFiles(x86)\Python*\python.exe",
        "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps\python.exe"
    )
    
    foreach ($path in $pythonPaths) {
        try {
            $testPath = if ($path -like "*\*") { 
                Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
            } else { 
                $path 
            }
            
            if ($testPath) {
                $version = & $testPath --version 2>&1
                if ($version -match "Python (\d+\.\d+)") {
                    $versionNumber = [version]$matches[1]
                    if ($versionNumber -ge [version]"3.7") {
                        Write-Host "[OK] Python bulundu: $version" -ForegroundColor Green
                        Write-Host "  Konum: $testPath" -ForegroundColor Gray
                        $script:pythonInstalled = $true
                        $script:pythonPath = $testPath
                        return $true
                    } else {
                        Write-Host "[UYARI] Python surumu cok eski: $version (3.7+ gerekli)" -ForegroundColor Yellow
                    }
                }
            }
        } catch {
            # Sessizce devam et
        }
    }
    
    Write-Host "[HATA] Python kurulu degil veya PATH'de degil" -ForegroundColor Red
    return $false
}

# Edge-TTS kurulumunu kontrol et
function Test-EdgeTTSInstallation {
    if (-not $script:pythonInstalled) {
        return $false
    }
    
    Write-Host "`nedge-tts paketi kontrol ediliyor..." -ForegroundColor Yellow
    
    try {
        $pipList = & $script:pythonPath -m pip list 2>&1
        if ($pipList -match "edge-tts") {
            $edgeVersion = & $script:pythonPath -c "import importlib.metadata; print(importlib.metadata.version('edge-tts'))" 2>&1
            Write-Host "[OK] edge-tts kurulu: v$edgeVersion" -ForegroundColor Green
            $script:edgeTTSInstalled = $true
            return $true
        }
    } catch {
        # pip veya edge-tts yok
    }
    
    Write-Host "[HATA] edge-tts paketi kurulu degil" -ForegroundColor Red
    return $false
}

# Embedded Python kur (portable)
function Install-EmbeddedPython {
    Write-Host "`nPython kurulumu baslatiliyor..." -ForegroundColor Yellow
    
    $pythonVersion = "3.11.7"
    $pythonUrl = "https://www.python.org/ftp/python/$pythonVersion/python-$pythonVersion-embed-amd64.zip"
    $pipUrl = "https://bootstrap.pypa.io/get-pip.py"
    
    $pythonDir = "$env:LOCALAPPDATA\QuadroAIPilot\Python"
    $pythonZip = "$env:TEMP\python-embed.zip"
    $getPipScript = "$env:TEMP\get-pip.py"
    
    try {
        # Python dizinini oluştur
        if (-not (Test-Path $pythonDir)) {
            New-Item -ItemType Directory -Path $pythonDir -Force | Out-Null
        }
        
        # Python'u indir
        Write-Host "Python indiriliyor..." -ForegroundColor Yellow
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $pythonUrl -OutFile $pythonZip -UseBasicParsing
        
        # Python'u çıkart
        Write-Host "Python çıkartılıyor..." -ForegroundColor Yellow
        Expand-Archive -Path $pythonZip -DestinationPath $pythonDir -Force
        
        # python._pth dosyasını düzenle (pip'i etkinleştir)
        $pthFile = "$pythonDir\python311._pth"
        if (Test-Path $pthFile) {
            $pthContent = Get-Content $pthFile
            $pthContent = $pthContent -replace "#import site", "import site"
            $pthContent += "`nLib\site-packages"
            Set-Content -Path $pthFile -Value $pthContent
        }
        
        # pip'i kur
        Write-Host "pip kurulumu yapılıyor..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $pipUrl -OutFile $getPipScript -UseBasicParsing
        & "$pythonDir\python.exe" $getPipScript --no-warn-script-location
        
        # PATH'e ekle (kalıcı)
        $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
        if ($currentPath -notlike "*$pythonDir*") {
            [Environment]::SetEnvironmentVariable("PATH", "$pythonDir;$pythonDir\Scripts;$currentPath", "User")
            $env:PATH = "$pythonDir;$pythonDir\Scripts;$env:PATH"
        }
        
        $script:pythonPath = "$pythonDir\python.exe"
        $script:pythonInstalled = $true
        
        Write-Host "[OK] Python basariyla kuruldu: $pythonDir" -ForegroundColor Green
        
        # Temp dosyaları temizle
        Remove-Item -Path $pythonZip -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $getPipScript -Force -ErrorAction SilentlyContinue
        
        return $true
    } catch {
        Write-Host "[HATA] Python kurulumu basarisiz: $_" -ForegroundColor Red
        return $false
    }
}

# edge-tts paketini kur
function Install-EdgeTTS {
    if (-not $script:pythonInstalled) {
        Write-Host "✗ Python kurulu değil, edge-tts kurulamaz!" -ForegroundColor Red
        return $false
    }
    
    Write-Host "`nedge-tts paketi kuruluyor..." -ForegroundColor Yellow
    
    try {
        # pip'i güncelle
        Write-Log "pip guncelleniyor..."
        & $script:pythonPath -m pip install --upgrade pip --quiet
        
        # edge-tts'i kur
        Write-Host "edge-tts kuruluyor..." -ForegroundColor Yellow
        & $script:pythonPath -m pip install edge-tts --quiet
        
        # Kurulumu doğrula
        $verification = & $script:pythonPath -c "import edge_tts; print('OK')" 2>&1
        if ($verification -eq "OK") {
            $version = & $script:pythonPath -c "import importlib.metadata; print(importlib.metadata.version('edge-tts'))" 2>&1
            Write-Log "[OK] edge-tts basariyla kuruldu: v$version"
            $script:edgeTTSInstalled = $true
            return $true
        } else {
            throw "edge-tts import edilemedi"
        }
    } catch {
        Write-Log "[HATA] edge-tts kurulumu basarisiz: $_"
        return $false
    }
}

# Test fonksiyonu
function Test-EdgeTTSFunctionality {
    if (-not $script:edgeTTSInstalled) {
        return $false
    }
    
    Write-Host "`nEdge-TTS işlevsellik testi..." -ForegroundColor Yellow
    
    try {
        $testScript = @"
import asyncio
import edge_tts

async def test():
    voices = await edge_tts.list_voices()
    turkish_voices = [v for v in voices if 'tr-TR' in v['Locale']]
    if turkish_voices:
        print(f'Türkçe ses sayısı: {len(turkish_voices)}')
        for voice in turkish_voices[:2]:
            print(f"  - {voice['ShortName']}: {voice['FriendlyName']}")
        return True
    return False

result = asyncio.run(test())
print('Test başarılı' if result else 'Test başarısız')
"@
        
        $testResult = $testScript | & $script:pythonPath 2>&1
        if ($testResult -match "Test başarılı") {
            Write-Host "✓ Edge-TTS test başarılı!" -ForegroundColor Green
            Write-Host $testResult -ForegroundColor Gray
            return $true
        } else {
            Write-Host "⚠ Edge-TTS çalışıyor ama Türkçe ses bulunamadı" -ForegroundColor Yellow
            return $true  # Yine de başarılı say
        }
    } catch {
        Write-Host "✗ Edge-TTS test başarısız: $_" -ForegroundColor Red
        return $false
    }
}

# Ana kurulum akışı
Write-Host ""
$pythonOK = Test-PythonInstallation
if (-not $pythonOK) {
    $pythonOK = Install-EmbeddedPython
}

if ($pythonOK) {
    $edgeTTSOK = Test-EdgeTTSInstallation
    if (-not $edgeTTSOK) {
        $edgeTTSOK = Install-EdgeTTS
    }
    
    if ($edgeTTSOK) {
        Test-EdgeTTSFunctionality | Out-Null
    }
}

# Sonuç raporu
Write-Host "`n===============================================" -ForegroundColor Cyan
Write-Host "Kurulum Özeti:" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

if ($script:pythonInstalled) {
    Write-Host "✓ Python: KURULU" -ForegroundColor Green
    Write-Host "  Konum: $script:pythonPath" -ForegroundColor Gray
} else {
    Write-Host "✗ Python: KURULU DEĞİL" -ForegroundColor Red
}

if ($script:edgeTTSInstalled) {
    Write-Host "✓ edge-tts: KURULU" -ForegroundColor Green
} else {
    Write-Host "✗ edge-tts: KURULU DEĞİL" -ForegroundColor Red
}

# Cikis kodu
if ($script:pythonInstalled -and $script:edgeTTSInstalled) {
    Write-Log "[BASARILI] TUM GEREKSINIMLER KARSILANDI"
    Stop-Transcript
    exit 0
} else {
    Write-Log "[UYARI] BAZI GEREKSINIMLER EKSIK"
    Write-Log "QuadroAIPilot yine de calisacak ancak TTS ozellikleri sinirli olabilir."
    Stop-Transcript
    exit 0  # Hata olsa da kuruluma devam et - exit 1 kurulumu durdurabilir
}