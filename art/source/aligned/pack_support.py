"""Register the four new cardinal views between the previously painted diagonal views."""
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage

root=Path(__file__).resolve().parent
src=Image.open(root/'support-cardinals.png').convert('RGBA')
for row,key in enumerate(['mcv','arty']):
    old=Image.open(root.parent/'isometric/packed'/('hd-'+key+'.png'))
    sheet=Image.new('RGBA',(2048,512))
    for i,(x0,x1) in enumerate(zip([0,.191,.495,.687],[.191,.495,.687,1])):
        w,h=src.size
        im=src.crop((round(w*x0),row*h//2,round(w*x1),(row+1)*h//2))
        a=np.array(im.getchannel('A'))
        lab,_=ndimage.label(a>120)
        core=lab==np.argmax(np.bincount(lab.ravel())[1:])+1
        a=np.where(ndimage.binary_dilation(core,iterations=2),a,0).astype('uint8')
        im.putalpha(Image.fromarray(a));im=im.crop(im.getbbox())
        im.thumbnail((246,246),Image.Resampling.LANCZOS)
        x=(256-im.width)//2;y=(256-im.height)//2
        pix=np.array(im)
        r,g,b=[pix[:,:,j].astype(float) for j in range(3)]
        mask=(b>r*1.12)&(b>g*.94)&(b>42)
        for side in range(2):
            if side:
                for j,k in enumerate([1.27,.56,.43]):
                    pix[:,:,j][mask]=np.clip(np.maximum(g,b)[mask]*k,0,255)
            sheet.alpha_composite(Image.fromarray(pix),(512*i+x,side*256+y))
            sheet.alpha_composite(old.crop((i*256,side*256,(i+1)*256,side*256+256)),(512*i+256,side*256))
    sheet.save(root/('views-'+key+'.png'),optimize=True)
