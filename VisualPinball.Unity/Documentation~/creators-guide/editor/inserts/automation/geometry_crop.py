"""Cuts each shape's Geometry shot (Pinball/Automation/Geometry/Shoot in LookDev~) into geometry/inspector-<shape>.png: from the top through its last outline field, before the Body heading, at 680 wide."""
from PIL import Image
import numpy as np
from paths import DOCS, LOOKDEV, WORK, output
G = str(output(LOOKDEV, 'geometry')) + '/'
D = str(DOCS / 'geometry') + '/'
tiles = []
for n in ['triangle', 'rectangle', 'circle', 'banana', 'diamond', 'flash', 'bullet', 'arrow', 'custom']:
	im = Image.open(G + f'geometry-{n}.png').convert('RGB')
	a = np.asarray(im).astype(int)
	W = a.shape[1]
	def busy(y, x0, x1):
		row = a[y, x0:x1]
		return np.abs(row - row.mean(axis=0)).max() >= 8
	blocks, start = [], None
	for y in range(a.shape[0]):
		b = busy(y, 5, W - 5)
		if b and start is None:
			start = y
		if not b and start is not None:
			if y - start > 3:
				blocks.append((start, y))
			start = None
	# The preview is the tall block. A field's widget sits right of 0.45 W (Flash's Mirror checkbox starts left of the middle); the next block without anything on the right is the Outline heading.
	preview = next(i for i, (s, e) in enumerate(blocks) if e - s > 200)
	right = lambda blk: any(busy(y, int(W * 0.45), W - 5) for y in range(blk[0], blk[1]))
	i = preview + 1
	while right(blocks[i]):
		i += 1
	i += 1
	last = blocks[i]
	while i < len(blocks) and right(blocks[i]):
		last = blocks[i]
		i += 1
	end = last[1] + round(8 * W / 680)
	out = im.crop((0, 0, W, end)).resize((680, round(end * 680 / W)), Image.LANCZOS)
	out.save(D + f'inspector-{n}.png', optimize=True)
	print(n, out.size)
	tiles.append(out)
h = max(t.height for t in tiles)
m = Image.new('RGB', (680 * 5, h * 2), (255, 0, 255))
for i, t in enumerate(tiles):
	m.paste(t, ((i % 5) * 680, (i // 5) * h))
m.resize((m.width // 3, m.height // 3)).save(WORK / 'geometry-montage.png')
print('check', WORK / 'geometry-montage.png')
