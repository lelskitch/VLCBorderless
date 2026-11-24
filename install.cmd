@echo off
rem Double-click to install the extension for your Windows user (no administrator needed).
rem Runs install.ps1 with the PowerShell execution policy relaxed for this one run only. Extra arguments are passed on (for example -System).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
if errorlevel 1 (echo. & echo Install failed - see the message above.)
if "%~1"=="" pause
