[CmdletBinding()]
param(
    [string]$Version = "0.6.0",
    [string]$PublishDirectory = "artifacts\win-x64",
    [string]$OutputDirectory = "dist"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$isccCommand = Get-Command iscc.exe -ErrorAction SilentlyContinue
$iscc = if ($null -eq $isccCommand) { $null } else { $isccCommand.Path }
if ([string]::IsNullOrWhiteSpace($iscc)) {
    $iscc = @(
        (Join-Path ${env:ProgramFiles} "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:LOCALAPPDATA} "Programs\Inno Setup 6\ISCC.exe")
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not (Test-Path $iscc)) {
    throw "Inno Setup compiler was not found. Install Inno Setup 6 and run this script again."
}

$publishPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PublishDirectory))
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

dotnet publish src/LanTransfer.Host/LanTransfer.Host.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    "-p:Version=$Version" `
    -p:PublishSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    --output $publishPath

$requiredWebAssets = @(
    "wwwroot\index.html",
    "wwwroot\css\app.css",
    "wwwroot\js\app.js"
)
foreach ($asset in $requiredWebAssets) {
    $assetPath = Join-Path $publishPath $asset
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "Published application is missing required web asset: $assetPath"
    }
}

& $iscc `
    "/DAppVersion=$Version" `
    "/DSourceDir=$publishPath" `
    "/DOutputDir=$outputPath" `
    (Join-Path $repoRoot "installer\LanTransfer.iss")

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Installer created in $outputPath"
