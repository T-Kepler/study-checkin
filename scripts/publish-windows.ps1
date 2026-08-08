param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\windows\StudyCheckin.Desktop\StudyCheckin.Desktop.csproj"
$outputPath = Join-Path $repositoryRoot "dist"

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $outputPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

$executable = Join-Path $outputPath "StudyCheckin.exe"
if (-not (Test-Path $executable))
{
    throw "Publish completed, but the executable was not found: $executable"
}

$sizeMb = [Math]::Round((Get-Item $executable).Length / 1MB, 1)
Write-Host "Published: $executable ($sizeMb MB)"
