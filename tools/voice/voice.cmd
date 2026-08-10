@echo off
REM The voice switch. Works from any folder - it resolves its own location rather than the caller's,
REM so `voice.cmd off` silences it the same whether you are in the Brain, in a Revit project folder,
REM or anywhere else. See voice-control.mjs for what each command does.
node "%~dp0voice-control.mjs" %*
