const fs=require('fs'),path=require('path'),http=require('http');const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),out=path.join(root,'art/source/infantry-v12');let browser;
const png=(name,data)=>{const file=path.join(out,name);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,Buffer.from(data.split(',')[1],'base64'));};
const server=http.createServer((req,res)=>{const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!p.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':p.endsWith('.png')?'image/png':'text/html');fs.createReadStream(p).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(`http://127.0.0.1:${server.address().port}/art/source/infantry-v12/index.html`);
 for(const key of ['squad','sniper','at']){
  for(const deployed of key==='sniper'?[false]:[false,true]){
   await page.evaluate(([k,d])=>CombatBake.open(k,d),[key,deployed]);
   for(let v=0;v<8;v++)for(let i=0;i<17;i++){const f=await page.evaluate(([v,i])=>CombatBake.frame(v,i),[v,i]);for(const layer of ['base','paint'])png(`frames/combat/${key}${deployed?'-deployed':''}/${v}/${i}${layer==='paint'?'-paint':''}.png`,f[layer]);}
   console.log(key+(deployed?' deployed':' mobile')+': 136 aim / recoil poses');
  }
  // Lower the weapon with a solved grip/arm pose before animating legs.
  await page.evaluate(async key=>{const original=UnitMaster.build;UnitMaster.build=(k,w)=>{const model=original(k,w),rig=CombatRig.capture(model,key,false);CombatRig.pose(rig,0,0);return model;};try{await GaitBake.open(key==='at'?'at-mobile':key);}finally{UnitMaster.build=original;}},key);
  for(let v=0;v<8;v++)for(let i=0;i<12;i++){const f=await page.evaluate(([v,i])=>GaitBake.frame(v,i),[v,i]);for(const layer of ['base','paint'])png(`frames/gait/${key==='at'?'at-mobile':key}/${v}/${i}${layer==='paint'?'-paint':''}.png`,f[layer]);}
  await page.evaluate(()=>GaitBake.dispose());console.log(key+': 96 low-ready marching poses');
 }
 if(errors.length)throw Error(errors.join('\n'));
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
