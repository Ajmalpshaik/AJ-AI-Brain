@echo off
REM Rebuild the search index for the AJ AI Brain.
REM
REM Run this whenever you add or change a skill, a knowledge file, a script
REM fragment, or one of the root docs. Safe to run as often as you like.
REM
REM It only re-reads what actually changed, so it is normally 2-4 seconds.
REM A full rebuild (~80s) happens by itself when the chunking rules change.
REM
REM   index-brain          normal - only what changed
REM   index-brain --full   force a complete rebuild from scratch
REM
REM Reads skills/, knowledge/, scripts/ and the root docs.
REM Writes only inside semantic-index/.

"%~dp0venv\Scripts\python.exe" "%~dp0brain_index.py" %*
echo.
pause
