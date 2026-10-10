"""blocks.py <image> [threshold]: prints the image's row blocks, runs of rows with something on them, to find where a section starts and ends.

A row is empty when no pixel strays more than threshold (8) from the row's mean colour. Use it to re-derive a crop when the GUI or the display scale changed.
"""
import sys
import numpy as np
from PIL import Image

a = np.asarray(Image.open(sys.argv[1]).convert('RGB')).astype(int)
threshold = int(sys.argv[2]) if len(sys.argv) > 2 else 8
W = a.shape[1]
blocks, start = [], None
for y in range(a.shape[0]):
	row = a[y, 5:W - 5]
	busy = np.abs(row - row.mean(axis=0)).max() >= threshold
	if busy and start is None:
		start = y
	if not busy and start is not None:
		if y - start > 3:
			blocks.append((start, y))
		start = None
print(f'{sys.argv[1]}: {W}x{a.shape[0]}')
print(' '.join(f'{s}-{e}' for s, e in blocks))
