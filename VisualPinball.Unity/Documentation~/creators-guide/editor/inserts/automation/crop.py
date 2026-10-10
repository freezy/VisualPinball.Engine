"""crop.py <src> <dst> <top> <bottom> [width]: cuts rows top..bottom at full width, resized to width if given (keeping the aspect)."""
import sys
from PIL import Image

im = Image.open(sys.argv[1]).convert('RGB')
top, bottom = int(sys.argv[3]), int(sys.argv[4])
im = im.crop((0, top, im.width, bottom))
if len(sys.argv) > 5:
	width = int(sys.argv[5])
	im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
im.save(sys.argv[2], optimize=True)
print(sys.argv[2], im.size)
