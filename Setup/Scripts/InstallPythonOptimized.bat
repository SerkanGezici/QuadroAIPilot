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

REM 6. TTS Turkce ses kontrolu
echo [6/6] Turkce ses sistemi kontrol ediliyor...
echo.
echo Turkce ses kontrolu baslatiliyor... >> "%LOGFILE%"

REM TTS modulunu test et
"%PYTHON_DIR%\python.exe" -c "import edge_tts" 2>nul
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

REM Turkce sesleri kontrol et
echo Turkce sesler kontrol ediliyor... >> "%LOGFILE%"
"%PYTHON_DIR%\python.exe" -c "import asyncio, edge_tts; voices = asyncio.run(edge_tts.list_voices()); tr = [v for v in voices if 'tr-TR' in v['Locale']]; print('Turkce Sesler:'); [print(f'  - {v[\"ShortName\"]}: {v[\"Gender\"]}') for v in tr]" > "%TEMP%\tts_test.txt" 2>&1

type "%TEMP%\tts_test.txt" >> "%LOGFILE%"

REM Turkce ses kontrolu
findstr /C:"tr-TR-EmelNeural" "%TEMP%\tts_test.txt" >nul 2>&1
if %errorlevel% neq 0 (
    echo [HATA] Turkce kadin sesi bulunamadi! >> "%LOGFILE%"
    goto :TTS_ERROR
)

findstr /C:"tr-TR-AhmetNeural" "%TEMP%\tts_test.txt" >nul 2>&1
if %errorlevel% neq 0 (
    echo [HATA] Turkce erkek sesi bulunamadi! >> "%LOGFILE%"
    goto :TTS_ERROR
)

REM Basarili kurulum
echo [BASARILI] Tum Turkce sesler hazir >> "%LOGFILE%"
echo.
echo ============================================
echo    KURULUM BASARILI!
echo ============================================
echo Python ve Turkce ses sistemi kullanima hazir.
echo.
echo Yuklenen sesler:
echo   - Kadin Ses (Emel)
echo   - Erkek Ses (Ahmet)
echo.
goto :CLEANUP

:TTS_ERROR
echo.
echo ============================================
echo    HATA: Turkce ses sistemi yuklenemedi!
echo ============================================
echo.
echo Sorun devam ederse:
echo 1. Internet baglantinizi kontrol edin
echo 2. Windows Defender'i gecici olarak kapatin
echo 3. Kurulumu yeniden calistirin
echo.
echo Detayli log: %LOGFILE%
echo.
timeout /t 15 /nobreak > nul
exit /b 1

:CLEANUP
REM PATH'e ekle
echo PATH guncelleniyor... >> "%LOGFILE%"
setx PATH "%PYTHON_DIR%;%PYTHON_DIR%\Scripts;%PATH%" >nul 2>&1

REM Temizlik
if exist "%TEMP%\python_quadro.zip" del /f /q "%TEMP%\python_quadro.zip" 2>nul
if exist "%TEMP%\get-pip.py" del /f /q "%TEMP%\get-pip.py" 2>nul
if exist "%TEMP%\tts_test.txt" del /f /q "%TEMP%\tts_test.txt" 2>nul

echo Log dosyasi: %LOGFILE%
echo.
timeout /t 3 /nobreak > nul

REM Basarili cikis
exit /b 0