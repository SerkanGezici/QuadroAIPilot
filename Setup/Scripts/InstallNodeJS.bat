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

REM Node.js MSI dosyasini bul
set "NODE_MSI=%~dp0..\Prerequisites\node-v20.11.1-x64.msi"

if not exist "%NODE_MSI%" (
    echo [HATA] Node.js MSI bulunamadi: %NODE_MSI% >> "%LOGFILE%"
    echo.
    echo HATA: Node.js kurulum dosyasi bulunamadi!
    echo Beklenen konum: %NODE_MSI%
    echo.
    timeout /t 5 /nobreak > nul
    exit /b 1
)

echo Node.js MSI bulundu: %NODE_MSI% >> "%LOGFILE%"

REM Node.js MSI kurulumu (sessiz mod)
echo Node.js kuruluyor... (1-2 dakika)
echo Node.js kuruluyor (msiexec)... >> "%LOGFILE%"

msiexec /i "%NODE_MSI%" /qn /norestart /l*v "%LOGFILE%_msi.txt"

if %errorlevel% neq 0 (
    echo [HATA] Node.js kurulum hatasi (exit code: %errorlevel%) >> "%LOGFILE%"
    echo MSI log: %LOGFILE%_msi.txt >> "%LOGFILE%"
    echo.
    echo HATA: Node.js kurulamadi!
    echo Detaylar: %LOGFILE%
    echo MSI Log: %LOGFILE%_msi.txt
    echo.
    timeout /t 10 /nobreak > nul
    exit /b 1
)

REM PATH'i yenile (Node.js eklendi)
echo PATH yenileniyor... >> "%LOGFILE%"
call refreshenv 2>nul

REM Kurulumu dogrula
echo Node.js kurulum dogrulamasi... >> "%LOGFILE%"
timeout /t 2 /nobreak > nul

where node >nul 2>&1
if %errorlevel% neq 0 (
    echo [UYARI] Node.js PATH'te bulunamadi, manuel dogrulama gerekebilir >> "%LOGFILE%"
    echo.
    echo UYARI: Node.js kuruldu ama PATH'te bulunamadi.
    echo Lutfen bilgisayari yeniden baslatin.
    echo.
    timeout /t 5 /nobreak > nul
    exit /b 0
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
