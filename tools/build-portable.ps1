$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'artifacts/publish'
$portable = Join-Path $root 'artifacts/GSPTaskMiningAgentPortable'
$dist = Join-Path $root 'dist'
Remove-Item (Join-Path $root 'artifacts') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
dotnet test (Join-Path $root 'GSPTaskMiningAgent.sln') -c Release
dotnet publish (Join-Path $root 'src/GSPTaskMiningAgent/GSPTaskMiningAgent.csproj') -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None /p:DebugSymbols=false -o $publish
& (Join-Path $publish 'GSPTaskMiningAgent.exe') --self-test
New-Item -ItemType Directory -Path $portable,$dist | Out-Null
Copy-Item (Join-Path $publish 'GSPTaskMiningAgent.exe') $portable
Copy-Item (Join-Path $root 'src/GSPTaskMiningAgent/config.example.json') $portable
Copy-Item (Join-Path $root 'packaging/*') $portable
$zip = Join-Path $root 'artifacts/GSPTaskMiningAgentPortable.zip'
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -Force
$zipList = tar -tf $zip
foreach ($required in @('GSPTaskMiningAgent.exe','START_AGENT.cmd','ENABLE_AUTOSTART.cmd','DISABLE_AUTOSTART.cmd')) { if (-not ($zipList -match $required)) { throw "Portable ZIP missing $required" } }
Copy-Item (Join-Path $publish 'GSPTaskMiningAgent.exe') (Join-Path $dist 'GSPTaskMiningAgent.exe')
Copy-Item (Join-Path $root 'artifacts/GSPTaskMiningAgentPortable.zip') (Join-Path $dist 'GSPTaskMiningAgentPortable.zip')
Get-FileHash (Join-Path $dist 'GSPTaskMiningAgent.exe'),(Join-Path $dist 'GSPTaskMiningAgentPortable.zip') -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $(Split-Path $_.Path -Leaf)" } | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Copy-Item (Join-Path $root 'packaging/README.txt') (Join-Path $dist 'README.txt')
