@echo off
cd /d "%~dp0"
echo Starting GSM Diagnostic Agent...
echo Keep this window open while the website scans a phone.
gsm-agent.exe
pause
