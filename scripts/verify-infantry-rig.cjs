// Meaningful geometry checks: weapons must remain rigid through leg/stance motion,
// reverse deployment must retrace the same geometry, endpoints must not pop.
const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..');let browser;
const server=http.createServer((req,res)=>{const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!p.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':p.endsWith('.png')?'image/png':'text/html');fs.createReadStream(p).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage();await page.goto(`http://127.0.0.1:${server.address().port}/art/source/infantry-v12/index.html`);
 const report=await page.evaluate(async()=>{
  const T=THREE,report={};await TankMaster.ready;
  function snapshot(root,role){root.updateMatrixWorld(true);const a=[];root.traverse(o=>{if(!o.isMesh||role&&o.userData.weaponRole!==role)return;const p=o.geometry.attributes.position;for(const i of [0,Math.floor(p.count/3),Math.floor(p.count*2/3)])a.push(new T.Vector3().fromBufferAttribute(p,i).applyMatrix4(o.matrixWorld));});return a;}
  function delta(a,b){if(a.length!==b.length)throw Error('mesh switched mid-animation');return Math.max(...a.map((v,i)=>v.distanceTo(b[i])));}
  function rigidity(a,b){if(a.length!==b.length)throw Error('weapon topology switched');return Math.max(...a.map((v,i)=>Math.abs(v.distanceTo(a[0])-b[i].distanceTo(b[0]))));}
  for(const key of ['squad','at']){
   await StanceBake.open(key);let previous,forward=[],original,errors=0,largestStep=0,largestAt=0,largestPoint=0;
   for(let i=0;i<=32;i++){const root=StanceBake.model(0,i),all=snapshot(root),weapons=['rifle',...(key==='at'?['launcher']:[])].map(r=>snapshot(root,r));if(!i)original=weapons;else{errors=Math.max(errors,...weapons.map((p,j)=>rigidity(original[j],p)));if(delta(previous,all)>largestStep){largestStep=delta(previous,all);largestAt=i;largestPoint=all.findIndex((v,j)=>v.distanceTo(previous[j])===largestStep);}}forward.push(all);previous=all;}
   for(let i=32;i>=0;i--)if(delta(forward[i],snapshot(StanceBake.model(0,i)))>1e-6)throw Error('reverse mismatch '+key);
   const end=StanceBake.model(0,32),before=snapshot(end),rig=CombatRig.capture(end,key,true);CombatRig.pose(rig,0,0);const pop=delta(before,snapshot(end));
   const meshLabels=[];end.traverse(o=>{if(o.isMesh)meshLabels.push({material:o.material.name,tags:o.userData});});const offending=meshLabels[Math.floor(largestPoint/3)];if(errors>.0001||pop>.02||largestStep>.35)throw Error(JSON.stringify({key,errors,pop,largestStep,largestAt,largestPoint,offending}));
   report[key]={rigidError:errors,endpointError:pop,maxFrameTravel:largestStep,reverse:'identical',frames:33};
  }
  for(const key of ['squad','sniper','at']){
   const base=UnitMaster.build;UnitMaster.build=(k,w)=>{const model=base(k,w),rig=CombatRig.capture(model,key,false);CombatRig.pose(rig,0,0);return model;};
   try{await GaitBake.open(key==='at'?'at-mobile':key);}finally{UnitMaster.build=base;}
   const original=snapshot(GaitBake.model(0,0),'rifle');let error=0;
   for(let i=1;i<12;i++)error=Math.max(error,rigidity(original,snapshot(GaitBake.model(0,i),'rifle')));
   if(error>.0001)throw Error('walking bends gun '+key+': '+error);report[key+'-walking']={rigidError:error,frames:12};GaitBake.dispose();
  }
  return report;
 });
 const out=path.join(root,'docs/infantry-motion-20261110');fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'rig-verification.json'),JSON.stringify(report,null,2));console.log('PASS rigid weapons, reversible deployment and pose continuity');console.log(report);
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
