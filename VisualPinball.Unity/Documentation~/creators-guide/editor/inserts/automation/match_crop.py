"""match_crop.py <new> <old> [publish]: scales the new shot to the old's width, finds where the old image sits in it, and crops it there."""
import sys
from PIL import Image
import numpy as np
new, old = Image.open(sys.argv[1]).convert('RGB'), Image.open(sys.argv[2]).convert('RGB')
scaled = new.resize((old.width, round(new.height * old.width / new.width)), Image.LANCZOS)
g = lambda im: np.asarray(im.convert('L').resize((im.width // 4, im.height // 4)), dtype=float)
o = g(old)
best = None
for y in range(0, max(1, scaled.height - old.height + 1), 2):
	c = g(scaled.crop((0, y, old.width, y + old.height)))
	h = min(c.shape[0], o.shape[0])
	d = np.abs(c[:h] - o[:h]).mean()
	if best is None or d < best[0]:
		best = (d, y)
d, y = best
crop = scaled.crop((0, y, old.width, min(scaled.height, y + old.height)))
print(f'{sys.argv[1].split("/")[-1]}: offset {y}, diff {d:.1f}, {crop.size}')
if len(sys.argv) > 3:
	crop.save(sys.argv[2], optimize=True)
