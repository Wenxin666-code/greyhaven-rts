/* Optional preview tooling. Install Playwright or set PLAYWRIGHT_MODULE to its
   package path, then run: node scripts/export-animation-review.cjs [--export].
   ffmpeg and Python/Pillow must be available for export. Does not build or modify the game. */
const fs=require('fs'),path=require('path'),http=require('http'),os=require('os');
const {spawnSync}=require('child_process');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),review=path.join(root,'art/review/animation-v01');
const types={'.html':'text/html','.js':'text/javascript','.css':'text/css','.png':'image/png'};
const server=http.createServer((req,res)=>{
 try{const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));
  if(!p.startsWith(root+path.sep)){res.writeHead(403);return res.end()}
  res.setHeader('Content-Type',types[path.extname(p)]||'application/octet-stream');fs.createReadStream(p).on('error',()=>{res.writeHead(404);res.end()}).pipe(res);
 }catch{res.writeHead(400);res.end()}
});
let browser,temp;
async function run(){
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 browser=await chromium.launch({channel:'msedge',headless:true});
 const page=await browser.newPage({viewport:{width:1488,height:1140},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(`http://127.0.0.1:${server.address().port}/art/review/animation-v01/index.html`);await page.evaluate(()=>window.ready);await page.click('#play');
 await page.evaluate(()=>window.renderAt(.24));await page.screenshot({path:path.join(review,'验收页.png'),fullPage:true});
 for(const id of ['fire','tank','build','ruin','repair','fall','walk']){
  await page.click(`[data-id=${id}]`);const state=await page.evaluate(()=>window.previewState());if(state.active!==id)throw Error('Selection failed: '+id);
 }
 await page.click('#step');if((await page.evaluate(()=>window.previewState())).frame!==1)throw Error('Frame step failed');
 await page.selectOption('#ground','light');await page.click('#grid');let state=await page.evaluate(()=>window.previewState());if(state.ground!=='light'||state.grid)throw Error('Review controls failed');
 await page.selectOption('#ground','harbor');await page.click('#grid');
 for(const [id,t] of [['walk',.32],['build',3.5],['ruin',2.35]]){
  await page.evaluate(([id,t])=>{window.selectClip(id);window.renderAt(t)},[id,t]);
  fs.writeFileSync(path.join(review,`${id}-detail.png`),Buffer.from((await page.evaluate(()=>document.getElementById('stage').toDataURL())).split(',')[1],'base64'));
 }
 if(errors.length)throw Error(errors.join('\n'));
 console.log('Seven clips loaded; selection, pause, frame-step, background and grid checks passed.');
 if(!process.argv.includes('--export'))return;
 temp=fs.mkdtempSync(path.join(os.tmpdir(),'greyhaven-art-review-'));
 const fps=25,count=200;
 for(let i=0;i<count;i++){
  const data=await page.evaluate(t=>{window.renderAt(t);return document.getElementById('overview').toDataURL('image/png')},i/fps);
  fs.writeFileSync(path.join(temp,String(i).padStart(4,'0')+'.png'),Buffer.from(data.split(',')[1],'base64'));
  if(i%50===0)console.log(`Preview render ${i}/${count}`);
 }
 function encode(args){const r=spawnSync('ffmpeg',['-y','-loglevel','error',...args],{encoding:'utf8',windowsHide:true});if(r.error||r.status)throw Error(r.error||r.stderr)}
 const input=['-framerate',String(fps),'-i',path.join(temp,'%04d.png')];
 function webp(dir,out){const r=spawnSync('python',[path.join(__dirname,'pack-animation-preview.py'),dir,out,String(fps)],{encoding:'utf8',windowsHide:true});if(r.error||r.status)throw Error(r.error||r.stderr)}
 webp(temp,path.join(review,'动画总览.webp'));
 encode([...input,'-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(review,'动画总览.mp4')]);
 // Deterministic contact sheet animation; retain original keyframe alpha unchanged.
 for(const [id,filename] of [['walk','士兵行进'],['fire','士兵射击'],['tank','坦克开炮'],['build','建筑搭建'],['ruin','建筑损毁'],['repair','工兵维修'],['fall','士兵倒地']]){
  const n=await page.evaluate(([id,fps])=>Math.ceil(clips.find(c=>c.id===id).seconds*fps),[id,fps]);
  const seq=path.join(temp,id);fs.mkdirSync(seq);
  for(let i=0;i<n;i++){
   const data=await page.evaluate(([id,t])=>{
    const c=clips.find(c=>c.id===id),f=frameAt(c,t),can=document.createElement('canvas');can.width=512;can.height=440;const g=can.getContext('2d');
    terrain(g,0,0,512,440);g.fillStyle='#14232a';g.fillRect(0,0,512,48);text(g,c.title,20,31,19,'#e2ba75',true);
    sprite(g,c,f,256,387,440);g.fillStyle='#14232a';g.fillRect(0,404,512,36);text(g,c.phases[f],20,429,13,'#c5d0ca');text(g,'样片 / 代表视角',370,429,12,'#91a6a6');return can.toDataURL();
   },[id,i/fps]);
   fs.writeFileSync(path.join(seq,String(i).padStart(4,'0')+'.png'),Buffer.from(data.split(',')[1],'base64'));
  }
  webp(seq,path.join(review,filename+'.webp'));console.log('Exported '+filename);
 }
 console.log('Exports: '+review);
}
run().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{if(browser)await browser.close();server.close();/* Keep temporary frames for reproducibility; OS temp cleanup can remove them. */});
