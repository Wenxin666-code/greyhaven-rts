// Independently tagged, rigid weapons. No location-based weapon guessing.
(()=>{
 const base=UnitMaster,T=THREE;
 function build(key,wreck=false){
  if(!['squad','sniper','at-mobile'].includes(key))return base.build(key,wreck);
  const root=base.build('squad',wreck),k=TankMaster.kit(wreck),{box,bevel,beam,cyl,mesh,p}=k;
  p.paint.roughness=.92;p.paint.metalness=0;p.armor.color.set(0x596551);
  function tagFrom(start,role){for(const o of k.root.children.slice(start)){o.userData.weaponRole=role;o.userData.rigidEquipment=true;}}
  if(key==='sniper'){
   // Replace the entire old rifle, including its muzzle and optic.
   for(const o of [...root.children])if(o.userData.weaponRole==='rifle')root.remove(o);
   const start=k.root.children.length;
   bevel(.078,.10,.30,.008,.13,1.25,.36,'steel');
   bevel(.083,.105,.20,.009,.13,1.25,.12,'dark');
   bevel(.06,.105,.075,.005,.13,1.16,.32,'dark');
   bevel(.078,.075,.23,.006,.13,1.255,.60,'armor');
   for(let i=0;i<5;i++)box(.085,.009,.014,.13,1.299,.52+i*.038,'steel');
   // One straight bore and a compact muzzle brake, not stacked rifle barrels.
   beam([.13,1.277,.69],[.13,1.277,1.04],.018,'steel');
   cyl(.029,.065,.13,1.277,1.055,'dark','z');
   cyl(.014,.002,.13,1.277,1.089,'dark','z');
   cyl(.032,.24,.13,1.38,.365,'dark','z');cyl(.029,.006,.13,1.38,.489,'glass','z');
   for(const z of [.29,.43])bevel(.034,.06,.022,.003,.13,1.34,z,'steel');
   for(const s of [-1,1])beam([.13+s*.035,1.23,.63],[.13+s*.045,1.20,.81],.009,'steel');
   tagFrom(start,'rifle');
   const vertices=[-.28,1.47,-.05,.27,1.47,-.05,-.34,1.14,-.29,.30,1.14,-.29,-.20,1.51,-.20,.18,1.51,-.20];
   const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(vertices,3));geo.setIndex([0,2,4,4,2,5,5,2,3,1,5,3,0,4,1,1,4,5]);geo.computeVertexNormals();p.armor.side=T.DoubleSide;mesh(geo,'armor');
   bevel(.25,.20,.03,.008,0,1.29,-.31,'paint');
   for(const s of [-1,1])bevel(.075,.12,.025,.008,s*.25,1.42,-.01,'paint');
  }else if(key==='at-mobile'){
   for(const s of [-1,1]){bevel(.19,.09,.20,.012,s*.27,1.43,0,'paint');bevel(.17,.21,.035,.008,s*.11,1.26,.195,'paint');}
   const start=k.root.children.length;
   // Canonical launcher is the SAME object on the back and in the hands.
   cyl(.082,.98,0,0,0,'steel','z');
   for(const z of [-.44,-.22,.25,.46])cyl(.102,.045,0,0,z,'armor','z');
   cyl(.116,.07,0,0,.50,'steel','z');cyl(.084,.008,0,0,.54,'dark','z');
   cyl(.108,.08,0,0,-.50,'armor','z');
   bevel(.07,.09,.15,.007,0,.115,.14,'dark');cyl(.026,.03,0,.13,.23,'glass','z');
   bevel(.047,.12,.06,.005,.01,-.10,.13,'dark');
   const launcher=new T.Group();launcher.position.set(.25,1.30,-.32);launcher.rotation.x=-Math.PI/2;
   for(const o of k.root.children.slice(start)){o.userData.weaponRole='launcher';o.userData.rigidEquipment=true;launcher.add(o);}k.root.add(launcher);
   root.userData.launcherRest={center:[.25,1.30,-.32],angle:-Math.PI/2};
   bevel(.27,.33,.09,.01,-.06,1.26,-.27,'paint');
  }
  if(key==='squad'||key==='at-mobile'){
   root.userData.fieldPanels=[];
   for(let i=0;i<4;i++){
    const start=k.root.children.length,center=[0,1.12,-.35-i*.025],heavy=key==='at-mobile';
    bevel(.38,heavy?.29:.19,heavy?.035:.10,.012,...center,heavy?'paint':'armor');
    box(.31,.012,.008,center[0],center[1]+.045,center[2]-.06,'steel');
    for(const o of k.root.children.slice(start))o.userData.fieldPanel=i;
    root.userData.fieldPanels.push(center);
   }
  }
  root.add(k.root);root.userData.designRevision='infantry-v12-motion2';return root;
 }
 window.UnitMaster={...base,specs:{...base.specs,sniper:{foot:true,width:2.7,cell:512},'at-mobile':{foot:true,width:2.7,cell:512}},build};
})();
