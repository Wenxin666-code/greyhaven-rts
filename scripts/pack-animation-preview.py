"""Package already rendered PNG frames as a looping WebP; no artwork edits."""
from pathlib import Path
import sys
from PIL import Image

directory, destination, fps = sys.argv[1:]
paths = sorted(Path(directory).glob('[0-9][0-9][0-9][0-9].png'))
if not paths:
    raise SystemExit('No rendered frames')
frames = [Image.open(p) for p in paths]
try:
    frames[0].save(destination, save_all=True, append_images=frames[1:],
                   duration=round(1000 / float(fps)), loop=0, quality=85, method=4)
finally:
    for frame in frames:
        frame.close()
