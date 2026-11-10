"""Render a planning diagram, not game art or runtime terrain. Requires Pillow."""
from pathlib import Path
import argparse
import json
import math
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DEFAULT = ROOT / 'docs/design/雾脊中继站-v01'
parser = argparse.ArgumentParser()
parser.add_argument('--folder', type=Path, default=DEFAULT)
args = parser.parse_args()
plan = json.loads((args.folder / 'level-plan.json').read_text(encoding='utf-8'))

W, H = 2080, 1380
canvas = Image.new('RGB', (W, H), '#101a1e')
d = ImageDraw.Draw(canvas)
FONT = Path('C:/Windows/Fonts/msyh.ttc')
BOLD = Path('C:/Windows/Fonts/msyhbd.ttc')
fonts = {}
def font(size, bold=False):
    key = (size, bold)
    if key not in fonts:
        fonts[key] = ImageFont.truetype(str(BOLD if bold else FONT), size)
    return fonts[key]
def text(p, s, size=22, color='#e4e8df', bold=False):
    d.text(p, s, font=font(size,bold), fill=color)
def panel(box, fill='#1c2a2e', border='#435453'):
    d.rectangle(box, fill=fill, outline=border, width=1)
def lines(x, y, body, size=22, color='#cbd2ca', gap=10):
    for line in body:
        text((x,y),line,size,color)
        y += size+gap
    return y

text((48,30),'04  雾脊中继站',49,bold=True)
text((50,98),'重连灰港  /  山地夺控与通信恢复',24,'#d5b471')
text((1490,42),'关卡设计草案  V01',26,bold=True)
text((1490,87),'2026.11.09   ·   尚未接入正式游戏',20,'#98aaa8')

mx,my,mw,mh = 48,176,1392,1010
def pt(p):return (mx+p[0]*mw/100, my+p[1]*mh/100)
def world(p):
    u=-180+p[0]*4.2;v=p[1]*4.2
    return ((u+v)/2,(v-u)/2)
def dist_segment(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]
    k=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/(dx*dx+dy*dy)))
    return math.hypot(p[0]-a[0]-k*dx,p[1]-a[1]-k*dy)
def inside(p,poly):
    x,y=p;hit=False
    for i,a in enumerate(poly):
        b=poly[i-1]
        if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:hit=not hit
    return hit
route_segments=[]
for r in plan['routes']:
    points=[world(p) for p in r['points']]
    route_segments += [(a,b,r['width_cells']/2) for a,b in zip(points,points[1:])]
river_points=[world(p) for p in plan['river']]
river_segments=list(zip(river_points,river_points[1:]))

def water(p,w):
    return inside(p,plan['lake']) or any(dist_segment(w,a,b)<2.3 for a,b in river_segments)

# Each diamond represents one planning cell in the same x-y / x+y projection.
# Coordinates outside the authored core fill the entire radar-domain rectangle.
tilex=mw/420;tiley=mh/420
for x in range(-91,332):
    for y in range(-121,302):
        u=(x-y+180)/4.2;v=(x+y+1)/4.2
        if not (-.3<u<100.3 and -.3<v<100.3):continue
        p=(u,v);w=(x+.5,y+.5);h=(x*73856093^y*19349663)&0xffffffff
        wet=water(p,w)
        road=any(dist_segment(w,a,b)<=half for a,b,half in route_segments)
        ridge=any(inside(p,poly) for poly in plan['ridges'])
        forest=any(((u-a)/rx)**2+((v-b)/ry)**2<1 for a,b,rx,ry in plan['forest_zones'])
        clear=any(math.hypot(u-a,v-b)<5 for a,b in [plan['player']['position'],plan['enemy']['position'],plan['relay']['position']]) or any(math.hypot(u-patch['at'][0],v-patch['at'][1])<3 for patch in plan['ore'])
        if road: colors=['#a3a38a','#969982','#b0ad92'] if not wet else ['#9a9b89','#aaaa94']
        elif wet:colors=['#314f5c','#355560','#3a5b64']
        elif ridge and not clear:colors=['#697568','#717b6a','#626c61','#7a8372']
        elif forest and not clear:colors=['#304a40','#385043','#405b4b','#47604e']
        else:colors=['#64735a','#6a775e','#707d60','#607056']
        cx,cy=pt(p)
        poly=[(cx-tilex,cy),(cx,cy-tiley),(cx+tilex,cy),(cx,cy+tiley)]
        d.polygon(poly,fill=colors[h%len(colors)])
        if forest and not road and not clear and not wet and h%9==0:
            d.line((cx-1,cy-3,cx+1,cy+1),fill='#293d36',width=2)

# Clip outside terrain to the rectangular radar panel.
d.rectangle((0,my-8,mx-1,my+mh+8),fill='#101a1e')
d.rectangle((mx+mw+1,my-8,W,my+mh+8),fill='#101a1e')
d.rectangle((mx-8,my-8,mx+mw+8,my-1),fill='#101a1e')
d.rectangle((mx-8,my+mh+1,mx+mw+8,H),fill='#101a1e')
d.rectangle((mx,my,mx+mw,my+mh),outline='#67756e',width=2)

def dash_line(a,b,color,width=2,dash=11):
    length=math.dist(a,b)
    for n in range(0,int(length),dash*2):
        t0=n/length;t1=min(1,(n+dash)/length)
        d.line((a[0]+(b[0]-a[0])*t0,a[1]+(b[1]-a[1])*t0,a[0]+(b[0]-a[0])*t1,a[1]+(b[1]-a[1])*t1),fill=color,width=width)
def arrow(a,b,color):
    dx,dy=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dy);dx/=length;dy/=length
    d.polygon([b,(b[0]-dx*14-dy*6,b[1]-dy*14+dx*6),(b[0]-dx*14+dy*6,b[1]-dy*14-dx*6)],fill=color)
for r in plan['routes']:
    points=[pt(p) for p in r['points']]
    if r['id']=='L':continue
    for a,b in zip(points,points[1:]):
        dash_line(a,b,r['color'],3,13) if r['id']=='R' else d.line((*a,*b),fill=r['color'],width=3)
    for i in [2,len(points)-2]:
        a,b=points[i],points[i+1]
        tip=(a[0]+(b[0]-a[0])*.7,a[1]+(b[1]-a[1])*.7)
        arrow(a,tip,r['color'])

def label(p,title,color='#e5e7dc',size=20):
    x,y=pt(p);bounds=d.textbbox((0,0),title,font=font(size,True));ww=bounds[2]+18;hh=size+14
    d.rectangle((x-8,y-5,x+ww-8,y+hh-5),fill='#19262b',outline=color,width=1)
    text((x,y),title,size,color,True)

for patch in plan['ore']:
    x,y=pt(patch['at'])
    for i in range(4):
        for j in range(3):
            xx=x+(i-j)*6;yy=y+(i+j)*3
            d.polygon([(xx-5,yy),(xx,yy-3),(xx+5,yy),(xx,yy+3)],fill='#c2a763',outline='#e1c583')
    text((x+22,y-6),patch['id'],16,'#f2d69b',True)

for cluster in plan['houses']['clusters']:
    x,y=pt(cluster['at'])
    for i in range(cluster['count']):
        xx=x+(i%3-1)*20;yy=y+(i//3-1)*17
        d.polygon([(xx-8,yy),(xx,yy-5),(xx+8,yy),(xx,yy+5)],fill='#c8cabe',outline='#ebede1')

# Strategic symbols denote locations, not enlarged in-game building models.
for key,color in [('player','#80b6de'),('enemy','#df9287'),('relay','#efd18a')]:
    x,y=pt(plan[key]['position'])
    ww,hh=(40,26) if key!='relay' else (30,22)
    d.rectangle((x-ww,y-hh,x+ww,y+hh),fill='#243337',outline=color,width=2)
    if key=='relay':
        d.line((x,y+10,x,y-16),fill=color,width=3)
        d.line((x-11,y-11,x+11,y-11),fill=color,width=3)
        d.polygon([(x-13,y+12),(x-9,y+3),(x+9,y+3),(x+13,y+12)],outline=color,fill='#4a4d40')
    else:
        d.polygon([(x-19,y+12),(x-19,y-9),(x,y-19),(x+19,y-9),(x+19,y+12)],outline=color)
        d.line((x-19,y-9,x+19,y-9),fill=color,width=2)

for pos in [item['at'] for item in plan['locations'] if item['type']=='enemy_outpost']:
    x,y=pt(pos);d.rectangle((x-8,y-8,x+8,y+8),fill='#684740',outline='#edb1a3',width=2)
    d.line((x,y-8,x+10,y-17),fill='#e8aa9b',width=3)

label([6,88],'P  我方展开区','#8ac1e4')
label([47,12],'T  中继控制站','#efd18a')
label([78,50],'E  敌方生产基地','#e3a094')
label([39,68],'山脚旧村')
label([21,39],'林间驿点')
label([47,51],'正面封锁点','#e1a194')
label([60,52],'东麓路口')
label([59,25],'东侧站口','#e1a194',18)
label([35,61],'A','#d5b471',25)
label([15,53],'B','#78c8bc',25)
label([60,74],'C','#8eb5e5',25)
label([76,22],'反夺通路','#d88780',18)
label([69,64],'8 格桥面','#c3d4d1',17)
text(pt([74,84]),'雾脊湖',24,'#c3d4d1',True)
text(pt([4,9]),'林地与岩脊',24,'#c5cebd')
text(pt([4,14]),'阻挡沿地格组织',18,'#b7c0b1')
text(pt([47,8]),'站顶高地 +2',17,'#e0dfc7')
text(pt([41,95]),'共享矿区 / 可选外扩',18,'#d2d4bd')

x=1480;right=2032
panel((x,176,right,424))
text((x+24,194),'主任务',28,'#efd18a',True)
lines(x+24,246,['工兵占领中继控制站','保持控制，累计恢复 360 游戏秒','默认 2×：恢复阶段约 3 分钟','失守暂停并保留进度，完成即获胜'],22)

cards=[('A  正面公路','#d5b471',['8 格宽 · 较短 · 正面设防','适合坦克、步兵与火炮协同']),('B  林区侧路','#78c8bc',['6 格宽 · 较长 · 两段缓坡','全部地面单位可走，适合侧击']),('C  东麓截击线','#8eb5e5',['8 格宽 · 可选行动 · 一座宽桥','打击矿车与工厂，降低反夺压力'])]
y=444
for title,color,body in cards:
    panel((x,y,right,y+144));d.rectangle((x,y,x+5,y+144),fill=color)
    text((x+24,y+16),title,27,color,True);lines(x+24,y+64,body,21,gap=9);y+=162

panel((x,946,right,1186))
text((x+24,963),'敌军与目标规则',27,'#df9287',True)
lines(x+24,1012,['敌军从东侧基地真实采矿、生产','沿道路反夺，不凭空刷新援军','中继站不是雷达，不提供收入','任务站毁坏：本关失败'],22)

text((48,1211),'核心布局 240 × 180 格  ·  我方 1 方 / 敌方 1 方  ·  3 条战术路线',24,'#e5dfce',True)
text((48,1254),'蓝：我方起点   /   红：敌方阵地   /   金：任务目标与矿点   /   灰白：中立房屋',21,'#bdc8c1')
text((48,1294),'布局示意，非实机画面。格网为规划投影，标记放大用于阅读；正式建筑尺寸与现有占格一致。',20,'#90a3a3')
text((1480,1244),'地图与任务方案待试玩校准',23,'#cbd3c8',True)
text((1480,1282),'未制作第四关 · 未改动前三关',21,'#90a3a3')

args.folder.mkdir(parents=True,exist_ok=True)
path=args.folder/'布局示意.png'
canvas.save(path,optimize=True)
print(path)
