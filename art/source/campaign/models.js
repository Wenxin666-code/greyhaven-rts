/* Additional production miniatures use the same proportions, sky and team panels as v02. */
const original=Greyhaven.build, T3=THREE;
Greyhaven.build=function(k,team,pose){
 const extra=['mcv','scout','arty','engineer','at','barracks','radar','post'];
 if(!extra.includes(k))return original(k,team,pose);
 const dead=pose==='wreck', tan=dead?0x4a4944:0x817e60, steel=dead?0x424440:0x596568, stripe=dead?0x494945:team;
 const p=new T3.Group();
 function shape(geo,c,x,y,z){let m=new T3.Mesh(geo,new T3.MeshStandardMaterial({color:c,roughness:.87}));m.position.set(x,y,z);m.castShadow=true;m.receiveShadow=true;p.add(m);return m}
 function box(x,y,z,w,h,d,c){return shape(new T3.BoxGeometry(w,h,d),c,x,y,z)}
 function tube(x,y,z,r,h,c){return shape(new T3.CylinderGeometry(r,r,h,12),c,x,y,z)}
 if(k==='engineer'||k==='at'){
  p.add(original('squad',team,pose));
  if(!dead)for(const [x,z] of [[-.79,-.67],[.52,-.82],[-.55,.73],[.89,.53]]){
   if(k==='at'){tube(x+.31,1.23,z+.17,.16,1.13,0x555c46).rotation.x=1.52}
   else{box(x-.38,.57,z,.32,.33,.51,0xbc9452);box(x,1.68,z,.33,.06,.23,0xe1ae4f)}
  }
 }else if(['mcv','scout','arty'].includes(k)){
  const w=k==='scout'?1.84:2.61,d=k==='scout'?3.4:4.9;
  box(0,.92,0,w,.67,d,tan);
  for(const s of [-1,1])for(let j=0;j<(k==='scout'?2:3);j++){
   const z=k==='scout'?j*1.94-.97:j*1.55-1.55;
   tube(s*w*.5,.51,z,.5,.32,0x333839).rotation.z=1.57;
   tube(s*(w*.5+.18),.51,z,.29,.05,steel).rotation.z=1.57}
  box(0,1.5,d*.24,w*.85,.79,d*.33,steel);box(0,1.69,d*.417,w*.7,.24,.05,0x40575c);box(0,1.92,d*.23,w*.73,.08,.38,stripe);
  if(k==='mcv'){box(0,1.51,-.93,2.43,.9,2.56,steel);for(let i=-1;i<=1;i++)box(i*.65,2.04,-.91,.32,.22,2.13,stripe);box(.57,2.19,-.68,.18,.2,2.45,0xd0ad60)}
  if(k==='scout'){tube(0,1.54,-.56,.32,.6,stripe);tube(0,2.27,-.56,.038,1.13,steel);box(0,2.84,-.56,.53,.19,.25,tan)}
  if(k==='arty'){box(0,1.62,-.71,1.61,.65,1.83,stripe);const g=tube(0,2.02,1.24,.17,4.1,steel);g.rotation.x=dead?1.8:1.42}
  if(dead){p.scale.y=.61;p.rotation.z=.11}
 }else{
  box(0,.14,0,5,.27,4.6,0x999a87);
  if(dead){box(0,.55,0,4,.78,3.8,steel);box(-.52,.9,.15,2.5,.3,2,tan).rotation.z=.28;return p}
  if(k==='barracks'){for(const s of [-1,1]){box(s*1.12,.84,0,2.12,1.41,3.95,tan);box(s*1.12,1.58,0,2.22,.26,4.11,steel);box(s*1.12,1.74,.2,1.96,.06,.59,stripe);box(s*1.12,.7,2.01,.81,1.06,.12,0x373f40)}}
  if(k==='radar'){box(0,.72,0,3.75,1.16,3.42,steel);box(0,1.32,0,3.72,.11,1.06,stripe);tube(0,2,0,.29,1.53,tan);const dish=shape(new T3.SphereGeometry(1.58,20,9,0,Math.PI*2,0,.95),0xc0c1ae,0,2.34,0);dish.rotation.x=1.0;dish.scale.y=.44}
  if(k==='post'){box(0,.8,0,3.65,1.32,3.17,tan);box(0,1.49,0,3.8,.16,3.32,steel);box(0,1.82,0,1.95,.6,1.7,stripe);for(const s of [-1,1])tube(s*.31,1.87,1.19,.11,2.1,steel).rotation.x=1.57}
 }
 return p;
};
