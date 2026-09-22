@echo off
setlocal
set "AGENT_DIR=%~dp0"
set "HANDLER=%AGENT_DIR%gsm-agent-url-handler.bat"

reg add "HKCU\Software\Classes\gsm-agent" /ve /d "URL:GSM Diagnostic Agent" /f >nul
reg add "HKCU\Software\Classes\gsm-agent" /v "URL Protocol" /d "" /f >nul
reg add "HKCU\Software\Classes\gsm-agent\shell\open\command" /ve /d "\"%HANDLER%\" \"%%1\"" /f >nul

echo GSM Diagnostic Agent was installed for this Windows account.
echo The agent will now start. Keep its window open while scanning phones.
call "%AGENT_DIR%start-gsm-agent.bat"
