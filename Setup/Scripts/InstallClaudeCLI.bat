@echo off
REM InstallClaudeCLI.bat - Claude CLI kurulumu
REM QuadroAIPilot AI Provider Setup

setlocal enabledelayedexpansion
set "LOGFILE=%TEMP%\QuadroAI_ClaudeCLI.log"

echo ============================================ > "%LOGFILE%"
echo QuadroAIPilot Claude CLI Kurulumu >> "%LOGFILE%"
echo %date% %time% >> "%LOGFILE%"
echo ============================================ >> "%LOGFILE%"

echo.
echo ============================================
echo    Claude CLI Kurulumu
echo ============================================
echo.

REM PATH'i registry'den yenile (InstallNodeJS.bat ile ayni mantik)
echo PATH yenileniyor (native method)... >> "%LOGFILE%"
for /f "skip=2 tokens=2*" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path 2^>nul') do set "SYSTEM_PATH=%%b"
for /f "skip=2 tokens=2*" %%a in ('reg query "HKCU\Environment" /v Path 2^>nul') do set "USER_PATH=%%b"
set "PATH=%SYSTEM_PATH%;%USER_PATH%"

echo PATH degiskeni guncellendi >> "%LOGFILE%"
echo %PATH% >> "%LOGFILE%"
echo. >> "%LOGFILE%"

REM Node.js ve npm kontrolu - robust method
echo Node.js ve npm kontrol ediliyor... >> "%LOGFILE%"

REM Oncelik 1: where komutu ile
where node >nul 2>&1
set "NODE_FOUND=%errorlevel%"

REM Oncelik 2: Direkt path ile
if %NODE_FOUND% neq 0 (
    if exist "C:\Program Files\nodejs\node.exe" (
        set "PATH=%PATH%;C:\Program Files\nodejs"
        set "NODE_FOUND=0"
        echo [BILGI] Node.js direkt path ile bulundu >> "%LOGFILE%"
    )
)

if %NODE_FOUND% neq 0 (
    echo [HATA] Node.js bulunamadi! >> "%LOGFILE%"
    echo.
    echo ============================================
    echo    KRITIK HATA: Node.js Bulunamadi!
    echo ============================================
    echo.
    echo Node.js kurulumu basarili olmaliydi ama bulunamadi.
    echo.
    echo BU BIR SETUP HATASIDIR!
    echo.
    echo Lutfen GitHub'da issue acin:
    echo https://github.com/anthropics/quadroaipilot/issues
    echo.
    echo Log dosyasi: %LOGFILE%
    echo.
    pause
    exit /b 1
)

echo Node.js bulundu: >> "%LOGFILE%"
node --version >> "%LOGFILE%" 2>&1
npm --version >> "%LOGFILE%" 2>&1

REM npm version kontrolü (Node.js varsa npm da olmalı)
npm --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [HATA] npm calismiyor! >> "%LOGFILE%"
    echo.
    echo HATA: npm komutu bulunamadi!
    echo Node.js kurulu ama npm eksik - bu olmamaliydi!
    echo.
    pause
    exit /b 1
)

REM Claude CLI zaten kurulu mu kontrol et
echo Claude CLI kontrol ediliyor... >> "%LOGFILE%"
where claude >nul 2>&1
if %errorlevel% equ 0 (
    echo Claude CLI zaten kurulu, version: >> "%LOGFILE%"
    claude --version >> "%LOGFILE%" 2>&1
    echo [BILGI] Claude CLI zaten kurulu >> "%LOGFILE%"
    echo Claude CLI zaten sistemde kurulu.
    echo.
    timeout /t 2 /nobreak > nul
    exit /b 0
)

REM Claude CLI global kurulum (npm install -g) - Buffer cozumu ile
echo.
echo Claude CLI kuruluyor... (1-2 dakika)
echo Bu islem internet gerektirir, lutfen bekleyin...
echo.
echo npm install -g @anthropics/claude basladi... >> "%LOGFILE%"

REM Temp dosya ile output buffer problemini coz
set "TEMP_NPM_LOG=%TEMP%\npm_install_claude.txt"

REM npm install calistir ve output'u temp dosyaya yaz
call npm install -g @anthropics/claude > "%TEMP_NPM_LOG%" 2>&1
set "NPM_EXIT=%errorlevel%"

REM Temp log'u ana log'a ekle
echo. >> "%LOGFILE%"
echo === npm install output baslangici === >> "%LOGFILE%"
type "%TEMP_NPM_LOG%" >> "%LOGFILE%"
echo === npm install output sonu === >> "%LOGFILE%"
echo. >> "%LOGFILE%"

REM Temp dosyayi sil
del "%TEMP_NPM_LOG%" 2>nul

if %NPM_EXIT% neq 0 (
    echo [HATA] Claude CLI kurulum hatasi (exit code: %NPM_EXIT%) >> "%LOGFILE%"
    echo.
    echo ============================================
    echo    KRITIK HATA: Claude CLI Kurulamadi!
    echo ============================================
    echo.
    echo Exit Code: %NPM_EXIT%
    echo.
    echo Olasi sebepler:
    echo - Internet baglantisi kesildi veya yavas
    echo - npm kayit sunucusuna erisilemedi (npmjs.com)
    echo - Guvenlik duvari npm'i engelliyor
    echo - npm cache bozuk
    echo - @anthropics/claude paketi bulunamadi
    echo.
    echo Cozum:
    echo 1. Internet baglantinizi kontrol edin
    echo 2. Kurulum tamamlandiktan sonra manuel calistirin:
    echo    npm install -g @anthropics/claude
    echo.
    echo Log dosyasi: %LOGFILE%
    echo Npm output: %TEMP_NPM_LOG%
    echo.
    echo ONEMLI: Claude AI ozelligi bu hatayla calismaz!
    echo.
    pause
    exit /b 1
)

echo [BASARILI] npm install tamamlandi >> "%LOGFILE%"

REM PATH'i guncelle (claude komutu npm global'e eklendi)
echo Claude PATH'e ekleniyor... >> "%LOGFILE%"
set "PATH=%PATH%;%APPDATA%\npm"
timeout /t 2 /nobreak > nul

REM Kurulumu dogrula
echo Claude CLI kurulum dogrulamasi... >> "%LOGFILE%"
where claude >nul 2>&1
if %errorlevel% neq 0 (
    echo [UYARI] Claude CLI PATH'te bulunamadi, manuel dogrulama gerekebilir >> "%LOGFILE%"
    echo.
    echo UYARI: Claude CLI kuruldu ama PATH'te bulunamadi.
    echo Lutfen bilgisayari yeniden baslatin veya
    echo yeni bir terminal penceresi acin.
    echo.
    timeout /t 5 /nobreak > nul
    exit /b 0
)

REM Version bilgisi
claude --version >> "%LOGFILE%" 2>&1

echo [BASARILI] Claude CLI kuruldu >> "%LOGFILE%"
echo.
echo ============================================
echo    KURULUM BASARILI!
echo ============================================
echo Claude CLI kullanima hazir.
echo.
echo NOT: Claude CLI kullanmak icin API key gereklidir.
echo API key: https://console.anthropic.com/
echo.
timeout /t 3 /nobreak > nul

REM Basarili cikis
exit /b 0
