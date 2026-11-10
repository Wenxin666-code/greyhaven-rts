// One persistent rig: planted-foot crouch, a carried folding field kit, and
// the same launcher moving from its back bracket into the soldier's hands.
window.StanceBake=(()=>{
 const T=THREE,N=384,renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true});renderer.setSize(N*2,N*2);renderer.setClearColor(0,0);renderer.outputColorSpace=T.SRGBColorSpace;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.1;let current;
 const V=a=>new T.Vector3(...a),S=x=>CombatRig.smooth(x),mix=(a,b,t)=>V(a).lerp(V(b),S(t));
 async function open(key){
  await TankMaster.ready;const root=UnitMaster.build(key==='at'?'at-mobile':'squad'),rig=CombatRig.capture(root,key,false),arms=CombatRig.pose(rig,0,0),parts=[];
  const anchors={};for(const s of [-1,1])anchors[s]={a:arms[s].shoulder.clone(),b:arms[s].elbow.clone(),c:arms[s].hand.clone()};
  root.updateMatrixWorld(true);root.traverse(o=>{if(!o.isMesh)return;const a=o.geometry.attributes.position,world=new Float32Array(a.count*3),v=new T.Vector3();for(let i=0;i<a.count;i++){v.fromBufferAttribute(a,i).applyMatrix4(o.matrixWorld);world.set(v.toArray(),i*3);}parts.push({o,world,inverse:o.matrixWorld.clone().invert(),material:o.material});});
  const scene=new T.Scene();scene.add(root);scene.add(new T.HemisphereLight(0xf3f2ec,0x505c61,1.2));const sun=new T.DirectionalLight(0xfff1db,3.2);sun.position.set(-7,12,8);scene.add(sun);const fill=new T.DirectionalLight(0xc3d4e4,.7);fill.position.set(7,5,-7);scene.add(fill);
  const width=3,aim=.41*width/Math.cos(Math.PI/6),camera=new T.OrthographicCamera(-width/2,width/2,width/2,-width/2,.1,100);camera.position.set(0,aim+20/Math.sqrt(3),20);camera.lookAt(0,aim,0);current={root,key,parts,anchors,scene,camera};
 }
 function curve(q,keys){for(let i=1;i<keys.length;i++)if(q<=keys[i][0])return mix(keys[i-1][1],keys[i][1],(q-keys[i-1][0])/(keys[i][0]-keys[i-1][0]));return V(keys.at(-1)[1]);}
 function pose(view,frame){
  const c=current,q=frame/32,at=c.key==='at',crouch=S((q-.15)/.66),offset=V([0,-.34*crouch,.05*crouch]),v=new T.Vector3();
  const launcherCenter=curve(q,[[0,[.25,1.30,-.32]],[.25,[.25,1.30,-.32]],[.43,[.38,1.61,-.22]],[.63,[.39,1.64,.10]],[.88,[.26,1.54,.19]],[1,[.26,1.54,.19]]]).add(offset);
  const launcherAngle=-Math.PI/2+(Math.PI/2+.12)*S((q-.36)/.52);
  const launcherPoint=p=>p.clone().sub(V([.25,1.30,-.32])).applyAxisAngle(V([1,0,0]),Math.PI/2+launcherAngle).add(launcherCenter);
  const launcherGrip=s=>V(s<0?[-.09,-.11,.29]:[.01,-.12,.13]).applyAxisAngle(V([1,0,0]),launcherAngle).add(launcherCenter);
  const sling=S(q/.23),riflePivot=V([.13,1.25,.20]);
  const riflePoint=p=>{const a=p.clone();if(at)a.sub(riflePivot).applyAxisAngle(V([1,0,0]),.72*sling).applyAxisAngle(V([0,1,0]),-.22*sling).add(riflePivot).add(V([.19*sling,-.19*sling,-.15*sling]));return a.add(offset);};
  const arms={};for(const s of [-1,1]){
   const old=c.anchors[s],shoulder=old.a.clone().add(offset);let hand=riflePoint(old.c);
   if(at){
    const reach=s>0?V([.30,1.58,-.22]).add(offset):V([-.23,1.19,-.25]).add(offset);
    hand.lerp(reach,S((q-.09)/.15));hand.lerp(launcherGrip(s),S((q-(s>0?.27:.43))/(s>0?.12:.18)));
   }else if(s<0){
    // Support hand takes the folded kit down, opens it, then returns to the grip.
    const reach=curve(q,[[0,old.c.toArray()],[.18,[-.24,1.14,-.28]],[.40,[-.37,.44,.31]],[.67,[-.35,.32,.38]],[.88,old.c.clone().add(offset).toArray()],[1,old.c.clone().add(offset).toArray()]]);
    hand=reach;
   }
   arms[s]=CombatRig.solve(old.a,old.b,old.c,shoulder,hand,s);
  }
  function body(p){if(crouch===0)return p.clone();if(p.y>.90)return p.clone().add(offset);if(p.y<.19)return p.clone();const side=p.x<0?-1:1,shift=side*.055,hip=V([side*.125,.94,-.015]),knee=V([side*.15,.54,.035+shift]),ankle=V([side*.16,.145,shift-.025]),newHip=hip.clone().add(offset);
   const L1=hip.distanceTo(knee),L2=knee.distanceTo(ankle),d=newHip.distanceTo(ankle),axis=ankle.clone().sub(newHip).normalize(),along=(L1*L1-L2*L2+d*d)/(2*d),h=Math.sqrt(Math.max(0,L1*L1-along*along));let forward=V([0,0,1]);forward.addScaledVector(axis,-forward.dot(axis)).normalize();const newKnee=newHip.clone().addScaledVector(axis,along).addScaledVector(forward,h);
   function transform(a,b,d,e){return p.clone().sub(a).applyQuaternion(new T.Quaternion().setFromUnitVectors(b.clone().sub(a).normalize(),e.clone().sub(d).normalize())).add(d);}
   return transform(ankle,knee,ankle,newKnee).lerp(transform(hip,knee,newHip,newKnee),S((p.y-.46)/.16));
  }
  for(const part of c.parts){const a=part.o.geometry.attributes.position,role=part.o.userData.weaponRole,side=part.o.userData.armSide,panel=part.o.userData.fieldPanel;
   for(let i=0;i<a.count;i++){
    v.fromArray(part.world,i*3);let result;
    if(role==='launcher')result=launcherPoint(v);else if(role==='rifle')result=riflePoint(v);else if(side)result=CombatRig.armVertex(v,arms[side]);
    else if(panel!==undefined){
     const start=V(c.root.userData.fieldPanels[panel]);const lower=S((q-.18)/.24),open=S((q-.40-panel*.045)/.23),a=(panel-1.5)*.78;
     const folded=V([-.27,.27,.32]),end=V([Math.sin(a)*.46,at?.19:.11,Math.cos(a)*.42]);
     const center=start.clone().add(offset).lerp(folded,lower).lerp(end,open);
     result=v.clone().sub(start).applyAxisAngle(V([0,1,0]),a*open).add(center);
    }else result=body(v);
    result.applyMatrix4(part.inverse);a.setXYZ(i,result.x,result.y,result.z);
   }a.needsUpdate=true;part.o.geometry.computeVertexNormals();
  }
  c.root.userData.rigArms=Object.fromEntries([-1,1].map(s=>[s,{a:arms[s].shoulder.toArray(),b:arms[s].elbow.toArray(),c:arms[s].hand.toArray()}]));
  c.root.userData.combatBaseReady=true;c.root.userData.weaponPivot=(at?launcherCenter:riflePivot.clone().add(offset)).toArray();
  c.root.rotation.y=view*Math.PI/4;c.root.updateMatrixWorld(true);
 }
 function image(paint){const c=current,temp=[];for(const p of c.parts){p.o.material=p.material;if(paint&&!p.material.userData.factionPaint){const m=new T.MeshBasicMaterial({color:0,opacity:0,transparent:false,blending:T.NoBlending,depthWrite:true,side:p.material.side});p.o.material=m;temp.push(m);}}renderer.render(c.scene,c.camera);const canvas=document.createElement('canvas');canvas.width=canvas.height=N;canvas.getContext('2d').drawImage(renderer.domElement,0,0,N,N);for(const p of c.parts)p.o.material=p.material;temp.forEach(m=>m.dispose());return canvas.toDataURL();}
 function frame(view,index){pose(view,index);return {base:image(false),paint:image(true)};}
 return {open,frame,model:(view,index)=>{pose(view,index);return current.root;}};
})();
