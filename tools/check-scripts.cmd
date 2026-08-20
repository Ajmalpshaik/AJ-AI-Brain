@echo off
REM  "Will my scripts work in this Revit?"  -  answered in about a minute.
REM
REM  Double-click this after installing a new Revit version. It checks every Revit on
REM  this PC against the whole script library WITHOUT opening Revit and changes nothing.
REM
REM     check-scripts                check every Revit found
REM     check-scripts -Only 2024     check just that one
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0check-scripts.ps1" %*
echo.
pause
