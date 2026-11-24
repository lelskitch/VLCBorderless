# Builds dist\VlcBorderless.exe with the C# compiler that ships with Windows (no SDK / Visual Studio needed).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
New-Item -ItemType Directory -Force "$root\dist" | Out-Null
& $csc /nologo /target:winexe /platform:x64 /optimize+ /warn:4 `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    /out:"$root\dist\VlcBorderless.exe" "$root\src\VlcBorderless.cs"
if ($LASTEXITCODE -ne 0) { throw "compile failed ($LASTEXITCODE)" }
Get-Item "$root\dist\VlcBorderless.exe" | Select-Object Name, Length, LastWriteTime
