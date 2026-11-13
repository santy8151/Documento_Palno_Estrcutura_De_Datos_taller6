#!/usr/bin/env bash
# Linux launcher. Usage: ./run.sh [start] [--en]
#   ./run.sh          -> console + Spanish map at http://localhost:5000/
#   ./run.sh D --en   -> console + English map at http://localhost:5000/en/
set -euo pipefail
cd "$(dirname "$0")/../DeliveryRoutes"

command -v dotnet >/dev/null || { echo ".NET 8 SDK not found. See linux/README.md"; exit 1; }

START="${1:-D}"
URL_PATH=""
[[ "${2:-}" == "--en" ]] && URL_PATH="en/"

# Process.Start cannot open a browser on most Linux desktops, so we do it here.
if command -v xdg-open >/dev/null; then
  (sleep 3 && xdg-open "http://localhost:5000/${URL_PATH}" >/dev/null 2>&1 &) 
fi
dotnet run -- city.txt packages.txt "$START" --web --no-open
