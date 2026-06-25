@echo off
setlocal
cd /d "%~dp0"
set "AGENT_DIR=%~dp0"
set "AGENT_EXE=%AGENT_DIR%GSPTaskMiningAgent.exe"
set "STARTUP_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
set "SHORTCUT=%STARTUP_DIR%\GSPTaskMiningAgent.lnk"

if not exist "%AGENT_EXE%" (
  echo FAILED: GSPTaskMiningAgent.exe not found.
  exit /b 1
)

mkdir "%STARTUP_DIR%" 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "$shell = New-Object -ComObject WScript.Shell; $shortcut = $shell.CreateShortcut($env:SHORTCUT); $shortcut.TargetPath = $env:AGENT_EXE; $shortcut.WorkingDirectory = $env:AGENT_DIR; $shortcut.WindowStyle = 7; $shortcut.Description = 'GSP Task Mining Agent portable autostart'; $shortcut.Save()"
if errorlevel 1 exit /b %errorlevel%

echo AUTOSTART_ENABLED
exit /b 0
