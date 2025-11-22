@echo off
REM InstallPythonOptimized.bat - TTS kontrollu optimize Python kurulumu
REM QuadroAIPilot için tek Python kurulum scripti

set LOGFILE=%TEMP%\QuadroAI_PythonInstall.log
set PYTHON_DIR=%LOCALAPPDATA%\QuadroAIPilot\Python
set PYTHON_URL=https://www.python.org/ftp/python/3.11.7/python-3.11.7-embed-amd64.zip
set PIP_URL=https://bootstrap.pypa.io/get-pip.py

echo ============================================ > "%LOGFILE%"
echo QuadroAIPilot Python ve TTS Kurulumu >> "%LOGFILE%"
echo %date% %time% >> "%LOGFILE%"
echo ============================================ >> "%LOGFILE%"

echo.
echo ============================================
echo    QuadroAIPilot Ses Sistemi Kurulumu
echo ============================================
echo.

REM 1. Dizin temizleme ve olusturma
echo [1/6] Hazirlik...
if exist "%PYTHON_DIR%" (
    echo Eski kurulum temizleniyor... >> "%LOGFILE%"
    rmdir /s /q "%PYTHON_DIR%" 2>nul
    timeout /t 2 /nobreak > nul
)

mkdir "%PYTHON_DIR%" 2>nul
mkdir "%PYTHON_DIR%\Lib" 2>nul
mkdir "%PYTHON_DIR%\Lib\site-packages" 2>nul
mkdir "%PYTHON_DIR%\Scripts" 2>nul
echo Dizinler olusturuldu >> "%LOGFILE%"

REM 2. Python indir
echo [2/6] Python indiriliyor... (1-2 dakika)
echo Python indiriliyor: %PYTHON_URL% >> "%LOGFILE%"

curl -L -o "%TEMP%\python_quadro.zip" "%PYTHON_URL%" 2>nul
if %errorlevel% neq 0 (
    echo curl basarisiz, certutil deneniyor... >> "%LOGFILE%"
    certutil -urlcache -split -f "%PYTHON_URL%" "%TEMP%\python_quadro.zip" >nul 2>&1
    if %errorlevel% neq 0 (
        echo [HATA] Python indirilemedi >> "%LOGFILE%"
        echo.
        echo HATA: Python indirilemedi!
        echo Internet baglantinizi kontrol edin.
        timeout /t 5 /nobreak > nul
        exit /b 1
    )
)
echo Python indirildi >> "%LOGFILE%"

REM 3. ZIP aç
echo [3/6] Python kuruluyor...
cd /d "%PYTHON_DIR%"
tar -xf "%TEMP%\python_quadro.zip" 2>nul
if %errorlevel% neq 0 (
    echo tar basarisiz, PowerShell deneniyor... >> "%LOGFILE%"
    powershell -ExecutionPolicy Bypass -NoProfile -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::ExtractToDirectory('%TEMP%\python_quadro.zip', '%PYTHON_DIR%')" 2>nul
    if %errorlevel% neq 0 (
        echo [HATA] Python ZIP acilamadi >> "%LOGFILE%"
        echo.
        echo HATA: Python kurulamadi!
        timeout /t 5 /nobreak > nul
        exit /b 1
    )
)
echo Python kuruldu >> "%LOGFILE%"

REM 4. python._pth olustur
echo [4/6] Python yapilandiriliyor...
(
echo python311.zip
echo .
echo Lib
echo Lib\site-packages
echo import site
) > "%PYTHON_DIR%\python311._pth"
echo Python yapilandirildi >> "%LOGFILE%"

REM 5. pip ve edge-tts kur
echo [5/6] Ses sentezleme modulleri kuruluyor... (2-3 dakika)
echo.

REM get-pip.py indir
echo pip indiriliyor... >> "%LOGFILE%"
curl -L -o "%TEMP%\get-pip.py" "%PIP_URL%" 2>nul
if %errorlevel% neq 0 (
    certutil -urlcache -split -f "%PIP_URL%" "%TEMP%\get-pip.py" >nul 2>&1
    if %errorlevel% neq 0 (
        echo [HATA] pip indirilemedi >> "%LOGFILE%"
        echo.
        echo HATA: pip indirilemedi!
        timeout /t 5 /nobreak > nul
        exit /b 1
    )
)

REM pip kur
echo pip kuruluyor... >> "%LOGFILE%"
"%PYTHON_DIR%\python.exe" "%TEMP%\get-pip.py" --no-warn-script-location 2>>"%LOGFILE%"
if %errorlevel% neq 0 (
    echo [HATA] pip kurulum hatasi >> "%LOGFILE%"
    echo.
    echo HATA: pip kurulamadi!
    echo Detaylar: %LOGFILE%
    timeout /t 5 /nobreak > nul
    exit /b 1
)

REM edge-tts kur
echo edge-tts kuruluyor... >> "%LOGFILE%"
"%PYTHON_DIR%\python.exe" -m pip install edge-tts 2>>"%LOGFILE%"
if %errorlevel% neq 0 (
    echo [HATA] edge-tts kurulum hatasi >> "%LOGFILE%"
    echo.
    echo HATA: Ses sentezleme modulu kurulamadi!
    echo Detaylar: %LOGFILE%
    timeout /t 5 /nobreak > nul
    exit /b 1
)

REM 6. Playwright ve websockets kur (ChatGPT/Gemini AI Bridges icin)
echo [6/9] AI Bridge paketleri kuruluyor (ChatGPT/Gemini)...
echo.
echo playwright ve websockets kuruluyor... >> "%LOGFILE%"
"%PYTHON_DIR%\python.exe" -m pip install --no-warn-script-location playwright==1.40.0 websockets==12.0 2>>"%LOGFILE%"
if %errorlevel% neq 0 (
    echo [UYARI] Playwright kurulum hatasi (ChatGPT/Gemini etkilenebilir) >> "%LOGFILE%"
)

REM 7. Playwright Chromium browser kur
echo [7/9] Chromium browser kuruluyor... (2-3 dakika surebilir)
echo.
echo Playwright Chromium kuruluyor... >> "%LOGFILE%"
"%PYTHON_DIR%\python.exe" -m playwright install chromium 2>>"%LOGFILE%"
if %errorlevel% neq 0 (
    echo [UYARI] Chromium kurulum hatasi (ChatGPT/Gemini etkilenebilir) >> "%LOGFILE%"
) else (
    echo [BASARILI] Playwright Chromium kuruldu >> "%LOGFILE%"
)

REM 8. edge-tts-nossl.py dosyasini kopyala
echo [8/9] SSL bypass scripti kopyalaniyor...
echo edge-tts-nossl.py kopyalanıyor... >> "%LOGFILE%"
if exist "%~dp0edge-tts-nossl.py" (
    copy /y "%~dp0edge-tts-nossl.py" "%PYTHON_DIR%\Scripts\edge-tts-nossl.py" >nul 2>&1
    if %errorlevel% equ 0 (
        echo [BASARILI] edge-tts-nossl.py kopyalandi >> "%LOGFILE%"
    ) else (
        echo [UYARI] edge-tts-nossl.py kopyalanamadi >> "%LOGFILE%"
    )
) else (
    echo [UYARI] edge-tts-nossl.py kaynak dosyada bulunamadi: %~dp0edge-tts-nossl.py >> "%LOGFILE%"
)

REM 9. TTS modulu ve Turkce sesler kontrolu (SSL bypass ile)
echo [9/9] TTS sistemi kontrol ediliyor...
echo.
echo TTS modulu kontrol ediliyor... >> "%LOGFILE%"

REM TTS modulunu test et
"%PYTHON_DIR%\python.exe" -c "import edge_tts" 2>>"%LOGFILE%"
if %errorlevel% neq 0 (
    echo [HATA] TTS modulu yuklenemedi >> "%LOGFILE%"
    echo.
    echo ============================================
    echo    HATA: Ses sistemi yuklenemedi!
    echo ============================================
    echo Lutfen internet baglantinizi kontrol edin.
    echo Log: %LOGFILE%
    echo.
    timeout /t 10 /nobreak > nul
    exit /b 1
)

REM Turkce sesleri kontrol et (SSL bypass script ile)
echo Turkce sesler kontrol ediliyor (SSL bypass)... >> "%LOGFILE%"
if exist "%PYTHON_DIR%\Scripts\edge-tts-nossl.py" (
    "%PYTHON_DIR%\python.exe" "%PYTHON_DIR%\Scripts\edge-tts-nossl.py" --list-voices > "%TEMP%\tts_voices.txt" 2>&1
    findstr /C:"tr-TR-EmelNeural" "%TEMP%\tts_voices.txt" >nul 2>&1
    if %errorlevel% equ 0 (
        echo [BASARILI] Turkce kadin sesi (Emel) hazir >> "%LOGFILE%"
    ) else (
        echo [UYARI] Turkce sesler listenemedi, uygulama ilk kullanimda indirecek >> "%LOGFILE%"
    )
) else (
    echo [UYARI] edge-tts-nossl.py bulunamadi, sesler ilk kullanimda indirilecek >> "%LOGFILE%"
)

REM Basarili kurulum
echo [BASARILI] TTS modulu yuklendi >> "%LOGFILE%"
echo.
echo ============================================
echo    KURULUM BASARILI!
echo ============================================
echo Python ve TTS sistemi kullanima hazir.
echo.

:CLEANUP
REM PATH'e ekle
echo PATH guncelleniyor... >> "%LOGFILE%"
setx PATH "%PYTHON_DIR%;%PYTHON_DIR%\Scripts;%PATH%" >nul 2>&1

REM Temizlik
if exist "%TEMP%\python_quadro.zip" del /f /q "%TEMP%\python_quadro.zip" 2>nul
if exist "%TEMP%\get-pip.py" del /f /q "%TEMP%\get-pip.py" 2>nul
if exist "%TEMP%\tts_test.txt" del /f /q "%TEMP%\tts_test.txt" 2>nul
if exist "%TEMP%\tts_voices.txt" del /f /q "%TEMP%\tts_voices.txt" 2>nul

echo Log dosyasi: %LOGFILE%
echo.
timeout /t 3 /nobreak > nul

REM Basarili cikis
exit /b 0