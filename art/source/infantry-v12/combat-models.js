// Rigid weapon transforms and two-bone hand constraints; tags survive gait baking.
window.CombatRig=(()=>{
 const T=THREE,V=a=>new T.Vector3(...a),smooth=x=>{x=Math.max(0,Math.min(1,x));return x*x*(3-2*x);};
 function bone(a,b,c,d){return {a,c,q:new T.Quaternion().setFromUnitVectors(b.clone().sub(a).normalize(),d.clone().sub(c).normalize())};}
 const transform=(v,b)=>v.clone().sub(b.a).applyQuaternion(b.q).add(b.c);
 function segmentDistance(v,a,b){const d=b.clone().sub(a),t=Math.max(0,Math.min(1,v.clone().sub(a).dot(d)/d.lengthSq()));return v.distanceTo(a.clone().addScaledVector(d,t));}
 function solve(a,b,c,shoulder,hand,side){
  const l1=a.distanceTo(b),l2=b.distanceTo(c),delta=hand.clone().sub(shoulder),actual=delta.length(),d=Math.max(.001,Math.min(l1+l2-.001,actual)),dir=delta.normalize();
  // Reach is clamped to preserve limb lengths. Animation anchors stay reachable.
  hand=shoulder.clone().addScaledVector(dir,d);
  const along=(l1*l1-l2*l2+d*d)/(2*d),height=Math.sqrt(Math.max(0,l1*l1-along*along));
  const down=V([side,-.15,0]);down.addScaledVector(dir,-down.dot(dir)).normalize();const elbow=shoulder.clone().addScaledVector(dir,along).addScaledVector(down,height);
  return {a,b,c,hand,elbow,shoulder,upper:bone(a,b,shoulder,elbow),lower:bone(b,c,elbow,hand)};
 }
 function armVertex(v,arm){const du=segmentDistance(v,arm.a,arm.b),dl=segmentDistance(v,arm.b,arm.c),weight=smooth(.5+(dl-du)/.065);return transform(v,arm.lower).lerp(transform(v,arm.upper),weight);}
 function capture(root,key,crouched=false){
  root.rotation.y=0;root.updateMatrixWorld(true);const parts=[],heavy=key==='at'&&crouched;
  root.traverse(o=>{if(!o.isMesh)return;o.geometry=o.geometry.clone();const a=o.geometry.attributes.position,world=new Float32Array(a.count*3),v=new T.Vector3();for(let i=0;i<a.count;i++){v.fromBufferAttribute(a,i).applyMatrix4(o.matrixWorld);world.set(v.toArray(),i*3);}
   parts.push({o,world,inverse:o.matrixWorld.clone().invert(),weapon:o.userData.weaponRole===(heavy?'launcher':'rifle'),arm:o.userData.armSide||0,material:o.material});
  });
  const anchors=root.userData.rigArms||Object.fromEntries([-1,1].map(s=>[s,{a:[s*.25,1.4,0],b:[s*.33,1.16,.15],c:s<0?[.035,1.22,.48]:[.15,1.25,.29]}]));
  return {root,key,crouched,heavy,parts,anchors,ready:!!root.userData.combatBaseReady,pivot:V(root.userData.weaponPivot||[.13,1.25,.20])};
 }
 function gunPoint(rig,point,aim,recoil){
  const angle=rig.ready?(rig.heavy?-.12:-.455)*aim:(1-aim)*.43-aim*.025;
  const v=point.clone().sub(rig.pivot).applyAxisAngle(V([1,0,0]),angle).add(rig.pivot);
  v.y+=(rig.heavy?.02:.29)*aim+.008*recoil;v.z-=.02*aim+.045*recoil;return v;
 }
 function pose(rig,aim=0,recoil=0){
  const arms={};for(const side of [-1,1]){const t=rig.anchors[side];arms[side]=solve(V(t.a),V(t.b),V(t.c),V(t.a),gunPoint(rig,V(t.c),aim,recoil),side);}
  const v=new T.Vector3();for(const part of rig.parts){const a=part.o.geometry.attributes.position;for(let i=0;i<a.count;i++){
   v.fromArray(part.world,i*3);let result=v.clone();if(part.weapon)result=gunPoint(rig,v,aim,recoil);else if(part.arm)result=armVertex(v,arms[part.arm]);
   else if(v.y>(rig.crouched?1.24:1.56)&&Math.abs(v.x)<.18&&v.z<.2){result.z+=.02*aim;result.y-=.012*aim;}
   result.applyMatrix4(part.inverse);a.setXYZ(i,result.x,result.y,result.z);
  }a.needsUpdate=true;part.o.geometry.computeVertexNormals();}
  rig.root.updateMatrixWorld(true);return arms;
 }
 return {capture,pose,gunPoint,solve,armVertex,V,smooth};
})();
window.CombatBake=(()=>{
 const T=THREE,N=384,renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true});renderer.setSize(N*2,N*2);renderer.setClearColor(0,0);renderer.outputColorSpace=T.SRGBColorSpace;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.1;let current;
 async function open(key,crouched){
  await TankMaster.ready;let root;if(crouched){await StanceBake.open(key);root=StanceBake.model(0,32);}else root=UnitMaster.build(key==='at'?'at-mobile':key);
  const rig=CombatRig.capture(root,key,crouched),scene=new T.Scene();scene.add(root);scene.add(new T.HemisphereLight(0xf3f2ec,0x505c61,1.2));const sun=new T.DirectionalLight(0xfff1db,3.2);sun.position.set(-7,12,8);scene.add(sun);const fill=new T.DirectionalLight(0xc3d4e4,.7);fill.position.set(7,5,-7);scene.add(fill);
  const width=3,aim=.41*width/Math.cos(Math.PI/6),camera=new T.OrthographicCamera(-width/2,width/2,width/2,-width/2,.1,100);camera.position.set(0,aim+20/Math.sqrt(3),20);camera.lookAt(0,aim,0);current={root,rig,scene,camera};
 }
 function image(paint){const c=current,temp=[];for(const p of c.rig.parts){p.o.material=p.material;if(paint&&!p.material.userData.factionPaint){const m=new T.MeshBasicMaterial({color:0,opacity:0,transparent:false,blending:T.NoBlending,depthWrite:true,side:p.material.side});p.o.material=m;temp.push(m);}}renderer.render(c.scene,c.camera);const canvas=document.createElement('canvas');canvas.width=canvas.height=N;canvas.getContext('2d').drawImage(renderer.domElement,0,0,N,N);for(const p of c.rig.parts)p.o.material=p.material;temp.forEach(m=>m.dispose());return canvas.toDataURL();}
 function frame(view,index){CombatRig.pose(current.rig,index<=8?index/8:1,index>8?Math.sin((index-8)/8*Math.PI):0);current.root.rotation.y=view*Math.PI/4;current.root.updateMatrixWorld(true);return {base:image(false),paint:image(true)};}
 return {open,frame};
})();
