# Installs the "Borderless Window" VLC extension.
#   .\install.ps1            per-user install (no admin needed): %APPDATA%\vlc\...
#   .\install.ps1 -System    into C:\Program Files\VideoLAN\VLC\lua\extensions (run from an elevated PowerShell)
# Restart VLC afterwards (extensions are scanned at startup).
[CmdletBinding()]
param([switch]$System)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe  = "$root\dist\VlcBorderless.exe"
if (-not (Test-Path $exe)) { & "$root\build.ps1" | Out-Null }
if (-not (Test-Path $exe)) { throw "VlcBorderless.exe is missing and could not be built." }

if ($System) {
    $base = "C:\Program Files\VideoLAN\VLC"
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { throw "-System needs an elevated (Run as administrator) PowerShell." }
} else {
    $base = "$env:APPDATA\vlc"
}
if (-not (Test-Path (Split-Path $base -Parent)) -and $System) { throw "VLC not found at $base" }

# stop a running helper first so the exe is not locked
& $exe --stop; Start-Sleep -Milliseconds 800

New-Item -ItemType Directory -Force "$base\lua\extensions", "$base\borderless" | Out-Null
Copy-Item $exe "$base\borderless\VlcBorderless.exe" -Force
Copy-Item "$root\extension\borderless.lua" "$base\lua\extensions\borderless.lua" -Force
"Installed:"
"  $base\lua\extensions\borderless.lua"
"  $base\borderless\VlcBorderless.exe"
"Restart VLC, then use View > Borderless Window (in Minimal Interface: right-click the video > View > Borderless Window)."
