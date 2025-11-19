@echo off
echo ========================================
echo ChatGPT Bridge - Python Dependencies
echo ========================================
echo.

REM Python kontrolu
python --version >nul 2>&1
if errorlevel 1 (
    echo [HATA] Python bulunamadi!
    echo Lutfen Python 3.8+ yukleyin: https://www.python.org/downloads/
    pause
    exit /b 1
)

echo [OK] Python bulundu
python --version

echo.
echo Python paketleri yukleniyor...
echo.

REM Playwright ve websockets yukle
python -m pip install --upgrade pip
python -m pip install playwright==1.40.0 websockets==12.0

if errorlevel 1 (
    echo [HATA] Paket yukleme basarisiz!
    pause
    exit /b 1
)

echo.
echo Playwright browser binary'lerini yukleniyor...
echo.

REM Playwright Chromium yukle
python -m playwright install chromium

if errorlevel 1 (
    echo [HATA] Playwright browser yukleme basarisiz!
    pause
    exit /b 1
)

echo.
echo ========================================
echo [BASARILI] Kurulum tamamlandi!
echo ========================================
echo.
echo ChatGPT Bridge kullanima hazir.
echo QuadroAIPilot uygulamasini calistirabilirsiniz.
echo.
pause
