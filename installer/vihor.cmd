@echo off
rem "vihor" command (added to PATH by the installer). Without arguments: open the window.
if "%~1"=="" (
  start "" "%~dp0VihorIPScanner.exe"
  exit /b 0
)
"%~dp0VihorIPScanner.exe" %*
exit /b %errorlevel%
