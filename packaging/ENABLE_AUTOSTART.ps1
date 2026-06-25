$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'GSPTaskMiningAgent.exe'
New-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'GSPTaskMiningAgent' -Value ('"{0}"' -f $exe) -PropertyType String -Force | Out-Null
