@echo off
setlocal
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v GSPTaskMiningAgent /t REG_SZ /d "\"%~dp0GSPTaskMiningAgent.exe\"" /f
if errorlevel 1 exit /b 1
echo Autostart enabled for current user.
