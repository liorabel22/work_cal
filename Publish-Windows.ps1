$ErrorActionPreference = "Stop"

$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $sourceRoot "MauiDbManager\MauiDbManager.csproj"
$artifactsRoot = Join-Path $sourceRoot "artifacts"
$publishDirectory = Join-Path $artifactsRoot "windows-x64"
$applicationZip = Join-Path $artifactsRoot "MauiDbManager-windows-x64.zip"

Write-Host "Restoring the required .NET workloads..."
dotnet workload restore $projectFile
if ($LASTEXITCODE -ne 0) { throw "Workload restore failed." }

Write-Host "Restoring NuGet packages..."
dotnet restore $projectFile
if ($LASTEXITCODE -ne 0) { throw "Package restore failed." }

Write-Host "Publishing a self-contained Windows x64 application..."
dotnet publish $projectFile `
    -f net10.0-windows10.0.19041.0 `
    -c Release `
    --self-contained true `
    -p:RuntimeIdentifierOverride=win-x64 `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Windows publish failed." }

if (Test-Path $applicationZip) {
    Remove-Item $applicationZip -Force
}

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $applicationZip

Write-Host ""
Write-Host "Publish completed successfully."
Write-Host "Runnable folder: $publishDirectory"
Write-Host "ZIP to give to users: $applicationZip"
Write-Host "Recipients should extract the entire ZIP and run MauiDbManager.exe."
