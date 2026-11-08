"""Layer V10 rigid construction and fracture sequences over unchanged V09 idle art."""
from pathlib import Path
from PIL import Image, ImageChops, ImageStat
import hashlib, io, json, struct

root=Path(__file__).resolve().parents[1]
source=root/'art/source/buildings-v10'
old=root/'art/source/buildings-v09/frames'
entries={};records={};fingerprints=set()
def rgba(p): return Image.open(p).convert('RGBA')
def pair(name,a,b):
    if a.size!=b.size: raise ValueError(name+' layer size')
    for suffix,im in [('',a),('-paint',b)]:
        data=io.BytesIO();im.save(data,format='PNG',optimize=True);entries[name+suffix]=data.getvalue()
def delta(a,b):
    extrema=ImageChops.difference(a,b).getextrema()
    return max(high for low,high in extrema) if isinstance(extrema[0],tuple) else extrema[1]
material=hashlib.sha256(b''.join((source/'materials'/n).read_bytes() for n in ['coated-armor.png','cast-concrete.png'])).hexdigest()
for key in ['hq','barracks','factory','power','radar','satellite','post','refinery','residence','shop','municipal']:
    folder=source/'frames'/key;audit=json.loads((folder/'audit.json').read_text(encoding='utf-8'));fingerprints.add(audit['fingerprint'])
    if audit['materialFingerprint']!=material: raise ValueError('stale material '+key)
    complete=rgba(old/key/'base.png');paint=rgba(old/key/'paint.png');wreck=rgba(folder/'wreck.png');wreck_paint=rgba(folder/'wreck-paint.png')
    # Re-rendered edges / shadow filtering can differ between GPU sessions.
    # Bound premultiplied error and silhouette, then use the original endpoint
    # bytes so the shipped complete appearance and state change are exact.
    for original,rerender,alpha_limit in [(complete,rgba(folder/'base.png'),12),(paint,rgba(folder/'paint.png'),16)]:
        difference=ImageChops.difference(original.convert('RGBa'),rerender.convert('RGBa'))
        if max(ImageStat.Stat(difference).mean)>2 or delta(original.getchannel('A'),rerender.getchannel('A'))>alpha_limit:raise ValueError('completed appearance changed '+key)
    sheet=Image.new('RGBA',(320,960));mask=Image.new('RGBA',(320,960))
    for y in [0,320]:sheet.paste(complete,(0,y));mask.paste(paint,(0,y))
    sheet.paste(wreck,(0,640));pair('iso-'+key,sheet,mask)
    # The rejected physical-collapse frames stay archived in the authoring
    # folder. Ship construction, static ruins and the upright damage states.
    kinds=[('build',72)]+([('deploy',96)] if key=='hq' else [])
    for kind,count in kinds:
        if len(audit['frames'][kind])!=count:raise ValueError('incomplete '+key+'/'+kind)
        for i in range(count):
            a=rgba(folder/kind/(str(i)+'.png'));b=rgba(folder/kind/(str(i)+'-paint.png'))
            if i==count-1:
                target,target_paint=complete,paint
                endpoint=ImageChops.difference(a.convert('RGBa'),rgba(folder/'base.png').convert('RGBa'))
                if max(ImageStat.Stat(endpoint).mean)>2 or endpoint.getextrema()[3][1]>12:raise ValueError('endpoint '+key+'/'+kind)
                a,b=target,target_paint
            pair(f'b08/{key}/{kind}/{i}',a,b)
    for level in range(4):pair(f'b08/{key}/partial-{level}/47',rgba(folder/f'partial-{level}/47.png'),rgba(folder/f'partial-{level}/47-paint.png'))
    damage=json.loads((folder/'damage/audit.json').read_text(encoding='utf-8'))
    for level in range(1,4):
        pair(f'b08/{key}/damage-{level}',rgba(folder/f'damage/stage-{level}.png'),rgba(folder/f'damage/stage-{level}-paint.png'))
        pair(f'b08/{key}/damage-{level}-background',rgba(folder/f'damage/background-{level}.png'),rgba(folder/f'damage/background-{level}-paint.png'))
        if key=='factory':
            prior=json.loads((root/'art/source/buildings-v09/packed/manifest.json').read_text(encoding='utf-8'))
            crop=tuple(prior['buildings'][key]['doorCrop'])
            for i in range(16):pair(f'b08/{key}/damage-door-{level}/{i}',rgba(folder/f'damage/door-{level}/{i}.png').crop(crop),rgba(folder/f'damage/door-{level}/{i}-paint.png').crop(crop))
    records[key]={'stats':audit['stats'],'structure':audit['structure'],'damage':damage}
    print(key,audit['structure']['rigidPieces'],'rigid parts',flush=True)
if len(fingerprints)!=1:raise ValueError('mixed source revisions')
dest=source/'packed';dest.mkdir(exist_ok=True);index=io.BytesIO();offset=0
for name,data in entries.items():
    key=name.encode('utf-8');index.write(struct.pack('<H',len(key)));index.write(key);index.write(struct.pack('<qi',offset,len(data)));offset+=len(data)
with (dest/'structures-v10.pak').open('wb') as file:
    file.write(b'GHBLD008');file.write(struct.pack('<i',len(entries)));file.write(index.getvalue())
    for data in entries.values():file.write(data)
manifest={'version':10,'frameSize':320,'buildFrames':72,'partialLevels':[.125,.375,.625,.875],'terminalBlastAt':1.28,'terminalWreckAt':1.43,'destructionSeconds':2.4,'physicalCollapseShipped':False,'bytes':offset,'sourceFingerprint':next(iter(fingerprints)),'materialFingerprint':material,'buildings':records}
(dest/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['// Generated by pack-structure-v10.py; canonical 320px model sockets.','using System.Drawing;','using System.Collections.Generic;','namespace Greyhaven {','internal sealed class StructuralSite {',' internal readonly float Time,Impact; internal readonly PointF Point,Ground;',' internal StructuralSite(float t,PointF p,float impact,PointF ground){Time=t;Point=p;Impact=impact;Ground=ground;}','}','internal static class StructuralV10 {',' internal static readonly Dictionary<string,StructuralSite[][]> Sites=new Dictionary<string,StructuralSite[][]>{']
def point(v):return 'new PointF(%.6ff,%.6ff)'%tuple(v)
for key,record in records.items():
    events=record['structure']['events'];build=','.join('new StructuralSite(%.6ff,%s,0,PointF.Empty)'%(v['t'],point(v['point'])) for v in events['build']);collapse=','.join('new StructuralSite(%.6ff,%s,%.6ff,%s)'%(v['t'],point(v['point']),v['impact'],point(v['ground'])) for v in events['collapse'])
    lines.append(' {"%s",new StructuralSite[][]{new StructuralSite[]{%s},new StructuralSite[]{%s}}},'%(key,build,collapse))
lines+=[' };',' internal static readonly Dictionary<string,PointF[]> FireSites=new Dictionary<string,PointF[]>{']
for key,record in records.items():lines.append(' {"%s",new PointF[]{%s}},'%(key,','.join(point(v) for v in record['damage']['points'])))
lines+=[' };','}','}']
(root/'src/StructuralV10Metadata.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8-sig')
print('Packed',len(entries),'layer images;',round(offset/1048576,2),'MiB')
