$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'GSPTaskMiningAgent.sln'
$project = Join-Path $root 'src/GSPTaskMiningAgent/GSPTaskMiningAgent.csproj'
$publish = Join-Path $root 'artifacts/publish'
$portable = Join-Path $root 'artifacts/GSPTaskMiningAgentPortable'
$dist = Join-Path $root 'dist'

Remove-Item (Join-Path $root 'artifacts') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Path $publish -Force | Out-Null

dotnet restore $solution
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

dotnet test $solution -c Release --no-build
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE"
}

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $publish 'GSPTaskMiningAgent.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "Publish completed without expected EXE: $exe"
}

& $exe --self-test --debug
if ($LASTEXITCODE -ne 0) {
    throw "Self-test failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Path $portable,$dist | Out-Null
Copy-Item $exe $portable
Copy-Item (Join-Path $root 'src/GSPTaskMiningAgent/config.example.json') $portable
Copy-Item (Join-Path $root 'packaging/*') $portable

$zip = Join-Path $root 'artifacts/GSPTaskMiningAgentPortable.zip'
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -Force
$zipList = tar -tf $zip
foreach ($required in @('GSPTaskMiningAgent.exe','START_AGENT.cmd','ENABLE_AUTOSTART.cmd','DISABLE_AUTOSTART.cmd')) {
    if (-not ($zipList -match $required)) {
        throw "Portable ZIP missing $required"
    }
}

Copy-Item $exe (Join-Path $dist 'GSPTaskMiningAgent.exe')
Copy-Item $zip (Join-Path $dist 'GSPTaskMiningAgentPortable.zip')
Get-FileHash (Join-Path $dist 'GSPTaskMiningAgent.exe'),(Join-Path $dist 'GSPTaskMiningAgentPortable.zip') -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $(Split-Path $_.Path -Leaf)" } | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Copy-Item (Join-Path $root 'packaging/README.txt') (Join-Path $dist 'README.txt')
