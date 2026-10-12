@echo off
setlocal
set "HANDLER=%~dp0gsm-agent-url-handler.bat"
reg add "HKCU\Software\Classes\gsm-agent" /ve /d "URL:GSM Diagnostic Agent" /f >nul
reg add "HKCU\Software\Classes\gsm-agent" /v "URL Protocol" /d "" /f >nul
reg add "HKCU\Software\Classes\gsm-agent\shell\open\command" /ve /d "\"%HANDLER%\" \"%%1\"" /f >nul
echo GSM Diagnostic Agent protocol registered.
echo You can now use the Start local agent button on the website.
pause
