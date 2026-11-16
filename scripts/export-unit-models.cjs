// Uses the approved tank's offline renderer; never overwrites archived artwork.
const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const args=process.argv.slice(2),revision=(args.find(a=>a.startsWith('--revision='))||'--revision=04').split('=')[1];
const freight=args.includes('--freight');
if(!['03','04'].includes(revision))throw Error('Supported revisions: 03, 04');
const root=path.resolve(__dirname,'..'),source=freight?'freight-v02':`units-v${revision}`,out=path.join(root,'art/source',source);
const packed=freight?path.join(root,'game/assets/freight-model'):path.join(out,'packed');
const server=http.createServer((req,res)=>{const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!p.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':p.endsWith('.png')?'image/png':'text/html');fs.createReadStream(p).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});
let browser;
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage();
 page.on('pageerror',e=>{throw e;});await page.goto(`http://127.0.0.1:${server.address().port}/art/source/${source}/index.html`);
 fs.mkdirSync(packed,{recursive:true});
 if(!freight)fs.mkdirSync(path.join(out,'models'),{recursive:true});
 const specs=await page.evaluate(()=>UnitMaster.specs),filter=args.filter(a=>a!=='--freight'&&!a.startsWith('--revision='));const audit={};
 for(const key of filter)if(!specs[key])throw Error('Unknown unit: '+key);
 for(const key of Object.keys(specs).filter(k=>!filter.length||filter.includes(k))){
  const modes=freight?(key==='engine'?['base','paint']:['base']):specs[key].foot?['base','paint']:['base','paint','wreck'];
  audit[key]={};for(const mode of modes){
   const r=await page.evaluate(([m,k])=>TankBake.bake(m,k),[mode,key]);fs.writeFileSync(path.join(packed,`${key}-${mode}.png`),Buffer.from(r.png.split(',')[1],'base64'));audit[key][mode]=r.margins;
   console.log(`${key}/${mode}: ${r.margins.length} views passed bounds`);
  }
  if(!freight)fs.writeFileSync(path.join(out,'models',key+'.json'),JSON.stringify(await page.evaluate(k=>TankBake.model(k),key)));
 }
 const auditFile=path.join(out,'audit.json'),previous=fs.existsSync(auditFile)?JSON.parse(fs.readFileSync(auditFile,'utf8')):{};
 fs.writeFileSync(auditFile,JSON.stringify({...previous,...audit},null,2));
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
