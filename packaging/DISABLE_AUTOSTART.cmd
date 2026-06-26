@echo off
setlocal
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v GSPTaskMiningAgent /f >nul 2>nul
echo Autostart disabled for current user.
