window.BuildingBake=(()=>{
 const T=THREE,cell=320,span=224,anchor=220;
 const renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true});renderer.setClearColor(0,0);renderer.setSize(1024,1024);
 renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.3;renderer.outputColorSpace=T.SRGBColorSpace;
 renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;
 const canvas=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c;};
 const studio=canvas(512,256),sg=studio.getContext('2d'),gradient=sg.createLinearGradient(0,0,0,256);
 gradient.addColorStop(0,'#c2d2df');gradient.addColorStop(.45,'#89969e');gradient.addColorStop(.5,'#353c40');gradient.addColorStop(1,'#171c1b');sg.fillStyle=gradient;sg.fillRect(0,0,512,256);sg.fillStyle='#e6e2d9';sg.fillRect(35,40,80,70);sg.fillStyle='#b3bfc8';sg.fillRect(315,50,50,50);
 const tex=new T.CanvasTexture(studio);tex.mapping=T.EquirectangularReflectionMapping;tex.colorSpace=T.SRGBColorSpace;
 const pmrem=new T.PMREMGenerator(renderer),environment=pmrem.fromEquirectangular(tex).texture;
 function setup(model,spec){
  const width=cell*(spec.w+spec.d)/Math.SQRT2/span,aim=(anchor-cell/2)*width/(cell*Math.cos(Math.PI/6));
  const camera=new T.OrthographicCamera(-width/2,width/2,width/2,-width/2,.1,100);
  camera.position.set(19/Math.SQRT2,aim+19/Math.sqrt(3),19/Math.SQRT2);camera.lookAt(0,aim,0);camera.updateMatrixWorld();
  const scene=new T.Scene();scene.environment=environment;scene.add(model);scene.add(new T.HemisphereLight(0xf3f2ec,0x505c61,.65));
  const sun=new T.DirectionalLight(0xfff1db,3.2);sun.position.set(-7,12,8);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);Object.assign(sun.shadow.camera,{left:-3,right:3,top:3,bottom:-3,near:.1,far:35});sun.shadow.bias=-.0003;sun.shadow.normalBias=.008;scene.add(sun);
  const fill=new T.DirectionalLight(0xc3d4e4,.7);fill.position.set(7,5,-7);scene.add(fill);return {scene,camera};
 }
 function release(model,scene){const geos=new Set(),mats=new Set();model.traverse(o=>{if(o.geometry)geos.add(o.geometry);if(o.material)mats.add(o.material);if(o.customDepthMaterial)mats.add(o.customDepthMaterial);if(o.userData.original)mats.add(o.userData.original);});geos.forEach(g=>g.dispose());mats.forEach(m=>m.dispose());scene.traverse(o=>{if(o.shadow&&o.shadow.map)o.shadow.map.dispose();});}
 function bounds(ctx,x,y){const px=ctx.getImageData(x,y,cell,cell).data;let ink=0,l=cell,t=cell,r=0,b=0;for(let j=0;j<cell;j++)for(let i=0;i<cell;i++)if(px[(j*cell+i)*4+3]>80){ink++;l=Math.min(l,i);r=Math.max(r,i);t=Math.min(t,j);b=Math.max(b,j);}return {ink,bounds:[l,t,r,b]};}
 async function bake(key,mode){
  await BuildingMaster.ready;const animated=key==='refining',kind=animated?'refinery':key,spec=BuildingMaster.specs[kind];
  const cols=animated?8:1,rows=animated?4:3,sheet=canvas(cell*cols,cell*rows),ctx=sheet.getContext('2d');ctx.imageSmoothingQuality='high';
  const audit=[];let gate=null;
  // Legacy row layout stays valid, but both faction rows contain the same neutral master.
  for(let row=0;row<(animated?2:3);row++)for(let i=0;i<(animated?16:1);i++){
   if(mode==='paint'&&row===2&&!animated)continue;
   const model=BuildingMaster.build(kind,!animated&&row===2,i/16),{scene,camera}=setup(model,spec);
   if(model.userData.gate){const q=model.userData.gate;gate=[[q.left,q.top,q.z],[q.right,q.top,q.z],[q.right,q.bottom,q.z],[q.left,q.bottom,q.z]].map(p=>{const v=new T.Vector3(...p).project(camera);return [(v.x+1)*cell/2,(1-v.y)*cell/2];});}
   if(mode==='paint')model.traverse(o=>{if(o.isMesh&&!o.material.userData.factionPaint){o.userData.original=o.material;o.material=new T.MeshBasicMaterial({color:0,opacity:0,transparent:false,blending:T.NoBlending,depthWrite:true});o.customDepthMaterial=new T.MeshDepthMaterial({depthPacking:T.RGBADepthPacking});}});
   renderer.render(scene,camera);const x=i%cols*cell,y=(row*(animated?2:1)+Math.floor(i/cols))*cell;
   ctx.drawImage(renderer.domElement,x,y,cell,cell);const a=bounds(ctx,x,y);a.row=row;a.frame=i;
   if(a.ink<50||a.bounds[0]<2||a.bounds[1]<2||a.bounds[2]>cell-3||a.bounds[3]>cell-3)throw Error(key+'/'+mode+' clipped or empty: '+JSON.stringify(a));
   audit.push(a);release(model,scene);
  }
  return {png:sheet.toDataURL(),audit,gate};
 }
 async function animation(key,kind,mode){
  await BuildingMaster.ready;const count=kind==='build'?24:16;
  // Reuse the canonical endpoint so GPU shadow rounding cannot flash on handoff.
  const endpoint=new Image();endpoint.src=`packed/iso-${key}${mode==='paint'?'-paint':''}.png`;await endpoint.decode();
  const sheet=canvas(cell*8,cell*count/8),ctx=sheet.getContext('2d'),audit=[];ctx.imageSmoothingQuality='high';
  for(let i=0;i<count;i++){
   const model=BuildingMotion.pose(key,kind,i/(count-1)),{scene,camera}=setup(model,BuildingMaster.specs[key]);
   if(mode==='paint')model.traverse(o=>{if(o.isMesh&&!o.material.userData.factionPaint){o.userData.original=o.material;o.material=new T.MeshBasicMaterial({color:0,opacity:0,transparent:false,blending:T.NoBlending,depthWrite:true});o.customDepthMaterial=new T.MeshDepthMaterial({depthPacking:T.RGBADepthPacking});}});
   renderer.render(scene,camera);let x=i%8*cell,y=Math.floor(i/8)*cell;ctx.drawImage(renderer.domElement,x,y,cell,cell);
   if(i===count-1){ctx.clearRect(x,y,cell,cell);ctx.drawImage(endpoint,0,kind==='build'?0:cell*2,cell,cell,x,y,cell,cell);}
   const a=bounds(ctx,x,y);a.frame=i;
   if(mode==='base'&&a.ink<50||a.ink>0&&(a.bounds[0]<2||a.bounds[1]<2||a.bounds[2]>cell-3||a.bounds[3]>cell-3))throw Error(key+'/'+kind+'/'+mode+' invalid bounds '+JSON.stringify(a));
   audit.push(a);release(model,scene);
  }
  return {png:sheet.toDataURL(),audit};
 }
 return {bake,animation,model:key=>BuildingMaster.build(key).toJSON()};
})();
