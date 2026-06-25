@echo off
setlocal
set "SHORTCUT=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\GSPTaskMiningAgent.lnk"

if exist "%SHORTCUT%" (
  del /F /Q "%SHORTCUT%"
  if errorlevel 1 exit /b %errorlevel%
  echo AUTOSTART_DISABLED
) else (
  echo AUTOSTART_NOT_FOUND
)

exit /b 0
