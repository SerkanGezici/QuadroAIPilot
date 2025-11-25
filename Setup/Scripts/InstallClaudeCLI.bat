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

REM Claude CLI global kurulum (npm install -g) - Retry logic ile
echo.
echo Claude CLI kuruluyor... (1-2 dakika)
echo Bu islem internet gerektirir, lutfen bekleyin...
echo.

REM npm ayarlarini optimize et
echo npm ayarlari optimize ediliyor... >> "%LOGFILE%"
call npm config set registry https://registry.npmjs.org/ >> "%LOGFILE%" 2>&1
call npm config set fetch-retries 5 >> "%LOGFILE%" 2>&1
call npm config set fetch-retry-mintimeout 20000 >> "%LOGFILE%" 2>&1
call npm config set fetch-retry-maxtimeout 120000 >> "%LOGFILE%" 2>&1

echo npm install -g @anthropics/claude basladi... >> "%LOGFILE%"

REM Temp dosya ile output buffer problemini coz
set "TEMP_NPM_LOG=%TEMP%\npm_install_claude.txt"

REM Retry logic: 3 deneme
set "NPM_EXIT=1"
set "RETRY_COUNT=0"
set "MAX_RETRIES=3"

:RETRY_LOOP
if %RETRY_COUNT% geq %MAX_RETRIES% goto RETRY_FAILED

if %RETRY_COUNT% gtr 0 (
    echo [UYARI] Deneme %RETRY_COUNT% basarisiz, tekrar deneniyor... >> "%LOGFILE%"
    echo.
    echo Kurulum basarisiz oldu, tekrar deneniyor... ^(Deneme %RETRY_COUNT%/%MAX_RETRIES%^)
    timeout /t 3 /nobreak > nul
)

set /a RETRY_COUNT+=1
echo [DENEME %RETRY_COUNT%/%MAX_RETRIES%] npm install calistiriliyor... >> "%LOGFILE%"

REM npm install calistir ve output'u temp dosyaya yaz
call npm install -g @anthropics/claude > "%TEMP_NPM_LOG%" 2>&1
set "NPM_EXIT=%errorlevel%"

REM Temp log'u ana log'a ekle
echo. >> "%LOGFILE%"
echo === npm install output (deneme %RETRY_COUNT%) === >> "%LOGFILE%"
type "%TEMP_NPM_LOG%" >> "%LOGFILE%"
echo === npm install output sonu === >> "%LOGFILE%"
echo. >> "%LOGFILE%"

if %NPM_EXIT% equ 0 goto INSTALL_SUCCESS

REM Hata durumunda cache temizle ve tekrar dene
if %RETRY_COUNT% lss %MAX_RETRIES% (
    echo npm cache temizleniyor... >> "%LOGFILE%"
    call npm cache clean --force >> "%LOGFILE%" 2>&1
    goto RETRY_LOOP
)

:RETRY_FAILED
REM Temp dosyayi sil
del "%TEMP_NPM_LOG%" 2>nul

echo [HATA] Claude CLI kurulum hatasi - %MAX_RETRIES% deneme basarisiz (exit code: %NPM_EXIT%) >> "%LOGFILE%"
echo.
echo ============================================
echo    KRITIK HATA: Claude CLI Kurulamadi!
echo ============================================
echo.
echo %MAX_RETRIES% deneme yapildi, hepsi basarisiz oldu.
echo Exit Code: %NPM_EXIT%
echo.
echo Olasi sebepler:
echo - Internet baglantisi kesildi veya cok yavas
echo - npm kayit sunucusuna erisilemedi ^(npmjs.com^)
echo - Guvenlik duvari npm'i engelliyor
echo - Proxy/firewall ayarlari npm'i blokluyor
echo - @anthropics/claude paketi bulunamadi veya kaldirdi
echo.
echo Cozum:
echo 1. Internet baglantinizi kontrol edin
echo 2. Guvenlik duvari ayarlarinizi kontrol edin
echo 3. Kurulum tamamlandiktan sonra manuel calistirin:
echo    npm install -g @anthropics/claude
echo 4. Sorun devam ederse npm log dosyasina bakin:
echo    npm install -g @anthropics/claude --loglevel verbose
echo.
echo Log dosyasi: %LOGFILE%
echo.
echo ONEMLI: Claude AI ozelligi bu hatayla calismaz!
echo Uygulama diger AI servisleri ile kullanilabilir.
echo.
pause
exit /b 1

:INSTALL_SUCCESS
REM Temp dosyayi sil
del "%TEMP_NPM_LOG%" 2>nul

echo [BASARILI] npm install tamamlandi ^(Deneme %RETRY_COUNT%/%MAX_RETRIES%^) >> "%LOGFILE%"

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
