#!/bin/sh
# usage: shoot_presets.sh
# The 20 preset example stills, through unity/PresetStills.cs, which must be in vpe-terminator2/Assets/Editor and compiled.
# Writes d_*.png into vpe-terminator2/Library/Automation/presets; then: python publish_presets.py <that folder>.
# Lamp and color presets are applied to L35 for good and put back to Incandescent and Clear at the end: check
# color-done.txt says "... Clear True -> ... (Clear) / Insert Cover, Clear". Don't save the scene.
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(cd "$HERE" && python -c "import paths; print(paths.output(paths.T2, 'presets').as_posix())")"
export INSTANCE="vpe-terminator2"
menu() { "$HERE/mcp.sh" execute_menu_item "{\"menu_path\":\"Automation Stills/Presets/$1\"}" >/dev/null; sleep 2; }
cap() { rm -f "$OUT/done.txt"; menu Capture; for i in $(seq 1 60); do [ -f "$OUT/done.txt" ] && break; sleep 2; done; }
# Off and on from 5 cm above, a touch towards the player.
pair() { printf '{"shots":[{"name":"%s_off","intensity":0,"camOffset":[0,%s,-0.004]%s},{"name":"%s_on","intensity":1,"camOffset":[0,%s,-0.004]%s}]}' "$1" "$2" "$3" "$1" "$2" "$3" > "$OUT/capture.json"; cap; }

rm -f "$OUT"/d_*.png
printf 'Insert, Triangle, 1.5in, Starburst - L35' > "$OUT/target.txt"
for lamp in Incandescent LED; do printf "$lamp" > "$OUT/lamp.txt"; menu "Apply Lamp"; pair "d_lamp_$lamp" 0.05 ""; done
printf "Incandescent" > "$OUT/lamp.txt"; menu "Apply Lamp"
for color in Clear Red Green Blue; do printf "$color" > "$OUT/color.txt"; menu "Apply Color"; pair "d_color_$color" 0.05 ""; done
printf "Clear" > "$OUT/color.txt"; menu "Apply Color"; cat "$OUT/color-done.txt"; echo
for look in "Clear Plastic" "Colored Plastic"; do pair "d_look_clear_$(echo $look | tr -d ' ')" 0.05 ",\"look\":\"$look\""; done
# The red look stills are on the red L81, a 2 in triangle, from a little further up.
printf 'Starburst, Red, L81' > "$OUT/target.txt"
for look in "Colored Plastic" "Clear Plastic"; do pair "d_look_red_$(echo $look | tr -d ' ')" 0.065 ",\"look\":\"$look\""; done
printf 'Insert, Triangle, 1.5in, Starburst - L35' > "$OUT/target.txt"
ls "$OUT"/d_*.png | wc -l
