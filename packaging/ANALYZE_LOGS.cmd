@echo off
setlocal
set "ROOT=%~dp0"
set "REPORTS=%ROOT%reports"
set "LOG=%REPORTS%\analyzer-debug.log"
if not exist "%REPORTS%" mkdir "%REPORTS%"
echo Starting Task Mining Analyzer...
"%ROOT%GSPTaskMiningAnalyzer.exe" ^
  --input "%ROOT%data" ^
  --output "%REPORTS%" ^
  --debug > "%LOG%" 2>&1
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
    echo.
    echo ANALYZER FAILED
    echo Exit code: %EXITCODE%
    echo Diagnostic log:
    echo %LOG%
    start "" notepad.exe "%LOG%"
    pause
    exit /b %EXITCODE%
)
echo.
echo ANALYSIS COMPLETED
echo Reports:
echo %REPORTS%
start "" "%REPORTS%"
exit /b 0
