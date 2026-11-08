/* V10 structural sequences. Completed/idle/deployment masters are untouched.
   All construction pieces are rigid; collapse uses capped pieces of the actual
   geometry, with independent failure, flight, rotation, impact and settling. */
window.StructuralSequence=(()=>{
 const T=THREE,M=BuildingMasterV08,oldBuild=M.build,oldPose=M.pose,states=new WeakMap();let active;
 const clamp=x=>Math.max(0,Math.min(1,x)),smooth=x=>{x=clamp(x);return x*x*(3-2*x);};
 const random=n=>{const x=Math.sin(n*91.731+17.34)*43758.5453;return x-Math.floor(x);};
 const mix=(a,b,t)=>a+(b-a)*t;
 function vertex(g,i){const p=g.attributes.position,n=g.attributes.normal,u=g.attributes.uv;return {p:new T.Vector3().fromBufferAttribute(p,i),n:n?new T.Vector3().fromBufferAttribute(n,i):new T.Vector3(0,1,0),u:u?new T.Vector2().fromBufferAttribute(u,i):new T.Vector2()};}
 function interpolate(a,b,t){return {p:a.p.clone().lerp(b.p,t),n:a.n.clone().lerp(b.n,t).normalize(),u:a.u.clone().lerp(b.u,t)};}
 function hull(points,axis){
  const axes=[0,1,2].filter(i=>i!==axis),map=new Map();for(const p of points)map.set(axes.map(a=>Math.round(p.getComponent(a)*1e6)).join('/'),p);
  const q=[...map.values()].sort((a,b)=>a.getComponent(axes[0])-b.getComponent(axes[0])||a.getComponent(axes[1])-b.getComponent(axes[1]));
  const cross=(a,b,c)=>(b.getComponent(axes[0])-a.getComponent(axes[0]))*(c.getComponent(axes[1])-a.getComponent(axes[1]))-(b.getComponent(axes[1])-a.getComponent(axes[1]))*(c.getComponent(axes[0])-a.getComponent(axes[0]));
  const half=list=>{const h=[];for(const p of list){while(h.length>1&&cross(h[h.length-2],h[h.length-1],p)<=1e-10)h.pop();h.push(p);}return h;};
  if(q.length<3)return [];const low=half(q),high=half(q.slice().reverse());low.pop();high.pop();return low.concat(high);
 }
 function cut(triangles,axis,value,sign){
  const result=[],intersections=[];
  for(const tri of triangles){const poly=[];for(let i=0;i<3;i++){
   const a=tri[i],b=tri[(i+1)%3],da=sign*(a.p.getComponent(axis)-value),db=sign*(b.p.getComponent(axis)-value),inside=da>=-1e-8;
   if(inside)poly.push(a);if((da>1e-8&&db< -1e-8)||(da< -1e-8&&db>1e-8)){const q=interpolate(a,b,da/(da-db));poly.push(q);intersections.push(q.p);}
  }for(let i=1;i<poly.length-1;i++)result.push([poly[0],poly[i],poly[i+1]]);}
  const cap=hull(intersections,axis);if(cap.length>=3){const normal=new T.Vector3();normal.setComponent(axis,-sign);const axes=[0,1,2].filter(i=>i!==axis),v=cap.map(p=>({p,n:normal,u:new T.Vector2(p.getComponent(axes[0])/.85,p.getComponent(axes[1])/.85)}));
   for(let i=1;i<v.length-1;i++){const tri=[v[0],v[i],v[i+1]],n=tri[1].p.clone().sub(tri[0].p).cross(tri[2].p.clone().sub(tri[0].p));if(n.dot(normal)<0)[tri[1],tri[2]]=[tri[2],tri[1]];result.push(tri);}
  }return result;
 }
 function geometryFrom(faces){const p=[],n=[],uv=[];for(const face of faces)for(const v of face){p.push(...v.p.toArray());n.push(...v.n.toArray());uv.push(...v.u.toArray());}const q=new T.BufferGeometry();q.setAttribute('position',new T.Float32BufferAttribute(p,3));q.setAttribute('normal',new T.Float32BufferAttribute(n,3));q.setAttribute('uv',new T.Float32BufferAttribute(uv,2));q.computeBoundingBox();return q;}
 function fracture(g,hollow=false){
  g.computeBoundingBox();const b=g.boundingBox,size=b.getSize(new T.Vector3()),dims=size.toArray();
  const counts=dims.map(v=>v>.66?Math.min(3,Math.ceil(v/.61)):1);
  // Curved service pipes and tiny fasteners remain single rigid pieces.
  if(g.type==='TubeGeometry'||counts.every(v=>v===1))return [g];
  const source=g.index?g.toNonIndexed():g,triangles=[];for(let i=0;i<source.attributes.position.count;i+=3)triangles.push([vertex(source,i),vertex(source,i+1),vertex(source,i+2)]);
  if(hollow){
   // Closed static masters use solid architectural masses. During construction
   // and failure they become thin exterior walls plus floor/ceiling slabs, so
   // the frame and interior are visible and rubble consists of wall plates.
   const a=b.min.toArray(),z=b.max.toArray(),thickness=Math.min(.095,...dims.map(v=>v*.14)),slabs=[];
   const addSlab=(lo,hi)=>{let faces=triangles;for(let axis=0;axis<3&&faces.length;axis++){if(lo[axis]>a[axis]+1e-7)faces=cut(faces,axis,lo[axis],1);if(hi[axis]<z[axis]-1e-7)faces=cut(faces,axis,hi[axis],-1);}if(faces.length){const slab=geometryFrom(faces),parts=fracture(slab);slabs.push(...parts);if(!parts.includes(slab))slab.dispose();}};
   for(const sign of [-1,1]){let lo=a.slice(),hi=z.slice();if(sign<0)hi[2]=a[2]+thickness;else lo[2]=z[2]-thickness;addSlab(lo,hi);}
   for(const sign of [-1,1]){let lo=a.slice(),hi=z.slice();lo[2]+=thickness;hi[2]-=thickness;if(sign<0)hi[0]=a[0]+thickness;else lo[0]=z[0]-thickness;addSlab(lo,hi);}
   for(const sign of [-1,1]){let lo=a.slice(),hi=z.slice();lo[0]+=thickness;hi[0]-=thickness;lo[2]+=thickness;hi[2]-=thickness;if(sign<0)hi[1]=a[1]+thickness;else lo[1]=z[1]-thickness;addSlab(lo,hi);}
   if(source!==g)source.dispose();return slabs;
  }
  const fragments=[];
  for(let ix=0;ix<counts[0];ix++)for(let iy=0;iy<counts[1];iy++)for(let iz=0;iz<counts[2];iz++){
   const indices=[ix,iy,iz];let faces=triangles;
   for(let a=0;a<3&&faces.length;a++)if(counts[a]>1){const lo=b.min.getComponent(a)+dims[a]*indices[a]/counts[a],hi=b.min.getComponent(a)+dims[a]*(indices[a]+1)/counts[a];if(indices[a]>0)faces=cut(faces,a,lo,1);if(indices[a]<counts[a]-1)faces=cut(faces,a,hi,-1);}
   if(faces.length<2)continue;fragments.push(geometryFrom(faces));
  }if(source!==g)source.dispose();return fragments.length?fragments:[g];
 }
 function build(key){
  const root=oldBuild(key),state=M.bindings.get(root);oldPose(root,'idle',0);root.updateMatrixWorld(true);
  const group=new T.Group();group.name='rigid-construction-and-fracture-pieces';group.visible=false;root.add(group);
  const pieces=[],supports=[],boxes=[],events={build:[],collapse:[]};let serial=0;
  const dimensions=M.specs[key],completeBounds=new T.Box3();
  for(const p of state.parts)if(!p.node.userData.deploymentVehicle)completeBounds.union(new T.Box3().setFromObject(p.node));
  const height=completeBounds.max.y;
  function add(geometry,material,matrix,stage,name,support=false){
   const worldBounds=geometry.boundingBox.clone().applyMatrix4(matrix),center=worldBounds.getCenter(new T.Vector3()),size=worldBounds.getSize(new T.Vector3()),seed=++serial;
   const position=new T.Vector3(),quaternion=new T.Quaternion(),scale=new T.Vector3();matrix.decompose(position,quaternion,scale);
   const pivot=new T.Group(),mesh=new T.Mesh(geometry,material);pivot.name=name+'-piece-'+seed;pivot.position.copy(center);mesh.position.copy(position).sub(center);mesh.quaternion.copy(quaternion);mesh.scale.copy(scale);mesh.castShadow=!support;mesh.receiveShadow=true;pivot.add(mesh);group.add(pivot);
   const r=random(seed),y=clamp(center.y/height);let role=stage;
   if(stage==='shell'&&size.y<.22&&Math.max(size.x,size.z)>.34&&center.y>.45)role='roof';
   if(['glass','window'].includes(material.name))role='cladding';
   let start=role==='shell'?.24+y*.28+r*.07:role==='cladding'?.44+y*.20+r*.08:role==='roof'?.61+r*.14:role==='systems'?.75+y*.07+r*.09:.83+r*.09;
   if(support)start=.045+y*.16+r*.025;
   const duration=support?.10:role==='shell'?.11:.08,end=Math.min(.995,start+duration);
   const minAxis=size.x<size.y&&size.x<size.z?new T.Vector3(1,0,0):size.z<size.y?new T.Vector3(0,0,1):new T.Vector3(0,1,0);
   const flat=new T.Quaternion().setFromUnitVectors(minAxis,new T.Vector3(0,1,0)),yaw=new T.Quaternion().setFromAxisAngle(new T.Vector3(0,1,0),random(seed+7)*Math.PI*2),tilt=new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),(random(seed+11)-.5)*.32),land=yaw.multiply(tilt).multiply(flat);
   const relative=new T.Box3(worldBounds.min.clone().sub(center),worldBounds.max.clone().sub(center)),corners=[];for(const x of [relative.min.x,relative.max.x])for(const y of [relative.min.y,relative.max.y])for(const z of [relative.min.z,relative.max.z])corners.push(new T.Vector3(x,y,z));
   let lowest=Infinity;for(const p of corners)lowest=Math.min(lowest,p.clone().applyQuaternion(land).y);
   const ground=.066-lowest+.012+random(seed+31)*.055;
   const target=new T.Vector3(center.x*.77+(random(seed+3)-.5)*.24,ground,center.z*.77+(random(seed+5)-.5)*.24);
   const margin=Math.max(size.x,size.z)*.30+.04;target.x=Math.max(-dimensions.w/2+margin,Math.min(dimensions.w/2-margin,target.x));target.z=Math.max(-dimensions.d/2+margin,Math.min(dimensions.d/2-margin,target.z));
   const fail=.045+r*.10+y*.055+(role==='roof'?.09:role==='systems'?.025:0),velocity=(role==='cladding'?1.3:.35)*random(seed+12),gravity=8.6;
   const flight=(velocity+Math.sqrt(velocity*velocity+2*gravity*Math.max(.025,center.y-ground)))/gravity;
   const piece={pivot,center,size,role,start,end,support,land,ground,target,fail,velocity,gravity,flight,seed,corners};pieces.push(piece);if(support)supports.push(piece);
   if(!support&&Math.max(size.x,size.y,size.z)>.28&&events.build.length<70)events.build.push({t:mix(start,end,.72),point:[center.x,worldBounds.min.y+.04,center.z]});
   return piece;
  }
  for(const part of state.parts){if(part.stage==='foundation'||part.node.userData.deploymentVehicle)continue;
   part.node.traverse(mesh=>{if(!mesh.isMesh)return;mesh.geometry.computeBoundingBox();const bounds=mesh.geometry.boundingBox.clone().applyMatrix4(mesh.matrixWorld),size=bounds.getSize(new T.Vector3());
    if(part.stage==='shell'&&size.x>.42&&size.y>.27&&size.z>.40&&['wall','brick','steel'].includes(mesh.material.name))boxes.push(bounds);
    const hollow=part.stage==='shell'&&size.x>.50&&size.y>.40&&size.z>.50&&['wall','brick'].includes(mesh.material.name);
    for(const g of fracture(mesh.geometry,hollow))add(g,mesh.material,mesh.matrixWorld,part.stage,part.node.name);
   });
  }
  // Temporary beams are inside the final walls. They precede wall panels and
  // remain visible through unfinished bays, rather than scaling the full shell.
  for(const b of boxes){const size=b.getSize(new T.Vector3());if(size.y<.28)continue;const nx=Math.min(.065,size.x*.12),nz=Math.min(.065,size.z*.12),levels=Math.max(1,Math.ceil(size.y/.54));
   for(let level=0;level<levels;level++){const low=b.min.y+size.y*level/levels,high=b.min.y+size.y*(level+1)/levels;
    for(const sx of [-1,1])for(const sz of [-1,1]){
     const x=mix(b.min.x,b.max.x,(sx+1)/2)-sx*nx,z=mix(b.min.z,b.max.z,(sz+1)/2)-sz*nz,g=new T.BoxGeometry(nx,high-low,nz);g.computeBoundingBox();add(g,state.materials.steel,new T.Matrix4().makeTranslation(x,(low+high)/2,z),'shell','temporary-bearing-column',true);
    }
    for(const sz of [-1,1]){const g=new T.BoxGeometry(size.x-nx,.045,nz);g.computeBoundingBox();add(g,state.materials.edge,new T.Matrix4().makeTranslation((b.min.x+b.max.x)/2,high,sz<0?b.min.z+nz:b.max.z-nz),'shell','temporary-bearing-beam',true);}
   }
  }
  const candidates=pieces.filter(p=>!p.support&&p.size.x*p.size.y*p.size.z>.025).sort((a,b)=>b.center.y-a.center.y);
  for(let i=0;i<Math.min(5,candidates.length);i++){const p=candidates[Math.floor(i*candidates.length/Math.min(5,candidates.length))];events.collapse.push({t:p.fail,impact:p.fail+p.flight/2.4,point:p.center.toArray(),ground:[p.target.x,.07,p.target.z]});}
  root.userData.structureRevision=10;root.userData.structurePieces=pieces.length;root.userData.structureEvents=events;
  states.set(root,{root,state,group,pieces,supports,events,height});active=root;return root;
 }
 function resetPieces(s){for(const p of s.pieces){p.pivot.visible=true;p.pivot.position.copy(p.center);p.pivot.quaternion.identity();p.pivot.scale.set(1,1,1);}}
 function pose(root,kind,t){
  const s=states.get(root);if(!s)return oldPose(root,kind,t);s.group.visible=false;
  if(!['build','deploy','collapse'].includes(kind)&&!kind.startsWith('partial-'))return oldPose(root,kind,t);
  const deploying=kind==='deploy';oldPose(root,deploying?'deploy':'idle',deploying?t:0);if(deploying){if(t>=1)return root;kind='build';t=smooth((t-.22)/.78);}
  if(kind==='build'&&t>=1||kind==='collapse'&&t<=0)return root;
  const partial=kind.startsWith('partial-')?(Number(kind.split('-')[1])+.5)/4:null;
  for(const p of s.state.parts)if(p.stage!=='foundation'&&!(deploying&&p.node.userData.deploymentVehicle))p.node.visible=false;s.group.visible=true;resetPieces(s);
  if(kind==='build'){
   for(const p of s.pieces){const q=smooth((t-p.start)/(p.end-p.start));p.pivot.visible=q>0&&(!p.support||t<.94);if(!p.pivot.visible)continue;
    // Each rigid part approaches its own installed position. No whole-shell
    // translation, vertical stretch, or silhouette-wide reveal is used.
    const side=p.center.x<0?-1:1,unfinished=1-q;
    if(p.support){p.pivot.position.y+=.12*unfinished;p.pivot.rotation.z=side*.14*unfinished;}
    else if(p.role==='roof'||p.role==='systems'){p.pivot.position.y+=.18*unfinished;p.pivot.rotation.x=(random(p.seed+2)-.5)*.28*unfinished;}
    else{p.pivot.position.x+=side*.13*unfinished;p.pivot.position.y+=.055*unfinished;p.pivot.rotation.y=side*.18*unfinished;}
   }
  }else{
   for(const p of s.pieces){if(p.support?partial===null||p.end>partial:p.end>(partial===null?1:partial)){p.pivot.visible=false;continue;}
    const elapsed=Math.max(0,(t-p.fail)*2.4),rot=smooth(elapsed/(p.flight+.07));p.pivot.quaternion.identity().slerp(p.land,rot);
    const travel=smooth(elapsed/(p.flight+.09));p.pivot.position.lerpVectors(p.center,p.target,travel);
    const fall=p.center.y+p.velocity*elapsed-.5*p.gravity*elapsed*elapsed;
    let floor=.066+.012+random(p.seed+31)*.055;for(const v of p.corners)floor=Math.max(floor,.066+.012+random(p.seed+31)*.055-v.clone().applyQuaternion(p.pivot.quaternion).y);
    const after=Math.max(0,elapsed-p.flight),bounce=after>0?.065*Math.exp(-after*15)*Math.abs(Math.sin(after*32)):0;p.pivot.position.y=Math.max(floor,fall)+bounce;
    if(t>=.98){p.pivot.position.copy(p.target);p.pivot.quaternion.copy(p.land);}
   }
   const char=smooth(t/.68);for(const mat of s.state.allMaterials)if(mat.color&&mat.userData.originalColor!==undefined){mat.color.setHex(mat.userData.originalColor).lerp(new T.Color(0x46413b),char*.83);mat.userData.paintWeight=mat.name==='paint'?1-char:0;if(mat.emissive)mat.emissiveIntensity=0;}
  }
  root.updateMatrixWorld(true);return root;
 }
 M.build=build;M.pose=pose;
 return {states,pose,fracture,get current(){return active;}};
})();
