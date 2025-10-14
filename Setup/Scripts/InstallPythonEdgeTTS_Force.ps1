# InstallPythonEdgeTTS_Force.ps1
# ZORLA Python ve edge-tts kurulum scripti
# Mevcut kurulum olsa bile yeniden kurar

# Hata durumunda bile devam et
$ErrorActionPreference = "Continue"

# Log dosyasi
$logFile = "$env:TEMP\QuadroAI_PythonInstall.log"
Write-Output "========================================" | Out-File $logFile -Append
Write-Output "[FORCE PS1] $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Out-File $logFile -Append
Write-Output "PowerShell Force Install baslatildi" | Out-File $logFile -Append
Write-Output "========================================" | Out-File $logFile -Append

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "QuadroAIPilot Python ZORLA Kurulum (PS1)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Python dizinini temizle ve yeniden olustur
$pythonDir = "$env:LOCALAPPDATA\QuadroAIPilot\Python"
Write-Host "Python dizini temizleniyor: $pythonDir" -ForegroundColor Yellow
Write-Output "Python dizini temizleniyor: $pythonDir" | Out-File $logFile -Append

if (Test-Path $pythonDir) {
    Remove-Item -Path $pythonDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Output "Eski Python dizini silindi" | Out-File $logFile -Append
}

New-Item -ItemType Directory -Path $pythonDir -Force | Out-Null
Write-Output "Yeni Python dizini olusturuldu" | Out-File $logFile -Append

# Python versiyonu ve URL'ler
$pythonVersion = "3.11.7"
$pythonUrl = "https://www.python.org/ftp/python/$pythonVersion/python-$pythonVersion-embed-amd64.zip"
$pipUrl = "https://bootstrap.pypa.io/get-pip.py"
$pythonZip = "$env:TEMP\python-embed.zip"
$getPipScript = "$env:TEMP\get-pip.py"

try {
    # TLS 1.2 aktif et
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    
    # Python'u indir
    Write-Host "Python indiriliyor..." -ForegroundColor Yellow
    Write-Output "Python indiriliyor: $pythonUrl" | Out-File $logFile -Append
    Invoke-WebRequest -Uri $pythonUrl -OutFile $pythonZip -UseBasicParsing
    Write-Output "Python indirildi: $pythonZip" | Out-File $logFile -Append
    
    # Python'u cikart
    Write-Host "Python arsivi aciliyor..." -ForegroundColor Yellow
    Write-Output "Python arsivi aciliyor..." | Out-File $logFile -Append
    Expand-Archive -Path $pythonZip -DestinationPath $pythonDir -Force
    Write-Output "Python arsivi acildi" | Out-File $logFile -Append
    
    # python._pth dosyasini duzenle
    $pthFile = "$pythonDir\python311._pth"
    Write-Host "Python._pth dosyasi duzenleniyor..." -ForegroundColor Yellow
    Write-Output "Python._pth dosyasi duzenleniyor..." | Out-File $logFile -Append
    
    $pthContent = @"
python311.zip
.
import site
Lib\site-packages
"@
    Set-Content -Path $pthFile -Value $pthContent
    Write-Output "Python._pth dosyasi duzenlendi" | Out-File $logFile -Append
    
    # pip'i indir ve kur
    Write-Host "pip indiriliyor ve kuruluyor..." -ForegroundColor Yellow
    Write-Output "pip indiriliyor: $pipUrl" | Out-File $logFile -Append
    Invoke-WebRequest -Uri $pipUrl -OutFile $getPipScript -UseBasicParsing
    Write-Output "pip indirildi" | Out-File $logFile -Append
    
    & "$pythonDir\python.exe" $getPipScript --no-warn-script-location 2>&1 | Out-File $logFile -Append
    Write-Output "pip kuruldu" | Out-File $logFile -Append
    
    # pip'i guncelle
    Write-Host "pip guncelleniyor..." -ForegroundColor Yellow
    & "$pythonDir\python.exe" -m pip install --upgrade pip --quiet 2>&1 | Out-File $logFile -Append
    Write-Output "pip guncellendi" | Out-File $logFile -Append
    
    # edge-tts'i kur (zorla yeniden kur)
    Write-Host "edge-tts kuruluyor..." -ForegroundColor Yellow
    Write-Output "edge-tts kuruluyor..." | Out-File $logFile -Append
    & "$pythonDir\python.exe" -m pip uninstall edge-tts -y --quiet 2>&1 | Out-File $logFile -Append
    & "$pythonDir\python.exe" -m pip install edge-tts --quiet 2>&1 | Out-File $logFile -Append
    Write-Output "edge-tts kuruldu" | Out-File $logFile -Append
    
    # PATH'e ekle
    Write-Host "PATH'e ekleniyor..." -ForegroundColor Yellow
    Write-Output "PATH'e ekleniyor..." | Out-File $logFile -Append
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($currentPath -notlike "*$pythonDir*") {
        [Environment]::SetEnvironmentVariable("PATH", "$pythonDir;$pythonDir\Scripts;$currentPath", "User")
        $env:PATH = "$pythonDir;$pythonDir\Scripts;$env:PATH"
        Write-Output "PATH guncellendi" | Out-File $logFile -Append
    }
    
    # Test
    Write-Host "Kurulum test ediliyor..." -ForegroundColor Yellow
    Write-Output "Kurulum test ediliyor..." | Out-File $logFile -Append
    
    $pythonVersion = & "$pythonDir\python.exe" --version 2>&1
    Write-Host "[OK] Python kuruldu: $pythonVersion" -ForegroundColor Green
    Write-Output "Python version: $pythonVersion" | Out-File $logFile -Append
    
    $edgeVersion = & "$pythonDir\python.exe" -c "import importlib.metadata; print(importlib.metadata.version('edge-tts'))" 2>&1
    Write-Host "[OK] edge-tts kuruldu: v$edgeVersion" -ForegroundColor Green
    Write-Output "edge-tts version: $edgeVersion" | Out-File $logFile -Append
    
    # Turkce sesleri test et
    Write-Host "Turkce sesler test ediliyor..." -ForegroundColor Yellow
    $testScript = @"
import asyncio
import edge_tts
import json

async def test():
    voices = await edge_tts.list_voices()
    turkish_voices = [v for v in voices if 'tr-TR' in v['Locale']]
    print(f'Turkce ses sayisi: {len(turkish_voices)}')
    for voice in turkish_voices[:3]:
        print(f"  - {voice['ShortName']}: {voice['FriendlyName']}")
    return len(turkish_voices) > 0

result = asyncio.run(test())
"@
    $testResult = $testScript | & "$pythonDir\python.exe" 2>&1
    Write-Host $testResult -ForegroundColor Gray
    Write-Output "Test sonucu: $testResult" | Out-File $logFile -Append
    
    # Temizlik
    Remove-Item -Path $pythonZip -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $getPipScript -Force -ErrorAction SilentlyContinue
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "[BASARILI] Python ve edge-tts kurulumu tamamlandi!" -ForegroundColor Green
    Write-Host "Konum: $pythonDir" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Output "[BASARILI] Kurulum tamamlandi" | Out-File $logFile -Append
    
    exit 0
}
catch {
    Write-Host "[HATA] Kurulum sirasinda hata olustu: $_" -ForegroundColor Red
    Write-Output "[HATA] $_" | Out-File $logFile -Append
    Write-Output $_.Exception.Message | Out-File $logFile -Append
    Write-Output $_.ScriptStackTrace | Out-File $logFile -Append
    exit 1
}