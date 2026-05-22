@echo off
REM Launch the TLD Save Editor (local web app) on Windows.
REM Builds on first run, then serves http://127.0.0.1:5173 and opens your browser.
REM Requires the .NET 10 SDK (https://dotnet.microsoft.com/download).
REM
REM Usage:
REM   run.cmd                (double-click, or run from a terminal)
REM   run.cmd --no-browser   run without opening a browser
cd /d "%~dp0"
dotnet run --project "src\TldSaveEditor.Server" -c Release %*
