@echo off
cd /d "C:\Users\serkan\source\repos\QuadroAIPilot setup deneme2\Setup"
"C:\Users\serkan\AppData\Local\Programs\Inno Setup 6\ISCC.exe" /Q /O"..\Output" QuadroAIPilot.iss
if %ERRORLEVEL% NEQ 0 (
    echo Setup compilation failed with error %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)
echo Setup compiled successfully!