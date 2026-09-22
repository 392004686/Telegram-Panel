param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "modules/command-console/TelegramPanel.CommandConsole.csproj"
$stage = Join-Path $root "artifacts/modules/command-console"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish $project -c $Configuration -o (Join-Path $stage "lib") --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
$lib = Join-Path $stage "lib"
if (-not (Test-Path $lib)) { throw "Publish output directory not found: $lib" }
# This module has no private third-party dependency. All referenced assemblies are
# supplied by the host, so keep only the entry assembly and its build metadata.
Get-ChildItem $lib -Force |
  Where-Object { $_.Name -notin @(
    "TelegramPanel.CommandConsole.dll",
    "TelegramPanel.CommandConsole.pdb",
    "TelegramPanel.CommandConsole.deps.json"
  ) } |
  Remove-Item -Recurse -Force
Copy-Item (Join-Path $root "modules/command-console/manifest.json") $stage
Copy-Item (Join-Path $root "modules/command-console/mappings") (Join-Path $stage "mappings") -Recurse -Force
$out = Join-Path $root "artifacts/modules/command-console.tpm"
if (Test-Path $out) { Remove-Item $out -Force }
$zip = Join-Path $root "artifacts/modules/command-console.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Move-Item -LiteralPath $zip -Destination $out
Write-Host "Module package created: $out"
