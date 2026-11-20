@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\nara_start_live.ps1"
if errorlevel 1 pause
