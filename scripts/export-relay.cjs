// Offline asset authoring. --base-root overlays unchanged shared assets during staging.
const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),arg=process.argv.find(a=>a.startsWith('--base-root=')),base=arg?path.resolve(arg.split('=').slice(1).join('=')):root;
const out=path.join(root,'art/source/relay-v01'),frames=path.join(out,'frames');
function resolve(p){const local=path.resolve(root,'.'+p);if(!local.startsWith(root+path.sep))throw Error('Invalid authoring path');return fs.existsSync(local)?local:path.resolve(base,'.'+p);}
const server=http.createServer((req,res)=>{try{const file=resolve(decodeURIComponent(new URL(req.url,'http://localhost').pathname));res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.png')?'image/png':'text/html');fs.createReadStream(file).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);}catch(e){res.writeHead(403);res.end();}});
let browser;const png=(p,data)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,Buffer.from(data.split(',')[1],'base64'));};
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});
 const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(`http://127.0.0.1:${server.address().port}/art/source/relay-v01/index.html`);
 const stats=await page.evaluate(()=>BuildingBakeV08.open('relay'));console.log(JSON.stringify(stats));
 const whole=await page.evaluate(()=>BuildingBakeV08.endpoint()),background=await page.evaluate(()=>BuildingBakeV08.background());
 for(const [name,im]of [['base',whole.base],['paint',whole.paint],['background',background.base],['background-paint',background.paint]])png(path.join(frames,name+'.png'),im);
 for(const [name,color]of [['blue',[62,133,190]],['red',[201,72,55]],['white',[222,225,221]],['violet',[137,84,190]]])png(path.join(out,'review',name+'.png'),await page.evaluate(c=>BuildingBakeV08.hero(0,c),color));
 const audit={stats,complete:whole.audit,points:await page.evaluate(()=>BuildingBakeV08.points()),idle:[]};
 for(let i=0;i<96;i++){const f=await page.evaluate(i=>BuildingBakeV08.frame('idle',i,96),i);png(path.join(frames,'idle',i+'.png'),f.base);png(path.join(frames,'idle',i+'-paint.png'),f.paint);audit.idle.push(f.audit);}
 console.log('96 idle frames exported');
 // Preserve upright geometry for damaged states: only surfaces and specific fittings change.
 for(let level=1;level<=3;level++)for(const kind of ['full','background']){
  const f=await page.evaluate(([level,kind])=>{
   const original=BuildingMasterV08.pose;
   BuildingMasterV08.pose=(root,action,t)=>{original(root,action,t);const s=ArchitectureKit.bindings.get(root);
    for(const mat of s.allMaterials)if(mat.color){mat.color.lerp(new THREE.Color(0x37352e),level*.14);if(mat.name==='lamp')mat.emissiveIntensity*=1-level*.26;}
    // Broken panels reveal dark backing while the load-bearing tower remains upright.
    for(const name of ['armored-roof','front-panels']){const n=root.getObjectByName(name);if(n&&level>=2){n.rotation.z+=level*.012;n.position.y-=level*.009;}}
    root.updateMatrixWorld(true);return root;};
   try{return kind==='full'?BuildingBakeV08.endpoint():BuildingBakeV08.background();}finally{BuildingMasterV08.pose=original;}
  },[level,kind]);
  png(path.join(frames,`damage-${level}${kind==='background'?'-background':''}.png`),f.base);png(path.join(frames,`damage-${level}${kind==='background'?'-background':''}-paint.png`),f.paint);
 }
 fs.writeFileSync(path.join(out,'audit.json'),JSON.stringify(audit,null,2));
 if(errors.length)throw Error(errors.join('\n'));console.log('Relay authoring complete');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
