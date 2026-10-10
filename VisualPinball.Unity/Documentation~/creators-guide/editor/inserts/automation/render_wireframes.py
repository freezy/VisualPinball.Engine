"""render_wireframes.py [angled|top] [patterns...]: renders the pattern wireframes like the Scene view gizmo, a see-through yellow fill and yellow edges.

angled (the default, what patterns.md shows) reads wire3d-<pattern>.txt, which Pinball/Automation/Patterns/Export Wireframes From Scene View
writes in LookDev~/Library/Automation/patterns through the Scene view's camera, into patterns/wireframe-angled-<pattern>.png.
top reads wire-<pattern>.txt from Export Wireframes, seen from above, into WORK/wireframe-<pattern>.png (no page uses it).
A portrait image (the engraved line's flash) is padded to a square.
"""
import sys
import numpy as np
from PIL import Image, ImageDraw
from paths import DOCS, LOOKDEV, WORK, output

mode = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] in ('angled', 'top') else 'angled'
names = [a for a in sys.argv[1:] if a not in ('angled', 'top')] or ['none', 'starburst', 'ribbed', 'rings', 'gem', 'engraved-line', 'stippled']
SRC = output(LOOKDEV, 'patterns') / ('wire3d' if mode == 'angled' else 'wire')
DST = DOCS / 'patterns' / 'wireframe-angled' if mode == 'angled' else WORK / 'wireframe'

BOX = 680          # final box, in pixels (shown at half)
SS = 4             # supersampling
BG = (36, 36, 36)  # the inspector preview's background
YELLOW = np.array([255, 217, 26], float)
FILL_ALPHA = 0.3
LINE = 5           # at supersampled scale


def load(name):
	tris, edges = [], []
	for line in open(f'{SRC}-{name}.txt'):
		p = line.split()
		if not p:
			continue
		if p[0] == 't':
			tris.append([float(v) for v in p[1:]])
		elif p[0] == 'e':
			edges.append([float(v) for v in p[1:]])
	return np.array(tris).reshape(-1, 3, 2), np.array(edges).reshape(-1, 2, 2)


def render(name):
	tris, edges = load(name)
	pts = tris.reshape(-1, 2)
	lo, hi = pts.min(0), pts.max(0)
	size = hi - lo
	margin = 0.06
	scale = (BOX * SS * (1 - 2 * margin)) / size.max()
	w = int(round(size[0] * scale + 2 * margin * BOX * SS))
	h = int(round(size[1] * scale + 2 * margin * BOX * SS))
	off = np.array([(w - size[0] * scale) / 2, (h - size[1] * scale) / 2])
	# image x = mesh x, image y = mesh z: the generator's +Y is the mesh's -Z, which is up in the preview.
	to = lambda p: (p - lo) * scale + off
	count = np.zeros((h, w), np.uint16)
	for tri in tris:
		q = to(tri)
		x0, y0 = np.floor(q.min(0)).astype(int)
		x1, y1 = np.ceil(q.max(0)).astype(int) + 1
		if x1 - x0 < 1 or y1 - y0 < 1:
			continue
		mask = Image.new('L', (x1 - x0, y1 - y0), 0)
		ImageDraw.Draw(mask).polygon([tuple(v) for v in (q - [x0, y0])], fill=1)
		count[y0:y1, x0:x1] += np.asarray(mask, np.uint16)
	if mode == 'angled':
		# Seen at an angle the walls stack up; the gizmo shows about one layer, a little more where two overlap.
		alpha = np.where(count == 0, 0.0, np.where(count == 1, 0.45, 0.55))
	else:
		alpha = 1 - (1 - FILL_ALPHA) ** count
	img = np.array(BG, float)[None, None, :] * (1 - alpha[..., None]) + YELLOW * alpha[..., None]
	im = Image.fromarray(img.clip(0, 255).astype(np.uint8))
	d = ImageDraw.Draw(im)
	for a, b in edges:
		d.line([tuple(to(a)), tuple(to(b))], fill=tuple(int(c) for c in YELLOW), width=LINE)
	# Seen from above, as the Scene view and the inspector's preview show it: +Z is up.
	im = im.transpose(Image.FLIP_TOP_BOTTOM).resize((w // SS, h // SS), Image.LANCZOS)
	if im.height > im.width:
		square = Image.new('RGB', (im.height, im.height), BG)
		square.paste(im, ((im.height - im.width) // 2, 0))
		im = square
	im.save(f'{DST}-{name}.png', optimize=True)
	print(name, im.size)


for n in names:
	render(n)
