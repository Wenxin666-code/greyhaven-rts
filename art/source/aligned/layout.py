"""Prepare the relief mesh for the orthographic art renderer. Coordinates refer to the existing 256px master cells."""
import json
from pathlib import Path
import numpy as np
from scipy.spatial import Delaunay
root=Path(__file__).resolve().parent

# Three visible slab contacts (left, front, right), followed by the lot dimensions in tiles.
# The garage ramp extends beyond the first contact plane into the reserved front apron.
spec={
 "hq":((3,200),(170,233),(244,164),3,3),
 "power":((2,199),(151,230),(244,153),2,2),
 "factory":((9,180),(180,215),(245,147),4,3),
 "barracks":((2,184),(162,218),(245,147),3,2),
 "radar":((8,210),(157,241),(239,172),2,2),
 "post":((2,181),(162,214),(244,144),2,2),
 "depot":((2,186),(189,208),(245,161),2,2),
 "freight":((1,183),(192,201),(245,158),2,2),
 "harbormaster":((22,221),(170,241),(227,195),2,2)
}
cell=320
span=224
origin=np.array([160.,220.])
def mesh(key,v):
    left,front,right,w,d=v
    left,front,right=map(lambda p:np.array(p,dtype=float),(left,front,right))
    rear=left+right-front
    basis=np.column_stack([front-left,front-right])
    target=np.array([[1,-1],[.5,.5]]) @ np.diag([w,d])*span/(w+d)
    height_scale=np.sqrt(abs(np.linalg.det(target)/np.linalg.det(basis)))
    # Front walls lift vertically from the two visible edges. The roof is a horizontal
    # plane; projecting the tower above that plane keeps aerials and stacks upright.
    from PIL import Image
    art=np.asarray(Image.open(root.parent/'isometric/packed'/('hd-'+key+'.png')))[:256,:256,3]
    wall_h={'hq':65,'power':58,'factory':65,'barracks':58,'radar':63,'post':45,
            'depot':61,'freight':52,'harbormaster':77}[key]
    def edge(x,pivot):
        a,b=(left,pivot) if x<=pivot[0] else (pivot,right)
        return a[1]+(x-a[0])*(b[1]-a[1])/(b[0]-a[0])
    def surface(x,y):
        near,far=edge(x,front),edge(x,rear)
        base=min(wall_h,max(0,near-y))
        if key=='harbormaster':
            # Separate cabin, glazing and aerial from the low entrance block.
            sill=151+.14*(x-113)
            return base+min(73,max(0,sill-y))+max(0,sill-91-y)
        return base+max(0,.16*near+.84*far-wall_h-y)
    def project(x,y,z):
        unit=np.linalg.solve(basis,[x-rear[0],y+z-rear[1]])
        return origin+target@(unit-.5)-[0,height_scale*z]
    # Piecewise-linear strips split exactly on the eaves and foundation contacts.
    # Extra vertices at the four garage door corners attach its separate shutter.
    xs=sorted(set([0,256,left[0],front[0],right[0],rear[0],55,145]+list(range(0,257,8))))
    pts={}
    for x in xs:
        near,far=edge(x,front),edge(x,rear)
        for y in [0,256,near,near-wall_h,.16*near+.84*far-wall_h,136,151,178,193]+list(range(0,257,16)):
            if 0<=y<=256:pts[x,round(y,3)]=surface(x,y)
    # Allow taller buildings enough headroom without shifting their ground origin.
    for x in range(256):
        opaque=np.flatnonzero(art[:,x]>32)
        if not opaque.size:continue
        y=opaque[0]
        z=surface(x,y)
        if z>1:height_scale=min(height_scale,(project(x,y,z)[1]+height_scale*z-12)/z)
    uv=np.array(list(pts),dtype=float)
    coords=[project(x,y,z).round(4).tolist() for (x,y),z in pts.items()]
    return dict(uv=uv.tolist(),xy=coords,triangles=Delaunay(uv).simplices.tolist(),tiles=[w,d],door=[project(x,y,surface(x,y)).tolist() for x,y in [(55,136),(145,151),(145,193),(55,178)]])
data={k:mesh(k,v) for k,v in spec.items()}
(root/'relief.js').write_text('const relief='+json.dumps(data,separators=(',',':'))+';\n',encoding='utf-8')
