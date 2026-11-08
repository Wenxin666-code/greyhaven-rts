/* Editable architecture kit. All dimensions are ground cells, not screen pixels.
   Geometry is shared within a master; semantic groups retain rigid assembly pivots. */
window.ArchitectureKit=(()=>{
 const T=THREE,bindings=new WeakMap();
 let texture,concrete;const load=url=>new Promise((ok,no)=>new T.TextureLoader().load(url,t=>{t.wrapS=t.wrapT=T.RepeatWrapping;t.colorSpace=T.SRGBColorSpace;ok(t);},undefined,no));
 const ready=Promise.all([load('materials/coated-armor.png'),load('materials/cast-concrete.png')]).then(([a,b])=>{texture=a;concrete=b;});
 const glassCanvas=document.createElement('canvas');glassCanvas.width=glassCanvas.height=256;const gg=glassCanvas.getContext('2d'),glassGradient=gg.createLinearGradient(0,0,205,256);glassGradient.addColorStop(0,'#557784');glassGradient.addColorStop(.35,'#294851');glassGradient.addColorStop(1,'#15272e');gg.fillStyle=glassGradient;gg.fillRect(0,0,256,256);gg.fillStyle='rgba(194,214,210,.10)';gg.beginPath();gg.moveTo(34,0);gg.lineTo(69,0);gg.lineTo(256,225);gg.lineTo(256,255);gg.closePath();gg.fill();for(let y=0;y<256;y+=29){gg.fillStyle='rgba(3,14,18,.21)';gg.fillRect(0,y,256,2);}const glassMap=new T.CanvasTexture(glassCanvas);glassMap.wrapS=glassMap.wrapT=T.RepeatWrapping;glassMap.colorSpace=T.SRGBColorSpace;
 const palette={concrete:[0x959689,.91,.02],wall:[0xb2b1a1,.77,.06],edge:[0xc4c5b8,.55,.22],steel:[0x646e6d,.48,.45],dark:[0x242d30,.8,.08],glass:[0x1f353a,.25,.28],paint:[0xe4e4e4,.53,.12],yellow:[0xc6a354,.65,.1],lamp:[0xf7ce87,.4,.05],rubber:[0x242a27,.9,.01],brick:[0x827262,.9,.01],ore:[0xbba25d,.65,.16],window:[0x4b5147,.65,.02]};
 const stages={foundation:[0,.12],shell:[.08,.48],cladding:[.32,.67],roof:[.47,.80],systems:[.62,.94],detail:[.72,1]};
 function create(key,spec){
  const root=new T.Group();root.name=key+' v08 master';root.userData={key,foundation:[spec.w,spec.d],version:8};
  const geometry=new Map(),materials={},parts=[],dynamic=[];let parent=root,origin=new T.Vector3(),counter=0;
  for(const [name,[color,roughness,metalness]]of Object.entries(palette)){
   const m=new T.MeshStandardMaterial({color,roughness,metalness});m.name=name;m.userData.factionPaint=name==='paint';m.userData.originalColor=color;
   if(['edge','steel','paint'].includes(name)){m.map=texture;m.bumpMap=texture;m.bumpScale=.014;m.envMapIntensity=.7;}
   if(['wall','concrete'].includes(name)){m.map=concrete;m.bumpMap=concrete;m.bumpScale=.010;m.envMapIntensity=.45;}
   if(name==='glass'){m.map=glassMap;m.color.setHex(0x81989b);m.userData.originalColor=0x81989b;m.roughness=.20;m.envMapIntensity=1.35;}
   if(name==='lamp'){m.emissive.setHex(0xeeb45b);m.emissiveIntensity=.35;}
   if(name==='window'){m.emissive.setHex(0xa9804b);m.emissiveIntensity=.17;}
   materials[name]=m;
  }
  function geo(id,make){if(!geometry.has(id))geometry.set(id,make());return geometry.get(id);}
  const detailedMaterials=new Map();
  function add(g,m,x,y,z){
   let mat=materials[m];g.computeBoundingBox();const size=g.boundingBox.getSize(new T.Vector3());
   if(!g.userData.architectureUV&&g.attributes.normal&&g.type!=='CylinderGeometry'){
    const p=g.attributes.position,n=g.attributes.normal,uv=[];for(let i=0;i<p.count;i++){const ax=Math.abs(n.getX(i)),ay=Math.abs(n.getY(i));uv.push((ay>.6?p.getX(i):ax>.6?p.getZ(i):p.getX(i))/.85,(ay>.6?p.getZ(i):p.getY(i))/.85);}g.setAttribute('uv',new T.Float32BufferAttribute(uv,2));g.userData.architectureUV=true;
   }
   if(['paint','wall','edge','steel','concrete','brick'].includes(m)&&Math.max(size.x,size.y,size.z)>.19){
    const id=m+'/'+g.uuid;if(!detailedMaterials.has(id)){const copy=mat.clone();copy.userData.wearBounds=[g.boundingBox.min.toArray(),g.boundingBox.max.toArray()];decorate(copy);detailedMaterials.set(id,copy);}mat=detailedMaterials.get(id);
   }
   const mesh=new T.Mesh(g,mat);mesh.position.set(x-origin.x,y-origin.y,z-origin.z);mesh.castShadow=!parent.userData.dynamic;mesh.receiveShadow=true;mesh.name=m+'-'+counter++;parent.add(mesh);
   if(Math.max(size.x,size.y,size.z)>.27&&Math.min(size.x,size.y,size.z)>.025){
    const edgeGeo=geo('outline/'+g.uuid,()=>new T.EdgesGeometry(g,42));const line=new T.LineSegments(edgeGeo,new T.LineBasicMaterial({color:0x283332,transparent:true,opacity:.22,depthWrite:false}));line.name='fine-panel-edges';mesh.add(line);
   }
   return mesh;
  }
  function box(x,y,z,w,h,d,m='wall'){return add(geo(['box',w,h,d].join('/'),()=>new T.BoxGeometry(w,h,d)),m,x,y,z);}
  function bevel(x,y,z,w,h,d,m='wall',r=.025){
   r=Math.min(r,w*.16,h*.18,d*.16);
   const id=['bevel',w,h,d,r].join('/');return add(geo(id,()=>{
    const s=new T.Shape(),a=w/2-r,b=h/2-r;s.moveTo(-a,-b);s.lineTo(a,-b);s.lineTo(a,b);s.lineTo(-a,b);s.closePath();
    const g=new T.ExtrudeGeometry(s,{depth:d-2*r,steps:1,bevelEnabled:true,bevelSegments:1,bevelSize:r,bevelThickness:r,curveSegments:1});g.translate(0,0,-(d-2*r)/2);return g;
   }),m,x,y,z);
  }
  function prism(x,y,z,w,h,d,m='wall',top=.86){
   return add(geo(['prism',w,h,d,top].join('/'),()=>{
    const shape=[[-1,-.8],[-.8,-1],[.8,-1],[1,-.8],[1,.8],[.8,1],[-.8,1],[-1,.8]],v=[],idx=[];
    for(let j=0;j<2;j++)for(const [a,b]of shape)v.push(a*w/2*(j?top:1),j*h,b*d/2*(j?top:1));
    for(let i=0;i<8;i++){let n=(i+1)%8;idx.push(i,n,i+8,n,n+8,i+8);if(i>0&&i<7)idx.push(8,8+i,8+i+1,0,i+1,i);}
    const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(v,3));g.setIndex(idx.reduce((a,_,i)=>i%3?a:a.concat(idx[i],idx[i+2],idx[i+1]),[]));const q=g.toNonIndexed();q.computeVertexNormals();return q;
   }),m,x,y,z);
  }
  function gable(x,y,z,w,h,d,m='paint',ridge=.22){
   const shape=new T.Shape();shape.moveTo(-w/2,0);shape.lineTo(w/2,0);shape.lineTo(w*ridge/2,h);shape.lineTo(-w*ridge/2,h);shape.closePath();
   const g=new T.ExtrudeGeometry(shape,{depth:d,steps:1,bevelEnabled:false,curveSegments:1});g.translate(0,0,-d/2);return add(g,m,x,y,z);
  }
  function cylinder(x,y,z,r,h,m='steel',n=12){return add(geo(['cyl',r,h,n].join('/'),()=>new T.CylinderGeometry(r,r,h,n)),m,x,y,z);}
  function beam(a,b,w=.025,d=w,m='steel'){
   const v=new T.Vector3(...b).sub(new T.Vector3(...a)),mid=new T.Vector3(...a).addScaledVector(v,.5),o=box(mid.x,mid.y,mid.z,w,v.length(),d,m);o.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),v.normalize());return o;
  }
  function pipe(points,r=.025,m='steel'){
   const curve=new T.CatmullRomCurve3(points.map(p=>new T.Vector3(...p)),false,'centripetal');
   const g=new T.TubeGeometry(curve,Math.max(12,points.length*5),r,8,false);return add(g,m,0,0,0);
  }
  function part(name,stage,pivot,fn,opts={}){
   const group=new T.Group();group.name=name;group.position.set(...pivot);group.userData={stage,...opts};root.add(group);
   const oldParent=parent,oldOrigin=origin;parent=group;origin=new T.Vector3(...pivot);fn(group);parent=oldParent;origin=oldOrigin;
   parts.push({node:group,stage,pivot:group.position.clone(),rotation:group.rotation.clone(),scale:group.scale.clone(),seed:(parts.length*37%101)/101});return group;
  }
  function moving(name,pivot,kind,fn,opts={}){
   const group=part(name,'systems',pivot,fn,{dynamic:true,motion:kind,...opts});dynamic.push(group);return group;
  }
  function panel(x,y,z,w,h,m='paint'){
   box(x,y,z-.014,w+.014,h+.014,.015,'dark');bevel(x,y,z,w,h,.035,m,.012);for(const sx of [-1,1])for(const sy of [-1,1])cylinder(x+sx*(w/2-.035),y+sy*(h/2-.027),z+.021,.009,.009,'edge',6).rotation.x=Math.PI/2;
  }
  function sidePanel(x,y,z,d,h,m='paint'){
   box(x-.014,y,z,.015,h+.014,d+.014,'dark');bevel(x,y,z,.035,h,d,m,.012);for(const sz of [-1,1])for(const sy of [-1,1]){const o=cylinder(x+.021,y+sy*(h/2-.027),z+sz*(d/2-.035),.009,.009,'edge',6);o.rotation.z=Math.PI/2;}
  }
  function vent(x,y,z,w,h,side=false,count=7){
   if(side){box(x,y,z,.018,h,dummy(w),'dark');for(let i=0;i<count;i++)box(x+.012,y-h*.42+i*h*.84/(count-1),z,.024,.012,w*.9,'steel');}
   else{box(x,y,z,w,h,.018,'dark');for(let i=0;i<count;i++)box(x,y-h*.42+i*h*.84/(count-1),z+.014,w*.91,.012,.025,'steel');}
  }
  function dummy(x){return x;}
  function roofVent(x,y,z,w,d){bevel(x,y,z,w,.045,d,'dark',.006);for(let i=0;i<8;i++)box(x-w*.42+i*w*.84/7,y+.026,z,.017,.018,d*.91,'steel');}
  function door(x,y,z,w=.36,h=.6){
   bevel(x,y+h/2,z,w+.09,h+.055,.07,'edge',.012);box(x,y+h/2,z+.041,w,h,.015,'dark');
   for(const sign of [-1,1]){panel(x+sign*w*.25,y+h/2,z+.051,w*.48,h-.035,'steel');box(x+sign*.035,y+h*.46,z+.080,.012,.12,.012,'edge');}
   lamp(x,y+h+.045,z+.052,w*.72);
  }
  function windowBand(x,y,z,w,h,n=4){
   bevel(x,y,z,w+.055,h+.065,.055,'steel',.009);box(x,y,z+.031,w,h,.019,'glass');
   for(let i=1;i<n;i++)box(x-w/2+i*w/n,y,z+.045,.016,h+.02,.017,'edge');
   box(x,y-h/2-.024,z+.048,w+.065,.026,.06,'edge');
  }
  function sideWindows(x,y,z,d,h,n=4,lit=false){
   bevel(x,y,z,.055,h+.065,d+.055,'steel',.009);box(x+.032,y,z,.018,h,d,lit?'window':'glass');
   for(let i=1;i<n;i++)box(x+.047,y,z-d/2+i*d/n,.017,h+.02,.016,'edge');box(x+.05,y-h/2-.024,z,.06,.026,d+.065,'edge');
  }
  function lamp(x,y,z,w=.10,side=false){
   if(side)box(x,y,z,.032,.055,w,'dark');else box(x,y,z,w,.055,.032,'dark');
   moving('status-light-'+dynamic.length,[x,y,z],'beacon',()=>{if(side)box(x+.021,y,z,.012,.027,w*.81,'lamp');else box(x,y,z+.022,w*.82,.027,.012,'lamp');});
  }
  function stairs(x,z,w,count=3,forward=1){for(let i=0;i<count;i++)box(x,.026+(count-i)*.018,Math.max(-spec.d/2+.075,Math.min(spec.d/2-.075,z+forward*i*.045)),w,.02+(count-i)*.036,.15,'concrete');}
  function rail(a,b,h=.12,m='steel'){
   const p=new T.Vector3(...a),v=new T.Vector3(...b).sub(p),n=Math.max(1,Math.ceil(v.length()/.34));
   for(let i=0;i<=n;i++){const q=p.clone().addScaledVector(v,i/n);beam([q.x,q.y,q.z],[q.x,q.y+h,q.z],.016,.016,m);}
   beam([a[0],a[1]+h,a[2]],[b[0],b[1]+h,b[2]],.014,.014,m);
  }
  function rooftop(x,y,z,w,d){
   roofVent(x,y,z,w,d);for(const sx of [-1,1])for(const sz of [-1,1])box(x+sx*w*.43,y+.027,z+sz*d*.42,.018,.016,.018,'edge');
  }
  function hvac(x,y,z,w=.34,d=.30){
   bevel(x,y+.10,z,w,.2,d,'steel',.018);vent(x,y+.10,z+d*.5+.007,w*.8,.12,false,6);roofVent(x,y+.21,z,w*.8,d*.74);
  }
  function fan(x,y,z,r=.13){
   cylinder(x,y,z,r+.026,.055,'steel',12);cylinder(x,y+.012,z,r,.047,'dark',12);
   moving('fan-'+dynamic.length,[x,y+.041,z],'fan',()=>{
    for(let j=0;j<8;j++){const a=j*Math.PI/4,o=box(x+Math.sin(a)*r*.36,y+.041,z+Math.cos(a)*r*.36,r*.30,.015,r*.72,'edge');o.rotation.y=a+.35;}cylinder(x,y+.05,z,r*.17,.03,'steel',10);
   });
   for(const q of [-.5,0,.5])beam([x-r*.8,y+.084,z+q*r],[x+r*.8,y+.084,z+q*r],.007,.007,'steel');
  }
  function antenna(x,y,z,h=.7){
   cylinder(x,y+.025,z,.045,.05,'steel',8);beam([x,y+.04,z],[x,y+h,z],.015,.015,'steel');
   for(let i=0;i<3;i++)bevel(x,y+.13+i*.14,z,.033,.06,.033,'edge',.004);
   box(x,y+h,z,.024,.027,.024,'lamp');
  }
  function utilityMast(x,y,z){
   antenna(x,y,z,.68);beam([x-.055,y+.03,z],[x-.055,y+.48,z],.018,.018,'steel');
   for(let i=0;i<3;i++)bevel(x-.08+(i%2)*.13,y+.38+i*.08,z,.045,.23,.05,'edge',.007);
  }
  function ladder(x,y,z,height=.8){
   for(const sx of [-1,1])beam([x+sx*.07,y,z],[x+sx*.07,y+height,z],.015,.015,'yellow');
   for(let h=.04;h<height;h+=.10)beam([x-.07,y+h,z],[x+.07,y+h,z],.012,.012,'yellow');
  }
  function hazard(x,y,z,w=.55,d=.11){
   box(x,y,z,w,.005,d,'dark');for(let i=0;i<Math.floor(w/.065);i++){const o=box(x-w/2+.037+i*.065,y+.004,z,.025,.004,d*.93,'yellow');o.rotation.y=-.42;}
  }
  function finish(){
   root.updateMatrixWorld(true);const allMaterials=new Set();root.traverse(o=>{if(o.material)allMaterials.add(o.material);});bindings.set(root,{parts,dynamic,materials,allMaterials,stages});return root;
  }
  return {root,spec,materials,part,moving,box,bevel,prism,gable,cylinder,beam,pipe,panel,sidePanel,vent,roofVent,door,windowBand,sideWindows,lamp,stairs,rail,rooftop,hvac,fan,antenna,utilityMast,ladder,hazard,finish};
 }
 const clamp=x=>Math.max(0,Math.min(1,x)),smooth=x=>{x=clamp(x);return x*x*(3-2*x);};
 function pose(root,kind,t){
  const state=bindings.get(root);if(!state)throw Error('master bindings missing');
  doorPose(root,0);
  for(const p of state.parts){const n=p.node;n.visible=true;n.position.copy(p.pivot);n.rotation.copy(p.rotation);n.scale.copy(p.scale);
   if(kind==='build'&&p.stage!=='foundation'){
    const band=state.stages[p.stage],start=band[0]+p.seed*.045,end=Math.min(1,band[1]+p.seed*.02),q=smooth((t-start)/(end-start));n.visible=q>.001;
    if(p.stage==='shell'){n.scale.y=Math.max(.001,q);n.position.y=p.pivot.y*q;}
    else{n.position.y+=.18*(1-q);n.scale.y=.12+.88*q;}
   }else if(kind==='collapse'&&p.stage!=='foundation'){
    const q=smooth((t-p.seed*.13)/(.78-p.seed*.06)),direction=p.pivot.x<0?-1:1;
    n.rotation.z+=direction*q*(.22+p.seed*.32);n.rotation.x+=q*(p.seed-.38)*.62;n.position.x+=direction*q*.10;
    n.position.y=Math.max(.07,p.pivot.y*(1-q)-q*q*.35);n.scale.y=1-.48*q;
   }
  }
  if(kind==='idle')for(const n of state.dynamic){const m=n.userData.motion,p=state.parts.find(p=>p.node===n),a=t*Math.PI*2;
   if(m==='fan')n.rotation.y=p.rotation.y+t*Math.PI/2;
   else if(m==='radar')n.rotation.y=p.rotation.y+Math.sin(a)*.10;
   else if(m==='dish')n.rotation.y=p.rotation.y+Math.sin(a)*.065;
   else if(m==='gantry'){n.position.x=p.pivot.x+Math.sin(a)*.18;n.position.y=p.pivot.y+(.5-.5*Math.cos(a))*.025;}
   else if(m==='ore'){const q=(t+n.userData.offset)%1;n.position.lerpVectors(new T.Vector3(...n.userData.from),new T.Vector3(...n.userData.to),q);}
   else if(m==='beacon')n.scale.setScalar(.91+.09*Math.cos(a));
  }
  const char=kind==='collapse'?smooth(t):0;
  for(const mat of state.allMaterials){const name=mat.name;if(!mat.color||mat.userData.originalColor===undefined)continue;
   mat.color.setHex(mat.userData.originalColor);mat.color.lerp(new T.Color(0x45413c),char*.84);mat.userData.paintWeight=name==='paint'?1-char:0;
   if(name==='lamp')mat.emissiveIntensity=kind==='idle'?.34+.20*(.5+.5*Math.sin(t*Math.PI*2)):.44*(1-char);
  }
  root.updateMatrixWorld(true);return root;
 }
 function doorPose(root,open){
  const state=bindings.get(root);for(const n of state.dynamic)if(n.userData.motion==='door')for(const slat of n.children){
   const y=slat.userData.closedY;if(y===undefined)continue;slat.position.y=y+open*n.userData.travel;slat.visible=slat.position.y<n.userData.top;
  }
 }
 function decorate(mat,paintOnly=false){
  const bounds=mat.userData.wearBounds;if(!bounds)return mat;
  mat.onBeforeCompile=shader=>{
   shader.uniforms.uWearMin={value:new T.Vector3(...bounds[0])};shader.uniforms.uWearMax={value:new T.Vector3(...bounds[1])};shader.uniforms.uIsPaint={value:mat.userData.factionPaint?1:0};
   shader.vertexShader='varying vec3 vWearPosition;\n'+shader.vertexShader.replace('#include <begin_vertex>','#include <begin_vertex>\nvWearPosition=position;');
   shader.fragmentShader='varying vec3 vWearPosition;\nuniform vec3 uWearMin,uWearMax;uniform float uIsPaint;\n'+shader.fragmentShader;
   shader.fragmentShader=shader.fragmentShader.replace('#include <color_fragment>',`#include <color_fragment>
    vec3 bd=min(vWearPosition-uWearMin,uWearMax-vWearPosition);
    float edgeDistance=max(min(bd.x,bd.y),min(max(bd.x,bd.y),bd.z));
    float fineNoise=fract(sin(dot(floor(vWearPosition*850.0),vec3(12.9898,78.233,37.719)))*43758.5453);
    float edgeWear=(1.0-smoothstep(0.001,0.019,edgeDistance))*(0.30+0.70*fineNoise);
    float coatingCoverage=1.0-uIsPaint*edgeWear*0.24;
    diffuseColor.rgb*=0.967+0.033*fineNoise;
    diffuseColor.rgb=mix(diffuseColor.rgb,vec3(0.53,0.57,0.56),edgeWear*(uIsPaint>0.5?0.24:0.10));`);
   if(paintOnly)shader.fragmentShader=shader.fragmentShader.replace('#include <dithering_fragment>','#include <dithering_fragment>\ngl_FragColor.rgb*=coatingCoverage;gl_FragColor.a*=coatingCoverage;');
  };
  mat.customProgramCacheKey=()=>paintOnly?'architecture-v08-contribution':'architecture-v08-surface';return mat;
 }
 function maskMaterial(mat){const copy=mat.clone();return decorate(copy,true);}
 return {ready,create,pose,doorPose,bindings,maskMaterial};
})();
