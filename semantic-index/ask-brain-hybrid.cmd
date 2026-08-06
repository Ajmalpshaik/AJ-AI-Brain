@echo off
REM Ask the AJ AI Brain a question, matching BOTH meaning and exact words.
REM
REM   ask-brain-hybrid "how many diffusers do I need in this room"
REM   ask-brain-hybrid --explain "sprinkler spacing rules"
REM   ask-brain-hybrid --area fragment "undo my last change"
REM
REM Use this one by default. ask-brain.cmd is the semantic-only baseline,
REM kept for comparison.
REM
REM Reads only. Never changes the Brain, never touches Revit.

"%~dp0venv\Scripts\python.exe" "%~dp0brain_search_hybrid.py" %*
