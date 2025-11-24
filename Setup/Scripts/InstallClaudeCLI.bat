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

REM Node.js kurulu mu kontrol et
echo Node.js kontrol ediliyor... >> "%LOGFILE%"
echo PATH degiskeni: >> "%LOGFILE%"
echo %PATH% >> "%LOGFILE%"
echo. >> "%LOGFILE%"

where node >nul 2>&1
if %errorlevel% neq 0 (
    echo [HATA] Node.js bulunamadi! >> "%LOGFILE%"
    echo.
    echo ============================================
    echo    KRITIK HATA: Node.js Bulunamadi!
    echo ============================================
    echo.
    echo Claude CLI kurulumu icin Node.js gereklidir.
    echo.
    echo Olasi sebepler:
    echo - Node.js kurulum scripti hata verdi
    echo - PATH guncellemesi icin bilgisayar yeniden baslatilmali
    echo - Node.js kurulumu atlanmis olabilir
    echo.
    echo Cozum:
    echo 1. Bilgisayari yeniden baslatin
    echo 2. Hala calismiyorsa Node.js manuel kurun:
    echo    https://nodejs.org/dist/v20.11.1/
    echo.
    echo Log dosyasi: %LOGFILE%
    echo.
    pause
    exit /b 1
)

echo Node.js bulundu: >> "%LOGFILE%"
node --version >> "%LOGFILE%" 2>&1
npm --version >> "%LOGFILE%" 2>&1

REM npm kurulu mu kontrol et
where npm >nul 2>&1
if %errorlevel% neq 0 (
    echo [HATA] npm bulunamadi! >> "%LOGFILE%"
    echo.
    echo HATA: npm kurulu degil!
    echo npm Node.js ile birlikte kurulmali.
    echo.
    timeout /t 5 /nobreak > nul
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

REM PATH'i yenile (Node.js yeni eklendi olabilir)
echo PATH yenileniyor... >> "%LOGFILE%"
call refreshenv 2>nul

REM Claude CLI global kurulum (npm install -g)
echo Claude CLI kuruluyor... (1-2 dakika)
echo npm install -g @anthropics/claude basladi... >> "%LOGFILE%"

call npm install -g @anthropics/claude >> "%LOGFILE%" 2>&1

if %errorlevel% neq 0 (
    echo [HATA] Claude CLI kurulum hatasi (exit code: %errorlevel%) >> "%LOGFILE%"
    echo.
    echo ============================================
    echo    KRITIK HATA: Claude CLI Kurulamadi!
    echo ============================================
    echo.
    echo Exit Code: %errorlevel%
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
    echo.
    echo ONEMLI: Claude AI ozelligi bu hatayla calismaz!
    echo.
    pause
    exit /b 1
)

REM PATH'i tekrar yenile (claude komutunun PATH'e eklenmesi icin)
call refreshenv 2>nul
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
