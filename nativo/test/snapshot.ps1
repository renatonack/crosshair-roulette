# Builds the snapshot test and saves pictures of the window in both modes into $Out.
param([string]$Out = "$PSScriptRoot\out")
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot)

$csc = @(
  "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe",
  "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$icon = Resolve-Path 'assets\icon.ico'
New-Item -ItemType Directory -Force $Out | Out-Null

$sources = Get-ChildItem src\*.cs | Where-Object Name -ne 'Program.cs' | ForEach-Object FullName
& $csc -nologo -target:winexe -langversion:latest "-lib:$fw" -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Web.Extensions.dll `
  "-resource:$icon,icon.ico" "-out:$Out\SnapshotTest.exe" $sources test\Snapshot.cs
if ($LASTEXITCODE -ne 0) { throw "snapshot build failed" }

foreach ($mode in 'fixa', 'roleta') {
  $p = Start-Process "$Out\SnapshotTest.exe" -ArgumentList $Out, $mode, 'cruz' -PassThru -Wait
  "snapshot $mode -> exit $($p.ExitCode)"
}
Get-ChildItem $Out -Filter *.png | ForEach-Object { $_.FullName }
