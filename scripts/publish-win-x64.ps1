$ErrorActionPreference = 'Stop'

$rootDir = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $rootDir 'dist/win-x64'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error '.NET 8 SDK is not installed or not on PATH. Install it from https://dotnet.microsoft.com/download/dotnet/8.0'
}

dotnet publish "$rootDir/DailyToDoList/DailyToDoList.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$outputDir"

Write-Host "`n[OK] Published executable: $outputDir/DailyToDoList.exe"
