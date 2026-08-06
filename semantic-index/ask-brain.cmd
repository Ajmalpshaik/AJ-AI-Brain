@echo off
REM Ask the AJ AI Brain a question in plain English.
REM
REM   ask-brain "how do I colour ducts by system type"
REM   ask-brain --area fragment "undo my last change"
REM   ask-brain --top 10 "sprinkler spacing rules"
REM
REM Reads only. Never changes the Brain, never touches Revit.

"%~dp0venv\Scripts\python.exe" "%~dp0brain_search.py" %*
