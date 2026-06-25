@echo off
setlocal
cd /d "%~dp0.."
if not exist "GSPTaskMiningAgent.exe" (
  echo FAILED: GSPTaskMiningAgent.exe not found. Run this from the portable package scripts folder.
  exit /b 1
)
"%cd%\GSPTaskMiningAgent.exe" --debug
