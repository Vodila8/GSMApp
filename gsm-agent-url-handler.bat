@echo off
setlocal
cd /d "%~dp0"

curl --silent --fail http://127.0.0.1:5127/health >nul 2>&1
if %errorlevel%==0 (
    echo GSM Diagnostic Agent is already running.
    exit /b 0
)

start "GSM Diagnostic Agent" "%ComSpec%" /k call "%~dp0start-gsm-agent.bat"
exit /b 0
