# Builds the single-file app with the .NET Framework 4.8 compiler from the .NET SDK
# (Roslyn, modern C#) but links only against in-box .NET Framework assemblies.
param([string]$Name = 'ShiguangCountdown.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$srcDir = Join-Path $root 'src'
$distDir = Join-Path $root 'dist'
$buildDir = Join-Path $root 'build'
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$out = Join-Path $distDir $Name

if (-not (Test-Path (Join-Path $fw 'csc.exe'))) { throw '.NET Framework 4.8 not found' }

# Find Roslyn's csc.dll from any installed .NET SDK (Scoop or the standard installer).
$candidates = @()
$scoopSdk = Join-Path $env:USERPROFILE 'scoop\apps\dotnet-sdk\current\sdk'
if (Test-Path $scoopSdk) {
    $candidates += Get-ChildItem $scoopSdk -Directory |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'Roslyn\bincore\csc.dll' }
}
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    foreach ($line in (& dotnet --list-sdks 2>$null)) {
        if ($line -match '^(\S+)\s+\[(.+)\]$') {
            $candidates += Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
        }
    }
}
$csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'Roslyn csc.dll not found (a .NET SDK is required for the build only)' }

New-Item -ItemType Directory -Force $distDir | Out-Null
if (-not (Test-Path (Join-Path $buildDir 'app.ico'))) {
    & (Join-Path $buildDir 'make-icon.ps1')
}

$procName = [System.IO.Path]::GetFileNameWithoutExtension($Name)
$running = Get-Process -Name $procName -ErrorAction SilentlyContinue
if ($running) { throw "$Name is running. Close it before rebuilding." }

$sources = Get-ChildItem $srcDir -Filter '*.cs' | Select-Object -ExpandProperty FullName

$cscArgs = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/langversion:latest',
    '/optimize+', '/debug-', '/deterministic+', '/nostdlib+', '/codepage:65001',
    '/utf8output', '/warn:4',
    "/r:$fw\mscorlib.dll",
    "/r:$fw\System.dll",
    "/r:$fw\System.Core.dll",
    "/r:$fw\System.Drawing.dll",
    "/r:$fw\System.Windows.Forms.dll",
    "/r:$fw\System.Web.Extensions.dll",
    "/win32icon:$(Join-Path $buildDir 'app.ico')",
    "/win32manifest:$(Join-Path $buildDir 'app.manifest')",
    "/out:$out"
) + $sources

& dotnet exec $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "compile failed with exit code $LASTEXITCODE" }

$f = Get-Item $out
Write-Host ("built: {0}  ({1:N0} bytes)" -f $f.FullName, $f.Length)
Write-Host ("sha256: {0}" -f (Get-FileHash $out -Algorithm SHA256).Hash)
