@echo off
setlocal
cd /d "%~dp0"

if not exist "GSPTaskMiningAgent.exe" (
  echo FAILED: GSPTaskMiningAgent.exe not found.
  exit /b 1
)

start "" "%~dp0GSPTaskMiningAgent.exe"
timeout /t 2 /nobreak >nul

tasklist /FI "IMAGENAME eq GSPTaskMiningAgent.exe" | find /I "GSPTaskMiningAgent.exe" >nul
if errorlevel 1 (
  echo FAILED
  exit /b 1
)

echo RUNNING
exit /b 0
