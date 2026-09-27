@echo off
cd /d "%~dp0"
echo ==============================================
echo   CalWidget - Core Tests and Verification
echo ==============================================
"%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" run -c Release -- --test
echo.
pause
