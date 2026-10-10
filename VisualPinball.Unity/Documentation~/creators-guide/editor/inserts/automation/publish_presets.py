"""publish_presets.py [capture folder]: crops the preset example stills (d_*.png, 900x900, from shoot_presets.sh) into presets/*.png at 320x320.

The L35 stills are cropped to (150,150,750,750), the red L81 look stills, a larger insert from further up, to (120,120,780,780).
The names are mapped by hand: a CamelCase converter turns LED into l-e-d.
"""
import sys
from pathlib import Path
from PIL import Image
from paths import DOCS, T2, output

src = Path(sys.argv[1]) if len(sys.argv) > 1 else output(T2, 'presets')
L35, L81 = (150, 150, 750, 750), (120, 120, 780, 780)


def save(capture, name, crop):
	Image.open(src / f'{capture}.png').convert('RGB').crop(crop).resize((320, 320), Image.LANCZOS).save(DOCS / 'presets' / f'{name}.png', optimize=True)


for lamp in ['Incandescent', 'LED']:
	for state in ['off', 'on']:
		save(f'd_lamp_{lamp}_{state}', f'lamp-{lamp.lower()}-{state}', L35)
for color in ['Clear', 'Red', 'Green', 'Blue']:
	for state in ['off', 'on']:
		save(f'd_color_{color}_{state}', f'color-{color.lower()}-{state}', L35)
for insert, crop in [('clear', L35), ('red', L81)]:
	for look, slug in [('ClearPlastic', 'clear-plastic'), ('ColoredPlastic', 'colored-plastic')]:
		for state in ['off', 'on']:
			save(f'd_look_{insert}_{look}_{state}', f'look-{insert}-{slug}-{state}', crop)
print('20 published to presets')
