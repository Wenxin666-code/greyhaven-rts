// Source contract check: real 3D round-trip, shared textures and seamless idle loops.
const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),out=path.join(root,'art/source/buildings-v09');let browser;
const server=http.createServer((req,res)=>{const target=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!target.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',target.endsWith('.js')?'text/javascript':target.endsWith('.png')?'image/png':target.endsWith('.json')?'application/json':'text/html');fs.createReadStream(target).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(`http://127.0.0.1:${server.address().port}/art/source/buildings-v09/index.html`);
 const results=await page.evaluate(async()=>{
  const records=[];
  for(const key of Object.keys(BuildingMasterV08.specs)){
   const stats=await BuildingBakeV08.open(key),first=BuildingBakeV08.frame('idle',0,96),last=BuildingBakeV08.frame('idle',96,96);
   const pixels=async url=>{const im=new Image();im.src=url;await im.decode();const c=document.createElement('canvas');c.width=c.height=320;const g=c.getContext('2d');g.drawImage(im,0,0);return g.getImageData(0,0,320,320).data;};
   let max=0,changed=0;for(const layer of ['base','paint']){const a=await pixels(first[layer]),b=await pixels(last[layer]);for(let i=0;i<a.length;i++){const delta=Math.abs(a[i]-b[i]);max=Math.max(max,delta);if(delta>1)changed++;}}
   // Symmetric fan blades can rasterize a handful of edge channels differently
   // after a mathematically identical quarter-turn. Bound both error and area.
   if(max>24||changed>128)throw Error(key+' idle loop discontinuity: '+max+'/'+changed);
   const json=await(await fetch('models/'+key+'.json')).json(),model=await new THREE.ObjectLoader().setResourcePath('models/').parseAsync(json);model.updateMatrixWorld(true);
   let meshes=0,triangles=0;const materials=new Set(),geometry=new Set(),textures=new Set();model.traverse(o=>{if(o.isMesh){meshes++;triangles+=o.geometry.index?o.geometry.index.count/3:o.geometry.attributes.position.count/3;}if(o.geometry)geometry.add(o.geometry);if(o.material){materials.add(o.material);for(const name of ['map','bumpMap'])if(o.material[name])textures.add(o.material[name]);}});
   if(meshes!==stats.meshes||triangles!==stats.triangles||JSON.stringify(model.userData.foundation)!==JSON.stringify(stats.foundation))throw Error('3D round-trip mismatch '+key);
   for(const texture of textures)if(!texture.image||texture.image.width<1)throw Error('Unloaded texture '+key);
   records.push({key,meshes,triangles,foundation:stats.foundation,movingParts:stats.movingParts,idleLoopMaxChannelDelta:max,idleLoopChannelsOverOne:changed,loadedTextures:textures.size});
   geometry.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());textures.forEach(t=>t.dispose());BuildingBakeV08.close();
  }
  return records;
 });
 if(errors.length)throw Error(errors.join('\n'));
 const manifest=JSON.parse(fs.readFileSync(path.join(out,'packed/manifest.json'))),report={date:'2026-11-07',sourceFingerprint:manifest.sourceFingerprint,materialFingerprint:manifest.materialFingerprint,models:results,nativeChecks:'Native simulation and render checks are recorded separately by scripts/build.ps1 -Test -MotionPreview'};
 fs.mkdirSync(path.join(root,'art/review/buildings-v09'),{recursive:true});fs.writeFileSync(path.join(root,'art/review/buildings-v09/validation.json'),JSON.stringify(report,null,2));for(const r of results)console.log(`${r.key}: 3D ${r.meshes} meshes / ${r.triangles} triangles; ${r.loadedTextures} textures loaded; idle loop delta ${r.idleLoopMaxChannelDelta}.`);
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
