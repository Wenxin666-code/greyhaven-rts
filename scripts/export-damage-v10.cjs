const fs=require('fs'),path=require('path'),http=require('http'),crypto=require('crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),out=path.join(root,'art/source/buildings-v10'),severities=[.30,.65,1];let browser;
const fingerprint=crypto.createHash('sha256').update(fs.readFileSync(path.join(out,'damage.js'))).digest('hex');
const server=http.createServer((req,res)=>{const target=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!target.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',target.endsWith('.js')?'text/javascript':target.endsWith('.png')?'image/png':'text/html');fs.createReadStream(target).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});
const png=(file,data)=>{fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,Buffer.from(data.split(',')[1],'base64'));};
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.stack||e.message));await page.goto(`http://127.0.0.1:${server.address().port}/art/source/buildings-v10/index.html`);
 const keys=await page.evaluate(()=>Object.keys(BuildingMasterV08.specs));
 for(const key of keys){await page.evaluate(key=>BuildingBakeV08.open(key),key);const folder=path.join(out,'frames',key,'damage');
  const points=await page.evaluate(()=>{const r=StructuralSequence.current,saved=r.userData.sockets;r.userData.sockets={fire:r.userData.damageSites.map(v=>v.slice(0,3))};const p=BuildingBakeV08.points();r.userData.sockets=saved;return p.sockets.fire;});
  const audit={fingerprint,points,severities,stages:[]};
  for(let n=0;n<severities.length;n++){
   await page.evaluate(v=>{BuildingDamage.severity=v;},severities[n]);const full=await page.evaluate(()=>BuildingBakeV08.endpoint(false)),bg=await page.evaluate(()=>BuildingBakeV08.background());
   for(const [name,data]of Object.entries({['stage-'+(n+1)]:full.base,['stage-'+(n+1)+'-paint']:full.paint,['background-'+(n+1)]:bg.base,['background-'+(n+1)+'-paint']:bg.paint}))png(path.join(folder,name+'.png'),data);
   if(key==='factory')for(let i=0;i<16;i++){const frame=await page.evaluate(i=>BuildingBakeV08.frame('door',i,16),i);png(path.join(folder,'door-'+(n+1),i+'.png'),frame.base);png(path.join(folder,'door-'+(n+1),i+'-paint.png'),frame.paint);}
   audit.stages.push(full.audit);
  }
  fs.writeFileSync(path.join(folder,'audit.json'),JSON.stringify(audit,null,2));await page.evaluate(()=>{BuildingDamage.severity=0;BuildingBakeV08.close();});console.log(key+': three upright damaged surfaces and attached fire sites');
 }
 if(errors.length)throw Error(errors.join('\n'));
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
