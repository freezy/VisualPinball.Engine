"""publish.py <prefix> <doc folder> [names]: copies vpe-terminator2's stills named <prefix><name>.png to <doc folder>/<name>.png at 480x480.

names, comma separated without extension, limits it to those. Clear old <prefix>*.png in the output first, or stale stills get published too.

Stills of parameters that only act on the lit insert (the lower one) leave out the unlit one above it: the top third
is cropped off and they are published at 480x320. That's the Lamp Output and Virtual Lamp sections of the body
material, except Index of Refraction, which has a view of its own, plus Roughness and Facet Contrast.
"""
import sys
from PIL import Image
from paths import DOCS, T2, output

LIT_ONLY = {'body-material': ('max-brightness-', 'lamp-emission-', 'radius-', 'strength-', 'lamp-spread-', 'roughness-', 'facet-contrast-')}

prefix, folder = sys.argv[1], sys.argv[2]
names = sys.argv[3].split(',') if len(sys.argv) > 3 else None
count = 0
for path in sorted(output(T2, 'lighting').glob(prefix + '*.png')):
	name = path.name[len(prefix):]
	if names and name[:-4] not in names:
		continue
	im = Image.open(path).convert('RGB')
	if name.startswith(LIT_ONLY.get(folder, ())):
		im = im.crop((0, im.height // 3, im.width, im.height)).resize((480, 320), Image.LANCZOS)
	else:
		im = im.resize((480, 480), Image.LANCZOS)
	im.save(DOCS / folder / name, optimize=True)
	count += 1
print(count, 'published to', folder)
