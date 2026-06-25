$ErrorActionPreference = 'Stop'
Start-Process -FilePath (Join-Path $PSScriptRoot 'GSPTaskMiningAgent.exe') -WorkingDirectory $PSScriptRoot
