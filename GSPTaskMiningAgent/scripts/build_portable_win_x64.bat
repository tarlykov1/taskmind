@echo off
setlocal
cd /d "%~dp0.."

set "PROJECT=src\GSPTaskMiningAgent\GSPTaskMiningAgent.csproj"
set "PUBLISH_DIR=output\publish-win-x64"
set "PORTABLE_DIR=output\GSPTaskMiningAgentPortable"
set "ZIP_PATH=output\GSPTaskMiningAgentPortable.zip"

if exist "%PUBLISH_DIR%" rmdir /S /Q "%PUBLISH_DIR%"
if exist "%PORTABLE_DIR%" rmdir /S /Q "%PORTABLE_DIR%"
if exist "%ZIP_PATH%" del /F /Q "%ZIP_PATH%"

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o "%PUBLISH_DIR%"
if errorlevel 1 exit /b %errorlevel%

mkdir "%PORTABLE_DIR%\scripts" 2>nul
mkdir "%PORTABLE_DIR%\docs" 2>nul

copy /Y "%PUBLISH_DIR%\GSPTaskMiningAgent.exe" "%PORTABLE_DIR%\" >nul
copy /Y "src\GSPTaskMiningAgent\config.example.json" "%PORTABLE_DIR%\" >nul
copy /Y "src\GSPTaskMiningAgent\config.example.json" "%PORTABLE_DIR%\config.json" >nul
copy /Y "scripts\enable_autostart_user.bat" "%PORTABLE_DIR%\scripts\" >nul
copy /Y "scripts\disable_autostart_user.bat" "%PORTABLE_DIR%\scripts\" >nul
copy /Y "scripts\run_debug.bat" "%PORTABLE_DIR%\scripts\" >nul
copy /Y "docs\README.md" "%PORTABLE_DIR%\docs\" >nul
copy /Y "docs\PILOT_INSTRUCTION.md" "%PORTABLE_DIR%\docs\" >nul
copy /Y "docs\SECURITY.md" "%PORTABLE_DIR%\docs\" >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%PORTABLE_DIR%\*' -DestinationPath '%ZIP_PATH%' -Force"
if errorlevel 1 exit /b %errorlevel%

echo Portable folder: %cd%\%PORTABLE_DIR%
echo Portable ZIP: %cd%\%ZIP_PATH%
