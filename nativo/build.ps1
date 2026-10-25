# Builds dist\CrosshairRoulette.exe: a single native Windows program that runs
# on the .NET Framework 4.8 already included in Windows 10/11.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$csc = @(
  "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe",
  "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'Roslyn csc.exe (Visual Studio 2022 Build Tools) not found.' }

$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$icon = Resolve-Path 'assets\icon.ico'
New-Item -ItemType Directory -Force dist | Out-Null

& $csc -nologo -target:winexe -platform:anycpu -optimize+ -langversion:latest -warn:4 `
  "-lib:$fw" -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Web.Extensions.dll `
  "-win32icon:$icon" "-resource:$icon,icon.ico" `
  -out:dist\CrosshairRoulette.exe (Get-ChildItem src\*.cs | ForEach-Object FullName)
if ($LASTEXITCODE -ne 0) { throw "build failed ($LASTEXITCODE)" }

$exe = Get-Item dist\CrosshairRoulette.exe
'{0} ({1:N0} KB)' -f $exe.FullName, ($exe.Length / 1KB)
