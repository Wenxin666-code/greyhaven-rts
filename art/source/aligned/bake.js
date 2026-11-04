/* Reproject the illustrated reliefs, and bake vehicles with one orthographic camera.
   The screen rise of a 45-degree ground edge equals sin(camera elevation) = 1/2. */
const T=THREE, cell=320, span=224, origin={x:160,y:220};
const renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true});
renderer.setSize(cell,cell);renderer.setClearColor(0,0);
const paper=new T.OrthographicCamera(0,cell,0,-cell,.1,10);paper.position.z=2;
const vehicles=['tank','ifv','mcv','arty'], keys=Object.keys(relief).filter(k=>k!=="depot"&&k!=="freight").concat(vehicles,["refinery","refining","miner","miner-full"]);
const angle=i=>Math.PI*i/8;
function canvas(w,h){const c=document.createElement('canvas');c.width=w;c.height=h;return c}
async function loadArt(k){
 const im=new Image();im.src=['mcv','arty'].includes(k)?'views-'+k+'.png':'../isometric/packed/hd-'+k+'.png';await im.decode();
 const tex=new T.Texture(im);tex.colorSpace=T.SRGBColorSpace;tex.needsUpdate=true;return tex;
}
function meshFromLayout(data,tex,side){
 const g=new T.BufferGeometry();
 g.setAttribute('position',new T.Float32BufferAttribute(data.xy.flatMap(([x,y])=>[x,-y,0]),3));
 g.setAttribute('uv',new T.Float32BufferAttribute(data.uv.flatMap(([x,y])=>[x/256,1-(side+y/256)/2]),2));
 g.setIndex(data.triangles.flat());
 return new T.Mesh(g,new T.MeshBasicMaterial({map:tex,transparent:true,side:T.DoubleSide,depthTest:false,depthWrite:false}));
}
function slab(ctx,[w,d],wreck){
 const a=span*w/(w+d),b=span*d/(w+d),x=origin.x,y=origin.y;
 const corners=[[x-(a-b)/2,y-(a+b)/4],[x+(a+b)/2,y+(a-b)/4],[x+(a-b)/2,y+(a+b)/4],[x-(a+b)/2,y-(a-b)/4]];
 ctx.beginPath();corners.forEach(([u,v],i)=>i?ctx.lineTo(u,v):ctx.moveTo(u,v));ctx.closePath();
 ctx.fillStyle=wreck?'#605e54':'#989687';ctx.fill();ctx.strokeStyle='#bab6a6';ctx.lineWidth=1;ctx.stroke();
}
function ruinModel(key){
 const type={depot:'barracks',freight:'factory',harbormaster:'hq'}[key]||key;
 return Greyhaven.build(type,0x4b4b45,'wreck');
}
function lights(scene){
 scene.add(new T.HemisphereLight(0xfff4da,0x939d9e,1.6));const light=new T.DirectionalLight(0xffe9c5,2.35);light.position.set(-7,13,6);scene.add(light);
}
function camera(width,aim,diagonal){
 const cam=new T.OrthographicCamera(-width/2,width/2,width/2,-width/2,.1,100);
 const radius=19,phi=diagonal?Math.PI/4:0;
 cam.position.set(radius*Math.sin(phi),aim+radius/Math.sqrt(3),radius*Math.cos(phi));cam.lookAt(0,aim,0);cam.updateMatrixWorld();return cam;
}
function release(m){m.traverse(o=>{if(o.isMesh){o.geometry.dispose();o.material.dispose()}})}
async function building(key,tex){
 const data=relief[key],sheet=canvas(cell,cell*3),ctx=sheet.getContext('2d');
 for(let side=0;side<2;side++){
  ctx.save();ctx.translate(0,side*cell);slab(ctx,data.tiles,false);
  const scene=new T.Scene(),m=meshFromLayout(data,tex,side);scene.add(m);renderer.render(scene,paper);ctx.drawImage(renderer.domElement,0,0);release(m);ctx.restore();
 }
 // Rebuild the ruins on exactly the same lot and at the same elevation as their intact state.
 const wreck=ruinModel(key),bounds=new T.Box3().setFromObject(wreck),size=bounds.getSize(new T.Vector3());
 const [w,d]=data.tiles;wreck.scale.set(w/size.x*.92,Math.min(w/size.x,d/size.z)*.87,d/size.z*.92);
 const camWidth=cell*(w+d)/Math.SQRT2/span,aim=(origin.y-cell/2)*camWidth/(cell*Math.cos(Math.PI/6));
 const sc=new T.Scene();lights(sc);sc.add(wreck);renderer.render(sc,camera(camWidth,aim,true));
 ctx.translate(0,2*cell);slab(ctx,data.tiles,true);ctx.drawImage(renderer.domElement,0,0);release(wreck);return sheet;
}
// The source paintings are textured relief meshes. Their calibrated axles live in
// local vehicle space; the identical orthographic matrix projects every heading.
function chassisMesh(key,tex,i,side){
 const views=chassis[key],n=views.length,frame=n===4?Math.floor(i/4):Math.round(i/2)%8,data=views[frame];
 const yaw=angle(i),c=Math.cos(yaw),s=Math.sin(yaw);
 const length={tank:175,ifv:166,mcv:172,arty:170}[key],width=length*(key==='tank'?.59:.51);
 const liftScale=key==='arty'?.93:key==='tank'?.94:.92;
 const pos=data.xyz.flatMap(([u,v,h])=>{
  const x=u*width,z=v*length;
  return [160+.76*(c*x+s*z), -(188+.76*(.5*(c*z-s*x)-h*liftScale)),0];
 });
 const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(pos,3));
 geo.setAttribute('uv',new T.Float32BufferAttribute(data.uv.flatMap(([x,y])=>[(frame+x/256)/n,1-(side+y/256)/2]),2));
 geo.setIndex(data.tri.flat());return new T.Mesh(geo,new T.MeshBasicMaterial({map:tex,transparent:true,side:T.DoubleSide,depthTest:false,depthWrite:false}));
}
async function vehicle(key,tex){
 const sheet=canvas(8*cell,6*cell),ctx=sheet.getContext('2d');
 for(let side=0;side<3;side++)for(let i=0;i<16;i++){
  const sc=new T.Scene();let model,cam;
  if(side<2){model=chassisMesh(key,tex,i,side);cam=paper;}
  else{model=['mcv','arty'].includes(key)?supportRig(key,0x4b4b45,true):Greyhaven.build(key,0x4b4b45,'wreck');
   model.rotation.y=angle(i);const span=(key==='arty'?8.8:key==='tank'?8.5:7.2)/.76;cam=camera(span,28*span/(cell*Math.cos(Math.PI/6)),false);lights(sc);}
  sc.add(model);renderer.render(sc,cam);ctx.drawImage(renderer.domElement,(i%8)*cell,(side*2+(i>>3))*cell);release(model);
 }
 return sheet;
}
async function bake(key){
 if(['miner','miner-full','refinery','refining'].includes(key))return oreAtlas(key);
 if(key==='tank')return driveAtlas();
 const tex=await loadArt(key);
 try{return (await (relief[key]?building(key,tex):vehicle(key,tex))).toDataURL('image/png')}finally{tex.dispose()}
}
for(const k of keys)asset.add(new Option(k,k));
download.onclick=async()=>{const a=document.createElement('a');a.download='iso-'+asset.value+'.png';a.href=await bake(asset.value);a.click()};
