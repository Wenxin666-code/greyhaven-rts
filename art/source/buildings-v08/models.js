/* V07 concept -> V08 editable masters. Shared workmanship, distinct functions.
   The foundation dimensions are the simulation contract; all ground fittings fit. */
window.BuildingMasterV08=(()=>{
 const T=THREE,K=ArchitectureKit;
 const specs={hq:{w:3,d:2,name:'指挥部'},barracks:{w:2,d:2,name:'兵营'},factory:{w:3,d:2,name:'车辆工厂'},power:{w:2,d:2,name:'电站'},radar:{w:2,d:2,name:'雷达站'},satellite:{w:2,d:2,name:'间谍卫星上行站'},post:{w:1,d:1,name:'防御阵地'},refinery:{w:2,d:2,name:'矿石精炼厂'},residence:{w:2,d:2,name:'河岸民居'},shop:{w:3,d:2,name:'沿街商住楼'},municipal:{w:3,d:3,name:'镇公所'}};
 function build(key){
  const s=specs[key];if(!s)throw Error('Unknown master '+key);const k=K.create(key,s);
  const {part,moving,box,bevel,prism,gable,cylinder,beam,pipe,panel,sidePanel,vent,roofVent,door,windowBand,sideWindows,lamp,stairs,rail,rooftop,hvac,fan,antenna,utilityMast,ladder,hazard}=k;
  const shell=(name,x,z,w,d,h,opts={})=>{
   part(name+'-structure','shell',[x,.055,z],()=>{
    prism(x,.055,z,w,h,d,opts.masonry?'brick':'wall',.97);
    for(const sx of [-1,1])for(const sz of [-1,1])bevel(x+sx*(w/2-.045),h/2+.06,z+sz*(d/2-.045),.085,h+.025,.085,'edge',.012);
    box(x,.13,z+d/2+.008,w,.08,.045,'steel');box(x+w/2+.009,.13,z,.045,.08,d,'steel');
   });
   part(name+'-roof','roof',[x,h+.055,z],()=>{
    prism(x,h+.055,z,w+.035,.13,d+.035,opts.plain?'wall':'paint',.92);
    bevel(x,h+.20,z,w*.91,.04,d*.89,'edge',.012);
    for(let ix=0;ix<3;ix++)for(let iz=0;iz<2;iz++){
     const px=x-w*.29+ix*w*.29,pz=z-d*.25+iz*d*.50;
     bevel(px,h+.225,pz,w*.275,.014,d*.46,opts.plain?'wall':'paint',.003);
     for(const sign of [-1,1])cylinder(px+sign*w*.10,h+.239,pz-d*.15,.008,.006,'steel',6);
    }
    for(const sz of [-1,1])rail([x-w*.42,h+.225,z+sz*d*.41],[x+w*.42,h+.225,z+sz*d*.41],.085);
   });
  };
  const facades=(name,x,z,w,d,h,doors=true)=>part(name+'-armored-panels','cladding',[x,.08,z],()=>{
   const n=Math.max(2,Math.round(w/.43));
   for(let i=0;i<n;i++){let xx=x-w*.43+(i+.5)*w*.86/n;panel(xx,.34,z+d/2+.018,w*.76/n,.34);}
   const count=Math.max(2,Math.round(d/.42));for(let i=0;i<count;i++)sidePanel(x+w/2+.018,.36,z-d*.43+(i+.5)*d*.86/count,d*.74/count,.36);
   if(doors)door(x-w*.16,.08,z+d/2+.038,.35,Math.min(.65,h*.75));
  });
  const crown=(name,x,y,z,w,d)=>part(name+'-service-roof','systems',[x,y,z],()=>{
   hvac(x-w*.22,y+.05,z-d*.16,w*.24,d*.22);fan(x+w*.08,y+.09,z-d*.17,Math.min(w,d)*.12);
   roofVent(x+w*.28,y+.06,z+d*.20,w*.21,d*.22);antenna(x-w*.40,y+.04,z-d*.34,.48);
  });
  part('cell-aligned-foundation','foundation',[0,0,0],()=>{
   bevel(0,.025,0,s.w,.05,s.d,'concrete',.007);
   for(let x=1;x<s.w;x++)box(x-s.w/2,.052,0,.008,.003,s.d-.03,'steel');
   for(let z=1;z<s.d;z++)box(0,.052,z-s.d/2,s.w-.03,.003,.008,'steel');
   for(const sx of [-1,1])for(const sz of [-1,1])box(sx*(s.w/2-.05),.055,sz*(s.d/2-.05),.06,.006,.06,'edge');
  });

  if(key==='hq'){
   shell('operations-wing',-.43,-.06,1.96,1.62,.87);
   facades('operations-wing',-.43,-.06,1.96,1.62,.87,false);
   part('main-entrance','cladding',[-.54,.06,.78],()=>{
    door(-.54,.08,.78,.53,.62);stairs(-.54,.84,.58,3);hazard(-.54,.057,.93,.62,.07);
    for(const x of [-1.05,-.03]){prism(x,.07,.80,.24,.79,.29,'edge',.66);prism(x,.10,.84,.17,.72,.23,'paint',.65);vent(x,.30,.985,.12,.22,false,5);}
    prism(-.54,.79,.78,1.17,.14,.32,'paint',.86);vent(-1.20,.43,.785,.20,.39);roofVent(-.54,.96,.49,.63,.20);
   });
   part('command-tower','shell',[.70,.055,-.26],()=>{
    prism(.70,.055,-.26,1.28,1.30,1.25,'wall',.96);prism(.70,1.28,-.26,1.40,.28,1.36,'paint',.84);
    bevel(.70,1.55,-.26,1.40,.10,1.36,'edge',.025);
    windowBand(.70,1.15,.382,1.12,.24,4);sideWindows(1.35,1.15,-.26,1.08,.24,4);
    for(const z of [-.64,-.24,.16])sidePanel(1.35,.58,z,.33,.48);
    door(.94,.07,.385,.29,.57);vent(.36,.59,.385,.30,.24);
    pipe([[1.41,.19,.10],[1.41,.75,.10],[1.41,.85,-.10],[1.41,1.0,-.10]],.018);
    for(let i=0;i<3;i++)pipe([[1.45,.32,-.36+i*.06],[1.45,.69,-.36+i*.06],[1.45,.79,-.21+i*.06],[1.45,.94,-.21+i*.06]],.012);
    bevel(1.43,.52,-.60,.045,.19,.18,'steel',.008);vent(1.46,.52,-.60,.14,.12,true,5);
    for(let i=0;i<4;i++)box(.16+i*.35,.98,.395,.011,.07,.025,'steel');
   });
   part('command-crown','systems',[.70,1.61,-.26],()=>{
    for(const sz of [-1,1])rail([.10,1.63,-.26+sz*.53],[1.26,1.63,-.26+sz*.53],.085);
    for(let i=0;i<3;i++){bevel(.27+i*.34,1.646,-.22,.31,.012,.40,'wall',.003);for(const z of [-.39,-.05])cylinder(.27+i*.34,1.658,z,.008,.006,'steel',6);}
    hvac(.48,1.68,-.40,.34,.27);bevel(.90,1.72,-.21,.39,.17,.26,'steel');antenna(.23,1.64,-.67,.91);utilityMast(1.19,1.64,-.62);lamp(.70,1.49,.442,.60);
   });
   part('operations-cooling','systems',[-.48,1.07,-.16],()=>{
    fan(-1.05,1.10,.19,.15);fan(-.64,1.10,.19,.15);hvac(-.95,1.03,-.62,.32,.26);
    for(let i=0;i<4;i++)box(-1.08+i*.14,1.065,-.10,.12,.012,.07,'steel');
    for(let i=0;i<4;i++)box(-1.34,.30+i*.11,-.47,.045,.055,.22,'steel');
    pipe([[-1.28,1.01,-.45],[-1.28,.81,-.45],[-1.28,.74,.15],[-1.28,.46,.15]],.018);lamp(-1.24,.97,.67,.10);
   });
   part('sloped-entrance-armour','roof',[-.54,.86,.52],()=>{
    prism(-.54,.84,.55,1.85,.24,.45,'edge',.73);
    for(const x of [-1.14,-.54,.06])part('armour-slope-'+x,'detail',[x,.99,.67],g=>{roofVent(x,.99,.67,.34,.23);g.rotation.x=.57;});
    for(const x of [-1.32,.22]){bevel(x,.58,.77,.13,.65,.13,'steel');lamp(x,.23,.86,.08);}
   });
  }else if(key==='barracks'){
   for(const side of [-1,1]){
    const x=side*.47;
    shell('dormitory-'+side,x,-.03,.77,1.62,.80,{plain:true});
    part('gable-roof-'+side,'roof',[x,.88,-.03],()=>{
     gable(x,.84,-.03,.83,.40,1.65,'paint',.38);bevel(x,1.255,-.03,.31,.025,1.60,'edge',.009);
     for(const z of [-.43,.38])part('dorm-sloped-vent-'+side+'-'+z,'detail',[x+.24,1.125,z],group=>{roofVent(x+.24,1.125,z,.18,.22);group.rotation.z=-.99;});
     windowBand(x,.96,.803,.57,.18,3);panel(x,.47,.807,.59,.31);door(x,.075,.837,.31,.55);stairs(x,.90,.38,2);
     if(side===1){sideWindows(.873,.57,.20,.93,.17,4);sidePanel(.88,.27,-.04,1.18,.20);}
    });
   }
   part('barracks-central-entry','shell',[0,.055,-.42],()=>{
    bevel(0,.40,-.48,.25,.68,.62,'wall');door(0,.06,-.155,.22,.51);bevel(0,.78,-.48,.31,.06,.68,'paint');
   });
   part('barracks-roof-equipment','systems',[0,.84,-.46],()=>{hvac(0,.83,-.52,.24,.28);antenna(-.12,.86,-.72,.60);lamp(0,.77,-.105,.16);});
  }else if(key==='factory'){
   part('factory-bay-frame','shell',[-.40,.055,0],()=>{
    box(-.42,.065,0,1.75,.025,1.63,'dark');box(-.42,.30,-.79,1.90,.50,.09,'steel');
    for(const x of [-1.32,.49]){bevel(x,.63,-.03,.19,1.15,1.60,'wall',.024);prism(x,.07,.73,.22,1.12,.20,'paint',.72);}
    bevel(-.42,1.20,-.03,2.03,.22,1.71,'paint',.035);box(-.42,1.07,-.69,1.68,.11,.12,'steel');
    for(const z of [-.57,-.16,.25])beam([-1.2,1.05,z],[.38,1.05,z],.055,.065,'steel');
    hazard(-.42,.07,.88,1.70,.09);roofVent(-.99,1.34,.45,.37,.24);roofVent(.13,1.34,.45,.34,.24);lamp(-.42,1.20,.849,.87);
   });
   part('bay-interior-services','cladding',[-.42,.08,0],()=>{
    for(const x of [-1.21,.37]){for(const z of [-.56,.02,.58]){bevel(x,.56,z,.05,.84,.08,'steel');lamp(x,.93,z,.08,true);}}
    for(const z of [-.68,-.15,.37])box(-.42,.084,z,1.57,.009,.035,'steel');
   });
   moving('factory-rolling-shutter',[-.42,.08,.795],'door',group=>{
    for(let i=0;i<12;i++){const slat=bevel(-.42,.12+i*.078,.795,1.56,.073,.035,'steel',.006);slat.userData.closedY=slat.position.y;box(-.42,.102+i*.078,.818,1.47,.008,.006,'edge').userData.closedY=.102+i*.078-.08;}
   },{travel:1.02,top:.99});
   shell('service-annex',1.01,-.02,.74,1.63,.75);facades('service-annex',1.01,-.02,.74,1.63,.75,false);
   part('annex-services','systems',[1.0,.92,-.1],()=>{hvac(1.0,.95,.26,.36,.32);door(1.0,.075,.819,.28,.53);sideWindows(1.396,.63,.12,.98,.17,4);vent(1.396,.32,.0,.92,.19,true);});
   part('control-room','shell',[.82,1.0,-.47],()=>{
    bevel(.82,1.28,-.48,1.03,.45,.70,'wall',.025);windowBand(.82,1.43,-.10,.92,.19,4);sideWindows(1.35,1.43,-.48,.57,.19,3);
    prism(.82,1.54,-.48,1.12,.18,.78,'paint',.92);bevel(.82,1.74,-.48,1.05,.035,.71,'edge');
   });
   part('factory-gantry-rails','roof',[-.43,1.4,.0],()=>{
    for(const z of [-.45,.48]){beam([-1.28,1.44,z],[.36,1.44,z],.06,.06,'steel');box(-.44,1.47,z,1.71,.035,.045,'edge');}
    for(const x of [-1.23,.29])beam([x,1.38,-.50],[x,1.38,.56],.07,.07,'paint');
   });
   moving('overhead-crane',[-.42,1.48,.12],'gantry',()=>{
    bevel(-.42,1.53,.12,1.5,.12,.17,'paint',.017);for(let i=0;i<7;i++){const x=-1.11+i*.21;beam([x,1.49,.213],[x+.10,1.56,.213],.018,.015,'edge');}
    bevel(-.42,1.46,.13,.23,.17,.20,'steel');beam([-.42,1.4,.13],[-.42,1.23,.13],.011,.011,'steel');bevel(-.42,1.21,.13,.06,.065,.06,'yellow',.01);
   });
   part('factory-crown','systems',[.85,1.79,-.47],()=>{hvac(.67,1.78,-.43,.31,.23);antenna(1.20,1.78,-.67,.57);fan(-.51,1.37,-.32,.13);});
   k.root.userData.gate={left:-1.20,right:.36,top:1.075,bottom:.08,z:.82};
  }else if(key==='power'){
   for(const sign of [-1,1]){
    const x=sign*.45;
    part('generator-'+sign,'shell',[x,.06,.10],()=>{bevel(x,.37,.13,.74,.62,1.50,'steel',.04);prism(x,.50,.12,.75,.35,1.43,'paint',.69);vent(x,.38,.90,.53,.30);panel(x,.17,.90,.56,.13);sidePanel(x+.38,.36,.10,1.04,.33);});
    part('cooling-'+sign,'systems',[x,.89,.12],()=>{fan(x,.91,-.06,.19);roofVent(x,.86,.50,.28,.25);pipe([[x,.74,-.48],[x,.91,-.56],[x,1.04,-.70],[x,1.28,-.70]],.055);});
   }
   part('heat-exhaust-house','shell',[0,.055,-.65],()=>{bevel(0,.73,-.66,1.68,1.35,.40,'wall',.035);vent(0,1.10,-.44,1.31,.25);panel(0,.65,-.44,1.3,.29);});
   part('twin-square-exhausts','systems',[0,1.43,-.65],()=>{
    for(const [x,h]of [[-.47,.70],[.47,.88]]){
     bevel(x,1.42+h/2,-.65,.22,h,.23,'wall',.012);bevel(x,1.45+h*.50,-.65,.225,.17,.235,'paint',.006);bevel(x,1.44+h,-.65,.26,.14,.27,'steel',.009);
     vent(x,1.44+h,-.505,.20,.095,false,5);lamp(x,1.54+h,-.65,.05);ladder(x+.18,1.44,-.48,h-.1);
    }
   });
   part('power-transformer','systems',[.73,.06,.55],()=>{
    bevel(.73,.32,.53,.37,.48,.63,'steel',.025);for(let i=0;i<9;i++)box(.931,.32,.27+i*.065,.065,.36,.018,'edge');
    for(const z of [.34,.53,.72]){cylinder(.73,.67,z,.025,.16,'wall',8);for(let j=0;j<3;j++)cylinder(.73,.62+j*.045,z,.045,.013,'edge',8);}
    pipe([[.73,.76,.34],[.67,.79,.34],[.58,.78,.28],[.47,.84,.22]],.012);lamp(.73,.52,.87,.12);
   });
  }else if(key==='radar'||key==='satellite'){
   shell('equipment-bunker',0,0,1.76,1.70,.58);facades('equipment-bunker',0,0,1.76,1.70,.58,false);
   part('station-entrance','cladding',[-.25,.06,.84],()=>{door(-.25,.07,.88,.38,.47);prism(-.25,.55,.82,.84,.15,.27,'paint',.88);stairs(-.25,.91,.45,2);vent(-.66,.30,.88,.23,.27);});
   part('station-service-deck','systems',[0,.79,0],()=>{hvac(-.58,.80,-.46,.31,.26);roofVent(.59,.80,.34,.30,.29);utilityMast(.72,.80,-.65);});
   part('sensor-turntable','roof',[0,.80,-.07],()=>{cylinder(0,.82,-.07,.39,.12,'steel',12);prism(0,.88,-.07,.55,.23,.55,'wall',.78);cylinder(0,1.12,-.07,.23,.08,'steel',12);});
   if(key==='radar'){
    moving('phased-array-assembly',[0,1.16,-.06],'radar',()=>{
     for(const x of [-.48,.48]){beam([x,1.17,-.03],[x,1.62,-.20],.07,.07,'edge');beam([x,1.18,.12],[x,1.69,-.18],.055,.055,'steel');}
     const panelGroup=new T.Group();panelGroup.name='array-rigid-face';panelGroup.position.set(0,1.71,-.18);panelGroup.rotation.x=-.16;
     // Explicit modules retain geometry on the hidden side of the array.
     const createMesh=(geo,mat,x,y,z)=>{const m=new T.Mesh(geo,k.materials[mat]);m.position.set(x,y,z);m.castShadow=false;m.receiveShadow=true;panelGroup.add(m);return m;};
     createMesh(new T.BoxGeometry(1.48,1.03,.10),'paint',0,0,0);createMesh(new T.BoxGeometry(1.28,.84,.055),'dark',0,0,.08);
     for(let y=0;y<8;y++)for(let x=0;x<12;x++){createMesh(new T.BoxGeometry(.095,.09,.015),'steel',-.577+x*.105,-.365+y*.104,.115);}
     for(const sy of [-1,1])for(let i=0;i<7;i++)createMesh(new T.BoxGeometry(.014,.014,.012),'edge',-.62+i*.207,sy*.468,.065);
     panelGroup.position.sub(new T.Vector3(0,1.16,-.06));k.root.getObjectByName('phased-array-assembly').add(panelGroup);
     pipe([[-.35,1.54,-.20],[-.33,1.28,-.20],[-.19,1.19,-.08]],.025);
    });
   }else{
    moving('uplink-reflector',[0,1.18,-.04],'dish',()=>{
     const n=16,radius=.73,verts=[],indices=[];
     // The reflector opens towards +Z and upwards; rings form a faceted paraboloid.
     const group=new T.Group();group.name='parabolic-reflector';group.position.set(0,1.74,-.02);group.rotation.x=-.40;
     for(let ring=0;ring<=5;ring++)for(let j=0;j<n;j++){const r=radius*ring/5,a=j*Math.PI*2/n;verts.push(Math.sin(a)*r,Math.cos(a)*r,-.19+(.32*(r/radius)**2));}
     for(let q=0;q<5;q++)for(let j=0;j<n;j++){const a=q*n+j,b=q*n+(j+1)%n,c=(q+1)*n+j,d=(q+1)*n+(j+1)%n;indices.push(a,c,b,b,c,d);}
     const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(verts,3));g.setIndex(indices);g.computeVertexNormals();const mat=k.materials.edge.clone();mat.side=T.DoubleSide;const dish=new T.Mesh(g,mat);dish.castShadow=false;dish.receiveShadow=true;group.add(dish);
     const add=(geo,mat,x,y,z)=>{const m=new T.Mesh(geo,k.materials[mat]);m.position.set(x,y,z);m.castShadow=false;m.receiveShadow=true;group.add(m);return m;};
     for(let j=0;j<n;j++){const a=j*Math.PI*2/n,b=(j+1)*Math.PI*2/n,ax=Math.sin(a)*radius,ay=Math.cos(a)*radius,bx=Math.sin(b)*radius,by=Math.cos(b)*radius,v=new T.Vector3(bx-ax,by-ay,0);const rail=add(new T.BoxGeometry(.067,v.length()+.02,.10),'paint',(ax+bx)/2,(ay+by)/2,.142);rail.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),v.normalize());}
     for(let j=0;j<3;j++){const a=j*2*Math.PI/3,p=new T.Vector3(Math.sin(a)*radius*.9,Math.cos(a)*radius*.9,.15),end=new T.Vector3(0,0,.53),v=end.clone().sub(p);const arm=add(new T.BoxGeometry(.035,v.length(),.035),'steel',(p.x+end.x)/2,(p.y+end.y)/2,(p.z+end.z)/2);arm.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),v.normalize());}
     add(new T.BoxGeometry(.15,.13,.14),'steel',0,0,.54);add(new T.BoxGeometry(.10,.08,.015),'yellow',0,0,.62);
     group.position.sub(new T.Vector3(0,1.18,-.04));k.root.getObjectByName('uplink-reflector').add(group);
     for(const x of [-.28,.28])beam([x,1.17,-.04],[x,1.56,-.15],.075,.08,'paint');
    });
   }
  }else if(key==='post'){
   part('bunker-shell','shell',[0,.05,0],()=>{prism(0,.055,0,.87,.36,.83,'wall',.70);prism(0,.405,0,.75,.12,.74,'paint',.90);});
   part('bunker-armour','cladding',[0,.06,0],()=>{
    for(const x of [-.23,.23]){panel(x,.21,.365,.31,.17);box(x,.335,.30,.24,.035,.025,'dark');}
    sidePanel(.40,.22,0,.43,.20);vent(.40,.20,-.27,.16,.18,true);door(.04,.06,-.395,.18,.28);lamp(.22,.48,.386,.09);
   });
   part('weapon-mount','systems',[0,.52,-.03],()=>{
    cylinder(0,.54,-.03,.22,.085,'steel',12);prism(0,.58,-.03,.38,.22,.36,'steel',.78);panel(-.14,.71,.10,.12,.17);sidePanel(.21,.71,-.07,.24,.16);
    box(-.19,.66,-.14,.12,.17,.22,'steel');cylinder(0,.72,.24,.018,.36,'dark',12).rotation.x=Math.PI/2;cylinder(0,.72,.43,.027,.10,'steel',12).rotation.x=Math.PI/2;
    moving('rangefinder-scan',[0,.87,-.05],'radar',()=>{bevel(0,.87,-.05,.16,.12,.17,'edge',.012);box(0,.87,.042,.12,.08,.008,'dark');cylinder(-.027,.87,.050,.025,.009,'lamp',10).rotation.x=Math.PI/2;});
    antenna(.23,.53,-.24,.30);
   });
   k.root.userData.muzzle=[0,.72,.48];
  }else if(key==='refinery'){
   part('crusher-tower','shell',[.48,.06,-.38],()=>{
    prism(.48,.06,-.38,.81,1.32,.89,'wall',.92);prism(.48,1.39,-.38,.86,.22,.91,'paint',.84);bevel(.48,1.63,-.38,.72,.04,.77,'edge');
    vent(.48,.53,.085,.57,.57);sidePanel(.90,.81,-.41,.67,.42);sidePanel(.90,.31,-.41,.67,.27);box(.48,1.23,.085,.50,.21,.027,'dark');
    for(let i=0;i<4;i++){cylinder(.29+i*.12,1.23,.12,.08,.10,'steel',10).rotation.x=Math.PI/2;}
    ladder(.84,.31,.11,1.27);rail([.08,1.71,-.73],[.81,1.71,-.73],.10,'yellow');rail([.09,1.71,-.75],[.09,1.71,-.03],.10,'yellow');
   });
   part('receiving-hopper','shell',[-.55,.07,.35],()=>{
    for(const x of [-.87,-.25])beam([x,.07,.12],[x,.68,.12],.07,.07,'steel');
    prism(-.55,.37,.27,.42,.39,.45,'steel',1.50);prism(-.55,.73,.27,.81,.30,.79,'paint',.66);
    box(-.55,1.04,.27,.58,.018,.55,'dark');rail([-.91,.71,.65],[-.19,.71,.65],.12,'yellow');
    for(let i=0;i<7;i++){const o=bevel(-.79+i%3*.20,1.08+(i%2)*.035,.14+Math.floor(i/3)*.13,.15,.12,.12,'ore',.02);o.rotation.y=i*.7;}
   });
   part('angled-conveyor-structure','roof',[-.12,.65,.08],()=>{
    const from=[-.55,.79,.22],to=[.43,1.24,-.28];beam(from,to,.36,.22,'dark');
    for(const side of [-1,1])beam([from[0],from[1]+.10,from[2]+side*.17],[to[0],to[1]+.10,to[2]+side*.17],.07,.06,'paint');
    for(let i=0;i<8;i++){const q=i/7,x=from[0]+(to[0]-from[0])*q,y=from[1]+(to[1]-from[1])*q,z=from[2]+(to[2]-from[2])*q;beam([x,y-.1,z],[x,.12,z],.035,.035,'steel');}
   });
   for(let i=0;i<8;i++){
    const from=[-.51,.93,.19],to=[.41,1.38,-.27],at=from.map((v,j)=>v+(to[j]-v)*i/8);moving('ore-on-belt-'+i,at,'ore',()=>{const o=bevel(at[0],at[1],at[2],.10,.065,.08,'ore',.012);o.rotation.y=i*.9;},{offset:i/8,from,to});
   }
   part('refinery-machinery','systems',[.30,.08,.48],()=>{
    bevel(.34,.30,.48,.87,.44,.72,'steel',.035);prism(.34,.51,.48,.90,.20,.71,'paint',.84);sidePanel(.79,.30,.47,.54,.28);vent(.33,.32,.852,.64,.28);
    for(const z of [.40,.63]){cylinder(.18,.82,z,.09,.26,'steel',12);cylinder(.18,.97,z,.10,.026,'edge',12);}
    pipe([[.90,.26,-.44],[.94,.38,-.15],[.94,.60,.11],[.76,.73,.32]],.028);hvac(.49,1.72,-.42,.31,.24);fan(.48,1.84,-.25,.12);
    hazard(-.33,.058,.91,.76,.075);lamp(.34,.66,.84,.23);
   });
  }else if(key==='residence'){
   shell('domestic-shell',0,-.02,1.77,1.70,1.14,{masonry:true,plain:true});
   part('plaster-and-gables','cladding',[0,.61,0],()=>{
    bevel(0,.91,-.02,1.78,.62,1.71,'wall',.018);
    for(const y of [.37,.93]){windowBand(-.47,y,.85,.47,.27,2);sideWindows(.904,y,-.25,1.13,.27,3,true);}
    for(const x of [-.83,.83])bevel(x,.66,.868,.075,1.17,.055,'edge',.009);
    panel(.47,.68,.868,.52,.15);door(.36,.065,.88,.38,.60);stairs(.36,.94,.44,2);pipe([[.86,1.36,.78],[.94,1.29,.78],[.94,.15,.78]],.016);
   });
   part('house-pitched-roof','roof',[0,1.23,0],()=>{
    gable(0,1.23,-.03,1.90,.43,1.82,'paint',.22);bevel(0,1.68,-.03,.44,.025,1.78,'edge',.007);
    for(const sign of [-1,1])part('house-sloped-vent-'+sign,'detail',[sign*.55,1.48,sign*.2],group=>{roofVent(sign*.55,1.48,sign*.2,.27,.22);group.rotation.z=-sign*.53;});
    bevel(.35,.79,.95,.71,.11,.10,'paint');bevel(-.42,1.25,.887,.70,.08,.13,'edge');
   });
   part('house-utilities','systems',[0,1.7,-.30],()=>{
    bevel(.53,1.73,-.50,.22,.45,.23,'brick',.015);bevel(.53,1.97,-.50,.29,.06,.30,'edge');box(.53,2.04,-.50,.24,.08,.23,'dark');bevel(.53,2.10,-.50,.31,.04,.31,'edge');hvac(-.05,1.70,-.27,.25,.22);
   });
  }else if(key==='shop'||key==='municipal'){
   const civic=key==='municipal',depth=civic?2.70:1.71,w=civic?2.72:2.76,h=1.63;
   shell('civic-office',0,-.03,w,depth,h);
   part('storey-bands','cladding',[0,.08,0],()=>{
    for(const y of [.47,1.01,1.54]){
     bevel(0,y,-.03,w+.025,.065,depth+.025,'edge',.009);
     const n=civic?5:6;for(let i=0;i<n;i++)windowBand(-w*.41+i*w*.82/(n-1),y-.24,depth/2+.008,w*.68/n,.29,1);
     sideWindows(w/2+.025,y-.24,-.03,depth*.80,.29,civic?5:4,true);
    }
    for(let i=0;i<5;i++)panel(-w*.38+i*w*.19,.75,depth/2+.045,.16,.36);
    for(let i=0;i<4;i++)sidePanel(w/2+.047,.77,-depth*.35+i*depth*.23,.18,.36);
   });
   if(civic){
    part('municipal-central-tower','shell',[0,.055,-.56],()=>{
     bevel(0,1.18,-.56,1.03,2.24,.92,'wall',.035);for(const y of [.63,1.21,1.78])windowBand(0,y,-.082,.65,.27,2);
     prism(0,2.31,-.56,1.18,.20,1.05,'paint',.90);bevel(0,2.54,-.56,1.08,.04,.95,'edge');
    });
    part('municipal-entrance-canopy','roof',[0,.07,1.10],()=>{
     for(const x of [-.59,.59])bevel(x,.43,1.12,.12,.74,.17,'edge',.012);
     bevel(0,.86,1.07,1.43,.17,.48,'edge',.02);door(0,.08,1.05,.51,.65);stairs(0,1.22,.70,3);hazard(0,.06,1.39,.65,.07);
    });
    crown('municipal',0,2.58,-.56,.96,.78);
   }else{
    part('shop-frontage','cladding',[0,.08,.86],()=>{
     for(let i=0;i<4;i++)windowBand(-.84+i*.55,.32,.89,.43,.44,2);
     door(-1.12,.065,.89,.30,.59);prism(0,.66,.78,2.76,.16,.40,'paint',.84);stairs(-1.12,.93,.35,2);
     for(const x of [-.76,-.20,.36,.92])lamp(x,.62,1.0,.18);
    });
    crown('shop',0,1.86,-.11,2.35,1.28);
   }
   part('utility-downpipes','systems',[w/2-.08,.06,0],()=>{
    pipe([[w/2-.08,1.78,-.63],[w/2+.06,1.64,-.63],[w/2+.06,.15,-.63]],.017);hvac(w/2+.045,1.11,.18,.09,.24);
   });
  }
  // Continuous framing, drainage and repeated service plates remain geometry,
  // so hidden faces and moving construction stages keep the same workmanship.
  if(key!=='post')part('service-joints-and-sills','detail',[0,.07,0],()=>{
   const w=s.w-.24,d=s.d-.24;
   for(let i=0;i<Math.ceil(w/.40);i++){
    const x=-w/2+(i+.5)*w/Math.ceil(w/.40);
    bevel(x,.113,d/2,w/Math.ceil(w/.40)-.024,.047,.045,'edge',.006);
    vent(x,.18,-d/2-.025,w/Math.ceil(w/.40)*.70,.10,false,4);
   }
   for(let i=0;i<Math.ceil(d/.40);i++){
    const z=-d/2+(i+.5)*d/Math.ceil(d/.40);
    bevel(w/2,.113,z,.045,.047,d/Math.ceil(d/.40)-.024,'edge',.006);
   }
  });
  // Finer fasteners and surface divisions belong to the model, never a flat image decal.
  k.root.userData.design='Approved V07 concept; dimensioned editable reconstruction';return k.finish();
 }
 return {specs,ready:K.ready,build,pose:K.pose,doorPose:K.doorPose,bindings:K.bindings};
})();
