@echo off
rem Double-click to remove the extension and its helper (the title bar is put back first).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1" %*
if "%~1"=="" pause
