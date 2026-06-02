@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-terminal.ps1" %*
set "MBS_EXIT_CODE=%ERRORLEVEL%"

echo.
pause
exit /b %MBS_EXIT_CODE%
