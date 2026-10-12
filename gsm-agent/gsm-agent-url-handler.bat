@echo off
cd /d "%~dp0"
curl --silent --fail http://127.0.0.1:5127/health >nul 2>&1
if %errorlevel%==0 exit /b 0
start "GSM Diagnostic Agent" "%ComSpec%" /k call "%~dp0start-gsm-agent.bat"
