

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "==> Ensuring ScriptPortal.Vegas.dll reference..." -ForegroundColor Cyan
& "$PSScriptRoot\ensure-vegas-reference.ps1"

$msbuild = $null
$candidates = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\MSBuild\14.0\Bin\MSBuild.exe"
)
foreach ($c in $candidates) {
    if (Test-Path $c) { $msbuild = $c; break }
}
if (-not $msbuild) {
    throw "MSBuild.exe not found. Install Visual Studio Build Tools with .NET desktop development."
}

Write-Host "==> Building Release with $msbuild" -ForegroundColor Cyan
& $msbuild "$Root\AkkeohsVegas.sln" /restore /p:Configuration=Release /p:Platform="Any CPU" /m
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$dist = Join-Path $Root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Copy-Item "$Root\src\AkkeohsVegas.Installer\bin\Release\AkkeohsSubtitlesSetup.exe" $dist -Force

$payload = Join-Path $dist "payload"
New-Item -ItemType Directory -Force -Path "$payload\bin", "$payload\models" | Out-Null
$tpBin = Join-Path $Root "third_party\bin"
$tpModels = Join-Path $Root "third_party\models"
if (Test-Path $tpBin) {
    Copy-Item "$tpBin\*" "$payload\bin\" -Recurse -Force
}
if (Test-Path $tpModels) {
    Copy-Item "$tpModels\*" "$payload\models\" -Recurse -Force
}

Write-Host ""
Write-Host "Build OK." -ForegroundColor Green
Write-Host "  Installer (single file): dist\AkkeohsSubtitlesSetup.exe"
Write-Host ""
Write-Host "Ship only AkkeohsSubtitlesSetup.exe - Core + Extension DLLs are embedded."
Write-Host "At install time the wizard downloads whisper.cpp, FFmpeg, and selected models."
Write-Host "Uninstaller is created on install as akkeohs_subtitler_uninstall.exe."
