# Removes the "Borderless Window" VLC extension and helper (both the per-user and the Program Files locations).
[CmdletBinding()]
param()
$ErrorActionPreference = 'Continue'
$exe = "$env:APPDATA\vlc\borderless\VlcBorderless.exe"
if (Test-Path $exe) { & $exe --stop; Start-Sleep -Milliseconds 800 }      # puts the title bar back and exits the helper
foreach ($base in @("$env:APPDATA\vlc", "C:\Program Files\VideoLAN\VLC")) {
    foreach ($p in @("$base\lua\extensions\borderless.lua", "$base\borderless")) {
        if (Test-Path $p) {
            try { Remove-Item $p -Recurse -Force -ErrorAction Stop; "removed $p" }
            catch { "could not remove $p ($($_.Exception.Message)) - for the Program Files copy, run from an elevated PowerShell" }
        }
    }
}
