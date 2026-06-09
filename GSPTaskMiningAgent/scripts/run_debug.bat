@echo off
setlocal
set "AGENT_DIR=%~dp0.."
for %%I in ("%AGENT_DIR%") do set "AGENT_DIR=%%~fI"
cd /d "%AGENT_DIR%"
if not exist "%AGENT_DIR%\GSPTaskMiningAgent.exe" (
  echo GSPTaskMiningAgent.exe not found in "%AGENT_DIR%".
  echo Build the portable package first and run this script from output\GSPTaskMiningAgentPortable\scripts.
  exit /b 1
)
"%AGENT_DIR%\GSPTaskMiningAgent.exe" --debug
