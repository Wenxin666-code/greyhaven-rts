/* Greyhaven miniatures. All dimensions in shared model space; +Z is forward. */
const Greyhaven = (() => {
 const T=THREE, steel=0x515e60, olive=0x657056, armor=0x7b806a, rubber=0x30383a, hub=0x626951, ivory=0xeee7ce, roof=0x394e62, concrete=0xa6a490, brass=0xc59749, glass=0x324e58;
 const materials=new Map();
 function material(color){
  if(!materials.has(color)){
   const mat=new T.MeshStandardMaterial({color,roughness:.78,metalness:.08,flatShading:true});
   // Object-space mottling stays attached to each plate when the miniature turns.
   mat.onBeforeCompile=shader=>{
    shader.vertexShader='varying vec3 vPaint;\n'+shader.vertexShader.replace('#include <begin_vertex>','#include <begin_vertex>\n vPaint=position*vec3(length(modelMatrix[0].xyz),length(modelMatrix[1].xyz),length(modelMatrix[2].xyz));');
    shader.fragmentShader=`varying vec3 vPaint;
     float pigment(vec3 p){vec3 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);
      vec4 n=dot(i,vec3(1.,37.,79.))+vec4(0.,37.,79.,116.);
      vec4 v=mix(fract(sin(n)*13587.3),fract(sin(n+1.)*13587.3),f.x);
      return mix(mix(v.x,v.y,f.y),mix(v.z,v.w,f.y),f.z);}
     `+shader.fragmentShader.replace('#include <color_fragment>',`#include <color_fragment>
      float dust=pigment(vPaint*5.3)*.42+pigment(vPaint*28.)*.19+pigment(vPaint*1.6)*.39;
      diffuseColor.rgb=diffuseColor.rgb*(.76+.45*dust)+vec3(.07,.059,.044)*smoothstep(.52,.79,dust);`);
   };
   materials.set(color,mat);
  }
  return materials.get(color);
 }
 function part(parent,geo,color,x,y,z){
  const m=new T.Mesh(geo,material(color));m.position.set(x,y,z);m.castShadow=true;m.receiveShadow=true;parent.add(m);return m;
 }
 const boxGeo=new T.BoxGeometry(1,1,1), cylinder=new T.CylinderGeometry(1,1,1,16);
 function box(p,x,y,z,w,h,d,c){const m=part(p,boxGeo,c,x,y,z);m.scale.set(w,h,d);return m}
 function cyl(p,x,y,z,r,h,c){const m=part(p,cylinder,c,x,y,z);m.scale.set(r,h,r);return m}
 function beam(p,a,b,r,c){
  const v=new T.Vector3(...a),to=new T.Vector3(...b),axis=to.clone().sub(v);
  const m=cyl(p,0,0,0,r,axis.length(),c);m.position.copy(v.add(to).multiplyScalar(.5));m.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),axis.normalize());return m;
 }
 // Clipped corners and sloping armour, modelled as rings rather than painted on a billboard.
 function shell(p,x,y,z,w,h,d,c,upper=.83){
  const verts=[],idx=[],ring=[[-.72,-1],[.72,-1],[1,-.73],[1,.67],[.69,1],[-.69,1],[-1,.67],[-1,-.73]];
  for(const [level,k] of [[0,.88],[.24,1],[1,upper]])for(const [a,b] of ring)verts.push(a*w*k/2,level*h,b*d*k/2);
  for(let i=0;i<8;i++){const j=(i+1)%8;for(let k=0;k<16;k+=8)idx.push(k+i,k+8+i,k+j,k+j,k+8+i,k+8+j);if(i>0&&i<7)idx.push(16,17+i,16+i)}
  const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(verts,3));geo.setIndex(idx);geo.computeVertexNormals();return part(p,geo,c,x,y,z);
 }
 function mark(p,x,y,z,size){
  for(const sign of [-1,1]){const q=box(p,x+sign*size*.18,y,z,size*.17,.025,size*.54,ivory);q.rotation.y=sign*-.65}
 }
 function vent(p,x,y,z,w,d){
  box(p,x,y,z,w,.05,d,rubber);for(let i=0;i<5;i++)box(p,x,y+.04,z+d*(i-2)/6,w*.9,.04,.065,steel);
 }
 function lamp(p,x,y,z){box(p,x,y,z,.24,.13,.12,0xf2be69)}
 function wheel(p,x,y,z,r){
  const side=Math.sign(x);for(const [off,rad,len,col] of [[0,r,.44,rubber],[.23,r*.65,.08,hub],[.28,r*.23,.09,steel]]){
   const m=cyl(p,x+off*side,y,z,rad,len,col);m.rotation.z=Math.PI/2;
  }
 }
 function vehicle(p,kind,team,pose){
  const tank=kind==='tank',dead=pose==='wreck',clr=dead?0x484842:olive,trim=dead?0x555148:team;
  for(const s of [-1,1]){
   if(tank)shell(p,s*1.16,.09,0,.69,.83,4.58,rubber,.93);
   for(let i=0;i<(tank?6:4);i++)wheel(p,s*(tank?1.26:1.03),tank?.52:.61,(i-(tank?2.5:1.5))*(tank?.64:1.04),tank?.38:.57);
   if(tank){for(let j=0;j<13;j++)box(p,s*1.2,.88,-1.99+j*.32,.66,.07,.17,steel)}
  }
  shell(p,0,.66,0,tank?3.15:2.59,tank?.77:1.02,4.65,clr,.85);
  for(const sign of [-1,1]){
   for(let j=0;j<4;j++)box(p,sign*(tank?1.5:1.23),1.11,-1.65+j*.91,.18,.49,.8,j===1?trim:armor);
   lamp(p,sign*.81,1.23,2.07);
  }
  vent(p,0,tank?1.45:1.69,-1.39,1.13,.82);
  const top=new T.Group();p.add(top);top.position.set(dead?.45:0,tank?1.4:1.68,dead?-.42:.14);top.rotation.z=dead?.57:0;top.rotation.y=dead?-.76:0;
  shell(top,0,0,0,tank?2.45:1.52,tank?.68:.62,tank?2.52:1.55,dead?rubber:steel);
  for(const s of [-1,1]){const band=box(top,s*(tank?.79:.47),tank?.63:.55,0,.32,.055,tank?1.22:.8,trim);band.rotation.z=-s*.1}
  const end=tank?3.86:1.91;
  beam(top,[0,.29,.44],[0,.29,end],tank?.125:.085,dead?rubber:armor);
  beam(top,[0,.29,.68],[0,.29,1.24],tank?.26:.15,steel);
  beam(top,[0,.29,end-.31],[0,.29,end],tank?.155:.106,rubber);
  cyl(top,-.3,.69,-.22,tank?.34:.22,.12,olive);box(top,.25,.7,.26,.33,.15,.21,glass);
  beam(top,[.45,.53,-.6],[.5,1.64,-.72],.026,steel);
  for(const s of [-1,1]){
   const cheek=box(top,s*(tank?.94:.54),.33,.25,tank?.34:.23,.32,tank?1.02:.62,dead?rubber:armor);cheek.rotation.y=-s*.15;
   for(let j=0;j<3;j++){const bolt=cyl(top,s*(tank?.91:.51),.51,.55-j*.29,.055,.045,ivory);bolt.rotation.z=s*.12}
   box(p,s*.92,1.48,-1.47,.34,.22,.88,dead?rubber:hub);
   for(const z of [-1.67,-1.25])box(p,s*.92,1.607,z,.36,.04,.075,steel);
   if(tank){for(let j=0;j<5;j++)box(p,s*1.42,1.32,-1.55+j*.71,.38,.12,.62,dead?rubber:armor)}
  }
  for(const z of [1.36,end-.4])beam(top,[0,.29,z],[0,.29,z+.105],tank?.158:.101,steel);
  box(top,.19,.72,-.33,.46,.065,.28,hub);mark(top,0,.699,-.64,.52);
  if(dead){box(p,-1.38,.17,1.54,.72,.18,1.32,rubber).rotation.y=.53;vent(p,.24,1.51,-1.26,1,.8)}
 }
 function soldiers(p,team,pose){
  [[-.79,-.67],[.52,-.82],[-.55,.73],[.89,.53]].forEach(([x,z],i)=>{
   const man=new T.Group();p.add(man);man.position.set(x,0,z);man.rotation.y=(i-1.5)*.17;
   const fallen=pose==='wreck'||pose==='falling',step=pose==='action'?.24*(i%2?1:-1):0;
   if(fallen){const t=pose==='falling'?.52:1;man.rotation.x=-1.42*t;man.rotation.z=(i-1.5)*.13*t;man.position.y=.31*t}
   for(const s of [-1,1]){box(man,s*.14,.37,s*step,.23,.63,.23,olive).rotation.x=-s*step;box(man,s*.14,.11,.06+s*step,.26,.2,.38,rubber)}
   shell(man,0,.62,0,.68,.66,.49,olive,.79);box(man,0,1.04,-.25,.46,.45,.22,hub);
   cyl(man,0,1.38,.03,.205,.28,0xab9475);shell(man,0,1.46,0,.54,.21,.53,armor,.68);
   box(man,0,1.645,-.03,.24,.035,.25,team);box(man,-.31,1.08,.07,.16,.22,.29,team);
   beam(man,[.27,1.13,-.04],[.22,.94,.37],.095,olive);beam(man,[-.26,1.12,0],[.1,1,.46],.09,olive);
   box(man,.14,1.01,.41,.14,.14,.81,rubber);beam(man,[.14,1.04,.61],[.14,1.04,.99],.046,steel);
  });
 }
 function structure(p,k,team,pose){
  const dead=pose==='wreck',f=k==='factory',w=f?6.7:4.9,d=f?6.1:4.6;
  shell(p,0,0,0,w+.72,.22,d+1,concrete,.99);
  if(dead){
   shell(p,0,.18,0,w,.65,d,0x55554c,.94);
   for(let i=0;i<10;i++){const x=Math.sin(i*22)*w*.36,z=Math.cos(i*17)*d*.33;
    const slab=box(p,x,.5+i%3*.13,z,1.15,.3,.9,i%2?steel:0x716e62);slab.rotation.set(.23*i,i,.16*i)}
   for(let s of [-1,1])beam(p,[s*w*.4,.4,-1],[s*w*.43,1.75,-1.4],.105,steel);
   if(k==='power'){cyl(p,-1,1,-.7,.44,1.3,steel);beam(p,[.6,.55,-1.1],[1.4,.82,1.15],.43,rubber)}
   if(k==='hq'){beam(p,[-1.5,.5,-1.3],[1.8,1.1,.78],.14,steel);const dish=cyl(p,1.35,1,.54,.53,.14,concrete);dish.rotation.x=.65}
   if(f){for(const s of [-1,1])box(p,s*2.62,1.1,1.94,.35,1.9,.38,steel);
    box(p,.22,1.63,1.94,4.9,.33,.36,roof).rotation.z=-.19}
   return;
  }
  if(f){
   box(p,0,1.46,-d*.44,w,2.64,.42,concrete);
   for(const s of [-1,1]){
    box(p,s*(w/2-.28),1.45,0,.55,2.58,d,concrete);
    for(let j=-2;j<=2;j++)box(p,s*w*.5,1.21,j,.2,2.09,.21,armor);
   }
   box(p,0,2.52,d*.47,w,.51,.48,team);shell(p,0,2.71,0,w+.2,.56,d+.12,roof,.9);
   for(let i=0;i<3;i++)box(p,0,3.3,i*1.28-1.28,w*.56,.09,.46,steel);
   const lift=pose==='action'?1.93:pose==='opening'?.92:0;
   for(let i=0;i<8;i++){const y=.46+i*.24+lift;if(y<2.48)box(p,0,y,d*.46,w-.98,.224,.12,i%2?steel:0x65727a)}
   for(const s of [-1,1]){box(p,s*w*.41,.25,d*.51,.23,.04,.94,brass);lamp(p,s*w*.41,2.13,d*.51)}
   for(let j=-2;j<=2;j++){
    box(p,j*.97,3.26,0,.07,.04,4.65,steel);
    for(const s of [-1,1])box(p,s*2.97,1.78,j*.91,.2,.34,.54,glass);
   }
   for(const s of [-1,1])for(let i=0;i<4;i++){const bar=box(p,s*2.73,.48+i*.37,2.97,.34,.13,.08,brass);bar.rotation.z=.45}
   mark(p,0,3.29,2.04,.98);
  }else{
   shell(p,0,.21,0,w,1.67,d,concrete,.88);shell(p,0,1.82,0,w*.96,.47,d*.95,roof,.84);
   box(p,0,1.4,d*.43,3.2,.32,.16,team);box(p,0,.71,d*.49,1.06,.99,.18,rubber);
   for(const s of [-1,1]){box(p,s*w*.42,.96,.25,.23,1.25,.41,armor);lamp(p,s*.78,1.12,d*.47)}
   for(const s of [-1,1]){
    for(let j=0;j<3;j++)box(p,s*2.19,1.15,-1.14+j*.85,.085,.57,.55,steel);
    box(p,s*1.62,.5,1.95,.38,.65,.57,armor);
   }
   if(k==='hq'){
    shell(p,0,2.23,.22,3.15,.53,2.44,glass,.9);shell(p,0,2.73,.22,3.21,.23,2.55,roof);
    const mx=-1.28,mz=-1.18;for(const s of [-1,1]){
     beam(p,[mx+s*.32,2.1,mz],[mx+s*.12,4.93,mz],.064,steel);
     beam(p,[mx+s*.3,2.32,mz],[mx-s*.19,3.76,mz],.046,ivory)}
    const dish=cyl(p,mx+.18,4.1,mz+.05,.48,.12,ivory);dish.rotation.x=.94;
    for(let j=-1;j<=1;j++){box(p,j*.77,2.52,1.28,.09,.38,.1,steel);box(p,.99,2.36,-1.25+j*.22,.81,.07,.09,armor)}
    box(p,.53,.27,2.35,1.9,.14,.57,steel);
    cyl(p,mx,4.97,mz,.11,.24,brass);mark(p,.63,2.975,.57,.85);
   }else{
    for(const s of [-1,1]){
     cyl(p,s*.86,3.11,-1,.45,2,steel);cyl(p,s*.86,3.22,-1,.47,.28,team);
     cyl(p,s*.86,4.14,-1,.47,.1,concrete);cyl(p,s*.86,4.203,-1,.35,.04,rubber);
     cyl(p,s*.89,2.36,.78,.63,.17,rubber);
     for(let i=0;i<4;i++)box(p,s*.89,2.46,.78,1.03,.055,.095,steel).rotation.y=i*Math.PI/4;
     beam(p,[s*1.86,.6,.74],[s*1.86,2.39,.74],.12,brass);
    }
   }
  }
 }
 const types=['squad','ifv','tank','hq','power','factory'];
 function build(k,team,pose='idle'){
  const p=new T.Group();
  if(k==='squad')soldiers(p,team,pose);else if(k==='tank'||k==='ifv')vehicle(p,k,team,pose);else structure(p,k,team,pose);
  return p;
 }
 return {types,build};
})();
