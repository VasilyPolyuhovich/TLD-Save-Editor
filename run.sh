#!/usr/bin/env bash
# Launch the TLD Save Editor (local web app). Builds on first run, then starts the
# server bound to 127.0.0.1:5173 and opens your browser.
#
# Usage:
#   ./run.sh                 # build (Release) + run + open browser
#   ./run.sh --no-browser    # run without opening a browser
set -euo pipefail

cd "$(dirname "$0")"

exec dotnet run --project src/TldSaveEditor.Server -c Release "$@"
