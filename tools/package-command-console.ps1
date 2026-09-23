param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "modules/command-console/TelegramPanel.CommandConsole.csproj"
$artifactRoot = Join-Path $root "artifacts/modules"
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$packageId = [Guid]::NewGuid().ToString("N")
$stage = Join-Path $artifactRoot (".command-console-package-" + $packageId)
$lib = Join-Path $stage "lib"
$out = Join-Path $artifactRoot "command-console.tpm"
$zip = Join-Path $artifactRoot (".command-console-package-" + $packageId + ".zip")
try {
  # Always publish into an isolated temporary directory. A running host may hold
  # DLLs in its installed/staging directory open, which must not block packaging.
  dotnet publish $project -c $Configuration -o $lib --no-self-contained
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
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
  Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
  Move-Item -LiteralPath $zip -Destination $out -Force
  Write-Host "Module package created: $out"
}
finally {
  Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
}
