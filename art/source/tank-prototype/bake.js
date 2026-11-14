window.TankBake=(()=>{
 const T=THREE;
 const renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true});renderer.setClearColor(0,0);renderer.setSize(1024,1024);
 renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.3;
 renderer.outputColorSpace=T.SRGBColorSpace;renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;
 let camera;
 const canvas=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c;};
 // Broad studio reflections give metal a readable finish without photographic
 // lighting baked into the source texture. The same lights shade the paint pass.
 const studio=canvas(512,256),sg=studio.getContext('2d'),gradient=sg.createLinearGradient(0,0,0,256);
 gradient.addColorStop(0,'#c2d2df');gradient.addColorStop(.45,'#89969e');gradient.addColorStop(.5,'#353c40');gradient.addColorStop(1,'#171c1b');sg.fillStyle=gradient;sg.fillRect(0,0,512,256);sg.fillStyle='#e6e2d9';sg.fillRect(35,40,80,70);sg.fillStyle='#b3bfc8';sg.fillRect(315,50,50,50);
 const tex=new T.CanvasTexture(studio);tex.mapping=T.EquirectangularReflectionMapping;tex.colorSpace=T.SRGBColorSpace;const pmrem=new T.PMREMGenerator(renderer),environment=pmrem.fromEquirectangular(tex).texture;
 function scene(model){const s=new T.Scene();s.environment=environment;s.add(model);s.add(new T.HemisphereLight(0xf3f2ec,0x505c61,.65));
  const sun=new T.DirectionalLight(0xfff1db,3.2);sun.position.set(-7,12,8);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);Object.assign(sun.shadow.camera,{left:-5,right:5,top:5,bottom:-5,near:.1,far:35});sun.shadow.bias=-.0003;sun.shadow.normalBias=.015;s.add(sun);
  const fill=new T.DirectionalLight(0xc3d4e4,.7);fill.position.set(7,5,-7);s.add(fill);return s;
 }
 function project(v,model){return new T.Vector3(...v).applyMatrix4(model.matrixWorld).project(camera);}
 async function bake(mode,key='tank'){
  await TankMaster.ready;
  const hullLayer=mode.startsWith('hull-'),turretLayer=mode.startsWith('turret-');
  const spec=key==='tank'?{width:9.25,cell:hullLayer||turretLayer?256:512}:UnitMaster.specs[key],cell=spec.cell||384,views=key==='tank'?64:spec.foot?8:16,anchor=spec.foot?.91:188/320,width=spec.width;
  renderer.setSize(cell*2,cell*2);
  const aim=(anchor-.5)*width/Math.cos(Math.PI/6);camera=new T.OrthographicCamera(-width/2,width/2,width/2,-width/2,.1,100);
  camera.position.set(0,aim+20/Math.sqrt(3),20);camera.lookAt(0,aim,0);camera.updateMatrixWorld();
  const model=key==='tank'?TankMaster.build(mode==='wreck'):UnitMaster.build(key,mode==='wreck'),s=scene(model),sheet=canvas(cell*8,cell*views/8),ctx=sheet.getContext('2d');ctx.imageSmoothingQuality='high';
  const turret=key==='tank'?model.getObjectByName('turret'):null;
  if(hullLayer)turret.visible=false;
  if(turretLayer){
   for(const part of model.children)part.visible=part===turret;
   turret.position.x=0;turret.position.z=0;
  }
  // NoBlending prevents Three's OPAQUE shader define from forcing opacity=1.
  // Neutral geometry writes transparent pixels AND depth so hidden paint never leaks.
  if(mode==='paint'||mode.endsWith('-paint'))model.traverse(o=>{if(o.isMesh&&!o.material.userData.factionPaint){const old=o.material;o.material=new T.MeshBasicMaterial({color:0x000000,opacity:0,transparent:false,blending:T.NoBlending,depthWrite:true});o.customDepthMaterial=new T.MeshDepthMaterial({depthPacking:T.RGBADepthPacking});o.userData.original=old;}});
  const bearings=[],margins=[],sockets=[];
  for(let i=0;i<views;i++){
   model.rotation.y=i*Math.PI*2/views;model.updateMatrixWorld(true);renderer.render(s,camera);ctx.drawImage(renderer.domElement,i%8*cell,Math.floor(i/8)*cell,cell,cell);
   if(mode==='base'&&key==='tank'){
    const a=project(model.userData.gunAxis[0],model),b=project(model.userData.gunAxis[1],model);bearings.push([i,(b.x-a.x)*cell/2,-(b.y-a.y)*cell/2]);
   }
   if(mode==='hull-base'){
    const a=project([0,0,0],model),b=project([turret.position.x,0,turret.position.z],model);
    sockets.push([(b.x-a.x)/2,(a.y-b.y)/2]);
   }
   if(mode==='turret-base'){
    const tip=project(turret.userData.muzzle,turret);
    sockets.push([.5+tip.x/2,.5-tip.y/2]);
   }
   const px=ctx.getImageData(i%8*cell,Math.floor(i/8)*cell,cell,cell).data;let n=0,l=cell,t=cell,r=0,b=0;
   for(let y=0;y<cell;y++)for(let x=0;x<cell;x++)if(px[(y*cell+x)*4+3]>80){n++;l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);}
   if(n<80||l<2||t<2||r>cell-3||b>cell-3)throw Error(mode+' clipping/empty view '+i);
   margins.push({frame:i,ink:n,bounds:[l,t,r,b]});
  }
  const geos=new Set(),mats=new Set();model.traverse(o=>{if(o.geometry)geos.add(o.geometry);if(o.material)mats.add(o.material);if(o.customDepthMaterial)mats.add(o.customDepthMaterial);if(o.userData.original)mats.add(o.userData.original);});geos.forEach(x=>x.dispose());mats.forEach(x=>x.dispose());s.traverse(o=>{if(o.shadow&&o.shadow.map)o.shadow.map.dispose();});
  return {png:sheet.toDataURL(),bearings,margins,sockets};
 }
 return {bake,model:(key='tank')=>(key==='tank'?TankMaster.build():UnitMaster.build(key)).toJSON()};
})();
