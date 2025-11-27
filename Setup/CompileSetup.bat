@echo off
echo ============================================
echo QuadroAIPilot Setup Build (v56+)
echo ============================================
echo.

REM Step 1: Build C# Claude CLI Installer
echo [1/2] Building Claude CLI C# Installer...
cd /d "C:\Users\serkan\source\repos\QuadroAIPilot setup deneme2\Setup\Scripts\InstallClaudeCLI"
dotnet publish -c Release -r win-x64 --self-contained false --nologo --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo C# build failed with error %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)
echo Claude CLI Installer built successfully (InstallClaudeCLI.exe)
echo.

REM Step 2: Compile Inno Setup
echo [2/2] Compiling Inno Setup...
cd /d "C:\Users\serkan\source\repos\QuadroAIPilot setup deneme2\Setup"
"C:\Users\serkan\AppData\Local\Programs\Inno Setup 6\ISCC.exe" /Q /O"..\Output" QuadroAIPilot.iss
if %ERRORLEVEL% NEQ 0 (
    echo Setup compilation failed with error %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)
echo.
echo ============================================
echo Setup compiled successfully!
echo ============================================