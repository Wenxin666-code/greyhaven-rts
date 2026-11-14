// Offline authoring. Reuse the local Three.js, architecture kit and textures.
const fs = require('fs');
const http = require('http');
const path = require('path');
const {chromium} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const root = path.resolve(__dirname, '..');
const baseFlag = process.argv.find(value => value.startsWith('--base-root='));
const base = baseFlag ? path.resolve(baseFlag.slice('--base-root='.length)) : root;
const target = path.join(root, 'art/source/harbor-v01');
const requested = process.argv.filter(value => value.startsWith('--asset=')).map(value => value.slice(8));
const types = {'.js':'text/javascript; charset=utf-8','.html':'text/html; charset=utf-8','.png':'image/png'};

const server = http.createServer((request,response) => {
    const url = decodeURIComponent(new URL(request.url,'http://localhost').pathname);
    if (url === '/favicon.ico') { response.writeHead(204); response.end(); return; }
    if (!url.startsWith('/art/source/')) { response.writeHead(404); response.end(); return; }
    const relative = url.slice(1);
    const file = [root,base].map(folder=>path.resolve(folder,relative))
        .find((candidate,index)=>candidate.startsWith([root,base][index]+path.sep) && fs.existsSync(candidate));
    if (!file) { response.writeHead(404); response.end(); return; }
    response.setHeader('Content-Type',types[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).on('error',()=>response.end()).pipe(response);
});

function writePng(file, data){
    fs.writeFileSync(file,Buffer.from(data.slice(data.indexOf(',')+1),'base64'));
}

let browser;
(async()=>{
    await new Promise((resolve,reject)=>{
        server.once('error',reject);
        server.listen(0,'127.0.0.1',resolve);
    });
    browser = await chromium.launch({channel:'msedge',headless:true,args:['--enable-unsafe-swiftshader']});
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if(message.type()==='error')errors.push(message.text()); });
    await page.goto('http://127.0.0.1:'+server.address().port+'/art/source/harbor-v01/index.html');
    const names = await page.evaluate(()=>Object.keys(HarborMaster.specs));
    for (const key of requested) if (!names.includes(key)) throw Error('Unknown harbor asset '+key);
    const manifest = path.join(target,'frames/manifest.json');
    const models = requested.length && fs.existsSync(manifest) ? JSON.parse(fs.readFileSync(manifest,'utf8')) : {};
    for (const key of names.filter(name => !requested.length || requested.includes(name))){
        const spec = await page.evaluate(key=>HarborBake.open(key),key);
        const directory = path.join(target,'frames',key);
        fs.mkdirSync(directory,{recursive:true});
        for (let index=0;index<spec.frames;index++){
            const frame=await page.evaluate(([index,count])=>HarborBake.frame(index,count),[index,spec.frames]);
            writePng(path.join(directory,index+'.png'),frame.base);
            writePng(path.join(directory,index+'-paint.png'),frame.paint);
        }
        await page.evaluate(()=>HarborBake.close());
        models[key]=spec;
        console.log(key+' '+spec.frames+' frames; '+spec.parts+' components');
    }
    if(errors.length)throw Error(errors.join('\n'));
    fs.writeFileSync(manifest,JSON.stringify(models,null,2)+'\n');
})().catch(error=>{console.error(error);process.exitCode=1;}).finally(async()=>{
    if(browser)await browser.close();
    server.close();
});
