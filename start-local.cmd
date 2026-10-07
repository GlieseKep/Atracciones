@echo off
rem Lanza start-local.ps1 sin depender de la politica de ejecucion de PowerShell.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-local.ps1" %*
