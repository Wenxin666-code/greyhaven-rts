/* Offline atlas baker, fixed 30-degree projection matching FieldArt.cs.
   Reduced source resolution limits detail frequency without shrinking hitboxes. */
window.UnitBaker=(()=>{
 const T=THREE,renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true});
 renderer.setClearColor(0,0);renderer.outputColorSpace=T.SRGBColorSpace;
 const specs={squad:{foot:true},at:{foot:true},engineer:{foot:true},ifv:{width:7.45},tank:{width:8.65,tank:true},arty:{width:8.85},mcv:{width:8.2},miner:{width:7.45},'miner-full':{width:7.45}};
 const canvas=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c;};
 function config(key){const s=specs[key];return {...s,cell:s.foot?256:s.tank?288:320,views:s.foot?8:s.tank?64:16,teams:s.foot?2:3,width:s.foot?3.15:s.width,anchor:s.foot?.91:188/320,pixels:s.foot?96:144};}
 function camera(s){
  const a=(s.anchor-.5)*s.width/Math.cos(Math.PI/6),c=new T.OrthographicCamera(-s.width/2,s.width/2,s.width/2,-s.width/2,.1,100);
  c.position.set(0,a+20/Math.sqrt(3),20);c.lookAt(0,a,0);c.updateMatrixWorld();return c;
 }
 function scene(model){const s=new T.Scene();s.add(model);s.add(new T.HemisphereLight(0xfff5df,0x67736e,2.1));
  const sun=new T.DirectionalLight(0xfff2d9,2.25);sun.position.set(-8,14,7);s.add(sun);return s;
 }
 function dispose(model){const geometries=new Set(),materials=new Set();model.traverse(o=>{if(o.geometry)geometries.add(o.geometry);if(o.material)materials.add(o.material);});geometries.forEach(x=>x.dispose());materials.forEach(x=>x.dispose());}
 function draw(model,s,cam,yaw){model.rotation.y=yaw;model.updateMatrixWorld(true);renderer.render(s,cam);}
 async function bake(key){
  const s=config(key),rows=s.views/8,sheet=canvas(8*s.cell,rows*s.teams*s.cell),g=sheet.getContext('2d');g.imageSmoothingEnabled=false;
  const cam=camera(s);renderer.setSize(s.pixels,s.pixels);const metrics=[],bearing=[],bluePixels=[];
  for(let team=0;team<s.teams;team++){
   const model=ReadableUnits.build(key,team),world=scene(model);
   for(let i=0;i<s.views;i++){
    const yaw=i*2*Math.PI/s.views;draw(model,world,cam,yaw);
    const x=i%8*s.cell,y=(team*rows+Math.floor(i/8))*s.cell;g.drawImage(renderer.domElement,x,y,s.cell,s.cell);
    // Alpha margins catch clipped barrels/helmets in every heading; faction
    // coverage is measured from actual shaded pixels, not a mesh-area estimate.
    const c=canvas(s.pixels,s.pixels),cg=c.getContext('2d');cg.drawImage(renderer.domElement,0,0);const px=cg.getImageData(0,0,s.pixels,s.pixels).data;
    let ink=0,tint=0,l=s.pixels,t=s.pixels,r=0,b=0;
    for(let py=0;py<s.pixels;py++)for(let pxn=0;pxn<s.pixels;pxn++){
     const j=(py*s.pixels+pxn)*4;if(px[j+3]<96)continue;ink++;l=Math.min(l,pxn);r=Math.max(r,pxn);t=Math.min(t,py);b=Math.max(b,py);
     if(team===1){const a=bluePixels[i];if(Math.abs(px[j]-a[j])+Math.abs(px[j+1]-a[j+1])+Math.abs(px[j+2]-a[j+2])>60)tint++;}
    }
    if(!ink||l<2||t<2||r>s.pixels-3||b>s.pixels-3)throw Error(`${key}/${team}/${i} clipped or empty: ${l},${t},${r},${b}`);
    if(team===0)bluePixels[i]=px;
    const coverage=team===1?+(tint/ink).toFixed(3):null;
    if(team===1)metrics[i].teamPanelFraction=coverage;
    metrics.push({team,frame:i,teamPanelFraction:coverage,bounds:[l,t,r,b]});
    if(key==='tank'&&team===0){
     const [a,b]=model.userData.gunAxis.map(v=>new T.Vector3(...v).applyMatrix4(model.matrixWorld).project(cam));
     const dx=(b.x-a.x)*s.pixels/2,dy=-(b.y-a.y)*s.pixels/2;bearing.push([i,dx,dy]);
    }
   }dispose(model);
  }
  return {png:sheet.toDataURL(),metrics,bearing};
 }
 async function roster(){
  const keys=Object.keys(specs).filter(k=>k!=='miner-full'),names=['步枪兵','反装甲兵','工兵','步兵战车','主战坦克','自行火炮','工程车','采矿车'];
  const out=canvas(1440,940),g=out.getContext('2d');g.fillStyle='#182527';g.fillRect(0,0,out.width,out.height);
  const text=(t,x,y,size=16,color='#d4d6c1')=>{g.font=`${size}px "Microsoft YaHei"`;g.fillStyle=color;g.fillText(t,x,y);};
  text('断线前沿  /  单位重绘',32,46,28,'#dfbf77');text('实际方向图集 · 双方同模换色 · 下方为默认视角尺寸',32,78,15,'#99aaa1');
  for(let j=0;j<keys.length;j++){
   const key=keys[j],s=config(key),x=24+j%4*354,y=105+Math.floor(j/4)*394;
   g.fillStyle='#263532';g.fillRect(x,y,336,375);text(names[j],x+18,y+32,20,'#e7cf95');
   const data=await bake(key),img=new Image();img.src=data.png;await img.decode();
   for(let team=0;team<2;team++){
    const view=s.foot?7:s.tank?56:14,srcX=view%8*s.cell,srcY=(team*s.views/8+Math.floor(view/8))*s.cell;
    const w=s.foot?142:178,dx=x+84+team*161,dy=y+226;
    g.imageSmoothingEnabled=false;g.drawImage(img,srcX,srcY,s.cell,s.cell,dx-w/2,dy-s.anchor*w,w,w);
    text(team?'敌方':'己方',dx-17,y+259,13,team?'#e78172':'#72c9df');
    // Same destination size as a 1920x1080 client at zoom 1.05.
    const size=({squad:48,at:50,engineer:48,ifv:50,tank:57,arty:53,mcv:55,miner:44})[key];
    const actual=size*1.05*(1920/1472)*(s.foot?.54:1/.76);
    g.imageSmoothingEnabled=true;g.drawImage(img,srcX,srcY,s.cell,s.cell,dx-actual/2,y+338-s.anchor*actual,actual,actual);
   }
  }
  text('轮廓区分：步枪 / 肩扛火箭 / 黄盔工具 · 八轮 / 宽履带 / 长炮管 / 起重臂 / 敞口矿斗',32,923,16,'#a6b5aa');
  return out.toDataURL();
 }
 return {bake,roster,specs};
})();
