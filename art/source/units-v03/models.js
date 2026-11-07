/* The approved tank's modelling, coating, lighting and faction material contract
   are shared by the remaining roster. No texture contains a faction color. */
window.UnitMaster=(()=>{
 const T=THREE;
 const specs={squad:{foot:true,width:2.7},at:{foot:true,width:2.7},engineer:{foot:true,width:2.7},ifv:{width:7.5},arty:{width:9.2},mcv:{width:8.35},miner:{width:7.5},'miner-full':{width:7.5}};
 function build(key,wreck=false){
  const {root,p,mesh,box,cyl,beam,bevel,hull}=TankMaster.kit(wreck);root.name='Greyhaven '+key;
  function material(name,color,roughness=.7,metalness=.1){p[name]=new T.MeshStandardMaterial({color:wreck?0x454742:color,roughness,metalness});p[name].name=name;}
  material('cloth',0x62685d,.97,0);material('skin',0xb99b7e,.86,0);material('yellow',0xd4a740,.57,.1);material('ore',0xd5ae58,.57,.3);
  function oval(x,y,z,rx,ry,rz,m){const o=mesh(new T.SphereGeometry(1,24,16),m,x,y,z);o.scale.set(rx,ry,rz);return o;}
  function limb(a,b,r,m){beam(a,b,r,m);oval(...a,r,r,r,m);oval(...b,r,r,r,m);}
  function wheel(x,z,r=.49){
   const s=Math.sign(x);cyl(r,.36,x,r,z,'rubber','x');cyl(r*.69,.38,x,r,z,'steel','x');cyl(r*.43,.40,x,r,z,'armor','x');cyl(r*.16,.43,x,r,z,'bolt','x');
   for(let i=0;i<6;i++){const a=i*Math.PI/3;cyl(.025,.42,x,r+Math.sin(a)*r*.32,z+Math.cos(a)*r*.32,'bolt','x');}
   for(let i=0;i<22;i++){const a=i*Math.PI/11;const o=box(.37,.06,.12,x,r+Math.sin(a)*r,z+Math.cos(a)*r,'rubber');o.rotation.x=-a;}
  }
  function roadWheels(zs,w=2.3,r=.49){for(const s of [-1,1])for(const z of zs)wheel(s*w/2,z,r);}
  function tracks(w,d){
   for(const s of [-1,1]){
    bevel(.6,.86,d,.15,s*w/2,.51,0,'rubber');
    for(let j=0;j<6;j++){
     const z=(j-2.5)*(d-.8)/5;cyl(.30,.64,s*w/2,.48,z,'steel','x');cyl(.21,.67,s*w/2,.48,z,'armor','x');cyl(.07,.70,s*w/2,.48,z,'bolt','x');
    }
    for(let i=0;i<24;i++)for(const y of [.09,.94])box(.64,.055,.10,s*w/2,y,-d/2+.24+i*(d-.48)/23,'steel');
    for(const end of [-1,1])for(let i=0;i<7;i++){const a=-Math.PI/2+i*Math.PI/6;const o=box(.64,.065,.12,s*w/2,.51+.37*Math.sin(a),end*(d/2-.35+.34*Math.cos(a)),'steel');o.rotation.x=end*a;}
    for(let j=0;j<5;j++)bevel(.10,.42,(d-.4)/5-.03,.018,s*(w/2+.36),.91,(j-2)*(d-.4)/5,'paint');
   }
  }
  function vents(x,y,z,w,d){bevel(w,.05,d,.012,x,y,z,'dark');for(let i=0;i<8;i++)box(w*.86,.025,d*.045,x,y+.035,z-d*.4+i*d*.114,'steel');}
  function lamps(w,z,y){for(const s of [-1,1]){bevel(.35,.19,.16,.025,s*w,y,z,'dark');bevel(.25,.11,.035,.012,s*w,y,z+.09,'lamp');}}
  function truck(){
   roadWheels([-1.55,-.40,1.45],2.30,.51);bevel(2.3,.28,4.2,.055,0,.80,0,'steel');
   hull(2.35,1.35,1.62,0,.91,1.18,'paint',.88);
   bevel(2.02,.10,1.22,.025,0,2.29,1.16,'paint');
   // Windshield, neutral frames, mirrors, handles, front grille and steps.
   bevel(1.80,.55,.055,.018,0,1.91,1.97,'glass');box(.055,.55,.075,0,1.91,2.0,'steel');
   for(const s of [-1,1]){
    bevel(.05,.49,.75,.014,s*1.05,1.91,1.12,'glass');
    box(.06,.04,.20,s*1.16,1.55,1.15,'bolt');bevel(.37,.08,.68,.018,s*1.23,.80,1.1,'steel');
    beam([s*1.1,1.88,1.8],[s*1.42,1.88,1.8],.025,'steel');bevel(.08,.23,.17,.025,s*1.42,1.93,1.8,'dark');
   }
   bevel(1.16,.35,.07,.02,0,1.25,2.03,'dark');for(let i=0;i<5;i++)box(1.02,.025,.025,0,1.12+i*.065,2.08,'steel');
   lamps(.86,2.04,1.25);bevel(2.40,.18,.24,.035,0,.89,2.1,'steel');
   for(const s of [-1,1]){cyl(.08,.14,s*.65,2.42,.9,'yellow');bevel(.22,.15,.06,.018,s*.83,1.08,-2.1,'lamp');}
  }
  if(specs[key].foot){
   // Jointed anatomical figure, rather than blocks. Knees, cuffs and armor are
   // separate rounded shapes; rifle/launcher/tool provide distinct silhouettes.
   p.paint.roughness=.88;p.paint.metalness=.02;p.paint.bumpScale=.004;
   for(const s of [-1,1]){
    const hip=[s*.135,.93,0],knee=[s*.17,.56,.035],ankle=[s*.18,.16,-s*.035];
    limb(hip,knee,.103,'cloth');limb(knee,ankle,.083,'cloth');
    bevel(.17,.20,.075,.025,s*.17,.56,.13,'steel');
    bevel(.18,.16,.32,.04,s*.18,.10,.055-s*.035,'rubber');
    for(let j=0;j<3;j++)box(.10,.012,.015,s*.18,.184,.06+j*.035-s*.035,'steel');
    bevel(.10,.19,.14,.02,s*.25,.80,-.035,'cloth');
   }
   oval(0,1.15,0,.245,.31,.145,'cloth');
   bevel(.40,.45,.12,.045,0,1.21,.14,'paint');bevel(.38,.43,.10,.035,0,1.23,-.15,'paint');
   box(.42,.065,.30,0,.96,0,'dark');
   for(const s of [-1,1]){
    const shoulder=[s*.265,1.39,0],elbow=key==='at'?[s*.31,1.32,.24]:[s*.33,1.13,.15];
    limb(shoulder,elbow,.087,'cloth');oval(...shoulder,.11,.105,.115,'paint');
    const hand=key==='engineer'?[s*.32,1.00,.24]:s<0?[.02,1.20,.48]:[.16,1.25,.30];
    limb(elbow,hand,.065,'cloth');oval(...hand,.065,.063,.067,'skin');
    bevel(.085,.14,.055,.018,s*.16,1.12,.215,'armor');
    bevel(.075,.19,.07,.015,s*.17,1.37,.15,'cloth');
   }
   cyl(.075,.11,0,1.53,0,'skin');oval(0,1.67,.01,.115,.145,.105,'skin');
   oval(0,1.66,.112,.027,.04,.035,'skin'); // nose
   const helmet=key==='engineer'?'yellow':'armor';
   const dome=mesh(new T.SphereGeometry(.16,32,14,0,Math.PI*2,0,Math.PI*.60),helmet,0,1.73,0);dome.scale.z=1.08;
   cyl(.166,.035,0,1.69,.014,helmet);bevel(.15,.035,.045,.008,0,1.65,.118,'glass');
   for(const s of [-1,1])beam([s*.11,1.68,0],[s*.07,1.54,.045],.012,'dark');
   if(key==='squad'){
    bevel(.085,.12,.39,.015,.13,1.24,.36,'steel');beam([.13,1.27,.50],[.13,1.27,.89],.023,'dark');
    bevel(.065,.15,.09,.01,.13,1.15,.38,'dark');bevel(.07,.09,.17,.015,.13,1.23,.15,'dark');
    cyl(.025,.16,.13,1.35,.39,'glass','z');bevel(.25,.34,.12,.03,0,1.23,-.23,'paint');
   }else if(key==='at'){
    cyl(.095,1.04,.25,1.53,.23,'steel','z');cyl(.125,.13,.25,1.53,-.30,'armor','z');
    cyl(.12,.12,.25,1.53,.80,'armor','z');cyl(.082,.012,.25,1.53,.866,'dark','z');
    bevel(.05,.085,.1,.012,.25,1.65,.57,'dark');bevel(.29,.40,.16,.035,0,1.23,-.25,'paint');
   }else{
    bevel(.21,.26,.18,.035,-.31,.89,.04,'cloth');bevel(.20,.16,.06,.015,0,1.08,.23,'dark');
    beam([.32,.85,.25],[.32,1.18,.25],.025,'steel');bevel(.12,.07,.05,.012,.32,1.2,.25,'bolt');
    for(const s of [-1,1])box(.035,.08,.045,.32+s*.045,1.25,.25,'bolt');
    for(let i=0;i<3;i++)beam([-.07+i*.07,1.04,.27],[-.07+i*.07,1.22,.27],.014,'steel');
   }
  }else if(key==='ifv'){
   roadWheels([-1.62,-.54,.54,1.62],2.40,.48);hull(2.46,1.05,4.4,0,.62,0,'paint',.82);
   hull(1.98,.43,2.85,0,1.54,-.49,'armor',.91);bevel(1.66,.085,2.37,.025,0,2.00,-.5,'armor');
   vents(0,2.06,-1.26,1.15,.69);cyl(.43,.24,0,2.09,.35,'paint');
   bevel(.38,.22,.58,.035,0,2.24,.51,'armor');beam([0,2.27,.70],[0,2.27,1.49],.05,'steel');
   for(const s of [-1,1]){bevel(.045,.3,.7,.012,s*1.10,1.57,1.04,'glass');bevel(.08,.44,.8,.018,s*1.23,1.17,-.62,'paint');}
   bevel(1.30,.71,.055,.022,0,1.15,-2.08,'paint');box(.03,.60,.04,0,1.15,-2.12,'steel');
   lamps(.78,2.08,1.03);bevel(.75,.09,.40,.02,0,1.76,1.16,'glass');
   beam([.62,2.04,-.83],[.62,2.80,-.83],.012,'steel');
  }else if(key==='arty'){
   tracks(2.25,4.9);hull(2.48,.53,4.55,0,.75,0,'armor',.91);
   hull(2.14,1.28,2.47,0,1.3,-.89,'paint',.85);bevel(1.89,.09,2.12,.03,0,2.61,-.89,'armor');
   for(const s of [-1,1]){bevel(.08,.79,1.51,.024,s*1.03,1.94,-.91,'paint');vents(s*.54,2.69,-1.46,.69,.55);}
   bevel(.63,.55,.51,.05,0,2.13,.41,'armor');beam([0,2.15,.5],[0,2.45,3.78],.13,'steel');
   beam([0,2.15,.5],[0,2.29,1.95],.17,'armor');bevel(.32,.27,.39,.028,0,2.46,3.88,'steel');
   cyl(.094,.02,0,2.46,4.09,'dark','z');cyl(.29,.1,-.45,2.75,-.52,'armor');
   bevel(1.18,.48,.09,.018,0,1.92,-2.13,'paint');vents(0,1.34,1.43,1.2,.75);lamps(.89,2.09,1.12);
   beam([.65,2.64,-1.31],[.65,3.32,-1.31],.014,'steel');
  }else if(key==='mcv'){
   truck();hull(2.31,1.24,2.38,0,1.0,-.92,'armor',.95);
   for(const s of [-1,1]){bevel(.07,.69,1.84,.02,s*1.15,1.57,-.91,'paint');for(const z of [-1.60,-.3])bevel(.08,.10,.20,.012,s*1.20,1.58,z,'steel');}
   bevel(1.72,.58,.09,.018,0,1.57,-2.10,'paint');vents(-.4,2.27,-1.43,.85,.6);
   cyl(.32,.18,.42,2.36,-1.25,'steel');
   const a=[.42,2.48,-1.25],b=[.42,3.22,-.24],c=[.42,2.95,1.19];
   beam(a,b,.16,'yellow');beam(b,c,.13,'yellow');beam([.42,2.57,-1.1],[.42,2.92,-.23],.055,'steel');
   for(const pos of [a,b])cyl(.19,.25,...pos,'yellow','x');
   beam(c,[.42,2.45,1.19],.022,'steel');cyl(.09,.05,.42,2.39,1.19,'dark','x');
   for(const s of [-1,1])beam([s*1.10,1.10,-1.5],[s*1.45,1.10,-1.5],.07,'steel');
  }else if(key==='miner'||key==='miner-full'){
   truck();bevel(2.4,.2,2.8,.03,0,1.07,-.72,'steel');
   bevel(2.05,.07,2.44,.02,0,1.23,-.72,'dark');
   for(const s of [-1,1]){
    const panel=bevel(.13,.95,2.83,.025,s*1.18,1.67,-.72,'paint');panel.rotation.z=-s*.12;
    beam([s*1.27,2.16,-2.12],[s*1.27,2.16,.70],.07,'edge');
    for(const z of [-1.92,-1.13,-.34,.45]){const rib=bevel(.08,.95,.075,.014,s*1.26,1.67,z,'steel');rib.rotation.z=-s*.12;}
   }
   bevel(2.28,.92,.14,.024,0,1.65,-2.08,'paint');bevel(2.28,.96,.14,.024,0,1.67,.69,'paint');
   beam([-1.1,2.16,-2.1],[1.1,2.16,-2.1],.065,'edge');
   if(key==='miner-full')for(let i=0;i<20;i++){
    const o=mesh(new T.DodecahedronGeometry(.26+(i%3)*.045,1),'ore',((i*7)%5-2)*.35,1.63+(i%3)*.12,-1.65+(i%4)*.56);o.rotation.set(i,i*.3,i*.7);
   }
  }
  if(wreck){root.scale.y=.58;root.rotation.z=.07;}
  return root;
 }
 return {build,specs};
})();
