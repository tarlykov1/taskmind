@echo off
setlocal
set "SHORTCUT=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\GSPTaskMiningAgent.lnk"
if exist "%SHORTCUT%" (
  del /F /Q "%SHORTCUT%"
  if errorlevel 1 exit /b %errorlevel%
  echo Autostart disabled for current user.
) else (
  echo Autostart shortcut not found. Nothing to remove.
)
echo Logs and the portable agent folder were not deleted.
