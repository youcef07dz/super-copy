@echo off
setlocal
cd /d "%~dp0"
set CSC=%windir%\Microsoft.NET\Framework\v3.5\csc.exe
rem ---- embed the app icon in the EXE when the .ico is present ----
set "ICON="
if exist "SuperCopy.ico" set "ICON=/win32icon:SuperCopy.ico"

"%CSC%" /nologo /noconfig /target:winexe /optimize+ /debug- /warn:4 /codepage:65001 %LANG% %ICON%     /win32manifest:app.manifest     /out:SuperCopy.exe     /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll SuperCopy.cs

"%CSC%" /nologo /noconfig /target:winexe /optimize+ /debug- /warn:4 /codepage:65001 %LANG% %ICON%     /win32manifest:app.manifest     /out:SuperCopy-kimi.exe     /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll SuperCopy-kimi.cs

"%CSC%" /nologo /noconfig /target:winexe /optimize+ /debug- /warn:4 /codepage:65001 %LANG% %ICON%     /win32manifest:app.manifest     /out:SuperCopy-claud.exe     /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll SuperCopy-claud.cs

if errorlevel 1 (
    echo.
    echo  BUILD FAILED
    pause
    exit /b 1
)

echo.
echo  Build OK: %CD%\SuperCopy.exe
echo  Run it, then press "Install menu" in the Settings window.
echo.
pause
