"""stitch.py <prefix> <out> [window width]: stitches vpe-terminator2's Library/Automation/lighting/<prefix>-0.png, -500.png, ... (one inspector scrolled 500 points apart, from Lighting/Shoot Material Copy) into one tall image.

The window width is in points (340 by default, the material shots' width): it gives the scale from points to pixels.
"""
import sys
from PIL import Image
import numpy as np
from paths import T2, output

src, prefix, out = output(T2, 'lighting'), sys.argv[1], sys.argv[2]
slices = []
while (src / f'{prefix}-{len(slices) * 500}.png').exists():
	slices.append(Image.open(src / f'{prefix}-{len(slices) * 500}.png').convert('RGB'))
step = round(500 * slices[0].width / (float(sys.argv[3]) if len(sys.argv) > 3 else 340))
# The next slice starts where this one is step rows down: a large difference means the scale is wrong.
a, b = np.asarray(slices[0]).astype(int), np.asarray(slices[1]).astype(int)
print('overlap diff', np.abs(a[step:step + 50] - b[:50]).mean(), 'step', step)
tall = Image.new('RGB', (slices[0].width, step * (len(slices) - 1) + slices[-1].height))
for i, s in enumerate(slices):
	tall.paste(s if i == len(slices) - 1 else s.crop((0, 0, s.width, step)), (0, i * step))
tall.save(out)
print(tall.size)
