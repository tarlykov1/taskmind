$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'GSPTaskMiningAgent.sln'
$agentProject = Join-Path $root 'src/GSPTaskMiningAgent/GSPTaskMiningAgent.csproj'
$analyzerProject = Join-Path $root 'src/GSPTaskMiningAnalyzer/GSPTaskMiningAnalyzer.csproj'
$publish = Join-Path $root 'artifacts/publish'
$portable = Join-Path $root 'artifacts/GSPTaskMiningAgentPortable'
$dist = Join-Path $root 'dist'

$restoreIconsScript = Join-Path $root 'tools/restore-icons.ps1'
try {
    & $restoreIconsScript -RepositoryRoot $root
}
catch {
    throw "restore-icons failed: $($_.Exception.Message)"
}

$iconDirectory = Join-Path $root 'src/GSPTaskMiningAgent/Assets'
$requiredIcons = @(
    'GSPTaskMining.ico',
    'GSPTaskMiningGreen.ico',
    'GSPTaskMiningYellow.ico',
    'GSPTaskMiningRed.ico',
    'GSPTaskMiningGray.ico'
)
foreach ($iconName in $requiredIcons) {
    $iconPath = Join-Path $iconDirectory $iconName
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
        throw "Required icon was not restored: $iconPath"
    }
    if ((Get-Item -LiteralPath $iconPath).Length -le 0) {
        throw "Restored icon is empty: $iconPath"
    }
}

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

dotnet publish $agentProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$analyzerPublish = Join-Path $root 'artifacts/publish-analyzer'
dotnet publish $analyzerProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    -o $analyzerPublish
if ($LASTEXITCODE -ne 0) { throw "analyzer publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $publish 'GSPTaskMiningAgent.exe'
$analyzerExe = Join-Path $analyzerPublish 'GSPTaskMiningAnalyzer.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Publish completed without expected EXE: $exe" }
if (-not (Test-Path -LiteralPath $analyzerExe -PathType Leaf)) { throw "Publish completed without expected EXE: $analyzerExe" }

& $exe --self-test --debug
if ($LASTEXITCODE -ne 0) { throw "Self-test failed with exit code $LASTEXITCODE" }
& $analyzerExe --self-test --debug
if ($LASTEXITCODE -ne 0) { throw "Analyzer self-test failed with exit code $LASTEXITCODE" }

$testData = Join-Path $root 'artifacts/analyzer-test-data'
$testOutput = Join-Path $root 'artifacts/analyzer-test-output'
New-Item -ItemType Directory -Path $testData,$testOutput -Force | Out-Null
'{"eventType":"active_window_tick","timestampUtc":"2026-01-01T00:00:00Z","timestampLocal":"2026-01-01T00:00:00+00:00","machineName":"m","userName":"u","processName":"Excel","windowTitle":"Book1","isIdle":false,"durationSeconds":5}' | Set-Content (Join-Path $testData 'events.jsonl') -Encoding utf8NoBOM
& $analyzerExe `
  --input $testData `
  --output $testOutput `
  --html-only `
  --debug
if ($LASTEXITCODE -ne 0) { throw "Analyzer html-only failed with exit code $LASTEXITCODE" }
$html = Get-ChildItem $testOutput -Filter *.html
if (-not $html) { throw "Analyzer did not create HTML report" }
if ($html.Length -eq 0) { throw "Created HTML report is empty" }
Remove-Item (Join-Path $testOutput '*') -Force
& $analyzerExe `
  --input $testData `
  --output $testOutput `
  --xlsx-only `
  --debug
if ($LASTEXITCODE -ne 0) { throw "Analyzer xlsx-only failed with exit code $LASTEXITCODE" }
$xlsx = Get-ChildItem $testOutput -Filter *.xlsx
if (-not $xlsx) { throw "Analyzer did not create XLSX report" }
if ($xlsx.Length -eq 0) { throw "Created XLSX report is empty" }

New-Item -ItemType Directory -Path $portable,$dist | Out-Null
Copy-Item $exe $portable
Copy-Item $analyzerExe $portable
Copy-Item (Join-Path $root 'src/GSPTaskMiningAgent/config.example.json') $portable
Copy-Item (Join-Path $root 'packaging/*') $portable

$zip = Join-Path $root 'artifacts/GSPTaskMiningAgentPortable.zip'
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -Force
$zipList = tar -tf $zip
foreach ($required in @('GSPTaskMiningAgent.exe','GSPTaskMiningAnalyzer.exe','START_AGENT.cmd','ANALYZE_LOGS.cmd','ENABLE_AUTOSTART.cmd','DISABLE_AUTOSTART.cmd')) {
    if (-not ($zipList -match $required)) {
        throw "Portable ZIP missing $required"
    }
}

Copy-Item $exe (Join-Path $dist 'GSPTaskMiningAgent.exe')
Copy-Item $analyzerExe (Join-Path $dist 'GSPTaskMiningAnalyzer.exe')
Copy-Item $zip (Join-Path $dist 'GSPTaskMiningAgentPortable.zip')
Get-FileHash (Join-Path $dist 'GSPTaskMiningAgent.exe'),(Join-Path $dist 'GSPTaskMiningAnalyzer.exe'),(Join-Path $dist 'GSPTaskMiningAgentPortable.zip') -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $(Split-Path $_.Path -Leaf)" } | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Copy-Item (Join-Path $root 'packaging/README.txt') (Join-Path $dist 'README.txt')
