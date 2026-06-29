param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$encodedDirectory = Join-Path $RepositoryRoot 'src/GSPTaskMiningAgent/AssetsEncoded'
$assetDirectory = Join-Path $RepositoryRoot 'src/GSPTaskMiningAgent/Assets'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null

foreach ($encodedIcon in Get-ChildItem -LiteralPath $encodedDirectory -Filter '*.ico.b64') {
    $iconName = $encodedIcon.Name.Substring(0, $encodedIcon.Name.Length - 4)
    $target = Join-Path $assetDirectory $iconName
    $base64 = (Get-Content -LiteralPath $encodedIcon.FullName -Raw) -replace '\s', ''
    [System.IO.File]::WriteAllBytes($target, [Convert]::FromBase64String($base64))
}
