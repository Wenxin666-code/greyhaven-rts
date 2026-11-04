// One rigid mesh and pivot for every live tank view. No interpolation between
// independent paintings, whose silhouettes and turret positions differ.
// Register separate deck, armour skirt and turret samples on each physical face.
function laminate(mesh,source,side,regions){
 const geom=mesh.geometry.index?mesh.geometry.toNonIndexed():mesh.geometry.clone();
 geom.computeBoundingBox();geom.computeVertexNormals();
 const p=geom.attributes.position,n=geom.attributes.normal,box=geom.boundingBox,uv=[];
 const ratio=(v,a,b)=>(v-a)/Math.max(.0001,b-a);
 for(let i=0;i<p.count;i++){
  const x=ratio(p.getX(i),box.min.x,box.max.x),y=ratio(p.getY(i),box.min.y,box.max.y),z=ratio(p.getZ(i),box.min.z,box.max.z);
  let tile,u,v;
  if(Math.abs(n.getY(i))>.65){tile=regions.deck;u=x;v=z;}
  else if(Math.abs(n.getX(i))>.55){tile=regions.side;u=n.getX(i)>0?z:1-z;v=1-y;}
  else{tile=regions.front;u=x;v=1-y;}
  const [frame,l,t,w,h]=tile;uv.push((frame*256+l+u*w)/source.image.width,1-(side*256+t+v*h)/512);
 }
 geom.setAttribute('uv',new THREE.Float32BufferAttribute(uv,2));mesh.geometry=geom;
 mesh.material=new THREE.MeshStandardMaterial({map:source,color:0xe4e5dc,roughness:.83,metalness:.08,flatShading:true});
}
function armourSkin(model,turret,tex,side){
 const hull=model.children.find(m=>m.isMesh&&m.geometry.type==='BufferGeometry'&&m.position.x===0&&m.position.y===.66);
 const head=turret.children.find(m=>m.isMesh&&m.geometry.type==='BufferGeometry');
 laminate(hull,tex,side,{deck:[0,79,145,29,28],side:[2,32,141,156,20],front:[0,67,163,117,23]});
 laminate(head,tex,side,{deck:[0,86,62,73,65],side:[2,42,106,108,21],front:[0,84,112,77,20]});
 for(const m of model.children){
  if(m.isMesh&&Math.abs(m.position.x)===1.5&&m.geometry.type==='BoxGeometry'){
   const j=Math.round((m.position.z+1.65)/.91);laminate(m,tex,side,{deck:[0,64,69+18*j,15,14],side:[2,34+37*j,141,36,19],front:[2,40,141,6,18]});
  }
 }
 // Fixed rim and fasteners retain their highlights as the chassis turns.
 for(const s of [-1,1])for(let j=0;j<6;j++){
  const cap=new T.Mesh(new T.CylinderGeometry(.12,.12,.04,12),new T.MeshStandardMaterial({color:0x8b8d7c,roughness:.6,metalness:.32}));
  cap.rotation.z=Math.PI/2;cap.position.set(s*1.56,.52,j*.64-1.6);model.add(cap);
 }
 const rim=new T.Mesh(new T.TorusGeometry(.31,.035,6,28),new T.MeshStandardMaterial({color:0xa59e88,roughness:.66}));
 rim.rotation.x=Math.PI/2;rim.position.set(-.3,.754,-.22);turret.add(rim);
 const edges=[hull,head];for(const part of edges){
  const l=new T.LineSegments(new T.EdgesGeometry(part.geometry,25),new T.LineBasicMaterial({color:0xa6a18d,transparent:true,opacity:.60}));
  part.add(l);
 }
}
async function driveAtlas(){
 const n=64, px=192, sheet=canvas(8*px,24*px),ctx=sheet.getContext('2d');
 const cam=camera(8.65,8.65*(188/320-.5)/Math.cos(Math.PI/6),false);
 renderer.setSize(px,px);
 window.bearingAudit=[];const source=await loadArt('tank');
 try{for(let side=0;side<3;side++){
  const wreck=side===2,team=side===1?0xb74c40:0x3097a4;
  const model=Greyhaven.build('tank',team,wreck?'wreck':'idle'),scene=new T.Scene();scene.add(model);lights(scene);
  const turret=model.children.find(o=>o.isGroup);
  if(!wreck){
   armourSkin(model,turret,source,side);
   // Paint the angular cheek plates from the corresponding area of the approved broadside.
   const patch=canvas(120,54),g=patch.getContext('2d');g.drawImage(source.image,2*256+66,side*256+105,68,23,0,0,120,54);
   const map=new T.CanvasTexture(patch);map.colorSpace=T.SRGBColorSpace;
   for(const s of [-1,1]){
    const cheek=new T.Mesh(new T.BoxGeometry(.085,.40,1.12),new T.MeshStandardMaterial({map,roughness:.78}));
    cheek.position.set(s*1.12,.34,.12);turret.add(cheek);
    for(let j=0;j<5;j++){
     const rack=new T.Mesh(new T.BoxGeometry(.20,.14,.40),new T.MeshStandardMaterial({color:0x677568,metalness:.18,roughness:.78}));
     rack.position.set(s*1.49,1.29,1.69-j*.74);model.add(rack);
    }
   }
  }
  for(let i=0;i<n;i++){
   model.rotation.y=i*Math.PI*2/n;renderer.render(scene,cam);
   ctx.drawImage(renderer.domElement,(i%8)*px,(side*8+(i>>3))*px);
  }
  if(side===0){
   // Isolate two real pieces of the same gun for a pixel-level direction check.
   const tubes=turret.children.filter(o=>o.geometry?.type==='CylinderGeometry'&&Math.abs(o.position.y-.29)<.001);
   const rear=tubes.find(o=>Math.abs(o.position.z-.96)<.01),front=tubes.find(o=>Math.abs(o.position.z-3.705)<.01);
   if(!front||!rear)throw Error('Missing gun landmarks');
   model.traverse(o=>{if(o.isMesh)o.visible=o===rear||o===front});
   rear.material=new T.MeshBasicMaterial({color:0x00ff00});front.material=new T.MeshBasicMaterial({color:0xff0000});
   const mask=canvas(px,px).getContext('2d',{willReadFrequently:true});
   for(let i=0;i<n;i++){
    model.rotation.y=i*2*Math.PI/n;renderer.render(scene,cam);mask.clearRect(0,0,px,px);mask.drawImage(renderer.domElement,0,0);
    const pixels=mask.getImageData(0,0,px,px).data,ends=[[0,0,0],[0,0,0]];
    for(let j=0;j<px*px;j++){if(pixels[4*j+3]<128)continue;
     const v=ends[pixels[4*j]>pixels[4*j+1]?1:0];v[0]+=j%px;v[1]+=Math.floor(j/px);v[2]++;}
    if(ends.some(v=>!v[2]))throw Error('Missing landmark pixels');
    const dx=ends[1][0]/ends[1][2]-ends[0][0]/ends[0][2],dy=ends[1][1]/ends[1][2]-ends[0][1]/ends[0][2];
    const wx=Math.sin(i*2*Math.PI/n),wy=.5*Math.cos(i*2*Math.PI/n),error=Math.abs(Math.atan2(dx*wy-dy*wx,dx*wx+dy*wy))*180/Math.PI;
    if(error>3)throw Error('Tank visual heading error '+i+': '+error);
    window.bearingAudit.push([i,dx.toFixed(4),dy.toFixed(4),error.toFixed(3)].join(','));
   }
  }
  release(model);
 }}finally{renderer.setSize(cell,cell);source.dispose();}
 return sheet.toDataURL('image/png');
}
