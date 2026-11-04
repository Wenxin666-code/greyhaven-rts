"""Geometry calibration for each illustrated chassis view. Rear axle, front axle, and transverse ground vector in source pixels."""
from pathlib import Path
import numpy as np, json
from scipy.spatial import Delaunay

# Index order is the clockwise yaw of the miniature measured from a front view.
axes={
'tank':[
 ((126,100),(126,213),(105,0)),((54,149),(174,211),(65,-32)),
 ((42,170),(193,170),(0,-65)),((67,202),(183,141),(-67,-31)),
 ((127,211),(127,103),(-104,0)),((169,206),(62,147),(-73,36)),
 ((211,170),(62,170),(0,62)),((190,142),(79,204),(74,35))],
'ifv':[
 ((127,105),(127,230),(96,0)),((84,151),(186,208),(64,-36)),
 ((59,192),(202,192),(0,-61)),((82,201),(183,140),(-67,-32)),
 ((127,224),(127,105),(-97,0)),((165,202),(78,148),(-66,33)),
 ((202,191),(55,191),(0,59)),((181,147),(80,209),(63,33))],
'arty':[
 ((67,159),(180,208),(65,-29)),((84,206),(181,157),(-63,-27)),
 ((162,209),(62,163),(-66,34)),((190,162),(86,211),(62,26))],
'mcv':[
 ((78,155),(181,209),(67,-30)),((80,198),(177,142),(-67,-33)),
 ((180,196),(82,140),(-68,34)),((184,143),(86,204),(62,30))]
}

cardinal={
 'mcv':[((128,115),(128,241),(95,0)),((46,180),(190,180),(0,-48)),((128,241),(128,115),(-95,0)),((210,180),(67,180),(0,48))],
 'arty':[((128,146),(128,239),(94,0)),((67,207),(195,207),(0,-46)),((128,238),(128,142),(-94,0)),((192,207),(61,207),(0,46))]
}
for k,extra in cardinal.items():
    axes[k]=[v for pair in zip(extra,axes[k]) for v in pair]
# Breech to muzzle: lock the entire barrel to a single straight elevated beam.
guns={
 'tank':[((124,107),(123,247)),((128,107),(250,207)),((130,125),(251,125)),((148,101),(246,52)),
         ((126,105),(124,19)),((115,102),(20,57)),((126,129),(4,129)),((124,112),(8,220))],
 'arty':[((128,123),(128,13)),((123,129),(210,7)),((127,142),(239,32)),((137,121),(210,9)),
         ((128,121),(128,10)),((112,128),(44,8)),((124,140),(11,34)),((110,126),(47,9))]
}
def view(data,key,index):
    rear,front,width=map(lambda v:np.array(v,dtype=float),data)
    length=front-rear
    basis=np.column_stack((width,length))
    mid=(front+rear)/2
    corners=np.array([mid+width*u+length*v for u,v in [(-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5)]])
    corners=corners[np.argsort(np.arctan2(corners[:,1]-mid[1],corners[:,0]-mid[0]))]
    def limits(x):
        x=np.clip(x,corners[:,0].min()+.01,corners[:,0].max()-.01)
        ys=[a[1]+(x-a[0])*(b[1]-a[1])/(b[0]-a[0]) for a,b in zip(corners,np.roll(corners,-1,axis=0)) if min(a[0],b[0])<=x<=max(a[0],b[0]) and abs(b[0]-a[0])>.01]
        return min(ys),max(ys)
    points={}
    nominal={'tank':58,'ifv':76,'arty':67,'mcv':74}[key]
    def lift_at(x,y):
        far,near=limits(x)
        return min(nominal,max(0,near-y))+max(0,.65*far+.35*near-nominal-y)
    gun=guns.get(key)
    if gun:
        a,b=np.array(gun[index],dtype=float)
        ray=b-a
        ha=lift_at(*a)
        # Along the gun axis the transverse coordinate is constant.
        hb=ha+(ray[0]*length[1]/length[0] if abs(length[0])>10 else (ray[1]*.5 if key=='arty' else ray[1]))-ray[1]
    xs=sorted(set([*range(0,257,6),256,*corners[:,0]]))
    for x in xs:
        far,near=limits(x);ridge=far*.65+near*.35-nominal
        ys=[0,256,near,near-nominal,ridge,*range(0,257,10)]
        if gun and abs(ray[0])>1:
            center=a[1]+(x-a[0])*ray[1]/ray[0]
            ys.extend(center+v for v in (-10,-5,0,5,10))
        for y in set(ys):
            if 0<=y<=256:
                lift=lift_at(x,y)
                if gun:
                    t=np.clip(np.dot(np.array([x,y])-a,ray)/np.dot(ray,ray),0,1)
                    radius=np.linalg.norm(np.array([x,y])-a-t*ray)
                    weight=np.clip((19-radius)/9,0,1)
                    lift=(1-weight)*lift+weight*(ha+t*(hb-ha))
                u,v=np.linalg.solve(basis,np.array([x,y+lift])-mid)
                points[x,round(y,3)]=[round(u,5),round(v,5),round(lift,3)]
    uv=np.array(list(points))
    return dict(uv=uv.round(3).tolist(),xyz=list(points.values()),tri=Delaunay(uv).simplices.tolist())
data={k:[view(v,k,i) for i,v in enumerate(vs)] for k,vs in axes.items()}
Path(__file__).with_name('chassis.js').write_text('const chassis='+json.dumps(data,separators=(',',':'))+';\n',encoding='utf8')
