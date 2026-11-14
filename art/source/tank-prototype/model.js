/* Editable full-resolution master: 30-degree isometric tank based on the
   approved unit-style-v02 design. Team paint is a tagged material, never a
   hard-coded red/blue texture. +Z is forward, ground pivot is (0,0,0). */
window.TankMaster=(()=>{
 const T=THREE;
 let coating;
 const coatingUrl=new URL('coated-steel.png',document.currentScript.src).href;
 const ready=new Promise((resolve,reject)=>new T.TextureLoader().load(coatingUrl,t=>{coating=t;coating.wrapS=coating.wrapT=T.RepeatWrapping;coating.colorSpace=T.SRGBColorSpace;coating.anisotropy=8;resolve();},undefined,reject));
 function build(wreck=false,kitOnly=false){
  const root=new T.Group();root.name='Greyhaven MBT';
  const mat=(name,color,roughness=.69,metalness=.2)=>{const m=new T.MeshStandardMaterial({color:wreck?0x454742:color,roughness:wreck?.95:roughness,metalness:wreck?.08:metalness});m.name=name;return m;};
  const p={armor:mat('warm grey olive armour',0x85877b),edge:mat('bevel highlights',0xa3a398),dark:mat('recess',0x262c2a),rubber:mat('rubber',0x242726,.94,.02),steel:mat('gunmetal',0x535b59,.54,.6),bolt:mat('fastener',0xa1a59e,.48,.6),glass:mat('optics',0x203c46,.23,.55),lamp:mat('headlamp',0xe9e0bd,.3,.2),paint:mat('FACTION_PAINT',0xd7d7d7,.67,.18)};
  p.paint.userData.factionPaint=true;
  for(const k of ['armor','edge','steel','paint']){p[k].map=coating;p[k].bumpMap=coating;p[k].bumpScale=.018;p[k].roughness=.49;p[k].envMapIntensity=.85;}
  p.armor.color.set(0xa2a293);p.edge.color.set(0xb7b8ab);p.paint.color.set(0xf4f4f4);
  function mesh(geo,m,x=0,y=0,z=0,parent=root){
   // Metre-based planar UVs keep material grain consistent on different panels.
   if(['armor','edge','steel','paint'].includes(m)){
    geo=geo.index?geo.toNonIndexed():geo;geo.computeVertexNormals();const a=geo.attributes.position,n=geo.attributes.normal,uv=[];
    for(let i=0;i<a.count;i++){const ax=Math.abs(n.getX(i)),ay=Math.abs(n.getY(i));uv.push((ay>.6?a.getX(i):ax>.6?a.getZ(i):a.getX(i))*.6,(ay>.6?a.getZ(i):a.getY(i))*.6);}
    geo.setAttribute('uv',new T.Float32BufferAttribute(uv,2));
   }
   const o=new T.Mesh(geo,p[m]);o.position.set(x,y,z);o.castShadow=true;o.receiveShadow=true;parent.add(o);return o;
  }
  function box(w,h,d,x,y,z,m,parent=root){return mesh(new T.BoxGeometry(w,h,d),m,x,y,z,parent);}
  function cyl(r,h,x,y,z,m,axis='y',parent=root,r2=r){const o=mesh(new T.CylinderGeometry(r,r2,h,32),m,x,y,z,parent);if(axis==='x')o.rotation.z=Math.PI/2;if(axis==='z')o.rotation.x=Math.PI/2;return o;}
  function beam(a,b,r,m,parent=root){const v=new T.Vector3(...b).sub(new T.Vector3(...a)),o=mesh(new T.CylinderGeometry(r,r,v.length(),16),m,0,0,0,parent);o.position.copy(new T.Vector3(...a).addScaledVector(v,.5));o.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),v.normalize());return o;}
  // Bevels are actual geometry with hard face normals, not faceted low-poly
  // shading or an illustration pasted onto a box.
  function bevel(w,h,d,r,x,y,z,m,parent=root){
   const shape=new T.Shape(),a=w/2-r,b=h/2-r;
   shape.moveTo(-a,-b);shape.lineTo(a,-b);shape.lineTo(a,b);shape.lineTo(-a,b);shape.closePath();
   const geo=new T.ExtrudeGeometry(shape,{depth:d-2*r,steps:1,bevelEnabled:true,bevelSegments:3,bevelSize:r,bevelThickness:r,curveSegments:4});geo.translate(0,0,-(d-2*r)/2);
   return mesh(geo,m,x,y,z,parent);
  }
  function hull(w,h,d,x,y,z,m,upper=.85,parent=root){
   const ring=[[-.72,-1],[.72,-1],[1,-.7],[1,.65],[.7,1],[-.7,1],[-1,.65],[-1,-.7]],v=[],idx=[];
   for(const [s,l]of [[.9,0],[1,.2],[upper,1]])for(const [a,b]of ring)v.push(a*w/2*s,l*h,b*d/2*s);
   for(let j=0;j<2;j++)for(let i=0;i<8;i++){const a=j*8+i,b=j*8+(i+1)%8;idx.push(a,b+8,b,a,a+8,b+8);}
   for(let i=1;i<7;i++)idx.push(16,16+i+1,16+i);
   let geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(v,3));geo.setIndex(idx);geo=geo.toNonIndexed();geo.computeVertexNormals();return mesh(geo,m,x,y,z,parent);
  }
  if(kitOnly)return {root,p,mesh,box,cyl,beam,bevel,hull};
  // Continuous track belts, road wheels, raised pads and individual guide ribs.
  const wheelZ=[-1.8,-1.08,-.36,.36,1.08,1.8];
  for(const s of [-1,1]){
   bevel(.68,.98,4.72,.16,s*1.29,.57,0,'rubber');
   for(const z of wheelZ){
    cyl(.36,.69,s*1.29,.55,z,'rubber','x');cyl(.28,.72,s*1.29,.55,z,'steel','x');
    cyl(.15,.75,s*1.29,.55,z,'armor','x');cyl(.065,.78,s*1.29,.55,z,'bolt','x');
    for(let j=0;j<8;j++){const a=j*Math.PI/4;cyl(.028,.77,s*1.29,.55+Math.sin(a)*.205,z+Math.cos(a)*.205,'bolt','x');}
   }
   for(let i=0;i<25;i++){
    const z=-2.08+i*.173;
    box(.69,.065,.105,s*1.29,1.075,z,'steel');box(.69,.055,.105,s*1.29,.065,z,'steel');
   }
   for(const end of [-1,1])for(let i=0;i<9;i++){
    const a=-Math.PI/2+i*Math.PI/8,z=end*(1.91+.43*Math.cos(a)),y=.57+.44*Math.sin(a);
    const pad=box(.70,.075,.13,s*1.29,y,z,'steel');pad.rotation.x=end*a;
   }
  }
  hull(2.84,.60,4.58,0,.73,0,'armor',.89);
  bevel(2.66,.13,3.8,.045,0,1.35,-.08,'edge');
  // Sloped glacis, front hatch and lamp guards.
  const glacis=bevel(2.16,.13,1.2,.035,0,1.25,1.46,'armor');glacis.rotation.x=-.22;
  for(const s of [-1,1]){
   bevel(.43,.12,.82,.025,s*.85,1.40,1.50,'paint');
   bevel(.43,.21,.20,.035,s*1.02,1.20,2.12,'dark');bevel(.30,.12,.035,.012,s*1.02,1.21,2.24,'lamp');
   beam([s*1.26,1.13,2.05],[s*1.26,1.38,2.05],.034,'steel');
   beam([s*1.26,1.38,2.05],[s*.79,1.38,2.05],.034,'steel');
   cyl(.13,.075,s*.6,.84,2.21,'steel','z');
   // Independent modular skirts and small fasteners remain neutral.
   for(let j=0;j<5;j++){
    bevel(.12,.62,.77,.025,s*1.59,.99,1.55-j*.78,'paint');
    for(const y of [.77,1.21])for(const z of [1.28,1.82])cyl(.032,.135,s*1.60,y,z-j*.78,'bolt','x');
   }
   bevel(.18,.11,4.32,.025,s*1.52,1.39,-.05,'armor');
  }
  bevel(.62,.13,.70,.04,0,1.44,1.13,'armor');bevel(.36,.065,.15,.01,0,1.53,1.38,'glass');
  for(const s of [-1,1]){
   // Recessed tool trays, tow cables and mounting clamps follow the real hull.
   bevel(.32,.055,1.23,.014,s*1.04,1.47,-.10,'dark');
   beam([s*1.04,1.52,-.68],[s*1.04,1.52,.48],.027,'steel');
   for(const z of [-.55,.3])box(.14,.045,.07,s*1.04,1.56,z,'edge');
   for(let j=0;j<5;j++)box(.18,.026,.025,s*.72,1.34,1.46+j*.1,'steel');
  }
  // Engine grilles: nested frame, recess and real louvres.
  for(const x of [-.72,.72]){
   bevel(.82,.07,.83,.02,x,1.46,-1.58,'edge');box(.69,.03,.70,x,1.51,-1.58,'dark');
   for(let n=0;n<8;n++)box(.64,.035,.035,x,1.54,-1.87+n*.085,'steel');
  }
  for(const s of [-1,1]){
   bevel(.58,.38,.34,.04,s*.84,1.05,-2.24,'steel');
   for(let j=0;j<4;j++)box(.42,.035,.04,s*.84,.94+j*.075,-2.42,'dark');
   box(.18,.09,.03,s*1.23,1.19,-2.24,wreck?'dark':'lamp');
  }
  cyl(.87,.15,0,1.48,-.1,'dark');cyl(.78,.12,0,1.57,-.1,'steel');
  const turret=new T.Group();turret.name='turret';turret.position.set(0,1.58,-.22);root.add(turret);
  hull(2.51,.65,2.53,0,0,0,'armor',.76,turret);
  bevel(1.84,.11,1.98,.045,0,.68,-.07,'armor',turret);
  // Flush roof service panels and hinge lines; no oversized decorative blocks.
  for(const x of [-.55,.55]){
   bevel(.60,.025,.43,.012,x,.752,-.77,'dark',turret);bevel(.55,.029,.38,.01,x,.765,-.77,'armor',turret);
   for(const z of [-.88,-.67])box(.12,.035,.035,x+.20,.793,z,'steel',turret);
  }
  for(const s of [-1,1]){
   // Separate wedge-shaped cheeks; their silhouette echoes the approved board.
   const cheek=hull(.66,.48,1.18,s*.94,.12,.49,'paint',.72,turret);cheek.rotation.z=-s*.10;
   bevel(.14,.40,1.06,.025,s*1.2,.36,-.56,'paint',turret);
   for(const z of [-.83,-.30,.28,.80])cyl(.032,.15,s*1.275,.39,z,'bolt','x',turret);
   // Rear stowage rail, split into uprights to avoid a flat box silhouette.
   for(const z of [-1.25,-.85,-.45])beam([s*1.24,.41,z],[s*1.24,.95,z],.025,'steel',turret);
   for(const y of [.52,.91])beam([s*1.24,y,-1.32],[s*1.24,y,-.36],.027,'steel',turret);
   bevel(.50,.30,.64,.035,s*.78,.50,-1.13,'armor',turret);
  }
  for(const y of [.52,.91])beam([-1.24,y,-1.32],[1.24,y,-1.32],.027,'steel',turret);
  // Back-facing identification survives the rear rack and is visible when
  // retreating; side skirts alone disappear in a straight rear view.
  bevel(1.76,.38,.09,.018,0,.32,-1.275,'paint',turret);
  bevel(1.32,.32,.10,.018,0,1.04,-2.275,'paint');
  // Commander's hatch, periscopes, sensors and coaxial weapon.
  cyl(.33,.10,-.43,.79,-.27,'dark','y',turret);cyl(.29,.11,-.43,.85,-.27,'edge','y',turret);
  for(let j=0;j<4;j++){const a=j*Math.PI/2;box(.14,.08,.10,-.43+Math.cos(a)*.29,.90,-.27+Math.sin(a)*.29,'glass',turret);}
  bevel(.38,.17,.34,.035,.45,.82,.28,'armor',turret);bevel(.28,.09,.03,.01,.45,.83,.47,'glass',turret);
  bevel(.32,.21,.37,.035,-.55,.83,.67,'steel',turret);bevel(.23,.12,.025,.01,-.55,.84,.87,'glass',turret);
  const gunY=.34;
  bevel(.67,.46,.54,.065,0,gunY,1.23,'armor',turret);
  beam([0,gunY,1.40],[0,gunY,3.86],.125,'steel',turret);
  beam([0,gunY,1.55],[0,gunY,2.48],.155,'armor',turret);
  for(const z of [1.55,2.46,2.70,3.64])cyl(.17,.085,0,gunY,z,'edge','z',turret);
  cyl(.175,.29,0,gunY,3.87,'steel','z',turret);cyl(.122,.012,0,gunY,4.022,'dark','z',turret);
  turret.userData.muzzle=[0,gunY,4.028];
  beam([.43,gunY,1.12],[.43,gunY,1.75],.043,'dark',turret);
  bevel(.23,.23,.54,.025,.35,1.03,-.44,'steel',turret);beam([.35,1.08,-.15],[.35,1.08,.55],.035,'dark',turret);
  for(const s of [-1,1]){
   cyl(.075,.10,s*.86,.78,-.93,'dark','y',turret);
   beam([s*.86,.82,-.93],[s*.86,1.65,-.97],.013,'steel',turret);
   for(let n=0;n<3;n++)cyl(.065,.26,s*(1.03+n*.07),.32,.1-n*.19,'steel','z',turret);
  }
  root.userData.gunAxis=[[0,1.58+gunY,1.40-.22],[0,1.58+gunY,4.022-.22]];
  if(wreck){root.scale.y=.61;turret.rotation.z=.13;turret.rotation.y=.17;}
  root.userData.materialContract='FACTION_PAINT surfaces only; neutral structure, rubber, glass and fasteners unchanged';
  return root;
 }
 return {build,ready,kit:wreck=>build(wreck,true)};
})();
