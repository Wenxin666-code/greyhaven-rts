const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),out=path.join(root,'art/source/animation-v06');
const server=http.createServer((req,res)=>{const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!p.startsWith(root+path.sep)){res.writeHead(403);return res.end();}res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':p.endsWith('.png')?'image/png':'text/html');fs.createReadStream(p).on('error',()=>{res.writeHead(404);res.end();}).pipe(res);});let browser;
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(`http://127.0.0.1:${server.address().port}/art/source/buildings-v05/index.html`);
 fs.mkdirSync(path.join(out,'packed'),{recursive:true});const filter=process.argv.slice(2),audit={};
 for(const key of ['hq','power','barracks','factory','radar','satellite','post','refinery'].filter(k=>!filter.length||filter.includes(k)))for(const kind of ['build','collapse'])for(const mode of ['base','paint']){
  const r=await page.evaluate(([k,a,m])=>BuildingBake.animation(k,a,m),[key,kind,mode]);
  fs.writeFileSync(path.join(out,'packed',`anim-${key}-${kind}${mode==='paint'?'-paint':''}.png`),Buffer.from(r.png.split(',')[1],'base64'));audit[key+'/'+kind+'/'+mode]=r.audit;console.log(key+'/'+kind+'/'+mode+': '+r.audit.length+' frames');
 }
 const file=path.join(out,'building-audit.json'),old=fs.existsSync(file)?JSON.parse(fs.readFileSync(file,'utf8')):{};fs.writeFileSync(file,JSON.stringify({...old,...audit},null,2));if(errors.length)throw Error(errors.join('\n'));
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();server.close();});
