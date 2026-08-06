@echo off
REM Rebuild the search index for the AJ AI Brain.
REM
REM Run this whenever you add or change a skill, a knowledge file, or a script
REM fragment. Takes about 80 seconds. Safe to run as often as you like.
REM
REM Reads skills/, knowledge/ and scripts/. Writes only inside semantic-index/.

"%~dp0venv\Scripts\python.exe" "%~dp0brain_index.py"
echo.
pause
