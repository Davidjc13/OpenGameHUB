@echo off
setlocal
set "PS_EXE=powershell"
where pwsh >nul 2>nul && set "PS_EXE=pwsh"
"%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0make.ps1" %*
