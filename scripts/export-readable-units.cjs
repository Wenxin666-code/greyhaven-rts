/* npm install playwright, or set PLAYWRIGHT_MODULE to an existing installation.
   Offline artwork compilation only: built-in Three.js + installed Edge.
   node scripts/export-readable-units.cjs */
const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),out=path.join(root,'art/source/readable-units');
const server=http.createServer((req,res)=>{
 const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));
 if(!p.startsWith(root+path.sep)){res.writeHead(403);return res.end();}
 res.setHeader('Content-Type',path.extname(p)==='.js'?'text/javascript':path.extname(p)==='.html'?'text/html':'application/octet-stream');
 fs.createReadStream(p).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);
});
const save=(file,data)=>fs.writeFileSync(file,Buffer.from(data.split(',')[1],'base64'));
let browser;
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage();
 page.on('pageerror',e=>{throw e;});await page.goto(`http://127.0.0.1:${server.address().port}/art/source/readable-units/index.html`);
 fs.mkdirSync(path.join(out,'packed'),{recursive:true});const audit={};
 for(const key of await page.evaluate(()=>Object.keys(UnitBaker.specs))){
  const data=await page.evaluate(k=>UnitBaker.bake(k),key);
  save(path.join(out,'packed',(['squad','at','engineer'].includes(key)?'hd-':'iso-')+key+'.png'),data.png);
  audit[key]=data.metrics;
  if(data.bearing.length)fs.writeFileSync(path.join(out,'tank-bearing-check.csv'),'frame,dx,dy\n'+data.bearing.map(r=>r.join(',')).join('\n')+'\n');
  for(const side of [0,1]){
   const coverage=data.metrics.filter(x=>x.team===side).map(x=>x.teamPanelFraction),minimum=Math.min(...coverage);
   if(minimum<.12)throw Error(key+' faction color too small at a heading: '+minimum);
   console.log(`${key} faction ${side}: min ${Math.round(minimum*100)}%, mean ${Math.round(coverage.reduce((a,b)=>a+b)/coverage.length*100)}%`);
  }
 }
 fs.writeFileSync(path.join(out,'audit.json'),JSON.stringify(audit,null,2)+'\n');
 save(path.join(out,'单位重绘总览.png'),await page.evaluate(()=>UnitBaker.roster()));
 console.log('All heading margins, visible faction coverage and atlases passed.');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
