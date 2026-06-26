@echo off
setlocal
set ROOT=%~dp0
"%ROOT%GSPTaskMiningAnalyzer.exe" --input "%ROOT%data" --output "%ROOT%reports"
echo Reports saved to: %ROOT%reports
start "" "%ROOT%reports"
