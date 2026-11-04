"""Pack approved transparent art into fixed-anchor runtime sheets. Requires Pillow, NumPy and SciPy."""
from pathlib import Path
import numpy as np
from scipy import ndimage
from PIL import Image, ImageOps

root = Path(__file__).resolve().parents[1] / "art/source"
src = root / "isometric"
out = src / "packed"
out.mkdir(exist_ok=True)
cell = 256

def islands(image):
    alpha = np.asarray(image.getchannel("A"))
    labels, _ = ndimage.label(alpha > 90)
    ids = np.argsort(np.bincount(labels.ravel())[1:])[-3:] + 1
    boxes = []
    for key in ids:
        mask = labels == key
        mask = ndimage.binary_dilation(mask, iterations=3)
        item = image.copy()
        item.putalpha(Image.fromarray(np.where(mask, alpha, 0).astype("uint8")))
        bounds = item.getbbox()
        boxes.append((bounds[0], item.crop(bounds)))
    return [im for _, im in sorted(boxes, key=lambda v: v[0])]

def frame(image, cols, rows, idx):
    x, y = idx % cols, idx // cols
    w, h = image.size
    crop = image.crop((round(w*x/cols), round(h*y/rows), round(w*(x+1)/cols), round(h*(y+1)/rows)))
    return crop.crop(crop.getbbox())

def store(name, frames):
    sheet = Image.new("RGBA", (cell*len(frames), cell*2))
    for i, im in enumerate(frames):
        im.thumbnail((cell-10, cell-10), Image.Resampling.LANCZOS)
        # Keep the hull mass on the map anchor even when the barrel protrudes.
        alpha = np.asarray(im.getchannel("A"))
        yy, xx = np.nonzero(alpha > 160)
        x = int(np.clip(cell*.5-np.mean(xx), 0, cell-im.width))
        y = int(np.clip(cell*.55-np.mean(yy), 0, cell-im.height))
        sheet.alpha_composite(im,(i*cell+x,y))
        pix = np.array(im)
        r,g,b=[pix[:,:,k].astype(float) for k in range(3)]
        mask=(b>r*1.12)&(b>g*.94)&(b>42)
        intensity=np.maximum(g,b)
        for k, factor in enumerate([1.27,.56,.43]):
            pix[:,:,k][mask]=np.clip(intensity[mask]*factor,0,255)
        sheet.alpha_composite(Image.fromarray(pix),(i*cell+x,y+cell))
    sheet.save(out/("hd-"+name+".png"), optimize=True)

for source, names in [(root/"v01/base.png",["hq","power","factory"]), (src/"buildings.png",["barracks","radar","post"])]:
    for name, im in zip(names,islands(Image.open(source).convert("RGBA"))):
        store(name,[im])
for name in ["tank","ifv"]:
    source = Image.open(src/(name+".png")).convert("RGBA")
    frames=[frame(source,4,2,i) for i in range(8)]
    store(name,[frames[0],ImageOps.mirror(frames[1]),frames[6],frames[3],frames[4],frames[5],frames[2],frames[7]])
source=Image.open(src/"troopers.png").convert("RGBA")
for row,name in enumerate(["squad","at","engineer"]):
    store(name,[frame(source,4,3,row*4+i) for i in (1,2,3,0)])

for name,im in zip(["depot","freight","harbormaster"],islands(Image.open(src/"objectives.png").convert("RGBA"))):
    store(name,[im])
source=Image.open(src/"support.png").convert("RGBA")
for row,name in enumerate(["mcv","arty"]):
    store(name,[frame(source,4,2,row*4+i) for i in (1,2,3,0)])
