const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),out=path.join(root,'art/source/infantry-v12');let browser;
const png=(name,data)=>{const file=path.join(out,name);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,Buffer.from(data.split(',')[1],'base64'));};
const server=http.createServer((req,res)=>{const file=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!file.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.png')?'image/png':'text/html');fs.createReadStream(file).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(`http://127.0.0.1:${server.address().port}/art/source/infantry-v12/index.html`);
 const audit={};
 for(const key of (process.argv.includes('--bunker-only')?[]:['squad','sniper','at-mobile'])){
  for(const mode of ['base','paint']){const f=await page.evaluate(([key,mode])=>TankBake.bake(mode,key),[key,mode]);png(`frames/${key}-${mode}.png`,f.png);audit[key+'-'+mode]=f.margins;}
  await page.evaluate(k=>GaitBake.open(k),key);
  for(let view=0;view<8;view++)for(let i=0;i<12;i++){const f=await page.evaluate(([v,i])=>GaitBake.frame(v,i),[view,i]);png(`frames/gait/${key}/${view}/${i}.png`,f.base);png(`frames/gait/${key}/${view}/${i}-paint.png`,f.paint);}
  await page.evaluate(()=>GaitBake.dispose());console.log(key+' standing + 96 walking poses');
 }
 for(const key of (process.argv.includes('--bunker-only')?[]:['squad','at'])){
  await page.evaluate(k=>StanceBake.open(k),key);
  for(let view=0;view<8;view++)for(let i=0;i<33;i++){const f=await page.evaluate(([v,i])=>StanceBake.frame(v,i),[view,i]);png(`frames/stance/${key}/${view}/${i}.png`,f.base);png(`frames/stance/${key}/${view}/${i}-paint.png`,f.paint);}
  console.log(key+' 264 continuous deployment poses');
 }
 if(process.argv.includes('--infantry-only')){if(errors.length)throw Error(errors.join('\n'));return;}
 await page.evaluate(()=>BuildingBakeV08.open('bunker'));
 const f=await page.evaluate(()=>BuildingBakeV08.endpoint()),b=await page.evaluate(()=>BuildingBakeV08.background());
 for(const [name,im]of [['base',f.base],['paint',f.paint],['background',b.base],['background-paint',b.paint]])png(`frames/bunker/${name}.png`,im);
 audit.bunker=f.audit;audit.points=await page.evaluate(()=>BuildingBakeV08.points());
 for(const action of ['idle','build'])for(let i=0;i<(action==='idle'?96:72);i++){const f=await page.evaluate(([a,i,n])=>BuildingBakeV08.frame(a,i,n),[action,i,action==='idle'?96:72]);png(`frames/bunker/${action}/${i}.png`,f.base);png(`frames/bunker/${action}/${i}-paint.png`,f.paint);}
 for(let level=1;level<=3;level++)for(const kind of ['full','background']){
  const f=await page.evaluate(([level,kind])=>{const old=BuildingMasterV08.pose;BuildingMasterV08.pose=(root,action,t)=>{old(root,action,t);const s=ArchitectureKit.bindings.get(root);for(const mat of s.allMaterials)if(mat.color)mat.color.lerp(new THREE.Color(0x37352e),level*.15);const roof=root.getObjectByName('armored-roof');if(roof&&level>1)roof.rotation.z=.01*level;root.updateMatrixWorld(true);return root;};try{return kind==='full'?BuildingBakeV08.endpoint():BuildingBakeV08.background();}finally{BuildingMasterV08.pose=old;}},[level,kind]);
  const name=`damage-${level}${kind==='background'?'-background':''}`;png(`frames/bunker/${name}.png`,f.base);png(`frames/bunker/${name}-paint.png`,f.paint);
 }
 fs.writeFileSync(path.join(out,'audit.json'),JSON.stringify(audit,null,2));if(errors.length)throw Error(errors.join('\n'));console.log('Bunker construction, idle, damage complete');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
