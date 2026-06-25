@echo off
setlocal
cd /d "%~dp0.."

set "PROJECT=src\GSPTaskMiningAgent\GSPTaskMiningAgent.csproj"
set "PUBLISH_DIR=output\publish"
set "PORTABLE_DIR=output\GSPTaskMiningAgentPortable"
set "ZIP_PATH=output\GSPTaskMiningAgentPortable.zip"

if exist "%PUBLISH_DIR%" rmdir /S /Q "%PUBLISH_DIR%"
if exist "%PORTABLE_DIR%" rmdir /S /Q "%PORTABLE_DIR%"
if exist "%ZIP_PATH%" del /F /Q "%ZIP_PATH%"

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None /p:DebugSymbols=false -o "%PUBLISH_DIR%"
if errorlevel 1 exit /b %errorlevel%

if not exist "%PUBLISH_DIR%\GSPTaskMiningAgent.exe" (
  echo FAILED: %PUBLISH_DIR%\GSPTaskMiningAgent.exe was not created.
  exit /b 1
)

"%PUBLISH_DIR%\GSPTaskMiningAgent.exe" --self-test
if errorlevel 1 exit /b %errorlevel%

mkdir "%PORTABLE_DIR%\data" 2>nul
copy /Y "%PUBLISH_DIR%\GSPTaskMiningAgent.exe" "%PORTABLE_DIR%\" >nul
copy /Y "src\GSPTaskMiningAgent\config.example.json" "%PORTABLE_DIR%\" >nul
copy /Y "package\START_AGENT.cmd" "%PORTABLE_DIR%\" >nul
copy /Y "package\ENABLE_AUTOSTART.cmd" "%PORTABLE_DIR%\" >nul
copy /Y "package\DISABLE_AUTOSTART.cmd" "%PORTABLE_DIR%\" >nul
copy /Y "package\README.txt" "%PORTABLE_DIR%\" >nul
type nul > "%PORTABLE_DIR%\data\.gitkeep"

powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%PORTABLE_DIR%\*' -DestinationPath '%ZIP_PATH%' -Force"
if errorlevel 1 exit /b %errorlevel%

if not exist "%ZIP_PATH%" (
  echo FAILED: %ZIP_PATH% was not created.
  exit /b 1
)

echo Portable folder: %cd%\%PORTABLE_DIR%
echo Portable ZIP: %cd%\%ZIP_PATH%
