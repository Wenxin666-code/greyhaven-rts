// Offline master-model baking. Requires Playwright (or PLAYWRIGHT_MODULE)
// and installed Edge; game runtime only consumes the resulting PNG layers.
const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),out=path.join(root,'art/source/tank-prototype');
const server=http.createServer((req,res)=>{const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!p.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':'text/html');fs.createReadStream(p).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});
let browser;
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage();
 page.on('pageerror',e=>{throw e;});await page.goto(`http://127.0.0.1:${server.address().port}/art/source/tank-prototype/index.html`);
 const packed=path.join(out,'packed');
 fs.mkdirSync(packed,{recursive:true});const audit={},sockets={};
 const layersOnly=process.argv.includes('--layers-only');
 const layers=['hull-base','hull-paint','turret-base','turret-paint'];
 for(const mode of layersOnly?layers:['base','paint','wreck',...layers]){
  const r=await page.evaluate(m=>TankBake.bake(m),mode);fs.writeFileSync(path.join(packed,`tank-${mode}.png`),Buffer.from(r.png.split(',')[1],'base64'));audit[mode]=r.margins;
  if(r.sockets.length)sockets[mode]=r.sockets;
  if(r.bearings.length)fs.writeFileSync(path.join(out,'tank-bearing-check.csv'),'frame,dx,dy\n'+r.bearings.map(x=>x.join(',')).join('\n')+'\n');console.log(mode+': 64 views passed alpha bounds');
 }
 const positions=sockets['hull-base'].map((mount,i)=>[i,...mount,...sockets['turret-base'][i]].join(','));
 fs.writeFileSync(path.join(packed,'tank-sockets.csv'),'frame,mountX,mountY,muzzleX,muzzleY\n'+positions.join('\n')+'\n');
 if(!layersOnly){
  fs.writeFileSync(path.join(out,'audit.json'),JSON.stringify(audit,null,2));
  fs.writeFileSync(path.join(out,'tank-model.json'),JSON.stringify(await page.evaluate(()=>TankBake.model())));
 }
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
