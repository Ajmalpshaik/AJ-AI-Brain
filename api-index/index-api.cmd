@echo off
REM Build the SEPARATE Revit API index from api-index\corpus\.
REM
REM This is NOT the Brain's index. Different database, different collection; the
REM Brain's own search never reads it. See README.md here for why that matters.
REM
REM First: run scripts\context\harvest-revit-api.cs through the bridge to write the corpus.
"%~dp0..\semantic-index\venv\Scripts\python.exe" "%~dp0api_index.py" %*
echo.
pause
