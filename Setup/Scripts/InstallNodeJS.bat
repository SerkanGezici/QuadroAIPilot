@echo off
REM InstallNodeJS.bat - Node.js kurulumu (Claude CLI icin)
REM QuadroAIPilot AI Provider Setup

setlocal enabledelayedexpansion
set "LOGFILE=%TEMP%\QuadroAI_NodeJSInstall.log"

echo ============================================ > "%LOGFILE%"
echo QuadroAIPilot Node.js Kurulumu >> "%LOGFILE%"
echo %date% %time% >> "%LOGFILE%"
echo ============================================ >> "%LOGFILE%"

echo.
echo ============================================
echo    Node.js Kurulumu (Claude AI icin)
echo ============================================
echo.

REM Node.js zaten kurulu mu kontrol et
echo Node.js kontrol ediliyor... >> "%LOGFILE%"
where node >nul 2>&1
if %errorlevel% equ 0 (
    echo Node.js zaten kurulu, version: >> "%LOGFILE%"
    node --version >> "%LOGFILE%" 2>&1
    echo [BILGI] Node.js zaten kurulu >> "%LOGFILE%"
    echo Node.js zaten sistemde kurulu.
    echo.
    timeout /t 2 /nobreak > nul
    exit /b 0
)

REM Node.js MSI dosyasini bul - MUTLAK YOL (MSI installer .. karakterini kabul etmiyor)
set "SCRIPT_DIR=%~dp0"
set "NODE_MSI=%SCRIPT_DIR%..\Prerequisites\node-v20.11.1-x64.msi"

REM .. karakterini kaldir - mutlak yol olustur
for %%I in ("%NODE_MSI%") do set "NODE_MSI=%%~fI"

echo Aranan MSI konumu: %NODE_MSI% >> "%LOGFILE%"
echo Script konumu: %SCRIPT_DIR% >> "%LOGFILE%"
echo Calisan dizin: %CD% >> "%LOGFILE%"

if not exist "%NODE_MSI%" (
    echo [HATA] Node.js MSI bulunamadi: %NODE_MSI% >> "%LOGFILE%"
    echo.
    echo ============================================
    echo    KRITIK HATA: Node.js Kurulum Dosyasi Bulunamadi!
    echo ============================================
    echo.
    echo Beklenen konum: %NODE_MSI%
    echo Script konumu: %~dp0
    echo.
    echo Bu hata, setup paketinin eksik oldugunu gosterir.
    echo Lutfen setup dosyasini yeniden indirin.
    echo.
    echo Log dosyasi: %LOGFILE%
    echo.
    pause
    exit /b 1
)

echo [BASARILI] Node.js MSI bulundu: %NODE_MSI% >> "%LOGFILE%"

REM Node.js MSI kurulumu (sessiz mod)
echo Node.js kuruluyor... (1-2 dakika)
echo Node.js kuruluyor (msiexec)... >> "%LOGFILE%"

msiexec /i "%NODE_MSI%" /qn /norestart /l*v "%LOGFILE%_msi.txt"
set "MSI_EXIT=%errorlevel%"

REM MSI'nin dosya yazmayı tamamlamasini bekle
echo MSI tamamlaniyor... >> "%LOGFILE%"
timeout /t 5 /nobreak > nul

if %MSI_EXIT% neq 0 (
    echo [HATA] Node.js kurulum hatasi (exit code: %errorlevel%) >> "%LOGFILE%"
    echo MSI log: %LOGFILE%_msi.txt >> "%LOGFILE%"
    echo.
    echo ============================================
    echo    KRITIK HATA: Node.js Kurulamadi!
    echo ============================================
    echo.
    echo Exit Code: %errorlevel%
    echo.
    echo Olasi sebepler:
    echo - Yonetici yetkileri gerekli olabilir
    echo - Onceki Node.js kurulumu cakisiyor olabilir
    echo - Disk alani yetersiz
    echo - Windows Installer servisi calismiyordur
    echo.
    echo Log dosyalari:
    echo   Setup log: %LOGFILE%
    echo   MSI log  : %LOGFILE%_msi.txt
    echo.
    echo ONEMLI: Claude AI ozelligi bu hatayla calismaz!
    echo.
    pause
    exit /b 1
)

REM PATH'i yenile (Node.js eklendi) - Native Windows yontemi (refreshenv Chocolatey gerektirir)
echo PATH yenileniyor (native method)... >> "%LOGFILE%"

REM System PATH'i registry'den oku
for /f "skip=2 tokens=2*" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path 2^>nul') do set "SYSTEM_PATH=%%b"

REM User PATH'i registry'den oku
for /f "skip=2 tokens=2*" %%a in ('reg query "HKCU\Environment" /v Path 2^>nul') do set "USER_PATH=%%b"

REM Mevcut session icin PATH'i guncelle
set "PATH=%SYSTEM_PATH%;%USER_PATH%;C:\Program Files\nodejs"

echo [BILGI] PATH guncellendi (session icin) >> "%LOGFILE%"
echo System PATH: %SYSTEM_PATH% >> "%LOGFILE%"
echo User PATH: %USER_PATH% >> "%LOGFILE%"

REM Kurulumu dogrula
echo Node.js kurulum dogrulamasi... >> "%LOGFILE%"
timeout /t 3 /nobreak > nul

where node >nul 2>&1
if %errorlevel% neq 0 (
    echo [KRITIK] Node.js PATH'te bulunamadi - SETUP DURDURULDU >> "%LOGFILE%"
    echo.
    echo ============================================
    echo    KRITIK: Setup Durduruldu!
    echo ============================================
    echo.
    echo Node.js MSI kurulumu tamamlandi ancak PATH'te bulunamadi.
    echo.
    echo COZUM:
    echo 1. Bu setup penceresini kapatin
    echo 2. Bilgisayari yeniden baslatin
    echo 3. Setup'i tekrar calistirin
    echo.
    echo NOT: Restart sonrasi Node.js ve Claude CLI otomatik kurulacak.
    echo.
    echo Log dosyasi: %LOGFILE%
    echo.
    pause
    REM BASARISIZ exit code (Claude CLI kurulmasini engelle)
    exit /b 2
)

REM Version bilgisi
node --version >> "%LOGFILE%" 2>&1
npm --version >> "%LOGFILE%" 2>&1

echo [BASARILI] Node.js kuruldu >> "%LOGFILE%"
echo.
echo ============================================
echo    KURULUM BASARILI!
echo ============================================
echo Node.js kullanima hazir.
echo.
timeout /t 2 /nobreak > nul

REM Basarili cikis
exit /b 0
