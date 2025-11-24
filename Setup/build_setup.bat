@echo off
REM ====================================================================
REM QuadroAIPilot Setup Builder - Otomatik Versiyon Artışı
REM ====================================================================
REM Bu script her çalıştırıldığında setup dosyası v19, v20, v21... şeklinde
REM otomatik artan numarayla oluşturulur. Önceki setup dosyaları korunur.
REM ====================================================================

echo.
echo ====================================================================
echo QuadroAIPilot Setup Builder
echo ====================================================================
echo.

REM Mevcut dizini kaydet
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

REM Build version dosyasını kontrol et
if not exist "build_version.txt" (
    echo HATA: build_version.txt bulunamadi!
    echo Lutfen Setup klasorunde build_version.txt dosyasi oldugunu kontrol edin.
    pause
    exit /b 1
)

REM Mevcut build numarasını oku
set /p CURRENT_VERSION=<build_version.txt
echo [1/5] Mevcut build numarasi: v%CURRENT_VERSION%
echo.

REM Projeyi temizle
echo [2/5] Proje temizleniyor...
cd /d "%SCRIPT_DIR%.."
dotnet clean QuadroAIPilot.csproj -c Release -p:Platform=x64 >nul 2>&1
if %errorlevel% neq 0 (
    echo HATA: Proje temizleme basarisiz!
    pause
    exit /b 1
)
echo OK: Proje temizlendi.
echo.

REM Projeyi Release modunda publish et
echo [3/5] Proje publish ediliyor (Release/x64)...
echo Bu islem 1-2 dakika surebilir...
dotnet publish QuadroAIPilot.csproj -c Release -p:Platform=x64 --self-contained >build_output.txt 2>&1
if %errorlevel% neq 0 (
    echo HATA: Publish islemi basarisiz!
    echo Log dosyasi: build_output.txt
    pause
    exit /b 1
)
echo OK: Publish tamamlandi.
echo.

REM Publish klasörünü kontrol et
if not exist "bin\x64\Release\net8.0-windows10.0.22621.0\win-x64\publish\QuadroAIPilot.exe" (
    echo HATA: Publish klasorunde QuadroAIPilot.exe bulunamadi!
    echo Beklenen konum: bin\x64\Release\net8.0-windows10.0.22621.0\win-x64\publish\
    pause
    exit /b 1
)
echo OK: Publish dosyalari dogrulandi.
echo.

REM Browser extension icon'ları oluştur (eğer script varsa)
if exist "BrowserExtensions\create_temp_icons.sh" (
    echo [3.5/5] Browser extension icon'lari olusturuluyor...
    bash BrowserExtensions\create_temp_icons.sh >nul 2>&1
    if %errorlevel% equ 0 (
        echo OK: Icon'lar olusturuldu.
    ) else (
        echo UYARI: Icon olusturma basarisiz ^(devam ediliyor^).
    )
    echo.
)

REM Inno Setup ile setup dosyası oluştur
echo [4/5] Setup dosyasi olusturuluyor (v%CURRENT_VERSION%)...
echo Bu islem 1-2 dakika surebilir...

REM Inno Setup yolunu kontrol et
set ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
if not exist "%ISCC_PATH%" (
    set ISCC_PATH=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe
)
if not exist "%ISCC_PATH%" (
    echo HATA: Inno Setup bulunamadi!
    echo Beklenen konumlar:
    echo   - C:\Program Files ^(x86^)\Inno Setup 6\ISCC.exe
    echo   - %LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe
    pause
    exit /b 1
)

REM Setup oluştur (timeout: 10 dakika için arka planda çalıştır)
cd /d "%SCRIPT_DIR%"
start /wait /b "" "%ISCC_PATH%" "QuadroAIPilot.iss" >setup_build_v%CURRENT_VERSION%.txt 2>&1
if %errorlevel% neq 0 (
    echo HATA: Setup olusturma basarisiz!
    echo Log dosyasi: Setup\setup_build_v%CURRENT_VERSION%.txt
    pause
    exit /b 1
)
echo OK: Setup dosyasi olusturuldu.
echo.

REM Build numarasını artır (BOM olmadan)
cd /d "%SCRIPT_DIR%"
set /a NEW_VERSION=%CURRENT_VERSION%+1
echo|set /p="%NEW_VERSION%">build_version.txt

REM Sonucu doğrula ve dosya boyutunu kontrol et
cd /d "%SCRIPT_DIR%.."
if not exist "Output\QuadroAIPilot_Setup_1.2.1_Win11_Final_v%CURRENT_VERSION%.exe" (
    echo UYARI: Beklenen setup dosyasi bulunamadi!
    echo Aranan: Output\QuadroAIPilot_Setup_1.2.1_Win11_Final_v%CURRENT_VERSION%.exe
    echo.
    echo Output klasorundeki dosyalar:
    dir /b Output\*.exe
    echo.
    pause
) else (
    REM Dosya boyutunu kontrol et (117MB+ olmalı)
    for %%F in (Output\QuadroAIPilot_Setup_1.2.1_Win11_Final_v%CURRENT_VERSION%.exe) do (
        set SIZE=%%~zF
    )
    set /a SIZE_MB=!SIZE! / 1048576
    if !SIZE_MB! LSS 100 (
        echo UYARI: Setup dosyasi cok kucuk ^(!SIZE_MB! MB^) - muhtemelen bozuk!
        echo Beklenen boyut: 115-120 MB
        echo Dosya silinmeli ve tekrar derleme yapilmali.
        pause
    ) else (
        echo OK: Setup dosyasi dogrulandi ^(!SIZE_MB! MB^)
    )
)

REM Özet bilgi
echo [5/5] TAMAMLANDI!
echo ====================================================================
echo Setup Bilgileri:
echo   - Dosya: Output\QuadroAIPilot_Setup_1.2.1_Win11_Final_v%CURRENT_VERSION%.exe
echo   - Versiyon: 1.2.1
echo   - Build: v%CURRENT_VERSION%
echo   - Sonraki build: v%NEW_VERSION%
echo.
echo Output klasorundeki tum setup dosyalari:
dir /b Output\QuadroAIPilot_Setup_*.exe
echo ====================================================================
echo.

pause
exit /b 0
