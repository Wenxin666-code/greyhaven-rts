/* One camera, one geometry master, neutral contribution + occluded paint contribution.
   PNGs remain independent of factions. Runtime recolor preserves the base alpha. */
window.BuildingBakeV08=(()=>{
 const T=THREE,M=BuildingMasterV08,cell=320,span=224,anchor=220;
 const renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true});renderer.setSize(1024,1024);renderer.setClearColor(0,0);
 renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.14;renderer.outputColorSpace=T.SRGBColorSpace;renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;
 const canvas=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c;};
 const studio=canvas(512,256),sg=studio.getContext('2d'),gradient=sg.createLinearGradient(0,0,0,256);
 gradient.addColorStop(0,'#c2cbd3');gradient.addColorStop(.45,'#939d9e');gradient.addColorStop(.50,'#434c4e');gradient.addColorStop(1,'#202825');sg.fillStyle=gradient;sg.fillRect(0,0,512,256);sg.fillStyle='#e2ded1';sg.fillRect(35,32,80,90);
 const tex=new T.CanvasTexture(studio);tex.mapping=T.EquirectangularReflectionMapping;tex.colorSpace=T.SRGBColorSpace;const pmrem=new T.PMREMGenerator(renderer),env=pmrem.fromEquirectangular(tex).texture;
 const occluders=new Map(),paintMaterials=new Map();let current;
 function occluder(original,attenuateLine=false){
  const key=original.uuid+'/'+attenuateLine;
  if(!occluders.has(key)){const m=original.isLineBasicMaterial?new T.LineBasicMaterial({color:0,opacity:attenuateLine?original.opacity:0,transparent:attenuateLine,blending:attenuateLine?T.NormalBlending:T.NoBlending,depthWrite:false}):new T.MeshBasicMaterial({color:0,opacity:0,transparent:false,blending:T.NoBlending,depthWrite:true,side:original.side});occluders.set(key,m);}return occluders.get(key);
 }
 async function open(key){
  await M.ready;if(current)close();const root=M.build(key),spec=M.specs[key];
  const width=cell*(spec.w+spec.d)/Math.SQRT2/span,aim=(anchor-cell/2)*width/(cell*Math.cos(Math.PI/6));
  const camera=new T.OrthographicCamera(-width/2,width/2,width/2,-width/2,.1,100);
  const scene=new T.Scene();scene.environment=env;scene.add(root);scene.add(new T.HemisphereLight(0xf1eee4,0x4d5b61,.74));
  const sun=new T.DirectionalLight(0xfff2dc,3.0);sun.position.set(-7,12,8);sun.castShadow=true;sun.shadow.mapSize.set(1024,1024);Object.assign(sun.shadow.camera,{left:-4,right:4,top:4,bottom:-4,near:.1,far:35});sun.shadow.bias=-.00025;sun.shadow.normalBias=.006;scene.add(sun);
  const fill=new T.DirectionalLight(0xbdd0e0,.63);fill.position.set(7,5,-7);scene.add(fill);
  const nodes=[];root.traverse(o=>{if(o.isMesh||o.isLineSegments){let parent=o,dynamic=false,door=false;while(parent&&parent!==root){dynamic|=!!parent.userData.dynamic;door|=parent.userData.motion==='door';parent=parent.parent;}nodes.push({mesh:o,material:o.material,dynamic:!!dynamic,door:!!door});}});
  const ground=new T.Mesh(new T.PlaneGeometry(12,12),new T.ShadowMaterial({opacity:.22}));ground.rotation.x=-Math.PI/2;ground.position.y=-.009;ground.receiveShadow=true;ground.visible=false;scene.add(ground);
  current={root,spec,width,aim,camera,scene,sun,ground,nodes,key};setAngle(0);M.pose(root,'idle',0);return stats();
 }
 function setAngle(degrees){const c=current,a=(45+degrees)*Math.PI/180;c.camera.position.set(19*Math.sin(a),c.aim+19/Math.sqrt(3),19*Math.cos(a));c.camera.lookAt(0,c.aim,0);c.camera.updateMatrixWorld();}
 function restore(){for(const n of current.nodes){n.mesh.material=n.material;if(n.hidden!==undefined){n.mesh.visible=n.hidden;delete n.hidden;}}current.ground.material.opacity=.22;}
 function prepare(mode,selection){
  restore();
  for(const n of current.nodes){
   const selected=selection==='all'||selection==='background'&&!n.dynamic||selection==='motion'&&n.dynamic&&!n.door||selection==='door'&&n.door;
   if(selection==='background'&&n.dynamic){n.hidden=n.mesh.visible;n.mesh.visible=false;continue;}
   if(!selected||mode==='paint'&&!n.material.userData.factionPaint)n.mesh.material=occluder(n.material,selected&&mode==='paint'&&n.material.isLineBasicMaterial);
   else if(mode==='paint'&&n.material.userData.factionPaint){if(!paintMaterials.has(n.material))paintMaterials.set(n.material,ArchitectureKit.maskMaterial(n.material));const mat=paintMaterials.get(n.material);mat.color.copy(n.material.color);n.mesh.material=mat;}
  }
  if(mode==='paint'||selection==='motion'||selection==='door')current.ground.material.opacity=0;
 }
 function image(mode='base',size=cell,selection='all',updateShadow=true){
  prepare(mode,selection);renderer.shadowMap.autoUpdate=updateShadow;renderer.render(current.scene,current.camera);
  const c=canvas(size,size),ctx=c.getContext('2d');ctx.imageSmoothingQuality='high';
  let weight=1;for(const n of current.nodes)if(n.material.userData.factionPaint){weight=n.material.userData.paintWeight===undefined?1:n.material.userData.paintWeight;break;}
  ctx.globalAlpha=mode==='paint'?weight:1;ctx.drawImage(renderer.domElement,0,0,size,size);restore();return c;
 }
 function inspect(c){const ctx=c.getContext('2d'),data=ctx.getImageData(0,0,c.width,c.height).data;let l=c.width,t=c.height,r=-1,b=-1,ink=0;for(let y=0;y<c.height;y++)for(let x=0;x<c.width;x++)if(data[(y*c.width+x)*4+3]>40){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);ink++;}return {ink,bounds:r<0?[0,0,0,0]:[l,t,r+1,b+1]};}
 function combine(base,paint,color){
  const c=canvas(base.width,base.height),ctx=c.getContext('2d'),a=base.getContext('2d').getImageData(0,0,c.width,c.height),p=paint.getContext('2d').getImageData(0,0,c.width,c.height).data;
  for(let i=0;i<a.data.length;i+=4){const alpha=a.data[i+3]/255;if(!alpha)continue;const mask=p[i+3]/255;for(let ch=0;ch<3;ch++)a.data[i+ch]=Math.max(0,Math.min(255,(a.data[i+ch]*alpha-p[i+ch]*mask*(1-color[ch]/255))/alpha));}
  ctx.putImageData(a,0,0);return c;
 }
 function check(base,paint,label){
  const a=inspect(base),p=inspect(paint);if(a.ink<40)throw Error(label+' empty');if(a.bounds[0]<2||a.bounds[1]<2||a.bounds[2]>cell-2||a.bounds[3]>cell-2)throw Error(label+' clipped '+JSON.stringify(a));
  return {...a,paintInk:p.ink,paintFraction:p.ink/a.ink};
 }
 function stats(){const c=current,geos=new Set(),mats=new Set();let meshes=0,triangles=0;const ground=[Infinity,Infinity,-Infinity,-Infinity];for(const n of c.nodes){if(!n.mesh.isMesh)continue;meshes++;geos.add(n.mesh.geometry);mats.add(n.material);triangles+=n.mesh.geometry.index?n.mesh.geometry.index.count/3:n.mesh.geometry.attributes.position.count/3;const p=n.mesh.geometry.attributes.position;for(let i=0;i<p.count;i++){const v=new T.Vector3().fromBufferAttribute(p,i).applyMatrix4(n.mesh.matrixWorld);if(v.y<=.061&&v.y>=-.02){ground[0]=Math.min(ground[0],v.x);ground[1]=Math.min(ground[1],v.z);ground[2]=Math.max(ground[2],v.x);ground[3]=Math.max(ground[3],v.z);}}}
  if(ground[0]<-c.spec.w/2-.012||ground[1]<-c.spec.d/2-.012||ground[2]>c.spec.w/2+.012||ground[3]>c.spec.d/2+.012)throw Error('ground fittings exceed foundation '+c.key+' '+JSON.stringify(ground));
  return {key:c.key,meshes,uniqueGeometry:geos.size,materials:mats.size,triangles,parts:M.bindings.get(c.root).parts.length,movingParts:M.bindings.get(c.root).dynamic.length,foundation:[c.spec.w,c.spec.d],groundBounds:ground};}
 function endpoint(wreck=false){M.pose(current.root,wreck?'collapse':'idle',wreck?1:0);current.ground.visible=false;const b=image('base'),p=image('paint',cell,'all',false);return {base:b.toDataURL(),paint:p.toDataURL(),audit:check(b,p,current.key+(wreck?' wreck':' complete'))};}
 function hero(angle=0,color=[62,133,190]){M.pose(current.root,'idle',0);setAngle(angle);current.ground.visible=true;const b=image('base',1024),p=image('paint',1024,'all',false),colored=combine(b,p,color);current.ground.visible=false;
  const out=canvas(1024,1024),ctx=out.getContext('2d');ctx.fillStyle='#202c32';ctx.fillRect(0,0,1024,1024);ctx.drawImage(colored,0,0);setAngle(0);return out.toDataURL();}
 function background(){M.pose(current.root,'idle',0);current.ground.visible=false;const b=image('base',cell,'background'),p=image('paint',cell,'background',false);return {base:b.toDataURL(),paint:p.toDataURL()};}
 function frame(kind,index,count=48){
  const t=kind==='idle'?index/count:index/(count-1);M.pose(current.root,kind==='door'?'idle':kind,kind==='door'?0:t);if(kind==='door')M.doorPose(current.root,t);
  const select=kind==='idle'?'motion':kind==='door'?'door':'all';
  const b=image('base',cell,select,kind!=='idle'||index===0),p=image('paint',cell,select,false),a=inspect(b),m=inspect(p);
  if(kind!=='idle'&&kind!=='door')check(b,p,current.key+'/'+kind+'/'+index);
  if(a.ink&& (a.bounds[0]<2||a.bounds[1]<2||a.bounds[2]>cell-2||a.bounds[3]>cell-2))throw Error('moving layer clipped '+current.key+'/'+kind+'/'+index);
  return {base:b.toDataURL(),paint:p.toDataURL(),audit:{...a,paintInk:m.ink,frame:index}};
 }
 function model(){M.pose(current.root,'idle',0);restore();return current.root.toJSON();}
 function points(){const c=current,project=p=>{const v=new T.Vector3(...p).project(c.camera);return [(v.x+1)*cell/2,(1-v.y)*cell/2];};const sockets={};for(const [name,points]of Object.entries(c.root.userData.sockets||{}))sockets[name]=points.map(project);return {sockets,gate:c.root.userData.gate,muzzle:c.root.userData.muzzle?project(c.root.userData.muzzle):null};}
 function close(){if(!current)return;restore();const geos=new Set(),mats=new Set();current.root.traverse(o=>{if(o.geometry)geos.add(o.geometry);if(o.material)mats.add(o.material);});geos.forEach(g=>g.dispose());for(const m of mats)m.dispose();for(const m of occluders.values())m.dispose();for(const m of paintMaterials.values())m.dispose();occluders.clear();paintMaterials.clear();current.sun.shadow.map?.dispose();current.ground.geometry.dispose();current.ground.material.dispose();current=null;renderer.renderLists.dispose();}
 return {open,close,stats,endpoint,hero,background,frame,model,points};
})();
