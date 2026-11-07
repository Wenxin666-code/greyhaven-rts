/* Original editable production meshes. +Z is forward; one rigid model per type,
   shared by every faction and heading. No painted perspective baked onto faces. */
window.ReadableUnits=(()=>{
 const T=THREE;
 function palette(side){
  const wreck=side===2;
  const colors={hull:0x858b6b,top:0xb3b599,edge:0xd0ceb0,dark:0x343b37,
   tire:0x242e30,steel:0x596465,glass:0x203d49,skin:0xd4af83,gold:0xdab04e,
   team:side===1?0xcc453b:0x258fb8,teamLight:side===1?0xe96b51:0x4dc0dc};
  return Object.fromEntries(Object.entries(colors).map(([k,c])=>[k,new T.MeshLambertMaterial({color:wreck?(k==='tire'?0x242525:k==='gold'?0x5d5140:k==='glass'?0x242b2b:0x4c514a):c,flatShading:true})]));
 }
 function box(g,p,w,h,d,x,y,z,m,rz=0){const o=new T.Mesh(new T.BoxGeometry(w,h,d),p[m]);o.position.set(x,y,z);o.rotation.z=rz;g.add(o);return o;}
 function cylinder(g,p,r,h,x,y,z,m,axis='y',sides=10){const o=new T.Mesh(new T.CylinderGeometry(r,r,h,sides),p[m]);o.position.set(x,y,z);if(axis==='x')o.rotation.z=Math.PI/2;if(axis==='z')o.rotation.x=Math.PI/2;g.add(o);return o;}
 function beam(g,p,a,b,r,m){const v=new T.Vector3(...b).sub(new T.Vector3(...a)),o=new T.Mesh(new T.CylinderGeometry(r,r,v.length(),8),p[m]);o.position.copy(new T.Vector3(...a).addScaledVector(v,.5));o.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),v.normalize());g.add(o);return o;}
 // Chamfered armour: broad faces, shaded bevels, no micro-noise/bolts.
 function shell(g,p,w,h,d,x,y,z,m,cap=.83){
  const ring=[[-.72,-1],[.72,-1],[1,-.72],[1,.72],[.72,1],[-.72,1],[-1,.72],[-1,-.72]],v=[],ind=[];
  for(const [scale,level] of [[.89,0],[1,.22],[cap,1]])for(const [a,b]of ring)v.push(a*w*.5*scale,level*h,b*d*.5*scale);
  for(let j=0;j<2;j++)for(let i=0;i<8;i++){const a=j*8+i,b=j*8+(i+1)%8;ind.push(a,b+8,b,a,a+8,b+8);}
  for(let i=1;i<7;i++)ind.push(16,16+i+1,16+i);
  const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(v,3));geo.setIndex(ind);geo.computeVertexNormals();
  const o=new T.Mesh(geo,p[m]);o.position.set(x,y,z);g.add(o);return o;
 }
 function tracks(g,p,w,d){
  for(const s of [-1,1]){
   shell(g,p,.65,.86,d,s*(w/2-.28),.08,0,'tire',.9);
   for(let j=0;j<5;j++)cylinder(g,p,.29,.69,s*(w/2-.28),.47,(j-2)*d*.177,'steel','x');
   box(g,p,.74,.14,d*.88,s*(w/2-.28),.96,0,'top');
   // One large readable side plate instead of many small camouflage strips.
   box(g,p,.1,.43,d*.65,s*(w/2+.08),.79,.08,'team');
  }
 }
 function wheels(g,p,w,zs,r=.48){
  for(const s of [-1,1])for(const z of zs){
   cylinder(g,p,r,.4,s*w/2,r,z,'tire','x');
   cylinder(g,p,r*.48,.43,s*w/2,r,z,'steel','x');
  }
 }
 function truckBase(g,p,zs){
  wheels(g,p,2.15,zs);box(g,p,2.25,.3,4.1,0,.75,0,'dark');
  shell(g,p,2.35,1.2,1.5,0,.87,1.25,'team',.86);
  box(g,p,1.88,.12,1.12,0,2.1,1.23,'teamLight');
  box(g,p,1.68,.5,.045,0,1.71,1.97,'glass');
  for(const s of [-1,1]){box(g,p,.04,.43,.8,s*1.02,1.76,1.3,'glass');box(g,p,.32,.14,.1,s*.83,1.13,2.02,'edge');}
  box(g,p,2.35,.17,.2,0,.83,2.04,'steel');box(g,p,1.05,.25,.07,0,1.14,2.04,'dark');
 }
 function infantry(key,g,p){
  // Human proportions; three large readable identity blocks, wide enough to
  // survive reduction to 20–25 px in the default game camera.
  for(const s of [-1,1]){
   beam(g,p,[s*.19,.18,.07],[s*.18,.89,0],.145,'hull');
   box(g,p,.31,.2,.48,s*.19,.12,.13,'tire');
  }
  shell(g,p,.8,.86,.53,0,.84,0,'team',.76);
  box(g,p,.58,.26,.13,0,1.05,.31,'teamLight');
  box(g,p,.68,.13,.6,0,.89,0,'dark');
  cylinder(g,p,.23,.37,0,1.95,.015,'skin');
  const helmet=key==='engineer'?'gold':'top';
  const dome=new T.Mesh(new T.SphereGeometry(.32,10,5,0,Math.PI*2,0,Math.PI/2),p[helmet]);dome.position.set(0,2.06,0);g.add(dome);
  cylinder(g,p,.34,.12,0,2.05,.015,helmet);
  box(g,p,.29,.085,.09,0,1.99,.25,'dark');
  for(const s of [-1,1]){
   const arm=key==='at'?[[s*.41,1.64,0],[s*.44,1.63,.4]]:[[s*.39,1.62,0],[s*.39,1.24,.27]];
   beam(g,p,...arm,.155,'team');beam(g,p,arm[1],[s*.2,1.32,.53],.115,'hull');
  }
  if(key==='squad'){
   box(g,p,.14,.2,.7,.12,1.34,.5,'dark');cylinder(g,p,.055,.53,.12,1.38,1.0,'steel','z');box(g,p,.11,.27,.16,.12,1.15,.47,'dark');
   box(g,p,.47,.55,.22,0,1.42,-.36,'hull');
  }else if(key==='at'){
   cylinder(g,p,.18,1.66,.39,1.82,.19,'dark','z');cylinder(g,p,.24,.25,.39,1.82,-.65,'top','z');
   cylinder(g,p,.205,.16,.39,1.82,1.04,'steel','z');box(g,p,.15,.13,.26,.39,2.01,.57,'dark');
   box(g,p,.51,.65,.27,-.12,1.45,-.36,'hull');
  }else{
   box(g,p,.4,.38,.3,-.48,1.03,0,'gold');
   beam(g,p,[.48,.85,.3],[.48,1.65,.3],.07,'edge');
   box(g,p,.28,.12,.14,.48,1.64,.3,'steel');
   for(const s of [-1,1])box(g,p,.08,.19,.14,.48+s*.1,1.77,.3,'edge');
   box(g,p,.5,.5,.2,0,1.36,-.36,'team');
  }
 }
 function build(key,side){
  const p=palette(side),g=new T.Group();const base=key.replace('-full','');
  if(['squad','at','engineer'].includes(key))infantry(key,g,p);
  else if(base==='tank'){
   tracks(g,p,3.05,4.35);shell(g,p,2.74,.55,4.05,0,.68,0,'hull');
   shell(g,p,2.26,.78,2.35,0,1.24,-.1,'team',.72);
   box(g,p,1.55,.1,1.53,0,2.04,-.16,'top');
   cylinder(g,p,.34,.14,-.42,2.12,-.35,'hull');
   box(g,p,.6,.38,.5,0,1.63,1.11,'hull');
   beam(g,p,[0,1.67,1.22],[0,1.67,3.53],.13,'top');
   cylinder(g,p,.17,.33,0,1.67,3.55,'steel','z');
   box(g,p,1.5,.06,.62,0,1.26,-1.62,'dark');
   for(const s of [-1,1])box(g,p,.26,.14,.15,s*.96,1.13,1.85,'edge');
   g.userData.gunAxis=[[0,1.67,1.22],[0,1.67,3.715]];
  }else if(base==='ifv'){
   wheels(g,p,2.3,[-1.56,-.53,.53,1.56],.47);
   shell(g,p,2.42,.92,4.25,0,.66,0,'team',.85);
   shell(g,p,2.0,.5,2.45,0,1.47,-.58,'hull',.9);
   box(g,p,1.55,.1,1.74,0,2.03,-.68,'top');
   cylinder(g,p,.48,.31,0,2.08,.13,'team');
   box(g,p,.3,.2,1.15,0,2.24,.77,'dark');
   box(g,p,1.2,.12,.45,0,1.66,1.18,'glass');
   for(const s of [-1,1])box(g,p,.36,.15,.1,s*.77,1.02,1.99,'edge');
   box(g,p,1.45,.6,.08,0,1.27,-2.0,'team');
  }else if(base==='arty'){
   tracks(g,p,2.5,4.7);shell(g,p,2.25,.53,4.2,0,.73,0,'hull');
   shell(g,p,2.12,1.46,2.45,0,1.2,-.9,'team',.87);
   box(g,p,1.6,.11,1.93,0,2.7,-.92,'top');
   box(g,p,.8,.62,.48,0,2.13,.37,'hull');
   beam(g,p,[0,2.19,.5],[0,2.43,3.72],.16,'top');
   box(g,p,.34,.31,.43,0,2.43,3.7,'steel');
   box(g,p,.62,.09,.82,-.58,1.3,1.22,'teamLight');
  }else if(base==='mcv'){
   truckBase(g,p,[-1.55,-.35,1.43]);
   shell(g,p,2.25,1.4,2.25,0,.95,-.9,'top',.93);
   for(const s of [-1,1])box(g,p,.05,.5,1.85,s*1.14,1.49,-.9,'team');
   cylinder(g,p,.38,.18,.43,2.47,-1.37,'steel');
   beam(g,p,[.43,2.58,-1.4],[.43,3.22,-.4],.17,'gold');
   beam(g,p,[.43,3.22,-.4],[.43,2.95,1.26],.14,'gold');
   beam(g,p,[.43,2.94,1.26],[.43,2.47,1.26],.045,'dark');
   box(g,p,.24,.18,.18,.43,2.4,1.26,'steel');
   for(const z of [-1.7,-.7])box(g,p,1.8,.11,.1,0,2.41,z,'hull');
  }else if(base==='miner'){
   truckBase(g,p,[-1.55,-.45,1.4]);
   box(g,p,2.4,.21,2.7,0,1.01,-.68,'hull');
   box(g,p,2.03,.1,2.35,0,1.17,-.72,'dark');
   for(const s of [-1,1]){
    box(g,p,.18,.88,2.8,s*1.14,1.49,-.68,'team');
    box(g,p,.23,.13,2.82,s*1.14,1.98,-.68,'edge');
   }
   box(g,p,2.3,.9,.17,0,1.49,-2.02,'team');
   box(g,p,2.3,.95,.14,0,1.51,.67,'team');
   if(key==='miner-full')for(let i=0;i<13;i++){
    const o=new T.Mesh(new T.DodecahedronGeometry(.33+(i%3)*.06),p[i%3===0?'edge':'gold']);
    o.position.set(((i*7)%5-2)*.35,1.62+(i%3)*.11,-1.55+(i%4)*.54);o.rotation.set(i,.3*i,.7*i);g.add(o);
   }
  }else throw Error('Unknown mobile type '+key);
  if(side===2){g.scale.y=.52;g.rotation.z=.075;}
  return g;
 }
 return {build};
})();
