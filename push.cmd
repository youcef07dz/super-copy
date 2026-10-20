@echo off
setlocal
cd /d "%~dp0"

git add README.md README.ar.md README.fr.md SuperCopy.cs SuperCopy.exe update.txt build.cmd ^
        SuperCopy-kimi.cs SuperCopy-claud.cs HISTORY.md old progress.md task.md push.cmd
if errorlevel 1 goto fail

git commit -m "Release v2: XXH3 verification, history archive, mobile-friendly READMEs"
if errorlevel 1 goto fail

git push origin main
if errorlevel 1 goto fail

echo Pushed to origin/main.
exit /b 0

:fail
echo Push failed.
exit /b 1
