

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Lib = Join-Path $Root "lib"
$Dest = Join-Path $Lib "ScriptPortal.Vegas.dll"
New-Item -ItemType Directory -Force -Path $Lib | Out-Null

if (Test-Path $Dest) {
    $info = Get-Item $Dest
    if ($info.Length -gt 50000) {
        Write-Host "Using existing ScriptPortal.Vegas.dll ($($info.Length) bytes)"
        return
    }
}

$searchRoots = @(
    "${env:ProgramFiles}\VEGAS\VEGAS Pro 15.0",
    "${env:ProgramFiles}\VEGAS\VEGAS Pro 14.0",
    "${env:ProgramFiles}\VEGAS\VEGAS Pro 16.0",
    "${env:ProgramFiles}\VEGAS\VEGAS Pro 17.0",
    "${env:ProgramFiles}\VEGAS\VEGAS Pro 18.0",
    "${env:ProgramFiles}\Sony\Vegas Pro 15.0",
    "${env:ProgramFiles}\Sony\Vegas Pro 14.0",
    "${env:ProgramFiles}\Sony\Vegas Pro 16.0",
    "${env:ProgramFiles(x86)}\VEGAS\VEGAS Pro 15.0",
    "${env:ProgramFiles(x86)}\Sony\Vegas Pro 15.0"
)

foreach ($root in $searchRoots) {
    $candidate = Join-Path $root "ScriptPortal.Vegas.dll"
    if (Test-Path $candidate) {
        Copy-Item $candidate $Dest -Force
        Write-Host "Copied ScriptPortal.Vegas.dll from $candidate"
        return
    }
}

Write-Host "VEGAS Pro 15 not found - building compile stub ScriptPortal.Vegas.dll" -ForegroundColor Yellow

$msbuild = $null
$candidates = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
)
foreach ($c in $candidates) {
    if (Test-Path $c) { $msbuild = $c; break }
}
if (-not $msbuild) {
    throw "MSBuild not found and ScriptPortal.Vegas.dll is missing. Install VEGAS Pro 15 or Visual Studio Build Tools."
}

& $msbuild (Join-Path $Root "lib\stubs\ScriptPortal.Vegas\ScriptPortal.Vegas.csproj") /p:Configuration=Release
if ($LASTEXITCODE -ne 0) { throw "Stub build failed." }
if (-not (Test-Path $Dest)) {
    throw "Stub build did not produce lib\ScriptPortal.Vegas.dll"
}
Write-Host "Stub written to $Dest"
Write-Host "NOTE: Rebuild against the real DLL from VEGAS before shipping a production plugin."
