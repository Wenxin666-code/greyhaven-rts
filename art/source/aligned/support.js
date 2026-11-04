// Rig outlines, tyres and crane beams carry the vehicle identity even in a side elevation.
function supportRig(kind,team,dead){
 const group=new THREE.Group(),metal=dead?0x4a4b47:0x626b67,olive=dead?0x434641:0x70765f,paint=dead?0x52514a:team,amber=dead?0x575348:0xd9a74d,dark=0x30383a;
 function piece(geo,col,x,y,z){const m=new THREE.Mesh(geo,new THREE.MeshStandardMaterial({color:col,roughness:.84,metalness:.14,flatShading:true}));m.position.set(x,y,z);group.add(m);return m}
 function box(x,y,z,w,h,d,c){return piece(new THREE.BoxGeometry(w,h,d),c,x,y,z)}
 function rod(a,b,r,c){const v=new THREE.Vector3(...a),u=new THREE.Vector3(...b),axis=u.clone().sub(v);const m=piece(new THREE.CylinderGeometry(r,r,axis.length(),16),c,0,0,0);m.position.copy(v.add(u).multiplyScalar(.5));m.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),axis.normalize());return m}
 function hull(x,y,z,w,h,d,c){const shape=new THREE.Shape();const pts=[[-.82,-1],[.82,-1],[1,-.76],[1,.71],[.7,1],[-.7,1],[-1,.71],[-1,-.76]];pts.forEach(([a,b],i)=>i?shape.lineTo(a*w/2,b*d/2):shape.moveTo(a*w/2,b*d/2));shape.closePath();
  const geo=new THREE.ExtrudeGeometry(shape,{depth:h,bevelEnabled:true,bevelSegments:1,bevelSize:.09,bevelThickness:.09});geo.rotateX(-Math.PI/2);return piece(geo,c,x,y,z)}
 const truck=kind==='mcv';hull(0,.64,0,truck?2.38:2.65,.39,4.66,olive);
 for(const s of [-1,1]){
  if(!truck){hull(s*1.13,.18,0,.61,.52,4.63,dark);for(let i=0;i<12;i++)box(s*1.17,.79,-2.08+i*.37,.65,.07,.17,metal)}
  const n=truck?3:6;
  for(let i=0;i<n;i++){
   const z=-1.7+i*3.4/(n-1),x=s*(truck?1.13:1.27),r=truck?.53:.37;
   rod([x-s*.22,.54,z],[x+s*.19,.54,z],r,dark);rod([x+s*.20,.54,z],[x+s*.25,.54,z],r*.64,olive);
   rod([x+s*.25,.54,z],[x+s*.27,.54,z],r*.26,metal);
  }
  box(s*1.15,1.08,0,.35,.14,4.27,metal);
 }
 if(truck){
  hull(0,1.12,1.32,2.13,.79,1.55,metal);box(0,1.73,2.13,1.63,.37,.08,0x3b5157).rotation.x=.16;box(0,1.74,2.18,.085,.41,.06,olive);
  for(const s of [-1,1]){box(s*1.07,1.69,1.3,.07,.38,.74,0x3e535b);box(s*1.1,1.36,1.21,.07,.19,.84,paint);box(s*.85,1.19,2.12,.26,.14,.1,amber);box(s*.81,1.33,-1.43,.52,.53,1.04,olive);}
  rod([0,1.18,-1.45],[0,1.71,-1.45],.49,metal);
  for(const s of [-1,1]){rod([s*.27,1.55,-1.45],[s*.27,2.32,-.77],.16,amber);rod([s*.26,2.3,-.77],[s*.26,2.28,1.0],.16,amber);}
  box(0,2.33,.1,.58,.2,1.93,amber);rod([0,1.77,-1.33],[0,2.28,.76],.09,metal);box(0,2.27,1.16,.28,.19,.62,metal);
 }else{
  hull(0,1.1,-.57,2.12,.68,2.15,metal);
  for(const s of [-1,1]){box(s*1.06,1.5,-.7,.11,.3,1.03,paint);box(s*1.04,.65,-2.33,.58,.94,.19,olive).rotation.x=-.28;}
  rod([0,1.56,-.24],[0,dead?1.35:3.48,3.35],.148,metal);rod([0,1.56,-.24],[0,dead?1.5:2.28,1.1],.28,olive);
  box(.42,1.93,-.84,.56,.16,.74,olive);rod([-.74,1.48,-1.1],[-.74,2.44,-1.15],.026,dark);
 }
 if(dead){group.scale.y=.71;group.rotation.z=.13}
 return group;
}
