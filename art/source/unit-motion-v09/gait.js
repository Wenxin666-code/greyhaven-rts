// Deform the accepted human mesh with planted feet and a two-bone leg solve.
// The original standing models, size, team panels and equipment stay intact.
window.GaitBake=(()=>{
 const T=THREE,N=384,renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true});renderer.setSize(N*2,N*2);renderer.setClearColor(0,0);renderer.outputColorSpace=T.SRGBColorSpace;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.1;renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;
 const studio=document.createElement('canvas');studio.width=512;studio.height=256;const sg=studio.getContext('2d'),gradient=sg.createLinearGradient(0,0,0,256);gradient.addColorStop(0,'#c2d2df');gradient.addColorStop(.45,'#89969e');gradient.addColorStop(.5,'#353c40');gradient.addColorStop(1,'#171c1b');sg.fillStyle=gradient;sg.fillRect(0,0,512,256);sg.fillStyle='#e6e2d9';sg.fillRect(35,40,80,70);sg.fillStyle='#b3bfc8';sg.fillRect(315,50,50,50);const texture=new T.CanvasTexture(studio);texture.mapping=T.EquirectangularReflectionMapping;texture.colorSpace=T.SRGBColorSpace;const pmrem=new T.PMREMGenerator(renderer),environment=pmrem.fromEquirectangular(texture).texture;
 let current;const smooth=x=>{x=Math.max(0,Math.min(1,x));return x*x*(3-2*x);};
 function dispose(){if(!current)return;const g=new Set(),m=new Set();current.root.traverse(o=>{if(o.geometry)g.add(o.geometry);if(o.material)m.add(o.material);});g.forEach(v=>v.dispose());m.forEach(v=>v.dispose());current.sun.shadow.map?.dispose();current=null;renderer.renderLists.dispose();}
 async function open(key){await TankMaster.ready;dispose();const root=UnitMaster.build(key);root.updateMatrixWorld(true);const mesh=[];
  root.traverse(o=>{if(!o.isMesh)return;o.geometry=o.geometry.clone();const p=o.geometry.attributes.position,w=new Float32Array(p.count*3),v=new T.Vector3();for(let i=0;i<p.count;i++){v.fromBufferAttribute(p,i).applyMatrix4(o.matrixWorld);w.set(v.toArray(),i*3);}mesh.push({o,world:w,inverse:o.matrixWorld.clone().invert(),material:o.material});});
  const scene=new T.Scene();scene.environment=environment;scene.add(root);scene.add(new T.HemisphereLight(0xf3f2ec,0x505c61,.65));const sun=new T.DirectionalLight(0xfff1db,3.2);sun.position.set(-7,12,8);sun.castShadow=true;sun.shadow.mapSize.set(1024,1024);Object.assign(sun.shadow.camera,{left:-5,right:5,top:5,bottom:-5,near:.1,far:35});sun.shadow.bias=-.0003;sun.shadow.normalBias=.015;scene.add(sun);const fill=new T.DirectionalLight(0xc3d4e4,.7);fill.position.set(7,5,-7);scene.add(fill);
  const width=3.0,aim=(.91-.5)*width/Math.cos(Math.PI/6),camera=new T.OrthographicCamera(-width/2,width/2,width/2,-width/2,.1,100);camera.position.set(0,aim+20/Math.sqrt(3),20);camera.lookAt(0,aim,0);current={root,mesh,scene,sun,camera,key};
 }
 function solve(side,phase){const heavy=current.key==='at',stance=heavy?.18:.135,shift=side*(heavy?.025:.055),hip=new T.Vector3(side*.125,.94,-.015),knee=new T.Vector3(side*(stance+.015),.54,.035+shift),ankle=new T.Vector3(side*(stance+.025),.145,shift-.025);const p=(phase+(side<0?.5:0))%1,stancePhase=p<.6,q=(p-.6)/.4;
  const z=stancePhase?.21-.42*p/.6:-.21+.42*smooth(q),lift=stancePhase?0:.12*Math.sin(Math.PI*q),newHip=hip.clone().add(new T.Vector3(.008*Math.sin(phase*Math.PI*2),-.025+.01*Math.cos(phase*Math.PI*4),0)),newAnkle=ankle.clone().add(new T.Vector3(0,lift,z));
  const dy=newAnkle.y-newHip.y,dz=newAnkle.z-newHip.z,d=Math.min(.812,Math.sqrt(dy*dy+dz*dz)),L1=.406,L2=.407,angle=Math.atan2(dz,-dy)+Math.acos(Math.max(-1,Math.min(1,(L1*L1+d*d-L2*L2)/(2*L1*d))));const newKnee=new T.Vector3(knee.x,newHip.y-Math.cos(angle)*L1,newHip.z+Math.sin(angle)*L1);
  const rotation=(a,b,c,d)=>Math.atan2(d.z-c.z,d.y-c.y)-Math.atan2(b.z-a.z,b.y-a.y);
  return {hip,knee,ankle,newHip,newKnee,newAnkle,upper:rotation(hip,knee,newHip,newKnee),lower:rotation(ankle,knee,newAnkle,newKnee)};
 }
 function rotate(v,pivot,angle,target){const y=v.y-pivot.y,z=v.z-pivot.z,c=Math.cos(angle),s=Math.sin(angle);return new T.Vector3(v.x-pivot.x+target.x,y*c-z*s+target.y,y*s+z*c+target.z);}
 function pose(phase,view){const c=current,legs={};legs[-1]=solve(-1,phase);legs[1]=solve(1,phase);const v=new T.Vector3();
  for(const part of c.mesh){const pos=part.o.geometry.attributes.position;for(let i=0;i<pos.count;i++){v.fromArray(part.world,i*3);let transformed;
    if(part.o.userData.fieldPanel!==undefined||part.o.userData.rigidEquipment||part.o.userData.armSide||v.y>.91)transformed=v.clone().add(new T.Vector3(.008*Math.sin(phase*Math.PI*2),-.025+.01*Math.cos(phase*Math.PI*4),0));
    else{const leg=legs[v.x<0?-1:1];if(v.y<.19)transformed=rotate(v,leg.ankle,0,leg.newAnkle);else{const upper=rotate(v,leg.hip,leg.upper,leg.newHip),lower=rotate(v,leg.ankle,leg.lower,leg.newAnkle);transformed=lower.lerp(upper,smooth((v.y-.45)/.16));}}
    transformed.applyMatrix4(part.inverse);pos.setXYZ(i,transformed.x,transformed.y,transformed.z);
   }pos.needsUpdate=true;part.o.geometry.computeVertexNormals();}
  c.root.rotation.y=view*Math.PI/4;c.root.updateMatrixWorld(true);
 }
 function image(paint){const c=current,temporary=[];for(const p of c.mesh){p.o.material=p.material;if(paint&&!p.material.userData.factionPaint){const m=new T.MeshBasicMaterial({color:0,opacity:0,transparent:false,blending:T.NoBlending,depthWrite:true,side:p.material.side});p.o.material=m;temporary.push(m);}}
  renderer.render(c.scene,c.camera);const canvas=document.createElement('canvas');canvas.width=canvas.height=N;canvas.getContext('2d').drawImage(renderer.domElement,0,0,N,N);for(const p of c.mesh)p.o.material=p.material;temporary.forEach(m=>m.dispose());return canvas.toDataURL();
 }
 function frame(view,index){pose(index/12,view);return {base:image(false),paint:image(true)};}
 return {open,frame,dispose,model:(view,index)=>{pose(index/12,view);return current.root;}};
})();
