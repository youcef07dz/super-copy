@echo off
setlocal
cd /d "%~dp0"

rem ---- find the oldest available csc.exe (2.0 first, so XP can run the result) ----
set "CSC="
set "FW=%WINDIR%\Microsoft.NET\Framework"
for %%V in (v2.0.50727 v3.5 v4.0.30319) do (
    if not defined CSC if exist "%FW%\%%V\csc.exe" set "CSC=%FW%\%%V\csc.exe" & set "VER=%%V"
)

rem ---- pin C# 2.0: the 2.0 compiler only knows ISO-1/Default and IS C# 2.0 by default ----
set "LANG=/langversion:ISO-2"
if "%VER%"=="v2.0.50727" set "LANG="

if not defined CSC (
    echo.
    echo  csc.exe was not found.
    echo  Install .NET Framework 2.0 / 3.5 ^(on Windows 10/11: Control Panel
    echo  ^> Turn Windows features on or off ^> .NET Framework 3.5^).
    echo.
    pause
    exit /b 1
)

echo Using: %CSC%
if "%VER%"=="v4.0.30319" (
    echo.
    echo  NOTE: only the .NET 4 compiler was found. The EXE will need .NET 4.0
    echo        and will NOT run on a clean Windows XP. For a true .NET 2.0
    echo        build, enable the ".NET Framework 3.5" Windows feature first.
    echo.
)

rem ---- embed the app icon in the EXE when the .ico is present ----
set "ICON="
if exist "SuperCopy.ico" set "ICON=/win32icon:SuperCopy.ico"

"%CSC%" /nologo /target:winexe /optimize+ /debug- /warn:4 %LANG% %ICON% ^
    /out:SuperCopy.exe ^
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
    SuperCopy.cs

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
