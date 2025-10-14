@echo off
REM ManualPythonInstall.bat - Manuel Python ve Edge-TTS Kurulum
REM QuadroAIPilot TTS ozelliklerini etkinlestirmek icin

title QuadroAIPilot - Manuel Python Kurulum
color 0A

echo ============================================
echo    QuadroAIPilot Manuel Python Kurulum
echo ============================================
echo.
echo Bu script Python ve Edge-TTS kurulumunu
echo manuel olarak gerceklestirecektir.
echo.
echo LUTFEN YONETICI OLARAK CALISTIRIN!
echo ============================================
echo.
pause

REM Log dosyasi
set LOGFILE=%TEMP%\QuadroAI_ManualPythonInstall.log
echo [START] %date% %time% > "%LOGFILE%"

REM 1. Python kontrolu
echo [1/5] Python kurulumu kontrol ediliyor...
where python >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Python zaten kurulu:
    python --version
    echo Python kurulu >> "%LOGFILE%"
    goto :check_edgetts
)

REM 2. Python kurulumu (Embedded)
echo [2/5] Python indiriliyor ve kuruluyor...
echo.
set PYTHON_VERSION=3.11.7
set PYTHON_URL=https://www.python.org/ftp/python/%PYTHON_VERSION%/python-%PYTHON_VERSION%-embed-amd64.zip
set PIP_URL=https://bootstrap.pypa.io/get-pip.py
set PYTHON_DIR=%LOCALAPPDATA%\QuadroAIPilot\Python
set PYTHON_ZIP=%TEMP%\python-embed.zip

echo Python indiriliyor...
powershell -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri '%PYTHON_URL%' -OutFile '%PYTHON_ZIP%'" 2>>"%LOGFILE%"
if %errorlevel% neq 0 (
    echo [HATA] Python indirilemedi!
    echo Alternatif: python.org sitesinden manuel indirin
    goto :error
)

echo Python cikartiliyor...
if not exist "%PYTHON_DIR%" mkdir "%PYTHON_DIR%"
powershell -Command "Expand-Archive -Path '%PYTHON_ZIP%' -DestinationPath '%PYTHON_DIR%' -Force" 2>>"%LOGFILE%"

echo pip kuruluyor...
powershell -Command "Invoke-WebRequest -Uri '%PIP_URL%' -OutFile '%TEMP%\get-pip.py'" 2>>"%LOGFILE%"
"%PYTHON_DIR%\python.exe" "%TEMP%\get-pip.py" --no-warn-script-location 2>>"%LOGFILE%"

REM python._pth dosyasini duzenle
echo import site >> "%PYTHON_DIR%\python311._pth"
echo Lib\site-packages >> "%PYTHON_DIR%\python311._pth"

REM PATH'e ekle
echo [3/5] PATH ayarlaniyor...
setx PATH "%PYTHON_DIR%;%PYTHON_DIR%\Scripts;%PATH%" /M 2>>"%LOGFILE%"
set PATH=%PYTHON_DIR%;%PYTHON_DIR%\Scripts;%PATH%

echo [OK] Python kuruldu: %PYTHON_DIR%

:check_edgetts
REM 3. Edge-TTS kontrolu ve kurulumu
echo.
echo [4/5] Edge-TTS paketi kontrol ediliyor...
python -m pip show edge-tts >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Edge-TTS zaten kurulu
    goto :test_tts
)

echo Edge-TTS kuruluyor...
python -m pip install --upgrade pip 2>>"%LOGFILE%"
python -m pip install edge-tts 2>>"%LOGFILE%"

if %errorlevel% equ 0 (
    echo [OK] Edge-TTS basariyla kuruldu
) else (
    echo [HATA] Edge-TTS kurulamadi
    echo Internet baglantinizi kontrol edin
    goto :error
)

:test_tts
REM 4. TTS Testi
echo.
echo [5/5] TTS sesleri test ediliyor...
python -c "import asyncio; import edge_tts; voices = asyncio.run(edge_tts.list_voices()); tr_voices = [v for v in voices if 'tr-TR' in v['Locale']]; print(f'Turkce ses sayisi: {len(tr_voices)}')" 2>>"%LOGFILE%"

if %errorlevel% equ 0 (
    echo [OK] TTS sesleri basariyla yuklendi
    echo.
    echo ============================================
    echo    KURULUM BASARIYLA TAMAMLANDI!
    echo ============================================
    echo.
    echo QuadroAIPilot artik Turkce TTS kullanabilir.
    echo Uygulamayi yeniden baslatmaniz gerekebilir.
    echo.
    echo Log: %LOGFILE%
) else (
    echo [UYARI] TTS test edilemedi ama kurulum tamamlandi
)

goto :end

:error
echo.
echo ============================================
echo    KURULUM BASARISIZ!
echo ============================================
echo.
echo Sorun devam ederse:
echo 1. Bu scripti YONETICI olarak calistirin
echo 2. Internet baglantinizi kontrol edin
echo 3. Antivirusu gecici olarak kapatin
echo.
echo Log: %LOGFILE%

:end
echo.
pause
exit /b 0