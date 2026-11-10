/* v04: build the approved silhouettes, not a common truck with new accessories.
   Editable metre-space geometry. +Z forward, ground pivot zero. Neutral material
   and FACTION_PAINT share the accepted tank's renderer and lighting contract. */
window.UnitMaster=(()=>{
 const T=THREE;
 const specs={squad:{foot:true,width:2.7},at:{foot:true,width:2.7},engineer:{foot:true,width:2.7},ifv:{width:7.5},arty:{width:9.2},mcv:{width:8.35},miner:{width:8.1},'miner-full':{width:8.1}};
 specs['ifv-repair']={width:7.5};
 for(const spec of Object.values(specs))spec.cell=512;
 // Shared, deterministic woven fabric; broad tailoring supplies the detail
 // at battlefield scale, while this fine grain is only visible when zoomed in.
 const fabric=(()=>{const c=document.createElement('canvas');c.width=c.height=128;const ctx=c.getContext('2d'),im=ctx.createImageData(128,128);let seed=221;
  for(let y=0;y<128;y++)for(let x=0;x<128;x++){seed=(1664525*seed+1013904223)>>>0;const v=231+(seed>>>28)-((x%4===0||y%4===0)?11:0),i=(y*128+x)*4;im.data[i]=im.data[i+1]=im.data[i+2]=v;im.data[i+3]=255;}
  ctx.putImageData(im,0,0);const t=new T.CanvasTexture(c);t.wrapS=t.wrapT=T.RepeatWrapping;t.colorSpace=T.SRGBColorSpace;t.anisotropy=8;return t;})();
 function build(key,wreck=false){
  if(!specs[key])throw Error('Unknown model '+key);
  const {root,p,mesh,box,cyl,beam,bevel,hull}=TankMaster.kit(wreck);root.name='Greyhaven v04 '+key;
  function material(name,color,roughness=.75,metalness=.05){p[name]=new T.MeshStandardMaterial({color:wreck?0x444642:color,roughness,metalness});p[name].name=name;}
  material('cloth',0x62695e,.96,0);material('seam',0x7a7d70,.96,0);material('webbing',0x444b40,.94,0);
  material('skin',0xb69b80,.85,0);material('skinLight',0xc4aa90,.84,0);material('yellow',0xc3a147,.57,.2);material('ore',0xb49d63,.8,.15);
  material('tread',0x363b37,.88,.02);p.ore.flatShading=true;
  // Cloth uses its authored smooth normals and garment UVs, not planar armour
  // projection (which intentionally splits hard metal faces in the tank kit).
  p.fabricPaint=p.paint.clone();p.fabricPaint.userData.factionPaint=true;p.fabricPaint.name='FACTION_PAINT_CLOTH';
  p.fabricPaint.roughness=.88;p.fabricPaint.metalness=0;p.fabricPaint.bumpScale=.0015;
  function oval(x,y,z,rx,ry,rz,m){const o=mesh(new T.SphereGeometry(1,32,20),m,x,y,z);o.scale.set(rx,ry,rz);return o;}
  function tube(points,r,m){return mesh(new T.TubeGeometry(new T.CatmullRomCurve3(points.map(q=>new T.Vector3(...q))),Math.max(12,points.length*6),r,8,false),m);}
  function bar(a,b,w,d,m){const v=new T.Vector3(...b).sub(new T.Vector3(...a)),mid=new T.Vector3(...a).addScaledVector(v,.5);const o=bevel(w,v.length(),d,Math.min(w,d)*.12,...mid.toArray(),m);o.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),v.normalize());return o;}
  function hydraulic(a,b,r=.08){const end=new T.Vector3(...a).lerp(new T.Vector3(...b),.64).toArray();beam(a,end,r,'steel');beam(end,b,r*.51,'bolt');for(const q of [a,b])cyl(r*1.5,r*2.3,...q,'steel','x');}
  function handle(x,y,z,w=.23,axis='z',m='steel'){
   const a=axis==='z'?[x-w/2,y,z]:[x,y,z-w/2],b=axis==='z'?[x+w/2,y,z]:[x,y,z+w/2];
   tube([a,[a[0],y+.075,a[2]], [b[0],y+.075,b[2]],b],.018,m);
  }
  function bolts(x,y,z,n,spacing,axis='z'){for(let i=0;i<n;i++)cyl(.026,.04,x,y, z+(i-(n-1)/2)*spacing,'bolt',axis);}
  function hatch(x,y,z,w,d){bevel(w,.055,d,.018,x,y,z,'dark');bevel(w-.055,.06,d-.055,.018,x,y+.025,z,'armor');handle(x,y+.07,z,w*.34);for(const s of [-1,1])bevel(.11,.065,.08,.012,x+s*w*.32,y+.06,z-d*.4,'steel');}
  function vents(x,y,z,w,d){bevel(w,.065,d,.018,x,y,z,'edge');bevel(w-.08,.04,d-.07,.012,x,y+.035,z,'dark');for(let i=0;i<9;i++)box(w-.16,.028,d*.038,x,y+.07,z-d*.37+i*d*.093,'steel');}
  function lamps(x,y,z){for(const s of [-1,1]){bevel(.31,.16,.15,.025,s*x,y,z,'dark');bevel(.23,.085,.035,.01,s*x,y,z+.085,'lamp');handle(s*x,y+.11,z,.36);}}
  // Closed faceted hull loft with independent width and deck slope at each station.
  function body(rows,m){const vs=[],ix=[];for(const [z,w,b,t]of rows)vs.push(-w,b,z,w,b,z,-w,t,z,w,t,z);
   for(let i=0;i<rows.length-1;i++){const a=i*4,b=a+4;ix.push(a,b,a+1,a+1,b,b+1,a+2,a+3,b+2,a+3,b+3,b+2,a,a+2,b,a+2,b+2,b,a+1,b+1,a+3,a+3,b+1,b+3);}
   const end=(rows.length-1)*4;ix.push(0,1,2,1,3,2,end,end+2,end+1,end+1,end+2,end+3);
   for(let i=0;i<ix.length;i+=3)[ix[i+1],ix[i+2]]=[ix[i+2],ix[i+1]];
   let g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(vs,3));g.setIndex(ix);g=g.toNonIndexed();g.computeVertexNormals();return mesh(g,m);
  }
  // Side plates are a real beveled polygon in z/y, extruded along x.
  function sidePlate(points,x,thickness,m){const sh=new T.Shape();points.forEach(([z,y],i)=>i?sh.lineTo(z,y):sh.moveTo(z,y));sh.closePath();
   const g=new T.ExtrudeGeometry(sh,{depth:thickness,steps:1,bevelEnabled:true,bevelSegments:2,bevelSize:.018,bevelThickness:.018});g.translate(0,0,-thickness/2);g.rotateY(-Math.PI/2);return mesh(g,m,x,0,0);}
  function wheel(x,z,r=.49){
   const s=Math.sign(x);cyl(r,.35,x,r+.03,z,'rubber','x');cyl(r*.69,.37,x,r+.03,z,'steel','x');cyl(r*.50,.40,x,r+.03,z,'armor','x');cyl(r*.19,.44,x,r+.03,z,'steel','x');
   for(let i=0;i<8;i++){const a=i*Math.PI/4;cyl(r*.035,.43,x,r+.03+Math.sin(a)*r*.35,z+Math.cos(a)*r*.35,'bolt','x');}
   for(let i=0;i<28;i++)for(const lane of [-1,1]){const a=i*Math.PI/14+lane*.025;const o=bevel(.19,.06,r*.23,.012,x+lane*.095,r+.03+Math.sin(a)*r,z+Math.cos(a)*r,'tread');o.rotation.x=Math.PI/2-a;o.rotation.y=lane*.22;}
   beam([x-s*.25,.65,z],[x-s*.50,1.15,z+.18],.055,'steel');hydraulic([x-s*.12,.48,z-.08],[x-s*.32,1.15,z+.11],.045);
  }
  function trackPod(x,z,d,w=.57,r=.36){
   bevel(w,r*2,d,r*.5,x,r+.12,z,'rubber');
   const count=Math.max(3,Math.round(d/.63));for(let j=0;j<count;j++){const q=z-d/2+r+(d-2*r)*j/(count-1);cyl(r*.82,w+.025,x,r+.12,q,'steel','x');cyl(r*.59,w+.055,x,r+.12,q,'armor','x');cyl(r*.20,w+.08,x,r+.12,q,'bolt','x');
    for(let k=0;k<6;k++){const a=k*Math.PI/3;cyl(.021,w+.075,x,r+.12+Math.sin(a)*r*.40,q+Math.cos(a)*r*.40,'bolt','x');}}
   for(let i=0;i<Math.ceil(d/.17);i++)for(const y of [.10,r*2+.14]){const q=z-d/2+.2+i*(d-.4)/(Math.ceil(d/.17)-1);bevel(w+.06,.055,.105,.01,x,y,q,'steel');}
   for(const end of [-1,1])for(let i=0;i<8;i++){const a=-Math.PI/2+i*Math.PI/7;const o=bevel(w+.07,.065,.105,.01,x,r+.12+r*Math.sin(a),z+end*(d/2-r+r*Math.cos(a)),'steel');o.rotation.x=end*a;}
   bevel(w+.12,.13,d-.24,.025,x,r*2+.26,z,'armor');
   for(let j=0;j<3;j++){const q=z+(j-1)*(d-.25)/3;bevel(.08,.34,(d-.32)/3-.03,.018,x+Math.sign(x)*(w/2+.06),r*2+.12,q,'paint');bolts(x+Math.sign(x)*(w/2+.11),r*2+.19,q,2,.19,'x');}
  }
  function cab(x,z,w=1.15){
   hull(w,1.08,1.12,x,.99,z,'paint',.77);bevel(w*.79,.10,.89,.025,x,2.10,z-.04,'armor');
   const glass=bevel(w*.66,.48,.04,.012,x,1.78,z+.48,'glass');glass.rotation.x=-.15;
   beam([x-w*.26,1.56,z+.52],[x-w*.02,1.74,z+.50],.012,'dark');beam([x+w*.26,1.56,z+.52],[x+w*.06,1.73,z+.50],.012,'dark');
   for(const s of [-1,1]){bar([x+s*w*.36,1.54,z+.53],[x+s*w*.29,2.02,z+.45],.025,.025,'steel');cyl(.025,.035,x+s*w*.36,1.17,z+.57,'bolt','z');
    tube([[x+s*w*.42,1.94,z+.3],[x+s*w*.65,1.94,z+.36],[x+s*w*.65,1.80,z+.36]],.018,'steel');bevel(.04,.18,.12,.012,x+s*w*.65,1.81,z+.36,'dark');}
   for(const s of [-1,1]){const q=bevel(.035,.44,.52,.012,x+s*w*.42,1.79,z-.03,'glass');q.rotation.z=-s*.10;bevel(.04,.31,.62,.012,x+s*w*.49,1.30,z,'paint');handle(x+s*w*.5,1.52,z-.08,.21,'x');}
   bevel(.33,.20,.36,.025,x,2.24,z-.18,'armor');bevel(.23,.08,.03,.008,x,2.25,z+.02,'glass');
   for(const s of [-1,1])beam([x+s*w*.34,1.08,z+.58],[x+s*w*.34,1.43,z+.58],.022,'steel');
  }
  if(specs[key].foot){
   const heavy=key==='at',engineer=key==='engineer';p.paint.roughness=.83;p.paint.metalness=.015;p.paint.bumpScale=.002;
   for(const name of ['cloth','seam','webbing','fabricPaint']){p[name].map=fabric;p[name].bumpMap=fabric;p[name].bumpScale=.003;p[name].envMapIntensity=.15;}
   p.cloth.color.set(heavy?0x565c50:engineer?0x74786b:0x656d60);
   p.webbing.color.set(0x343e34);p.seam.color.set(0x929685);
   p.fabricPaint.color.set(0xf4f4f4);
   // Infantry surfaces are woven cloth and coated composite, not tank metal.
   for(const name of ['paint','armor']){p[name].roughness=.91;p[name].metalness=0;p[name].envMapIntensity=.12;p[name].bumpScale=.003;}
   p.armor.color.set(0x747b68);p.skin.color.set(0xb29b85);p.skinLight.color.set(0xbfa58f);
   p.yellow.roughness=.82;p.yellow.metalness=.02;
   // Broad bevelled planes with split side normals. Keep dense detail/texture,
   // but stop smoothing across garment corners into cylindrical limbs.
   function cloth(a,b,radii,m,depth=.9,fold=.05){
    if(m!=='skin'){
     const ring=[[1,.55],[.55,1],[-.55,1],[-1,.55],[-1,-.55],[-.55,-1],[.55,-1],[1,-.55]],v=[],uv=[],ix=[];
     for(let side=0;side<8;side++){
      const start=v.length/3;
      for(let j=0;j<radii.length;j++)for(const edge of [0,1]){
       const [t,r]=radii[j],[x,z]=ring[(side+edge)%8];
       const crease=1+fold*.32*Math.sin(j*1.8+side);
       v.push(x*r*crease,t,z*r*depth*crease);uv.push((side+edge)/8,t);
      }
      for(let j=0;j<radii.length-1;j++){const n=start+j*2;ix.push(n,n+2,n+1,n+1,n+2,n+3);}
     }
     for(const j of [0,radii.length-1]){
      const start=v.length/3,[t,r]=radii[j];for(const [x,z] of ring){v.push(x*r,t,z*r*depth);uv.push((x+1)/2,(z+1)/2);}
      for(let i=1;i<7;i++)if(j===0)ix.push(start,start+i,start+i+1);else ix.push(start,start+i+1,start+i);
     }
     const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(v,3));geo.setAttribute('uv',new T.Float32BufferAttribute(uv,2));geo.setIndex(ix);geo.computeVertexNormals();
     const axis=new T.Vector3(...b).sub(new T.Vector3(...a)),o=mesh(geo,m==='paint'?'fabricPaint':m,...a);o.scale.y=axis.length();o.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),axis.normalize());return o;
    }
    const verts=[],uv=[],indices=[],n=24;for(let j=0;j<radii.length;j++){const [t,r]=radii[j];for(let i=0;i<=n;i++){const theta=i/n*Math.PI*2,wrinkle=1+fold*Math.sin(theta*3+j*1.8)+fold*.35*Math.sin(theta*7-j);verts.push(Math.cos(theta)*r*wrinkle,t,Math.sin(theta)*r*depth*wrinkle);uv.push(i/n,t);}}
    for(let j=0;j<radii.length-1;j++)for(let i=0;i<n;i++){const k=j*(n+1)+i;indices.push(k,k+1,k+n+1,k+1,k+n+2,k+n+1);}
    for(const j of [0,radii.length-1])for(let i=1;i<n;i++){const k=j*(n+1);if(j===0)indices.push(k,k+i+1,k+i);else indices.push(k,k+i,k+i+1);}
    for(let i=0;i<indices.length;i+=3)[indices[i+1],indices[i+2]]=[indices[i+2],indices[i+1]];
    const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(verts,3));g.setAttribute('uv',new T.Float32BufferAttribute(uv,2));g.setIndex(indices);g.computeVertexNormals();
    const v=new T.Vector3(...b).sub(new T.Vector3(...a)),o=mesh(g,m==='paint'?'fabricPaint':m,...a);o.scale.y=v.length();o.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),v.normalize());return o;
   }
   function pad(x,y,z,w,h,d,m='armor'){
    // Clipped corners and one narrow bevel: no pill-shaped armour / pouches.
    const r=Math.min(w,h)*.10,sh=new T.Shape(),a=w/2,b=h/2;
    const corners=[[-a+r,-b],[a-r,-b],[a,-b+r],[a,b-r],[a-r,b],[-a+r,b],[-a,b-r],[-a,-b+r]];
    corners.forEach(([u,v],i)=>i?sh.lineTo(u,v):sh.moveTo(u,v));sh.closePath();
    const lip=Math.min(d*.12,.003),g=new T.ExtrudeGeometry(sh,{depth:d-2*lip,steps:1,bevelEnabled:true,bevelSegments:1,bevelSize:lip,bevelThickness:lip,curveSegments:1});g.translate(0,0,-(d-2*lip)/2);return mesh(g,m,x,y,z);
   }
   function pouch(x,y,z,w=.095,h=.13,d=.055){
    // Soft edged canvas pocket, flap seam and buckle, without a metal box rim.
    pad(x,y,z,w,h,d,'webbing');pad(x,y+h*.30,z+d*.45,w*.96,h*.23,.012,'cloth');
    bar([x-w*.38,y+h*.29,z+d*.58],[x+w*.38,y+h*.29,z+d*.58],.005,.005,'seam');
    box(.014,.028,.009,x,y+h*.17,z+d*.62,'dark');
   }
   function boot(x,z){
    // Foot-shaped heel, instep and toe, fitted inside the previous boot bounds.
    const outline=[[-.060,-.10],[.060,-.10],[.086,-.055],[.095,.115],[.072,.194],[.032,.22],[-.032,.22],[-.072,.194],[-.095,.115],[-.086,-.055]];
    function shell(levels,m){
     const v=[],ix=[],n=outline.length;
     for(const [k,y,rise] of levels)for(const [a,b] of outline)v.push(x+a*k,y+rise*Math.max(0,1-(b+.1)/.32),z+b*k);
     for(let j=0;j<levels.length-1;j++)for(let i=0;i<n;i++){const a=j*n+i,b=j*n+(i+1)%n;ix.push(a,b,a+n,b,b+n,a+n);}
     const end=(levels.length-1)*n;for(let i=1;i<n-1;i++){ix.push(0,i+1,i,end,end+i,end+i+1);}
     for(let i=0;i<ix.length;i+=3)[ix[i+1],ix[i+2]]=[ix[i+2],ix[i+1]];
     let g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(v,3));g.setIndex(ix);g=g.toNonIndexed();g.computeVertexNormals();return mesh(g,m);
    }
    shell([[.98,.016,0],[1,.037,0],[.94,.051,0]],'dark');
    shell([[.92,.050,0],[.91,.084,.025],[.75,.106,.064],[.55,.112,.07]],'rubber');
   }
   const stance=heavy?.18:.135;
   for(const s of [-1,1]){
    const shift=s*(heavy?.025:.055),hip=[s*.125,.94,-.015],knee=[s*(stance+.015),.54,.035+shift],ankle=[s*(stance+.025),.145,shift-.025];
    cloth(ankle,knee,[[0,.075],[.11,.078],[.20,.092],[.28,.080],[.38,.095],[.48,.086],[.58,.093],[.72,.085],[.85,.094],[1,.100]],'cloth',.92,.072);
    cloth(knee,hip,[[0,.100],[.12,.108],[.25,.099],[.38,.117],[.50,.122],[.65,.126],[.76,.134],[.9,.128],[1,.125]],'cloth',.94,.06);
    pad(knee[0],.55,knee[2]+.094,.137,heavy?.185:.15,.032,'webbing');
    pad(knee[0],.55,knee[2]+.112,.106,heavy?.15:.12,.022,'armor');
    cloth([ankle[0],.045,ankle[2]],[ankle[0],.25,ankle[2]],[[0,.085],[.12,.089],[.55,.082],[.83,.09],[1,.08]],'rubber',.94,.02);
    boot(ankle[0],ankle[2]);
    for(let j=0;j<5;j++)for(const side of [-1,1])box(.009,.012,.018,ankle[0]+side*.092,.032,ankle[2]-.05+j*.044,'webbing');
    for(let i=0;i<5;i++){beam([ankle[0]-.049,.127+i*.017,ankle[2]+.095-i*.016],[ankle[0]+.049,.131+i*.017,ankle[2]+.078-i*.016],.008,'seam');}
    pouch(s*(stance+.085),.79,shift,.085,.18,.11);
    if(heavy){const q=pad(s*.18,.81,.114,.17,.20,.045);q.rotation.z=s*.16;}
   }
   cloth([0,.86,-.01],[0,1.47,0],[[0,.195],[.12,.205],[.24,.18],[.40,.19],[.65,heavy?.27:.225],[.82,heavy?.29:.25],[1,.16]],'cloth',.62,.022);
   // Tailored plate carrier: flat chest, tapered ribs, soft cummerbund.
   // Keep the existing stature and shoulder envelope; remove balloon armour.
   cloth([0,1.015,.015],[0,1.45,.005],[[0,.193],[.13,.201],[.35,heavy?.243:.211],[.65,heavy?.252:.223],[.85,heavy?.24:.212],[1,.168]],'paint',.67,.018);
   const chest=pad(0,1.26,.158,heavy?.37:.31,.27,.036,'fabricPaint');chest.rotation.x=-.08;
   pad(0,1.255,-.147,heavy?.37:.30,.32,.043,'fabricPaint');
   for(const s of [-1,1])for(let j=0;j<3;j++){
    bar([s*.185,1.075+j*.035,-.10],[s*.198,1.075+j*.035,.10],.021,.014,'fabricPaint');
    box(.009,.016,.016,s*.204,1.075+j*.035,.01,'webbing');
   }
   tube([[-.095,1.45,.07],[-.12,1.50,-.02],[0,1.52,-.09],[.12,1.50,-.02],[.095,1.45,.07]],heavy?.045:.026,'webbing');
   for(const s of [-1,1]){
    bar([s*.13,1.02,.185],[s*.17,1.43,.115],.042,.025,'webbing');
    tube([[s*.17,1.43,.12],[s*.19,1.47,0],[s*.15,1.40,-.15],[s*.14,1.08,-.19]],.021,'webbing');
    pad(s*.165,1.36,.155,.05,.065,.025,'steel');pouch(s*.135,1.095,.188,.10,.14,.055);
   }
   cloth([0,.935,0],[0,.985,0],[[0,.205],[1,.203]],'webbing',.78,0);pad(0,.96,.175,.067,.048,.02,'steel');
   for(const s of [-1,1])pouch(s*.14,.935,-.16,.105,.13,.07);
   const shoulderX=heavy?.30:.25;
   for(const s of [-1,1]){
    const armStart=root.children.length;
    const a=[s*shoulderX,1.40,0],b=engineer?[s*.32,1.16,.015]:heavy?[s*.33,1.30,.25]:[s*.33,1.16,.15];
    const c=engineer?[s*.35,.985,.09]:heavy?(s<0?[.08,1.44,.48]:[.27,1.46,.32]):s<0?[.035,1.22,.48]:[.15,1.25,.29];
    cloth(b,a,[[0,.079],[.12,.085],[.24,.078],[.36,.091],[.5,.097],[.63,.092],[.80,.102],[1,.100]],'paint',.98,.058);
    cloth(c,b,[[0,.051],[.12,.055],[.28,.065],[.48,.072],[.61,.068],[.79,.074],[1,.079]],engineer?'skin':'cloth',.85,engineer?.012:.054);
    // Sleeved shoulder seam for light troops; low-profile segmented pads on AT.
    if(heavy){const shoulder=pad(s*shoulderX,1.432,.005,.205,.078,.191,'fabricPaint');shoulder.rotation.z=-s*.24;
     const panel=pad(s*(shoulderX+.025),1.396,.012,.171,.058,.173,'paint');panel.rotation.z=-s*.30;
    }else{
     cloth([s*shoulderX,1.36,0],[s*(shoulderX-.015),1.455,0],[[0,.100],[.35,.101],[.7,.091],[1,.068]],'paint',1.14,.025);
     const cap=pad(s*shoulderX,1.437,.005,.174,.063,.179,'fabricPaint');cap.rotation.z=-s*.22;
    }
    tube([[s*(shoulderX+.036),1.445,.096],[s*(shoulderX+.081),1.413,0],[s*(shoulderX+.036),1.445,-.096]],.004,'seam');
    if(engineer)cloth(new T.Vector3(...b).lerp(new T.Vector3(...a),.03).toArray(),new T.Vector3(...b).lerp(new T.Vector3(...a),.20).toArray(),[[0,.09],[.3,.095],[.7,.094],[1,.09]],'seam');
    oval(...c,.058,.068,.05,'webbing');for(let j=0;j<4;j++){const finger=oval(c[0]-.036+j*.023,c[1]-.025,c[2]+.032,.011,.033,.018,'webbing');finger.rotation.x=-.4;}
    if(heavy){const q=pad(s*.185,1.46,-.015,.12,.15,.175,'paint');q.rotation.z=-s*.26;}
    for(const mesh of root.children.slice(armStart))mesh.userData.armSide=s;
   }
   cyl(.063,.14,0,1.51,0,'skin');
   // Ring-sculpted skull, cheekbones, tapered jaw, nose, ears, eyes and chin.
   cloth([0,1.55,.005],[0,1.825,.005],[[0,.062],[.11,.080],[.30,.092],[.53,.105],[.76,.102],[.93,.077],[1,.025]],'skin',.92,0);
   for(const s of [-1,1]){oval(s*.101,1.686,0,.017,.033,.021,'skin');oval(s*.056,1.665,.079,.028,.030,.014,'skinLight');
    pad(s*.042,1.715,.092,.034,.009,.012,'dark');beam([s*.02,1.737,.091],[s*.067,1.732,.083],.009,'webbing');}
   oval(0,1.682,.102,.017,.034,.025,'skinLight');oval(0,1.615,.075,.039,.018,.017,'skin');beam([-.026,1.642,.093],[.026,1.642,.093],.0025,'webbing');
   const helmet=engineer?'yellow':'armor';
   // A flat crown, sloped temples and clipped brim replace the spherical dome.
   cloth([0,1.748,-.005],[0,1.905,-.005],[[0,.142],[.12,.146],[.48,.133],[.82,.095],[1,.068]],helmet,1.15,0);
   cloth([0,1.744,.006],[0,1.767,.006],[[0,.144],[.2,.146],[.8,.146],[1,.141]],helmet,1.15,0);
   if(!engineer){
    // Fabric helmet cover seams, rail fasteners and a rear retention strap.
    for(const s of [-1,1]){
     tube([[s*.091,1.78,.113],[s*.088,1.854,.03],[s*.068,1.865,-.082]],.003,'webbing');
     pad(s*.137,1.771,-.025,.014,.022,.093,'dark');
     for(const z of [-.056,.006])cyl(.006,.017,s*.146,1.774,z,'steel','x');
    }
    tube([[-.10,1.756,-.114],[0,1.739,-.162],[.10,1.756,-.114]],.008,'webbing');
   }
   if(engineer){
    tube([[-.065,1.79,.135],[-.065,1.90,.025],[-.065,1.82,-.13]],.009,'yellow');tube([[.065,1.79,.135],[.065,1.90,.025],[.065,1.82,-.13]],.009,'yellow');
    // Thin visor and frame, face remains visible under raised front edge.
    pad(0,1.745,.16,.21,.045,.026,'glass');for(const s of [-1,1])beam([s*.13,1.78,0],[s*.115,1.72,.16],.009,'steel');
   }else{
    pad(0,1.78,.138,.18,.058,.028,'dark');for(const s of [-1,1])pad(s*.046,1.782,.157,.070,.034,.015,'glass');
    for(const s of [-1,1])pad(s*.137,1.765,-.025,.018,.046,.09,'steel');
   }
   for(const s of [-1,1])tube([[s*.11,1.76,0],[s*.09,1.62,.018],[s*.045,1.58,.065]],.009,'webbing');
   // Garment seams and folds stay broad enough to survive the normal camera.
   for(const s of [-1,1]){
    tube([[s*(stance+.10),.23,s*.05],[s*(stance+.11),.43,s*.05],[s*(stance+.10),.56,s*.05]],.0045,'seam');
    for(let j=0;j<3;j++)tube([[s*(stance-.055),.37+j*.055,.10+s*.04],[s*stance,.39+j*.05,.122+s*.04],[s*(stance+.055),.38+j*.055,.10+s*.04]],.006,'seam');
    pouch(s*.12,1.16,.209,.09,.10,.045);
   }
   // Layered carrier, open coloured chest centre and fine edge stitching.
   // All additions sit inside the existing silhouette and ground envelope.
   for(const s of [-1,1]){
    bar([s*(heavy?.18:.145),1.15,.191],[s*(heavy?.18:.145),1.36,.164],.012,.012,'seam');
    pad(s*.055,1.335,.195,.081,.035,.012,'webbing');
    for(let j=0;j<3;j++)pad(s*.055,1.205+j*.039,.196,.071,.013,.012,'paint');
    pad(s*.125,.78,.116,.075,.10,.023,'fabricPaint');
    pad(s*(stance+.018),.56,.153,.063,.079,.012,'paint');
    const radio=pad(s*.15,1.39,s<0?.125:-.177,.06,.085,.043,'dark');
    for(let j=0;j<3;j++)box(.034,.007,.008,s*.15,1.38+j*.014,s<0?.15:-.203,'steel');
   }
   pad(0,1.39,.145,.10,.03,.016,'seam');
   if(!engineer){
    // Faction stripe follows the new flat crown; no curved decal floating above it.
    pad(0,1.907,-.005,.043,.003,.128,'fabricPaint');
    for(const s of [-1,1]){
     pad(s*.126,1.701,-.015,.035,.065,.058,'dark');
     pad(s*.148,1.704,-.015,.013,.045,.038,'paint');
    }
    tube([[.14,1.69,.013],[.14,1.648,.09],[.045,1.648,.124]],.008,'dark');
   }
   if(key==='squad'){
    // Compact weapon with receiver, stock, magazine, handguard, optic and rail.
    const rifleStart=root.children.length;
    pad(.13,1.25,.37,.07,.105,.30,'steel');pad(.13,1.24,.15,.085,.11,.19,'dark');
    beam([.13,1.277,.47],[.13,1.277,.82],.021,'steel');cyl(.03,.07,.13,1.277,.85,'dark','z');
    pad(.13,1.17,.37,.055,.13,.085,'dark');pad(.13,1.25,.58,.08,.085,.20,'dark');for(let j=0;j<6;j++)box(.09,.012,.012,.13,1.30,.5+j*.026,'steel');
    cyl(.022,.12,.13,1.355,.36,'glass','z');pad(.13,1.326,.36,.025,.04,.075,'dark');
    for(const o of root.children.slice(rifleStart)){o.userData.weaponRole='rifle';o.userData.rigidEquipment=true;}
    pad(0,1.24,-.233,.27,.29,.13,'paint');handle(0,1.40,-.23,.11);pouch(.24,.92,-.05,.10,.13,.11);
    // Slim rifleman: segmented magazine carrier, radio and compact pack.
    for(let i=0;i<3;i++){pad(-.085+i*.085,1.10,.245,.063,.135,.03,'webbing');pad(-.085+i*.085,1.151,.265,.052,.023,.012,'seam');}
    for(const s of [-1,1])bar([s*.09,1.12,-.307],[s*.09,1.35,-.307],.015,.013,'webbing');
    pad(0,1.25,-.311,.14,.08,.017,'fabricPaint');
    const detailStart=root.children.length;pad(.13,1.25,.405,.079,.06,.065,'armor');for(let j=0;j<4;j++)box(.084,.009,.009,.13,1.292,.52+j*.034,'edge');for(const o of root.children.slice(detailStart)){o.userData.weaponRole='rifle';o.userData.rigidEquipment=true;}
   }else if(heavy){
    cyl(.091,1.04,.26,1.54,.19,'steel','z');for(const z of [-.35,.0,.48,.72])cyl(.112,.06,.26,1.54,z,z===.48?'yellow':'armor','z');
    cyl(.135,.10,.26,1.54,.78,'steel','z');cyl(.098,.012,.26,1.54,.837,'dark','z');cyl(.13,.13,.26,1.54,-.38,'armor','z');
    pad(.26,1.67,.30,.10,.07,.21,'dark');cyl(.032,.07,.26,1.69,.43,'glass','z');pad(.26,1.44,.16,.045,.12,.06,'dark');
    pad(0,1.25,-.25,.32,.39,.14,'paint');for(const s of [-1,1]){beam([s*.15,1.06,-.34],[s*.15,1.53,-.34],.015,'steel');}
    cyl(.071,.65,-.17,1.40,-.34,'steel');for(const y of [1.10,1.25,1.55,1.70])cyl(.081,.035,-.17,y,-.34,y===1.55?'yellow':'armor');
    // Heavy specialist: lower face guard, split chest armour and launcher's
    // sight / reinforced breech. Yellow safety band stays neutral for all teams.
    pad(0,1.635,.107,.145,.079,.034,'dark');for(const s of [-1,1])pad(s*.051,1.637,.13,.032,.046,.015,'steel');
    for(const s of [-1,1]){pad(s*.087,1.285,.196,.13,.16,.021,'paint');bar([s*.177,1.20,.20],[s*.177,1.36,.175],.018,.014,'edge');}
    for(const z of [-.23,-.14,-.05])cyl(.096,.025,.26,1.54,z,'dark','z');
    pad(.26,1.662,.51,.14,.05,.13,'armor');pad(.26,1.663,.58,.09,.028,.01,'glass');
    pad(0,1.25,-.331,.18,.16,.013,'fabricPaint');
   }else{
    pad(0,1.32,-.26,.34,.47,.17,'paint');for(const s of [-1,1])bar([s*.19,1.06,-.31],[s*.19,1.57,-.31],.025,.03,'steel');
    handle(0,1.58,-.26,.23);cyl(.13,.09,0,1.27,-.385,'steel','z');for(let i=0;i<9;i++){const tor=mesh(new T.TorusGeometry(.105+i%2*.003,.009,8,32),'yellow',0,1.27,-.365-i*.008);}
    const armA=[-.1,1.61,-.23],armB=[-.1,1.94,-.28],armC=[.13,2.03,-.27];bar(armA,armB,.045,.04,'steel');bar(armB,armC,.04,.04,'steel');hydraulic([-.1,1.66,-.23],[-.1,1.89,-.27],.014);
    for(const q of [armA,armB,armC])cyl(.035,.055,...q,'yellow','z');
    pad(-.34,.86,.06,.22,.25,.16,'armor');pad(-.34,.99,.06,.24,.035,.17,'yellow');for(const x of [-.41,-.27])pad(x,.90,.15,.023,.07,.02,'steel');
    pad(.35,.88,.13,.13,.20,.20,'yellow');pad(.35,.86,.25,.11,.12,.12,'steel');bar([.28,.95,.1],[.42,.95,.1],.025,.025,'dark');
    for(let i=0;i<3;i++)pouch(-.08+i*.08,1.15,.205,.065,.11,.05);
    // Engineer: hardhat lamp, restrained reflective braces, diagnostic screen
    // and latches. The vest/arms/backpack remain the large faction-colour areas.
    pad(0,1.785,.157,.072,.047,.023,'dark');pad(0,1.785,.171,.045,.029,.01,'lamp');
    for(const s of [-1,1]){bar([s*.14,1.20,.188],[s*.14,1.40,.133],.018,.012,'yellow');pad(s*.13,1.40,-.353,.025,.08,.02,'steel');}
    pad(.35,.944,.15,.094,.016,.105,'dark');pad(.35,.954,.15,.066,.008,.069,'glass');
    for(const x of [-.41,-.27]){pad(x,.81,.147,.026,.068,.015,'dark');pad(x,.837,.157,.028,.022,.012,'steel');}
    pad(0,1.49,-.355,.21,.025,.014,'yellow');
   }
  }else if(key==='ifv'||key==='ifv-repair'){
   for(const s of [-1,1])for(const z of [-1.65,-.59,.47,1.53])wheel(s*1.12,z,.47);
   body([[-2.12,.88,.63,1.72],[-1.48,1.15,.60,1.85],[.65,1.16,.59,1.45],[2.20,.68,.65,.96],[2.47,.46,.70,.83]],'armor');
   body([[-1.98,.78,1.64,2.03],[-1.32,.86,1.72,2.10],[.12,.84,1.36,1.91],[.70,.61,1.29,1.54]],'armor');
   for(const s of [-1,1]){
    sidePlate([[-2.02,1.05],[-1.60,.97],[1.47,.99],[2.23,.91],[1.29,1.39],[-1.53,1.77],[-2.02,1.62]],s*1.155,.07,'paint');
    for(const z of [-1.55,-.6,.35,1.22]){sidePlate([[z-.36,1.04],[z+.34,1.04],[z+.42,1.27],[z-.31,1.36]],s*1.25,.07,'paint');bolts(s*1.30,1.20,z,2,.32,'x');}
    for(const z of [-1.65,-.59,.47,1.53]){bevel(.45,.09,.75,.025,s*1.12,1.02,z,'armor');}
    beam([s*.77,1.91,-.95],[s*.77,1.91,-1.68],.023,'steel');handle(s*.73,2.13,-.45,.26,'x');
    const window=bevel(.05,.17,.42,.012,s*.89,1.72,.02,'glass');window.rotation.z=-s*.16;
   }
   // Hinged troop ramp and layered rear door, recognizable from behind.
   bevel(1.41,.83,.09,.025,0,1.24,-2.15,'paint');bevel(1.14,.62,.05,.02,0,1.26,-2.21,'armor');
   for(const s of [-1,1]){bevel(.12,.09,.12,.018,s*.53,.87,-2.23,'steel');handle(s*.37,1.52,-2.25,.17);}
   hatch(0,2.12,-1.06,.83,.70);vents(.0,1.99,-.15,.72,.48);
   // Break the broad shell into meaningful access panels and armor layers.
   hatch(-.40,1.53,1.15,.46,.42);hatch(.36,1.54,1.04,.42,.48);
   for(const s of [-1,1]){
    const cheek=bevel(.27,.075,.79,.022,s*.61,1.24,1.61,'paint');cheek.rotation.x=-.30;
    for(const z of [-1.54,-.80,-.07]){const panel=bevel(.075,.25,.56,.02,s*.88,1.90,z,'armor');panel.rotation.z=-s*.2;bolts(s*.94,1.94,z,2,.3,'x');}
    for(let j=0;j<4;j++)box(.13,.025,.035,s*.56,1.90,.23+j*.1,'steel');
    tube([[s*.98,1.53,-1.72],[s*1.01,1.57,-1.28],[s*.98,1.53,-.86]],.025,'dark');
    for(const z of [-1.8,-.7,.42,1.37])bevel(.12,.035,.10,.01,s*.97,1.55-z*.11,z,'steel');
   }
   for(const s of [-1,1]){const v=bevel(.34,.07,.24,.018,s*.33,1.46,1.02,'glass');v.rotation.x=-.20;handle(s*.66,1.08,1.92,.26,'x');}
   if(key==='ifv-repair'){
    hull(.85,.34,.85,0,1.96,.38,'paint',.8);
    bar([0,2.25,.30],[0,2.92,.0],.16,.18,'yellow');bar([0,2.92,.0],[0,2.80,1.25],.15,.17,'paint');
    beam([0,2.80,1.25],[0,2.40,1.25],.055,'steel');
    for(const s of [-1,1])bar([0,2.42,1.25],[s*.19,2.26,1.32],.06,.08,'steel');
    bevel(.50,.30,.70,.02,.62,2.10,-.25,'yellow');
   }else{
   cyl(.31,.16,-.27,1.94,.41,'steel');hull(.74,.29,.68,-.27,1.97,.41,'paint',.75);
   bevel(.25,.20,.47,.025,-.27,2.25,.62,'steel');beam([-.27,2.27,.78],[-.27,2.27,1.69],.034,'steel');cyl(.048,.11,-.27,2.27,1.7,'dark','z');
   bevel(.21,.17,.24,.024,-.60,2.26,.40,'armor');bevel(.14,.08,.025,.008,-.60,2.27,.54,'glass');
   }
   lamps(.61,.96,2.27);for(const s of [-1,1]){cyl(.08,.06,s*.48,.72,2.39,'steel','z');beam([s*.74,2.1,-1.58],[s*.74,2.70,-1.58],.011,'steel');}
  }else if(key==='arty'){
   for(const s of [-1,1])trackPod(s*1.02,-.1,4.75,.56,.37);
   body([[-2.36,.96,.72,1.13],[1.8,1.03,.72,1.21],[2.23,.75,.78,1.13]],'armor');
   cab(-.40,1.45,1.13);vents(.61,1.31,1.38,.58,.80);
   for(const s of [-1,1]){for(const z of [-1.65,-.78,.10])bevel(.08,.35,.75,.025,s*1.15,1.18,z,'paint');handle(s*1.06,1.42,.40,.45,'x');}
   // A real open cradle: separate side cheeks, trunnions, breech and recoil
   // cylinders. Elevation 13 degrees retains a distinctive frame in all views.
   cyl(.65,.12,0,1.26,-.98,'steel');cyl(.52,.13,0,1.37,-.98,'paint');
   for(const s of [-1,1]){
    sidePlate([[-1.65,1.45],[-.38,1.45],[-.48,2.30],[-.85,2.50],[-1.18,2.49]],s*.49,.13,'armor');
    cyl(.25,.18,s*.54,2.21,-.73,'steel','x');cyl(.16,.20,s*.54,2.21,-.73,'paint','x');
    hydraulic([s*.38,1.52,.22],[s*.38,2.30,-.45],.09);
    tube([[s*.55,1.58,-1.50],[s*.71,1.9,-1.57],[s*.67,2.2,-.91]],.025,'dark');
   }
   const breech=[0,2.17,-1.52],muzzle=[0,3.16,3.36];
   const cannon=beam(breech,muzzle,.105,'steel');bar([0,2.13,-1.58],[0,2.42,-.13],.33,.32,'steel');
   beam([0,2.25,-1.14],[0,2.67,.90],.16,'armor');
   for(const s of [-1,1])beam([s*.20,2.10,-1.40],[s*.20,2.50,.49],.07,'steel');
   const brake=bar([0,3.13,3.20],[0,3.21,3.59],.29,.25,'steel');for(let i=0;i<3;i++)for(const s of [-1,1])bevel(.018,.12,.055,.005,s*.15,3.16+i*.015,3.25+i*.10,'dark');
   // Barrel aperture aligned to the actual elevated bore.
   const aperture=cyl(.082,.014,0,3.213,3.602,'dark','z');aperture.rotation.x=Math.PI/2+.20;
   for(const s of [-1,1]){bar([s*.79,1.12,-2.08],[s*1.12,.65,-2.63],.15,.18,'steel');sidePlate([[-2.77,.55],[-2.40,.65],[-2.33,.95],[-2.51,1.01]],s*1.12,.32,'armor');}
   hatch(-.4,2.17,1.37,.53,.48);lamps(.76,1.02,2.20);
   for(const s of [-1,1]){bevel(.10,.12,.05,.01,s*.83,1.08,-2.41,'lamp');bar([s*.88,1.26,-1.78],[s*.88,1.70,-1.78],.03,.03,'steel');}
   for(const s of [-1,1]){
    for(const z of [-1.9,-1.2,-.5]){hatch(s*.78,1.27,z,.31,.52);bolts(s*.60,1.69,z,2,.24,'x');}
    tube([[s*.68,1.42,-1.72],[s*.69,1.83,-1.7],[s*.69,1.83,-.6],[s*.69,1.55,-.42]],.024,'steel');
    bevel(.23,.18,.55,.025,s*.84,1.55,-1.20,'steel');
   }
  }else if(key==='mcv'){
   // Four independent short track pods leave open corners and a broad deck.
   for(const s of [-1,1])for(const z of [-1.20,1.20]){trackPod(s*1.43,z,1.87,.65,.34);beam([s*.91,.76,z],[s*1.44,.76,z],.14,'steel');}
   bevel(2.45,.29,3.77,.07,0,.93,0,'steel');hull(2.69,.22,3.64,0,1.07,0,'armor',.92);cab(-.64,1.08,1.03);
   for(const s of [-1,1]){
    for(const z of [-1.45,-.48,.49]){bevel(.10,.39,.81,.025,s*1.21,1.29,z,'paint');bolts(s*1.28,1.38,z,2,.40,'x');}
    // Folded outriggers are clearly mechanical, and do not extend the footprint.
    for(const z of [-1.46,1.45]){bevel(.25,.20,.39,.035,s*1.17,.83,z,'steel');bevel(.27,.10,.40,.022,s*1.26,.63,z,'armor');}
    const a=[s*.99,1.40,-1.22],b=[s*.93,2.61,-.55],c=[s*.55,2.94,-.43];
    bar(a,b,.22,.28,'yellow');bar(b,c,.23,.27,'yellow');hydraulic([s*.98,1.62,-1.08],[s*.95,2.48,-.57],.075);
    for(const q of [a,b,c]){cyl(.18,.30,...q,'steel','x');cyl(.115,.32,...q,'yellow','x');}
    tube([[s*1.10,1.43,-1.33],[s*1.16,1.99,-1.06],[s*1.04,2.64,-.64],[s*.54,2.99,-.53]],.025,'dark');
   }
   bar([-.55,2.94,-.43],[.55,2.94,-.43],.25,.30,'yellow');
   cyl(.16,.64,0,2.92,-.43,'steel','x');for(let i=0;i<11;i++){const t=mesh(new T.TorusGeometry(.154,.012,8,24),'dark',-.29+i*.058,2.92,-.43);t.rotation.y=Math.PI/2;}
   beam([0,2.80,-.43],[0,2.29,-.43],.016,'steel');tube([[0,2.32,-.43],[.10,2.22,-.43],[.08,2.11,-.43],[-.03,2.09,-.43],[-.07,2.18,-.43]],.038,'yellow');
   // Winch, hydraulics and service modules remain visible through the portal.
   cyl(.29,.85,0,1.62,-.97,'steel','x');for(let i=0;i<14;i++){const t=mesh(new T.TorusGeometry(.25,.018,8,24),'dark',-.39+i*.06,1.62,-.97);t.rotation.y=Math.PI/2;}
   for(const x of [-.51,.51])cyl(.32,.055,x,1.62,-.97,'armor','x');
   for(const x of [-.54,.54]){bevel(.80,.54,.74,.05,x,1.56,-1.54,'paint');handle(x,1.86,-1.54,.3);for(const z of [-1.78,-1.30])bar([x-.42,1.30,z],[x-.42,1.79,z],.035,.035,'steel');}
   bevel(.72,.48,.81,.045,.56,1.55,.79,'paint');vents(.56,1.83,.79,.62,.65);
   for(const s of [-1,1]){for(const z of [.0,.53])beam([s*1.13,1.34,z],[s*1.13,1.73,z],.023,'yellow');beam([s*1.13,1.73,0],[s*1.13,1.73,.53],.024,'yellow');}
   lamps(.86,1.19,1.87);bevel(1.6,.28,.07,.018,0,1.19,-1.9,'paint');
   for(const s of [-1,1]){
    for(const z of [-1.64,-.78,.16]){bevel(.06,.26,.53,.02,s*1.25,1.32,z,'armor');for(const zz of [-.18,.18])cyl(.025,.08,s*1.29,1.40,z+zz,'bolt','x');}
    for(const z of [-1.19,1.19]){bevel(.28,.18,.38,.025,s*1.41,.99,z,'paint');handle(s*1.41,1.10,z,.23,'x');}
    for(const y of [1.12,1.31,1.50])bar([s*.99,y,1.76],[s*.99,y,2.02],.036,.035,'steel');
   }
   hatch(0,1.31,.48,.56,.63);bevel(.47,.34,.62,.03,.40,1.53,-.27,'steel');
   for(let j=0;j<6;j++)tube([[.3+j*.045,1.61,-.12],[.3+j*.045,1.94,-.10],[.3+j*.045,2.12,-.39]],.016,'dark');
  }else{
   // Mining machine: a small articulated tractor nose and a separate high ore
   // tub on four giant tires. No common truck cab/chassis with the engineer.
   for(const s of [-1,1])for(const z of [-1.48,1.18])wheel(s*1.18,z,.70);
   body([[.34,.69,.72,1.06],[1.28,.91,.73,1.15],[2.20,.64,.65,.91],[2.70,.57,.32,.68]],'armor');
   hull(1.80,.36,2.40,0,.98,-1.24,'steel',.94);cyl(.27,.47,0,.96,.02,'steel');
   for(const s of [-1,1]){hydraulic([s*.59,.98,.48],[s*.67,1.0,-.45],.085);tube([[s*.39,1.13,.45],[s*.48,1.30,.05],[s*.4,1.16,-.49]],.028,'dark');}
   cab(-.39,1.17,.99);hatch(.51,1.23,1.01,.48,.55);
   for(const s of [-1,1]){
    sidePlate([[.48,1.19],[1.67,1.19],[1.87,.94],[1.35,1.36],[.53,1.42]],s*1.09,.12,'paint');
    bevel(.54,.10,1.35,.035,s*1.12,1.50,1.18,'armor');
    for(const z of [.60,1.7])bolts(s*1.18,1.31,z,2,.15,'x');
   }
   // Broad toothed intake nose with open mouth, ribs and an exposed roller.
   bevel(1.37,.11,.76,.028,0,.35,2.35,'steel');bevel(1.30,.30,.10,.025,0,.60,2.17,'dark');cyl(.15,1.18,0,.54,2.28,'steel','x');
   for(let i=0;i<8;i++)cyl(.17,.045,-.52+i*.15,.54,2.28,'dark','x');
   for(const s of [-1,1])sidePlate([[1.89,.47],[2.82,.35],[2.56,.70],[1.89,.92]],s*.68,.12,'armor');
   for(let i=0;i<5;i++){const tooth=bevel(.15,.09,.40,.018,-.52+i*.26,.31,2.76,'steel');tooth.rotation.x=-.12;}
   // Open tapered bin: shaped side walls, visible inner floor and external ribs.
   bevel(1.73,.12,2.21,.035,0,1.43,-1.30,'dark');
   for(const s of [-1,1]){
    sidePlate([[-2.48,1.50],[-2.61,2.51],[-.28,2.38],[.0,1.62],[-.48,1.46]],s*1.04,.11,'paint');
    for(const z of [-2.27,-1.70,-1.13,-.56]){bar([s*1.11,1.53,z+.11],[s*1.17,2.46,z],.10,.10,'steel');}
    tube([[s*1.05,2.52,-2.60],[s*1.09,2.52,-1.6],[s*1.06,2.40,-.28]],.06,'edge');
    hydraulic([s*.81,1.05,-.45],[s*.96,2.14,-1.41],.10);
   }
   const back=bevel(2.10,.93,.12,.04,0,2.01,-2.52,'paint');back.rotation.x=-.12;
   const front=bevel(2.06,.80,.12,.035,0,1.98,-.24,'paint');front.rotation.x=.30;
   for(const s of [-1,1]){bevel(.19,.19,.25,.02,s*.79,1.57,-2.54,'steel');cyl(.07,.25,s*.79,1.56,-2.58,'bolt','x');}
   beam([-1.08,2.51,-2.58],[1.08,2.51,-2.58],.06,'edge');
   if(key==='miner-full')for(let i=0;i<34;i++){
    const x=(((i*7)%11)/10-.5)*1.55,z=-2.20+(i%7)*.26,y=1.92+.25*(1-Math.abs(x))+(i%3)*.07;
    const stone=mesh(new T.DodecahedronGeometry(.19+(i%4)*.025,0),'ore',x,y,z);stone.rotation.set(i*.76,i*.31,i*.48);stone.scale.set(.8+(i%3)*.13,.55+(i%3)*.16,1.1);
   }
   lamps(.54,.86,2.20);for(const s of [-1,1]){bevel(.18,.14,.05,.012,s*.88,1.41,-2.6,'lamp');handle(s*.88,1.87,-.13,.37,'x','yellow');}
   for(const s of [-1,1]){
    for(const z of [-2.23,-1.65,-1.08,-.5]){bevel(.045,.21,.17,.012,s*1.13,2.26,z,'armor');cyl(.027,.06,s*1.15,2.28,z,'bolt','x');}
    for(const y of [1.02,1.20,1.38])bar([s*.81,y,.35],[s*.81,y,.65],.035,.035,'steel');
    tube([[s*.76,1.29,-.18],[s*.98,1.63,-.59],[s*.96,1.77,-.98]],.025,'dark');
    for(const z of [1.04,1.42])bevel(.28,.05,.08,.012,s*1.12,1.57,z,'steel');
   }
   vents(.37,1.28,1.49,.54,.46);bevel(.55,.06,.65,.025,0,.92,1.95,'paint');
  }
  if(wreck){root.scale.y=.58;root.rotation.z=.07;}
  root.userData.designRevision='v04';root.userData.reference='art/review/unit-redesign-v04';return root;
 }
 return {build,specs};
})();
