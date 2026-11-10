// Low 2 x 2 fortification: distinct firing slits, thick roof, visible access door.
(()=>{
 const parent=BuildingMasterV08,K=ArchitectureKit,spec={w:2,d:2,name:'驻兵碉堡'};
 function build(){
  const k=K.create('bunker',spec),{part,box,bevel,prism,door,stairs,panel,sidePanel,vent,fan,pipe,lamp}=k;
  part('foundation','foundation',[0,0,0],()=>{bevel(0,.025,0,2,.05,2,'concrete',.007);for(const x of [-.94,.94])for(const z of [-.94,.94])box(x,.052,z,.08,.009,.08,'edge');});
  part('reinforced-core','shell',[0,.06,0],()=>{
   prism(0,.06,0,1.77,.66,1.78,'wall',.94);
   for(const x of [-.79,.79])for(const z of [-.77,.77])prism(x,.07,z,.20,.68,.23,'concrete',.7);
   for(const z of [-.46,.14,.57]){box(.889,.45,z,.015,.16,.34,'dark');bevel(.90,.555,z,.06,.06,.40,'steel',.005);}
   for(const x of [-.50,.45]){box(x,.45,.90,.34,.16,.015,'dark');bevel(x,.555,.91,.40,.06,.06,'steel',.005);}
  });
  part('roof-beams','shell',[0,.72,0],()=>{for(const x of [-.65,0,.65])bevel(x,.72,0,.12,.13,1.86,'steel',.008);});
  part('armored-roof','roof',[0,.79,0],()=>{
   prism(0,.79,0,1.94,.17,1.94,'paint',.92);bevel(0,.97,0,1.76,.04,1.76,'edge',.008);
   for(const x of [-.57,0,.57])for(const z of [-.56,0,.56])bevel(x,1.0,z,.53,.018,.52,'paint',.005);
   for(const x of [-.85,.85])for(const z of [-.83,.83])box(x,1.0,z,.06,.012,.06,'steel');
  });
  part('firing-apertures','cladding',[0,.2,.88],()=>{
   door(0,.06,.91,.32,.44);stairs(0,.95,.38,1);
   for(const x of [-.55,.55]){panel(x,.23,.912,.35,.14);bevel(x,.61,.91,.43,.07,.13,'paint',.006);}
   for(const z of [-.46,.14,.57])sidePanel(.91,.23,z,.34,.14);
   lamp(.23,.64,.93,.075);
  });
  part('roof-systems','systems',[-.58,1.02,-.53],()=>{
   fan(-.58,1.02,-.54,.16);vent(.65,.71,-.46,.24,.12,true,5);
   bevel(.46,1.035,-.51,.45,.04,.39,'steel',.008);bevel(.46,1.06,-.51,.35,.025,.30,'paint',.008);
   pipe([[-.74,.11,-.74],[-.74,.71,-.74],[-.59,1.04,-.74]],.013);
  });
  k.root.userData.sockets={signal:[[.23,.64,.93]]};return k.finish();
 }
 window.BuildingMasterV08={...parent,specs:{...parent.specs,bunker:spec},build:key=>key==='bunker'?build():parent.build(key)};
})();
