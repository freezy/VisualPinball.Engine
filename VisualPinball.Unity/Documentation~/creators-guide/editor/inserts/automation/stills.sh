#!/bin/sh
# usage: stills.sh <config.json>
# Runs Pinball/Automation/Lighting/Capture in vpe-terminator2 with the config and waits for its done.txt.
# The stills land in vpe-terminator2/Library/Automation/lighting/<name>.png at 900x900.
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(cd "$HERE" && python -c "import paths; print(paths.output(paths.T2, 'lighting').as_posix())")"
export INSTANCE="vpe-terminator2"
cp "$1" "$OUT/shots.json"; rm -f "$OUT/done.txt"
n=$(python -c "import json,sys; print(len(json.load(open(sys.argv[1]))['shots']))" "$1")
"$HERE/mcp.sh" execute_menu_item '{"menu_path":"Pinball/Automation/Lighting/Capture"}' | grep -o '"success": [a-z]*'
timeout $((60 + n * 15)) sh -c "until [ -f '$OUT/done.txt' ]; do sleep 3; done"; echo "done $? ($n shots)"
"$HERE/mcp.sh" read_console '{"types":["error"],"count":5}' | grep -o '"Automation[^"]*"\|"[^"]*Exception[^"]*"' | head -3
