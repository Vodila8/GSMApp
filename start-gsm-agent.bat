@echo off
setlocal
cd /d "%~dp0"
echo Starting GSM Diagnostic Agent...
dotnet run --project "%~dp0gsm-agent\gsm-agent.csproj"
pause
