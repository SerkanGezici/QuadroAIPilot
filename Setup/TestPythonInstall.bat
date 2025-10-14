@echo off
echo =====================================
echo QuadroAI Python Kurulum Testi
echo =====================================
echo.

echo [1/4] Log dosyalarini kontrol ediyorum...
echo.

if exist "%TEMP%\QuadroAI_PythonInstall.log" (
    echo [OK] Python kurulum logu bulundu
    echo Log icerigi:
    echo --------------------------------
    type "%TEMP%\QuadroAI_PythonInstall.log" | findstr /I "basarili hata uyari kurulu"
    echo --------------------------------
) else (
    echo [HATA] Python kurulum logu bulunamadi!
)

echo.
if exist "%TEMP%\QuadroAI_PythonInstall_Transcript.log" (
    echo [OK] Transcript logu bulundu
) else (
    echo [UYARI] Transcript logu yok
)

echo.
echo [2/4] Python kurulumunu kontrol ediyorum...
where python >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Python kurulu
    python --version
) else (
    echo [HATA] Python bulunamadi!
)

echo.
echo [3/4] Edge-TTS kurulumunu kontrol ediyorum...
python -m pip show edge-tts >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] edge-tts kurulu
    python -m pip show edge-tts | findstr Version
) else (
    echo [HATA] edge-tts kurulu degil!
)

echo.
echo [4/4] QuadroAI Python dizinini kontrol ediyorum...
if exist "%LOCALAPPDATA%\QuadroAIPilot\Python" (
    echo [OK] QuadroAI Python dizini mevcut
    dir "%LOCALAPPDATA%\QuadroAIPilot\Python" | findstr python.exe
) else (
    echo [UYARI] QuadroAI Python dizini yok
)

echo.
echo =====================================
echo Test tamamlandi.
echo =====================================
echo.
echo Log dosyalarini gormek icin:
echo - %%TEMP%%\QuadroAI_PythonInstall.log
echo - %%TEMP%%\QuadroAI_PythonInstall_Transcript.log
echo.
pause