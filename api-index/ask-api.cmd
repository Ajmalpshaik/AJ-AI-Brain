@echo off
REM Ask the Revit API reference — signatures, properties, enum values.
REM
REM   ask-api "how do I collect every element of a category"
REM
REM For how THIS BRAIN actually does a job, ask the Brain instead:
REM   semantic-index\ask-brain-hybrid.cmd "..."
"%~dp0..\semantic-index\venv\Scripts\python.exe" "%~dp0api_search.py" %*
