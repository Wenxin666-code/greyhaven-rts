/* Editable, hard-surface architectural masters. One unit is one ground cell.
   Foundation extents and camera are contractual; identity comes from structure.
   Faction paint is a separate occluded material pass, never a red/blue texture. */
window.BuildingMaster=(()=>{
 const T=THREE;
 const specs={hq:{w:3,d:2,name:'指挥部'},power:{w:2,d:2,name:'电站'},barracks:{w:2,d:2,name:'兵营'},factory:{w:3,d:2,name:'车辆工厂'},radar:{w:2,d:2,name:'雷达站'},post:{w:1,d:1,name:'防御阵地'},refinery:{w:2,d:2,name:'矿石精炼厂'},harbormaster:{w:2,d:2,name:'港务中心'}};
 let coating;
 specs.satellite={w:2,d:2,name:'间谍卫星上行站'};
 Object.assign(specs,{residence:{w:2,d:2,name:'河岸民居'},shop:{w:3,d:2,name:'沿街商住楼'},municipal:{w:3,d:3,name:'镇公所'}});
 const ready=new Promise((resolve,reject)=>new T.TextureLoader().load(new URL('../tank-prototype/coated-steel.png',document.currentScript.src).href,t=>{coating=t;coating.wrapS=coating.wrapT=T.RepeatWrapping;coating.colorSpace=T.SRGBColorSpace;resolve();},undefined,reject));
 function build(key,wreck=false,phase=0){
  const spec=specs[key],root=new T.Group();root.name='Greyhaven '+key+' v05';
  const p={};
  function material(name,color,roughness=.65,metalness=.2){const m=new T.MeshStandardMaterial({color:wreck?0x51534b:color,roughness:wreck?.94:roughness,metalness:wreck?.08:metalness});m.name=name;p[name]=m;return m;}
  material('concrete',0x777b70,.92,.01);material('wall',0x999b8c,.77,.16);material('edge',0xb2b5a6,.62,.3);material('steel',0x555e59,.5,.5);material('dark',0x242c2b,.87,.06);material('glass',0x243d42,.29,.45);material('paint',0xf4f4f4,.60,.22);material('yellow',0xbca05b,.68,.2);material('light',0xe9d7a5,.42,.1);material('ore',0xa48b56,.87,.04);
  p.paint.userData.factionPaint=true;
  for(const k of ['wall','edge','steel','paint']){p[k].map=coating;p[k].bumpMap=coating;p[k].bumpScale=.008;p[k].envMapIntensity=.8;}
  function mesh(geo,m,x=0,y=0,z=0,parent=root){
   geo=geo.index?geo.toNonIndexed():geo;
   const a=geo.attributes.position,n=geo.attributes.normal,uv=[];
   for(let i=0;i<a.count;i++){const ax=Math.abs(n.getX(i)),ay=Math.abs(n.getY(i));uv.push((ay>.6?a.getX(i):ax>.6?a.getZ(i):a.getX(i))*1.8,(ay>.6?a.getZ(i):a.getY(i))*1.8);}
   geo.setAttribute('uv',new T.Float32BufferAttribute(uv,2));
   const o=new T.Mesh(geo,p[m]);o.position.set(x,y,z);o.castShadow=true;o.receiveShadow=true;parent.add(o);return o;
  }
  const box=(x,y,z,w,h,d,m,parent=root)=>mesh(new T.BoxGeometry(w,h,d),m,x,y,z,parent);
  function bevel(x,y,z,w,h,d,m,r=.025,parent=root){
   r=Math.min(r,w*.2,h*.2,d*.2);const s=new T.Shape(),a=w/2-r,b=h/2-r;
   s.moveTo(-a,-b);s.lineTo(a,-b);s.lineTo(a,b);s.lineTo(-a,b);s.closePath();
   const g=new T.ExtrudeGeometry(s,{depth:d-2*r,steps:1,bevelEnabled:true,bevelSegments:1,bevelSize:r,bevelThickness:r,curveSegments:1});g.translate(0,0,-(d-2*r)/2);return mesh(g,m,x,y,z,parent);
  }
  function beam(a,b,width,depth,m,parent=root){
   const v=new T.Vector3(...b).sub(new T.Vector3(...a));const o=box(0,0,0,width,v.length(),depth,m,parent);o.position.copy(new T.Vector3(...a).addScaledVector(v,.5));o.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),v.normalize());return o;
  }
  function prism(x,y,z,w,h,d,m,top=.85){
   const shape=[[-1,-.78],[-.78,-1],[.78,-1],[1,-.78],[1,.78],[.78,1],[-.78,1],[-1,.78]],v=[],idx=[];
   for(let j=0;j<2;j++)for(const [a,b]of shape)v.push(x+a*w/2*(j?top:1),y+j*h,z+b*d/2*(j?top:1));
   for(let i=0;i<8;i++){const n=(i+1)%8;idx.push(i,n,i+8,n,n+8,i+8);if(i>0&&i<7)idx.push(8,8+i,8+i+1,0,i+1,i);}
   const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(v,3));
   // X/Z perimeter runs counter-clockwise seen from below: reverse winding
   // so the roof and chamfered walls face outward (also for the paint pass).
   g.setIndex(idx.reduce((a,_,i)=>i%3?a:a.concat(idx[i],idx[i+2],idx[i+1]),[]));const q=g.toNonIndexed();q.computeVertexNormals();return mesh(q,m);
  }
  function vent(x,y,z,w,d,n=6){
   bevel(x,y,z,w,.045,d,'dark',.008);
   for(let i=0;i<n;i++)box(x-w*.42+i*w*.84/(n-1),y+.029,z,.022,.024,d*.84,'steel');
  }
  function fan(x,y,z,r){
   mesh(new T.CylinderGeometry(r,r,.05,8),'steel',x,y,z);
   mesh(new T.CylinderGeometry(r*.80,r*.80,.055,8),'dark',x,y+.014,z);
   for(let i=0;i<6;i++){const a=i*Math.PI/3;const o=box(x+Math.sin(a)*r*.37,y+.048,z+Math.cos(a)*r*.37,r*.34,.016,r*.68,'edge');o.rotation.y=a+.25;}
   mesh(new T.CylinderGeometry(r*.16,r*.16,.035,8),'steel',x,y+.055,z);
  }
  function bolts(x,y,z,w,h,n=3){for(const s of [-1,1])for(let i=0;i<n;i++)box(x-w*.42+i*w*.84/(n-1),y+s*h*.38,z,.025,.025,.018,'edge');}
  function panelFront(x,y,z,w,h,paint=true){bevel(x,y,z,w,h,.04,paint?'paint':'steel',.012);bolts(x,y,z+.024,w,h);}
  function windows(x,y,z,w,n=4){
   box(x,y,z,w,.18,.025,'dark');
   for(let i=0;i<n;i++){box(x-w*.5+(i+.5)*w/n,y,z+.018,w/n-.022,.14,.018,'glass');box(x-w*.5+(i+.5)*w/n,y+.065,z+.032,w/n-.022,.014,.016,'edge');}
  }
  function door(x,y,z,w=.28,h=.46){
   bevel(x,y,z,w+.07,h+.05,.07,'steel',.014);box(x,y,z+.04,w,h,.025,'dark');box(x,y,z+.055,w-.04,h-.04,.015,'wall');box(x+w*.28,y-.015,z+.07,.025,.07,.02,'yellow');box(x,y+h*.38,z+.068,w*.52,.035,.015,'glass');
  }
  function rail(x,z,w,y){
   for(let i=0;i<=4;i++)box(x-w/2+i*w/4,y-.1,z,.022,.24,.022,'steel');box(x,y+.02,z,w,.025,.025,'edge');box(x,y-.075,z,w,.018,.018,'steel');
  }
  function stairs(x,z,w,n=3){for(let i=0;i<n;i++)box(x,.04+i*.035,z-i*.07,w,.04+i*.07,.09,'concrete');}
  function hazard(x,z,w){for(let i=0;i<8;i++){const o=box(x-w/2+(i+.5)*w/8,.077,z,w/16,.009,.095,'yellow');o.rotation.y=-.35;}}
  function antenna(x,z,h,start){box(x,start+h/2,z,.025,h,.025,'steel');box(x,start+h,z,.055,.055,.055,'light');beam([x-.13,start+h*.65,z],[x+.13,start+h*.65,z],.016,.016,'edge');}
  // A thin slab defines exact cell contact, rather than an oversized decorative apron.
  box(0,.026,0,spec.w,.052,spec.d,'concrete');
  for(const x of [-1,1])box(x*(spec.w/2-.025),.057,0,.025,.016,spec.d-.04,'edge');
  for(const z of [-1,1])box(0,.057,z*(spec.d/2-.025),spec.w-.04,.016,.025,'edge');
  if(wreck){
   // Keep the building's characteristic machinery in its own debris footprint.
   for(let i=0;i<19;i++){
    const x=(((i*17)%29)/28-.5)*(spec.w-.4),z=(((i*11)%23)/22-.5)*(spec.d-.4);
    const o=bevel(x,.10+(i%3)*.045,z,.13+(i%4)*.06,.10+(i%5)*.035,.15+(i%3)*.05,i%3?'wall':'dark',.015);o.rotation.y=i*1.7;
   }
   for(let i=0;i<4;i++)box((i%2-.5)*(spec.w-.42),.24,(Math.floor(i/2)-.5)*(spec.d-.42),.09,.38,.24,'steel');
   if(key==='power')for(const x of [-.45,.4])prism(x,.06,0,.48,.25,1.25,'steel');
   if(key==='radar'){const o=bevel(0,.26,0,1.2,.52,.1,'steel');o.rotation.z=.6;o.rotation.x=.8;}
   if(key==='refinery')prism(.4,.06,-.35,.65,.5,.65,'steel');
   if(key==='factory')beam([-1.2,.12,-.45],[1.15,.4,.38],.14,.13,'steel');
   if(key==='hq'){box(-.45,.20,-.58,1.7,.30,.12,'wall');bevel(.65,.29,-.31,.73,.40,.78,'wall');const o=bevel(.62,.55,-.18,.88,.07,.86,'steel');o.rotation.z=.18;}
   if(key==='barracks')for(const x of [-.48,.48]){box(x,.17,-.57,.64,.23,.11,'wall');beam([x-.28,.12,.3],[x+.24,.43,-.2],.055,.06,'steel');}
   if(key==='post'){prism(0,.06,0,.79,.17,.76,'wall',.85);beam([-.16,.20,-.18],[.16,.26,.32],.05,.045,'steel');}
   if(key==='harbormaster')bevel(.24,.30,-.25,.82,.43,.75,'wall');
   return root;
  }
  if(key==='residence'||key==='shop'||key==='municipal'){
   const civic=key==='municipal',store=key==='shop',w=spec.w-.20,d=spec.d-.22;
   p.wall.color.set(store?0xa9957c:civic?0xa7a99b:0xa6a18c);p.wall.metalness=.02;p.wall.roughness=.96;
   box(0,.54,0,w,.96,d,'wall');
   box(0,.18,0,w+.02,.20,d+.02,'concrete');
   const rows=civic?2:1;
   if(civic)box(0,1.20,-.28,w-.48,.56,d-.62,'wall');
   for(let row=0;row<rows;row++){
    const y=.68+row*.58,front=row===0?d/2+.015:(d-.62)/2-.28+.015;
    for(const s of [-1,1])windows(s*(store?.91:civic?.83:.50),y,front,store?.56:.38,2);
    for(const s of [-1,1])for(const z of [-.49,.20]){
     box(s*(w/2+.012),y,z,.025,.25,.29,'dark');box(s*(w/2+.028),y,z,.018,.19,.23,'glass');
     box(s*(w/2+.04),y-.14,z,.045,.035,.35,'edge');
    }
   }
   door(0,.36,d/2+.04,store?.42:.31,.52);stairs(0,spec.d/2-.07,.52,2);
   if(civic){
    bevel(0,1.51,-.28,w-.28,.10,d-.43,'paint');
    bevel(0,1.02,d/2-.2,.85,.10,.58,'paint');
    for(const x of [-.34,.34])box(x,.50,d/2-.04,.07,.86,.08,'concrete');
    box(.79,1.64,-.6,.36,.19,.38,'steel');antenna(-.65,-.75,.40,1.58);
   }else{
    // Faceted pitched roof with visible seams; faction-colour fascia and awning.
    const shape=new T.Shape();shape.moveTo(-w/2-.04,0);shape.lineTo(0,.46);shape.lineTo(w/2+.04,0);shape.closePath();
    const roof=new T.ExtrudeGeometry(shape,{depth:d+.08,bevelEnabled:false});roof.translate(0,0,-(d+.08)/2);mesh(roof,'steel',0,1.02,0);
    for(let z=-d/2;z<=d/2;z+=.16){beam([-w/2-.04,1.025,z],[0,1.49,z],.012,.014,'edge');beam([0,1.49,z],[w/2+.04,1.025,z],.012,.014,'edge');}
    box(0,1.035,d/2+.05,w+.10,.12,.05,'paint');box(0,1.035,-d/2-.05,w+.10,.12,.05,'paint');
    box(-w*.28,1.45,-d*.23,.20,.50,.20,'wall');box(-w*.28,1.72,-d*.23,.25,.06,.25,'dark');
    bevel(0,.87,d/2+.13,store?1.12:.65,.085,.27,'paint',.006);
   }
   for(const s of [-1,1]){box(s*(w/2-.04),.58,d/2+.025,.07,.76,.055,'edge');box(s*(w/2-.04),.27,d/2+.06,.08,.15,.02,'paint');}
   if(store){box(-.84,.39,d/2+.02,.52,.32,.025,'glass');box(.84,.39,d/2+.02,.52,.32,.025,'glass');}
  }
  if(key==='hq'){
   // Low, stepped command bunker with asymmetric raised operations bridge.
   prism(0,.06,-.04,2.65,.57,1.62,'wall',.94);
   bevel(-.48,.69,-.12,1.55,.21,1.25,'paint',.055);
   bevel(.72,.83,-.34,.82,.78,.95,'wall',.055);
   bevel(.72,1.24,-.34,.94,.10,1.06,'paint');
   windows(.72,1.04,.158,.66,4);windows(-.45,.43,.754,1.2,5);
   for(const x of [-1.15,-.8,-.45,-.1])panelFront(x,.27,.775,.29,.17);
   door(.64,.32,.80,.34,.47);stairs(.64,.94,.5,3);
   vent(-.65,.82,-.35,.7,.34);fan(-.26,.83,.24,.18);
   antenna(.93,-.6,.43,1.3);antenna(.48,-.61,.25,1.3);
   box(.72,.89,.18,.9,.04,.09,'steel');
   for(const z of [-.55,-.1,.35]){bevel(1.33,.4,z,.035,.28,.32,'paint',.006);box(1.35,.56,z,.035,.028,.34,'edge');}
   rail(-.51,.45,1.30,.92);
   for(const x of [-1.14,-.77,-.40,-.03]){box(x,.54,.767,.018,.17,.015,'steel');box(x,.12,.794,.26,.025,.025,'steel');}
   for(const z of [-.57,-.18,.21,.60]){box(1.337,.34,z,.018,.35,.018,'steel');box(1.35,.18,z,.023,.027,.026,'edge');}
  }else if(key==='barracks'){
   // Two narrow accommodation wings; a central access court remains visible.
   for(const x of [-.48,.48]){
    bevel(x,.35,-.12,.66,.58,1.40,'wall');
    prism(x,.63,-.12,.76,.20,1.49,'paint',.76);
    for(const z of [-.65,-.20,.25])box(x,.848,z,.48,.025,.023,'edge');
    windows(x,.40,.604,.45,3);door(x,.29,.62,.23,.38);
    for(let i=0;i<4;i++)box(x+.34,.39,-.60+i*.29,.023,.15,.16,'glass');
    vent(x,.85,-.38,.3,.26,4);
    for(const z of [-.52,-.08,.36]){box(x+.337,.23,z,.022,.20,.018,'edge');box(x+.349,.13,z,.014,.03,.07,'steel');}
   }
   box(0,.21,-.65,.27,.3,.27,'steel');antenna(0,-.65,.55,.37);
   for(let i=0;i<4;i++)box(0,.064,-.33+i*.24,.22,.012,.15,'edge');
   for(const x of [-.78,.78])panelFront(x,.24,.72,.12,.23,false);
   box(0,.08,.79,1.65,.05,.20,'concrete');hazard(0,.90,.65);
  }else if(key==='factory'){
   // Wide open-span hall, side service spine, and a structural gantry roof.
   bevel(0,.59,-.13,2.66,1.04,1.47,'wall',.045);
   bevel(0,1.15,-.13,2.78,.15,1.58,'paint',.04);
   box(0,1.235,-.13,2.44,.035,1.24,'dark');
   for(const x of [-1.04,-.52,0,.52,1.04]){bevel(x,1.28,-.10,.27,.12,1.25,'paint');box(x,1.35,-.10,.1,.015,.97,'edge');}
   for(const x of [-1.30,1.30])for(const z of [-.75,.58]){
    bevel(x,.62,z,.12,1.12,.13,'steel');bevel(x,.45,z+.06,.07,.48,.018,'paint',.004);
   }
   const gate={left:-1.05,right:.67,bottom:.11,top:1.03,z:.625};root.userData.gate=gate;
   box((gate.left+gate.right)/2,.57,.616,1.83,.98,.035,'dark');
   for(let i=0;i<10;i++)bevel(-.19,.153+i*.087,.645,1.70,.076,.04,'steel',.006);
   for(const x of [-.95,.57])box(x,.57,.676,.028,.88,.025,'edge');
   box(-.19,1.06,.66,1.95,.09,.13,'edge');
   for(const x of [-1.13,.76])box(x,1.04,.74,.08,.045,.045,'light');
   panelFront(1.03,.72,.64,.36,.46);door(1.03,.27,.67,.24,.35);
   box(-.18,.068,.81,1.79,.03,.29,'steel');hazard(-.18,.94,1.85);
   for(let i=0;i<6;i++)box(1.35,.48,-.58+i*.20,.035,.28,.11,'dark');
   fan(.93,1.32,-.38,.18);antenna(1.18,-.68,.24,1.25);
  }else if(key==='power'){
   // Paired angular generator skids, heat exchangers and squared exhaust stacks.
   for(const x of [-.45,.45]){
    bevel(x,.29,0,.76,.42,1.65,'steel');prism(x,.46,-.12,.72,.36,1.22,'paint',.78);
    fan(x,.85,.03,.23);fan(x,.85,-.46,.20);
    bevel(x,.35,.78,.57,.32,.12,'dark');
    for(let j=0;j<6;j++)box(x-.23+j*.092,.36,.855,.025,.26,.035,'edge');
    box(x,.89,-.69,.20,1.43,.20,'steel');bevel(x,1.61,-.69,.27,.12,.27,'edge');box(x,1.677,-.69,.19,.014,.19,'dark');
    for(const y of [.64,1.04,1.44])box(x,y,-.58,.24,.04,.04,'paint');
    beam([x-.27,.25,.61],[x-.27,.57,.61],.055,.055,'yellow');beam([x-.27,.57,.61],[x-.27,.57,.22],.055,.055,'yellow');
   }
   bevel(0,.29,.66,.14,.4,.25,'wall');
   box(.88,.27,.20,.07,.25,.55,'paint');
  }else if(key==='radar'){
   // Rectangular phased-array panel rather than a generic spherical dish.
   prism(0,.06,0,1.68,.45,1.62,'wall',.89);bevel(0,.54,0,1.69,.13,1.60,'paint');
   door(-.43,.28,.81,.29,.4);windows(.38,.35,.81,.53,3);
   bevel(0,.89,-.12,.32,.65,.36,'steel');
   beam([-.55,.62,-.31],[0,1.14,-.12],.08,.08,'edge');beam([.55,.62,-.31],[0,1.14,-.12],.08,.08,'edge');
   const array=new T.Group();array.position.set(0,1.37,-.12);array.rotation.x=-.23;root.add(array);
   bevel(0,0,0,1.5,.70,.14,'steel',.04,array);bevel(0,0,.08,1.37,.57,.035,'paint',.015,array);
   for(let x=0;x<8;x++)for(let y=0;y<3;y++)box(-.59+x*.169,-.18+y*.18,.109,.136,.13,.018,'dark',array);
   for(const x of [-.68,.68])box(x,0,.12,.035,.55,.025,'edge',array);
   vent(.47,.62,.33,.45,.30,5);antenna(-.65,-.60,.55,.62);
  }else if(key==='satellite'){
   prism(0,.06,0,1.76,.40,1.72,'wall',.92);bevel(0,.49,0,1.73,.12,1.68,'paint');
   door(-.42,.28,.87,.31,.36);windows(.36,.30,.86,.60,3);
   prism(-.12,.55,-.16,.48,.52,.48,'steel');
   const dish=new T.Group();dish.position.set(-.12,1.12,-.16);dish.rotation.x=.48;dish.rotation.z=-.22;root.add(dish);
   // Faceted parabolic antenna: visibly distinct from the radar's flat array.
   for(let i=0;i<12;i++){
    const a=i*Math.PI/6,b=(i+1)*Math.PI/6,r=.78;
    const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute([0,0,0,Math.cos(a)*r,.30,Math.sin(a)*r,Math.cos(b)*r,.30,Math.sin(b)*r],3));geo.computeVertexNormals();
    const face=mesh(geo,i%3===0?'paint':'edge',0,0,0,dish);face.material=face.material.clone();face.material.side=T.DoubleSide;
    beam([0,0,0],[Math.cos(a)*r,.30,Math.sin(a)*r],.018,.018,'steel',dish);
   }
   beam([-.65,.25,0],[0,.72,0],.025,.025,'steel',dish);beam([.65,.25,0],[0,.72,0],.025,.025,'steel',dish);box(0,.73,0,.12,.14,.14,'paint',dish);
   vent(.59,.59,.45,.40,.40,5);antenna(-.76,.62,.55,.70);
  }else if(key==='post'){
   // Compact low pillbox, narrow firing slit and offset remote gun cradle.
   prism(0,.06,0,.90,.38,.87,'wall',.72);prism(0,.44,0,.77,.12,.73,'paint',.89);
   box(0,.29,.385,.49,.065,.025,'dark');box(0,.33,.405,.54,.03,.045,'edge');
   bevel(0,.64,-.06,.30,.18,.31,'steel');bevel(-.12,.72,-.09,.11,.25,.24,'paint');
   beam([.045,.73,-.05],[.045,.75,.46],.036,.045,'dark');box(.045,.75,.44,.06,.055,.08,'steel');
   box(.16,.68,-.12,.11,.10,.16,'yellow');antenna(-.24,-.25,.25,.55);
   for(const x of [-.36,.36])panelFront(x,.22,.33,.10,.15);
  }else if(key==='refinery'){
   // Faceted ore hopper + open diagonal truss conveyor + processing cabinet.
   bevel(-.48,.42,-.18,.73,.70,1.33,'wall');bevel(-.48,.83,-.18,.80,.14,1.40,'paint');
   vent(-.48,.92,-.43,.45,.54,5);windows(-.48,.52,.50,.47,3);
   prism(.42,.08,-.42,.79,1.22,.83,'steel',.85);prism(.42,1.30,-.42,.83,.17,.86,'paint',.83);
   for(const y of [.30,.69,1.09])bevel(.42,y,-.42,.82,.055,.85,'edge',.012);
   for(const x of [.25,.58])box(x,.81,.02,.025,1.10,.025,'yellow');
   for(let i=0;i<8;i++)box(.415,.30+i*.135,.04,.35,.024,.028,'yellow');
   // Transfer belt and cross bracing fit entirely within the two-cell lot.
   for(const x of [.05,.82])beam([x,.18,.87],[x,.78,.16],.065,.065,'steel');
   const belt=box(.435,.46,.52,.69,.055,.93,'dark');belt.rotation.x=.70;
   for(let i=0;i<10;i++){
    const q=((i+phase)/10)%1,z=.86-q*.69,y=.20+q*.58;
    const cleat=box(.435,y+.03,z,.65,.035,.045,'edge');cleat.rotation.x=.70;
    if(i%3===0)prism(.23+(i%2)*.30,y+.05,z,.14,.09,.12,'ore',.6);
   }
   for(const x of [.05,.82]){beam([x,.18,.87],[x,.18,.18],.04,.04,'yellow');beam([x,.18,.87],[x,.73,.18],.028,.028,'edge');}
   bevel(-.48,.21,.65,.55,.25,.30,'paint');hazard(.42,.94,.83);
   box(-.83,.47,.28,.06,.37,.36,'steel');
  }else if(key==='harbormaster'){
   bevel(0,.39,0,1.63,.67,1.57,'wall');bevel(0,.77,0,1.74,.13,1.65,'paint');
   bevel(.28,1.07,-.25,.95,.53,.85,'wall');bevel(.28,1.37,-.25,1.06,.10,.96,'yellow');
   windows(.28,1.10,.19,.78,5);windows(-.35,.44,.80,.70,4);door(.43,.32,.80,.32,.46);
   antenna(.52,-.40,.45,1.43);vent(-.49,.86,-.29,.37,.44,5);stairs(.43,.94,.45,3);
  }
  return root;
 }
 return {specs,ready,build};
})();
