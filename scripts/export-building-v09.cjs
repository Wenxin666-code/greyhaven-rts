// Offline authoring only. The native game consumes bounded, individually decoded PNG frames.
const fs=require('fs'),path=require('path'),http=require('http'),crypto=require('crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),out=path.join(root,'art/source/buildings-v09');
const args=process.argv.slice(2),staticOnly=args.includes('--static'),modelsOnly=args.includes('--models-only'),keys=args.filter(x=>!x.startsWith('--'));
const server=http.createServer((req,res)=>{const target=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!target.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',target.endsWith('.js')?'text/javascript':target.endsWith('.png')?'image/png':'text/html');fs.createReadStream(target).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});
let browser;
const writePNG=(file,data)=>{fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,Buffer.from(data.split(',')[1],'base64'));};
async function writeModel(page,key){
 const model=await page.evaluate(async key=>{
  await BuildingMasterV08.ready;const root=BuildingMasterV08.build(key);BuildingMasterV08.pose(root,'idle',0);
  const model=root.toJSON(),geometry=new Set(),materials=new Set();root.traverse(o=>{if(o.geometry)geometry.add(o.geometry);if(o.material)materials.add(o.material);});
  // Parametric JSON omits custom UVs/translations. EdgesGeometry also lacks a
  // fromJSON constructor in the bundled library. Export explicit buffers once.
  model.geometries=Array.from(geometry,g=>{const plain=new THREE.BufferGeometry().copy(g);plain.uuid=g.uuid;const record=plain.toJSON();plain.dispose();return record;});delete model.shapes;
  geometry.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());return model;
 },key),textures=new Map(model.textures.map(t=>[t.uuid,t])),externalImages=new Map();
 for(const mat of model.materials)if(mat.map&&['paint','steel','edge','wall','concrete'].includes(mat.name))externalImages.set(textures.get(mat.map).image,'../materials/'+(['wall','concrete'].includes(mat.name)?'cast-concrete.png':'coated-armor.png'));
 for(const image of model.images)if(externalImages.has(image.uuid))image.url=externalImages.get(image.uuid);
 fs.writeFileSync(path.join(out,'models',key+'.json'),JSON.stringify(model));
}
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true,args:['--disable-background-timer-throttling']});
 const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.stack||e.message));await page.goto(`http://127.0.0.1:${server.address().port}/art/source/buildings-v09/index.html`);
 const specs=await page.evaluate(()=>BuildingMasterV08.specs),chosen=keys.length?keys:Object.keys(specs),fingerprint=crypto.createHash('sha256').update(['kit.js','models.js','bake.js'].map(n=>fs.readFileSync(path.join(out,n))).join('\n')).digest('hex');
 fs.mkdirSync(path.join(out,'models'),{recursive:true});fs.mkdirSync(path.join(out,'review'),{recursive:true});
 for(const key of chosen){
  if(!specs[key])throw Error('Unknown key '+key);if(modelsOnly){await writeModel(page,key);console.log(key+' portable model exported');continue;}const marker=path.join(out,'frames',key,'audit.json'),materialFingerprint=crypto.createHash('sha256').update(fs.readFileSync(path.join(out,'materials/coated-armor.png'))).update(fs.readFileSync(path.join(out,'materials/cast-concrete.png'))).digest('hex');
  const previous=args.includes('--resume')&&fs.existsSync(marker)?JSON.parse(fs.readFileSync(marker)):null;
  if(previous&&previous.fingerprint===fingerprint&&previous.materialFingerprint===materialFingerprint){console.log(key+' already complete; retained');continue;}
  const stats=await page.evaluate(k=>BuildingBakeV08.open(k),key);console.log(key+': '+stats.meshes+' meshes, '+stats.triangles+' triangles, '+stats.movingParts+' moving groups');
  const complete=await page.evaluate(()=>BuildingBakeV08.endpoint(false)),wreck=await page.evaluate(()=>BuildingBakeV08.endpoint(true));
  for(const [name,data]of Object.entries({base:complete.base,paint:complete.paint,wreck:wreck.base,'wreck-paint':wreck.paint})){writePNG(path.join(out,'frames',key,name+'.png'),data);}
  for(const angle of [0,90,180,270])writePNG(path.join(out,'review',`${key}-${angle}.png`),await page.evaluate(([a,c])=>BuildingBakeV08.hero(a,c),[angle,['residence','shop','municipal'].includes(key)?[222,225,221]:[62,133,190]]));
  await writeModel(page,key);
  const record={fingerprint,materialFingerprint,stats,complete:complete.audit,wreck:wreck.audit,points:await page.evaluate(()=>BuildingBakeV08.points()),frames:{}};
  if(!staticOnly){
   const bg=await page.evaluate(()=>BuildingBakeV08.background());writePNG(path.join(out,'frames',key,'idle-background.png'),bg.base);writePNG(path.join(out,'frames',key,'idle-background-paint.png'),bg.paint);
   for(const kind of ['build','collapse','idle',...(key==='factory'?['door']:[]),...(key==='hq'?['deploy']:[])]){
    const count=kind==='door'?16:kind==='idle'||kind==='deploy'?96:48,sequence=[];
    for(let i=0;i<count;i++){
     const f=await page.evaluate(([a,i,n])=>BuildingBakeV08.frame(a,i,n),[kind,i,count]);
     writePNG(path.join(out,'frames',key,kind,`${i}.png`),f.base);writePNG(path.join(out,'frames',key,kind,`${i}-paint.png`),f.paint);sequence.push(f.audit);
    }
    record.frames[kind]=sequence;console.log('  '+key+'/'+kind+': '+count+' layered frames');
   }
   fs.writeFileSync(marker,JSON.stringify(record,null,2));
  }
  fs.writeFileSync(path.join(out,'frames',key,'static-audit.json'),JSON.stringify({...record,frames:{}},null,2));
  if(errors.length)throw Error(errors.join('\n'));await page.evaluate(()=>BuildingBakeV08.close());
 }
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
