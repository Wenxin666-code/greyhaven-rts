/* Damage art: local scorches, jagged breaches and slightly bent edge plates.
   Buildings stay upright. Smoke, flames and final detonation are runtime FX. */
window.BuildingDamage=(()=>{
 const T=THREE,M=BuildingMasterV08,K=ArchitectureKit,oldBuild=M.build,oldPose=M.pose,oldMask=K.maskMaterial;
 const applied=new WeakSet(),amount={value:0},centers={value:Array.from({length:5},()=>new T.Vector4(0,-20,0,.2))};let severity=0;
 const marks={
  hq:[[-.70,.48,.79,.30],[1.34,1.10,-.03,.29],[-1.23,.65,.75,.24],[.60,1.65,.15,.27]],
  power:[[-.45,.40,.89,.27],[.73,.34,.85,.23],[.0,1.07,-.44,.30],[.47,1.90,-.50,.21]],
  barracks:[[-.47,.52,.81,.24],[.47,.94,.81,.21],[.87,.48,.20,.26],[0,.44,-.15,.18]],
  factory:[[-1.23,.63,.79,.27],[1.35,1.35,-.22,.26],[1.39,.45,.30,.28],[-.42,.53,.795,.34]],
  radar:[[.48,.35,.86,.25],[-.63,.42,.86,.25],[.88,.40,.10,.22],[.58,.79,.34,.22]],
  satellite:[[.48,.35,.86,.25],[-.63,.42,.86,.25],[.88,.40,.10,.22],[.58,.79,.34,.22]],
  post:[[-.23,.25,.37,.15],[.40,.23,.08,.15],[.0,.50,.20,.14]],
  refinery:[[.48,.89,.085,.25],[-.55,.75,.62,.26],[.33,.34,.85,.26],[.89,.70,-.34,.22]],
  residence:[[-.47,.94,.85,.27],[.49,.43,.86,.24],[.90,.64,.28,.27],[.42,1.47,.65,.25]],
  shop:[[-.80,.39,.86,.27],[.28,1.30,.86,.29],[1.40,.92,.26,.28],[-.90,1.83,.52,.28]],
  municipal:[[-.69,.47,1.35,.29],[.48,1.33,1.35,.30],[1.40,.96,.21,.29],[.0,2.06,-.082,.25]]
 };
 function shaderDamage(mat){if(applied.has(mat))return mat;applied.add(mat);const compile=mat.onBeforeCompile,cache=mat.customProgramCacheKey.bind(mat);
  mat.onBeforeCompile=function(shader,renderer){compile.call(this,shader,renderer);shader.uniforms.uStructureDamage=amount;shader.uniforms.uStructureHits=centers;
   shader.vertexShader='varying vec3 vStructurePosition;\n'+shader.vertexShader.replace('#include <begin_vertex>','#include <begin_vertex>\nvStructurePosition=(modelMatrix*vec4(position,1.0)).xyz;');
   shader.fragmentShader='varying vec3 vStructurePosition;\nuniform float uStructureDamage;\nuniform vec4 uStructureHits[5];\n'+shader.fragmentShader;
   shader.fragmentShader=shader.fragmentShader.replace('#include <color_fragment>',`#include <color_fragment>
    float soot=0.0;float breach=0.0;float chipped=0.0;
    float grit=fract(sin(dot(floor(vStructurePosition*92.0),vec3(13.19,74.32,29.61)))*45817.3);
    for(int hit=0;hit<5;hit++){
     vec3 fractureDelta=abs(vStructurePosition-uStructureHits[hit].xyz);
     float distanceToHit=max(max(fractureDelta.x*.90,fractureDelta.y*1.12),fractureDelta.z);
     float radius=uStructureHits[hit].w;
     float irregular=distanceToHit+(grit-.5)*radius*.22;
     soot=max(soot,(1.0-smoothstep(radius*.18,radius*(.5+uStructureDamage*.90),irregular))*uStructureDamage);
     float holeRadius=radius*max(0.0,uStructureDamage-.40)*1.15;
     if(uStructureDamage>.43&&irregular<holeRadius)breach=1.0;
     chipped=max(chipped,(1.0-smoothstep(.0,radius*.10,abs(irregular-holeRadius)))*step(.6,uStructureDamage)*step(.27,grit));
    }
    if(breach>.5)discard;
    diffuseColor.rgb=mix(diffuseColor.rgb,vec3(.055,.045,.036),soot*.88);
    diffuseColor.rgb=mix(diffuseColor.rgb,vec3(.31,.29,.25)*(0.72+grit*.28),chipped*.76);
   `);
  };mat.customProgramCacheKey=()=>cache()+'-local-damage-v10';return mat;
 }
 K.maskMaterial=mat=>shaderDamage(oldMask(mat));
 M.build=key=>{const root=oldBuild(key),s=StructuralSequence.states.get(root);
  for(const p of s.pieces){const part=s.state.parts.find(q=>p.pivot.name.startsWith(q.node.name+'-piece-'));p.damageDynamic=!!(part&&part.node.userData.dynamic);if(p.damageDynamic){p.pivot.userData.dynamic=true;p.pivot.userData.motion=part.node.userData.motion;}}
  for(const mat of s.state.allMaterials)if(mat.isMeshStandardMaterial)shaderDamage(mat);root.userData.damageSites=marks[key];return root;
 };
 M.pose=(root,kind,t)=>{
  amount.value=0;if(severity<=0||kind!=='idle')return oldPose(root,kind,t);
  oldPose(root,'idle',t);const s=StructuralSequence.states.get(root);s.group.visible=true;
  for(const p of s.state.parts)if(p.stage!=='foundation'&&!p.node.userData.dynamic)p.node.visible=false;
  for(const p of s.pieces){p.pivot.visible=!p.support&&!p.damageDynamic;p.pivot.position.copy(p.center);p.pivot.quaternion.identity();p.pivot.scale.set(1,1,1);
   if(p.pivot.visible&&p.role==='roof'&&p.size.y<.16&&p.seed%9===0)p.pivot.rotation.z=(p.center.x<0?-1:1)*severity*.055;
  }
  const sites=root.userData.damageSites;for(let i=0;i<5;i++)centers.value[i].set(...(sites[i]||[0,-20,0,.2]));amount.value=severity;root.updateMatrixWorld(true);return root;
 };
 return {set severity(value){severity=value;},get severity(){return severity;},marks};
})();
