// Mission architecture. The 3 x 3 foundation is the collision/placement contract.
// Extend the shared authoring kit without changing any existing building master.
(()=>{
 const parent=window.BuildingMasterV08,K=ArchitectureKit;
 const spec={w:3,d:3,name:'中继控制站'};
 function relay(){
  const k=K.create('relay',spec);
  const {part,moving,box,bevel,prism,cylinder,beam,pipe,panel,sidePanel,vent,roofVent,door,windowBand,lamp,stairs,rail,hvac,fan,antenna,ladder,hazard}=k;
  part('foundation','foundation',[0,0,0],()=>{
   bevel(0,.025,0,3,.05,3,'concrete',.007);
   for(const a of [-.5,.5]){box(a,.052,0,.008,.003,2.98,'steel');box(0,.052,a,2.98,.003,.008,'steel');}
   for(const x of [-1.44,1.44])for(const z of [-1.44,1.44])box(x,.055,z,.08,.008,.08,'edge');
  });
  part('signal-bunker','shell',[-.37,.055,.25],()=>{
   prism(-.37,.055,.25,1.94,.88,2.22,'wall',.96);
   for(const x of [-1.29,.55])for(const z of [-.80,1.30])bevel(x,.50,z,.08,.89,.08,'edge',.01);
   box(-.37,.14,1.37,1.94,.08,.035,'steel');
  });
  part('armored-roof','roof',[-.37,.94,.25],()=>{
   prism(-.37,.94,.25,2.02,.16,2.28,'paint',.91);
   bevel(-.37,1.12,.25,1.87,.045,2.11,'edge',.012);
   for(const x of [-.94,-.36,.22])for(const z of [-.42,.25,.92])bevel(x,1.15,z,.55,.018,.62,'paint',.005);
   rail([-1.22,1.16,-.72],[.48,1.16,-.72],.11);rail([-1.22,1.16,-.72],[-1.22,1.16,1.23],.11);
  });
  part('front-panels','cladding',[-.37,.1,1.37],()=>{
   door(-.56,.07,1.385,.46,.62);stairs(-.56,1.44,.58,2);
   for(const x of [-1.08,.11,.43])panel(x,.35,1.385,.25,.43);
   windowBand(-.54,.80,1.385,.71,.14,3);prism(-.56,.91,1.35,.85,.10,.24,'paint',.86);
   hazard(-.56,.058,1.47,.67,.04);lamp(.23,.80,1.40,.13);
   for(const z of [-.58,-.06,.46,1.03]){sidePanel(.615,.39,z,.39,.53);vent(.64,.72,z,.23,.17,true,5);}
  });
  part('service-platform','systems',[-.4,1.17,.25],()=>{
   hvac(-.89,1.17,-.43,.38,.37);fan(-.86,1.21,.49,.18);fan(-.36,1.21,.49,.18);
   roofVent(.19,1.18,.90,.30,.32);antenna(-1.12,1.18,1.10,.52);
   pipe([[.57,.23,-.60],[.57,.88,-.60],[.38,1.19,-.60],[.19,1.19,-.40]],.018);
   ladder(.62,.10,.97,.94);
  });
  part('lattice-tower','shell',[.97,.06,-.75],()=>{
   for(const x of [.64,1.30])for(const z of [-1.15,-.49]){
    bevel(x,.12,z,.24,.13,.24,'wall');beam([x,.12,z],[.97+(x-.97)*.55,2.10,-.82+(z+.82)*.55],.07,.07,'steel');
   }
   for(let i=0;i<4;i++){
    const y=.35+i*.39,r=.30-i*.026;
    for(const sign of [-1,1]){beam([.97-r,y,-.82+sign*r],[.97+r-.03,y+.36,-.82+sign*(r-.026)],.029,.029,'edge');beam([.97+sign*r,y,-.82-r],[.97+sign*(r-.026),y+.36,-.82+r-.03],.029,.029,'edge');}
   }
   bevel(.97,2.07,-.82,.68,.09,.68,'paint');cylinder(.97,2.18,-.82,.16,.14,'steel',12);
  });
  moving('directional-relay-array',[.97,2.25,-.82],'scanner',()=>{
   bevel(.97,2.47,-.82,1.02,.57,.13,'paint',.025);
   box(.97,2.47,-.735,.88,.43,.04,'dark');
   for(let i=0;i<8;i++)for(let j=0;j<4;j++)box(.595+i*.107,2.303+j*.108,-.705,.083,.079,.012,'steel');
   beam([.75,2.22,-.84],[.75,2.50,-.87],.055,.055,'edge');beam([1.18,2.22,-.84],[1.18,2.50,-.87],.055,.055,'edge');
   lamp(.97,2.79,-.82,.11);
  });
  part('backup-generator','systems',[.98,.06,.85],()=>{
   bevel(.98,.34,.86,.65,.55,.93,'steel',.035);
   prism(.98,.62,.86,.69,.13,.96,'paint',.88);vent(.98,.35,1.34,.47,.31,false,7);
   for(const z of [.58,.88,1.17])sidePanel(1.325,.34,z,.19,.32);
   pipe([[1.22,.42,.52],[1.22,.89,.52],[1.11,.94,.52]],.025);lamp(1.04,.56,1.35,.11);
  });
  k.root.userData.sockets={signal:[[.97,2.79,-.82]],steam:[[1.11,.94,.52]]};
  k.root.userData.design='Mistridge mission relay; shared angular architecture, independent faction contribution';
  return k.finish();
 }
 window.BuildingMasterV08={...parent,specs:{...parent.specs,relay:spec},build:key=>key==='relay'?relay():parent.build(key)};
})();
