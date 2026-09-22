@echo off
setlocal
cd /d "%~dp0"

echo Starting GSM application on http://127.0.0.1:5123 ...
start "GSM application" cmd /k "dotnet run --project "%~dp0gsm\gsm.csproj" --urls http://127.0.0.1:5123"

echo Waiting for the application to start...
timeout /t 5 /nobreak >nul

echo.
echo Creating public ngrok link...
echo Keep this window open while your friend uses the application.
echo.
ngrok http 5123

pause
