@echo off
REM Score the Brain's search against semantic-index\test-questions.md.
REM
REM   score-brain              score every question
REM   score-brain --self-test  check the scorer itself, no model load needed
REM
REM Run this BEFORE and AFTER any change to the embedding model, the chunking, or
REM the files - so the change is measured instead of guessed. Every run appends one
REM line to score-history.md, and prints the previous run for comparison.
REM
REM NOTE FOR SCRIPTS AND HOOKS: this wrapper ends with `pause`, so it BLOCKS forever
REM waiting for a keypress. It is for double-clicking. Anything automated must call
REM score_brain.py through venv\Scripts\python.exe directly - same rule as
REM index-brain.cmd.
REM
REM Reads only. Never changes the Brain, never touches Revit.

"%~dp0venv\Scripts\python.exe" "%~dp0score_brain.py" %*
echo.
pause
