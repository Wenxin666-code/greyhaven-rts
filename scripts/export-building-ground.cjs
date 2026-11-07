// Rebuild the established textured relief geometry; no new painted artwork.
const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..');let browser;
const server=http.createServer((req,res)=>{
 const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));
 if(!p.startsWith(root+path.sep)){res.writeHead(403);return res.end();}
 res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':p.endsWith('.png')?'image/png':'text/html');
 fs.createReadStream(p).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);
});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});
 const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(`http://127.0.0.1:${server.address().port}/art/source/aligned/bake.html`);
 for(const key of ['hq','power','barracks','factory','radar','post','refinery','refining']){
  const png=await page.evaluate(k=>bake(k),key);
  fs.writeFileSync(path.join(root,'art/source/aligned/packed',`iso-${key}.png`),Buffer.from(png.split(',')[1],'base64'));
  console.log('Rebuilt full-footprint relief: '+key);
 }
 if(errors.length)throw Error(errors.join('\n'));
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
