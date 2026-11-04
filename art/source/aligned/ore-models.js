// Orthographic industrial models share the vehicle pivot and the building foundation.
function wornSteel(){
 const tile=canvas(96,96),ctx=tile.getContext('2d');ctx.fillStyle='#e1dfd6';ctx.fillRect(0,0,96,96);
 let seed=81753;const next=()=>((seed=(1664525*seed+1013904223)>>>0)/4294967296);
 for(let n=0;n<1550;n++){
  const x=next()*96,y=next()*96;ctx.fillStyle=next()>.45?'rgba(65,53,38,.12)':'rgba(255,248,220,.24)';
  ctx.fillRect(x,y,next()*2+.4,next()*1.3+.4);
 }
 const dirt=ctx.createLinearGradient(0,0,0,96);dirt.addColorStop(0,'rgba(240,236,212,.08)');dirt.addColorStop(.66,'rgba(30,28,21,0)');dirt.addColorStop(1,'rgba(41,33,24,.32)');ctx.fillStyle=dirt;ctx.fillRect(0,0,96,96);
 for(let n=0;n<16;n++){ctx.fillStyle='rgba(42,38,30,.16)';ctx.fillRect(next()*95,next()*85,.7,3+next()*10);}
 const tex=new T.CanvasTexture(tile);tex.colorSpace=T.SRGBColorSpace;return tex;
}
function oreRig(truck,side,loaded,phase,finish){
 const g=new T.Group(),wreck=side===2,team=side===1?0xac4c3a:0x237e91;
 const steel=0x62665b,dark=0x353e3e,ochre=0xb58b44,cream=0xb4b19a;
 function part(geo,x,y,z,c){
  const m=new T.Mesh(geo,new T.MeshStandardMaterial({map:finish,color:wreck?0x4d4c43:c,roughness:.83,metalness:.16,flatShading:true}));
  m.position.set(x,y,z);g.add(m);return m;
 }
 function box(x,y,z,w,h,d,c){return part(new T.BoxGeometry(w,h,d),x,y,z,c)}
 function drum(x,y,z,r,h,c){return part(new T.CylinderGeometry(r,r,h,16),x,y,z,c)}
 if(truck){
  box(0,.65,0,2.15,.46,4.4,dark);
  for(const s of [-1,1])for(const z of [-1.45,-.42,1.28]){
   drum(s*1.06,.49,z,.49,.39,dark).rotation.z=Math.PI/2;
   drum(s*1.27,.49,z,.29,.06,steel).rotation.z=Math.PI/2;
  }
  box(-.32,1.39,1.37,1.42,1.08,1.31,ochre);box(-.32,1.58,2.04,1.23,.37,.06,0x394f55);
  box(-.32,1.99,1.37,1.58,.12,1.42,team);box(.77,1.14,1.42,.59,.49,1.30,steel);
  box(0,.99,-.74,2.25,.19,2.83,steel);box(0,1.48,-2.11,2.44,.85,.15,ochre);
  for(const s of [-1,1]){
   box(s*1.13,1.49,-.74,.16,.96,2.86,ochre).rotation.z=-s*.14;
   for(const z of [-1.8,-1.22,-.64,-.06,.49])box(s*1.24,1.48,z,.08,.88,.1,steel).rotation.z=-s*.14;
   box(s*.83,1.09,2.22,.31,.16,.08,0xf7d985);
  }
  for(const sign of [-1,1]){
   box(sign*.92,.91,2.23,.36,.12,.14,steel);box(sign*1.14,1.20,1.44,.09,.16,.68,team);
   for(const z of [-1.45,-.42,1.28])for(let i=0;i<8;i++){
    const a=i*Math.PI/4;box(sign*1.285,.49+.33*Math.sin(a),z+.33*Math.cos(a),.05,.09,.10,steel).rotation.x=-a;
   }
  }
  for(let i=0;i<5;i++)box(.77,1.402,1.08+i*.15,.47,.04,.052,dark);
  for(let i=0;i<6;i++)box(0,1.103,-1.94+i*.43,2.11,.035,.05,steel);
  box(-.32,1.59,2.077,.073,.40,.04,dark);box(-.32,1.19,2.075,1.05,.15,.04,team);
  box(.46,1.40,.61,.13,.73,.14,steel);drum(.46,1.86,.61,.091,.31,dark);
  if(loaded)for(let i=0;i<23;i++){
   const x=((i*17)%23)/23*1.65-.82,z=((i*11)%23)/23*2.23-1.85;
   part(new T.DodecahedronGeometry(.37+(i%3)*.05),x,1.63+(1-Math.abs(x))*.2,z,i%2?ochre:0xd8b15d);
  }
 }else{
  box(0,.12,0,2.86,.24,1.89,steel);
  box(-.73,.81,-.1,1.13,1.36,1.43,cream);box(-.73,1.5,-.1,1.22,.13,1.5,team);
  for(let i=0;i<5;i++)box(-1.22+i*.23,1.12,.626,.14,.25,.04,dark);
  for(const x of [.17,.76]){drum(x,1.09,-.40,.33,1.92,steel);drum(x,1.79,-.40,.347,.21,team);drum(x,2.07,-.40,.35,.10,ochre);}
  box(.55,.45,.49,1.45,.26,.55,dark);
  for(let j=0;j<9;j++)box(-.1+(j+phase)*.15,.602,.49,.07,.052,.48,ochre);
  for(const x of [.17,.76]){
   for(const h of [.55,1.03,1.54])drum(x,h,-.40,.345,.043,cream);
   box(x-.16,1.45,-.06,.035,1.14,.04,ochre);box(x+.05,1.45,-.06,.035,1.14,.04,ochre);
   for(let i=0;i<7;i++)box(x-.055,.94+i*.14,-.044,.22,.032,.04,ochre);
  }
  for(let n=0;n<5;n++)box(-1.3,.75+n*.10,.626,.46,.047,.04,steel);
  for(const x of [-1.22,-.26])box(x,1.49,-.08,.07,.19,1.42,steel);
  box(-.79,1.64,-.43,.62,.25,.52,steel);
  for(let i=0;i<4;i++)box(-1+i*.13,1.78,-.43,.045,.03,.37,dark);
  const wheel=drum(1.15,.8,.55,.31,.14,dark);wheel.rotation.x=Math.PI/2;
  for(let j=0;j<3;j++)box(1.15,.8,.632,.52,.049,.025,ochre).rotation.z=phase*Math.PI*2/3+j*Math.PI/3;
  for(const x of [-.32,.94]){box(x,.53,.84,.075,.86,.075,ochre);box(x,.93,.69,.075,.07,.37,ochre);}
  for(let i=0;i<5;i++)box(-.05+i*.19,.47,.915,.095,.22,.028,ochre).rotation.z=-.4;
 }
 if(wreck){g.scale.y=.43;g.rotation.z=.12;}
 return g;
}
function oreAtlas(key){
 const truck=key.startsWith('miner'),animated=key==='refining',n=truck||animated?16:1,cols=n===1?1:8,rows=n/cols;
 const sheet=canvas(cols*cell,rows*(animated?2:3)*cell),ctx=sheet.getContext('2d');
 const finish=wornSteel();
 const width=truck?7.45:cell*5/Math.SQRT2/span;
 const cam=camera(width,(truck?28:60)*width/(cell*Math.cos(Math.PI/6)),!truck);
 for(let side=0;side<(animated?2:3);side++)for(let i=0;i<n;i++){
  const scene=new T.Scene(),model=oreRig(truck,side,key==='miner-full',animated?i/n:0,finish);lights(scene);scene.add(model);
  if(truck)model.rotation.y=angle(i);
  renderer.render(scene,cam);ctx.save();ctx.translate(i%cols*cell,(side*rows+Math.floor(i/cols))*cell);
  if(!truck)slab(ctx,[3,2],side===2);
  ctx.drawImage(renderer.domElement,0,0);ctx.restore();release(model);
 }
 finish.dispose();return sheet.toDataURL('image/png');
}
