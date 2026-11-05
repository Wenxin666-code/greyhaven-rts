/* Preview only. Original imagegen sheets remain unmodified; sampling, timing and
   ground anchors live here so approval never changes the current game build. */
'use strict';
const clips=[
 {id:'walk',title:'士兵行进',tag:'INFANTRY / LOCOMOTION',seconds:.96,game:38,desc:'交替迈腿、屈膝与重心起伏。持枪上身保持稳定，循环首尾连续衔接。',phases:['左脚着地','承重','右腿经过','右膝前提','右脚摆出','准备落脚','右脚着地','承重','左腿经过','左膝前提','左脚摆出','准备落脚'],hold:Array(12).fill(.08)},
 {id:'fire',title:'士兵三连发',tag:'INFANTRY / BURST FIRE',game:38,desc:'抬枪瞄准、三次短促枪口火光、肩部后坐与回稳，弹壳和硝烟跟随射击节奏。',phases:['低姿持枪','据枪瞄准','第一发','后坐','回稳','第二发','后坐','回稳','第三发','后坐','观察','待命'],hold:[.25,.32,.065,.09,.12,.065,.09,.12,.065,.15,.35,.45]},
 {id:'tank',title:'坦克开炮',tag:'ARMOUR / MAIN GUN',game:98.7,desc:'炮管后坐、复进与炮口扬尘。车体保持稳定，火光短促，烟尘随后消散。',phases:['瞄准','稳定','击发','后坐峰值','复进','复进','复位','余烟','余烟','待命','待命','待命'],hold:[.45,.45,.065,.11,.15,.18,.2,.2,.22,.25,.25,.5]},
 {id:'build',title:'建筑搭建',tag:'STRUCTURE / ASSEMBLY',game:240,desc:'先落地基，再架立柱与钢梁，安装墙体和屋面，最后接通灯光并撤走施工架。',phases:['地基定位','材料进场','竖立立柱','搭设框架','架设横梁','安装后墙','围护施工','墙体完成','屋面安装','设备装配','撤除脚手架','建造完成'],hold:[.6,.5,.5,.6,.6,.6,.6,.6,.6,.6,.6,1.4]},
 {id:'ruin',title:'建筑损毁',tag:'STRUCTURE / COLLAPSE',game:240,desc:'受损冒烟、内部起火、承重结构断裂、屋顶下坠，烟尘散去后留下可辨认的废墟。',phases:['完好','轻度受损','重度受损','内部起火','屋面断裂','侧墙垮塌','屋顶下坠','结构坍塌','粉尘散去','余火','冷却','残骸'],hold:[.7,.45,.45,.18,.16,.16,.2,.35,.55,.55,.65,1.4]},
 {id:'repair',title:'工兵维修',tag:'ENGINEER / MAINTENANCE',game:38,desc:'放下工具箱、蹲下作业、使用工具并检查，结束后收起工具。少量火花标明维修位置。',phases:['携箱待命','放下工具箱','取用工具','跪姿就位','开始作业','工具火花','调整工具','继续作业','检查修复','收回工具','提起工具箱','恢复待命'],hold:[.35,.25,.25,.3,.3,.35,.3,.35,.4,.3,.3,.55]},
 {id:'fall',title:'士兵倒地',tag:'INFANTRY / FALL',game:38,desc:'受击失衡、屈膝、手撑地、侧身落地，再停留于倒地姿态。装备随身体落下。',phases:['站立','受击失衡','屈膝','单膝触地','身体倾倒','手撑地','侧身落地','躺倒','静止','静止','静止','静止'],hold:[.5,.12,.13,.13,.13,.13,.13,.18,.25,.25,.25,.8]}
];
clips.forEach(c=>{c.seconds=c.hold.reduce((a,b)=>a+b,0);c.image=new Image();c.image.src='sheets/'+c.id+(['ruin','repair'].includes(c.id)?'-v2':'')+'.png'});
let active=clips[0],playing=true,time=0,last=0,speed=1,grid=true,ground='harbor';
const $=id=>document.getElementById(id),stage=$('stage'),overview=$('overview');
function frameAt(c,t){let left=((t%c.seconds)+c.seconds)%c.seconds;for(let i=0;i<12;i++){if(left<c.hold[i])return i;left-=c.hold[i]}return 11}
function text(ctx,s,x,y,size=15,color='#d8dedb',bold=false){ctx.font=(bold?'600 ':'')+size+'px "Microsoft YaHei UI", "Segoe UI", sans-serif';ctx.fillStyle=color;ctx.fillText(s,x,y)}
function line(ctx,points,color='#80928b30',width=1){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();points.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.stroke()}
function terrain(ctx,x,y,w,h,small=false){
 const bg=ground==='light'?'#c5c8bc':ground==='dark'?'#10191e':'#2d3b3d';ctx.fillStyle=bg;ctx.fillRect(x,y,w,h);ctx.save();ctx.beginPath();ctx.rect(x,y,w,h);ctx.clip();
 if(ground==='harbor'){
  ctx.fillStyle='#ffffff02';for(let i=0;i<550;i++){const px=x+((i*197)%997)/997*w,py=y+((i*373)%991)/991*h;ctx.fillRect(px,py,2,1)}
 }
 if(grid){const gap=small?37:52;for(let n=-35;n<=35;n++){line(ctx,[[x+n*gap,y],[x+n*gap+h*2,y+h]],ground==='light'?'#71817c24':'#7e9b9430');line(ctx,[[x+n*gap,y],[x+n*gap-h*2,y+h]],ground==='light'?'#71817c24':'#7e9b9430')}}
 ctx.restore();
}
function sprite(ctx,c,frame,cx,baseline,width){
 const iw=c.image.naturalWidth/4,ih=c.image.naturalHeight/3,h=width*ih/iw;
 // Lock the source-cell anchor. No independent per-frame resizing or silhouette fitting.
 ctx.drawImage(c.image,(frame%4)*iw,Math.floor(frame/4)*ih,iw,ih,cx-width*.5,baseline-h*.94,width,h);
}
function contact(ctx,x,y,r){ctx.strokeStyle='#89c1b37c';ctx.lineWidth=1;ctx.beginPath();ctx.ellipse(x,y,r,r*.5,0,0,Math.PI*2);ctx.stroke()}
function renderStage(t){
 const ctx=stage.getContext('2d'),c=active,f=frameAt(c,t);terrain(ctx,0,0,1060,620);
 ctx.fillStyle='#122026bc';ctx.fillRect(714,0,346,620);line(ctx,[[714,0],[714,620]],'#74817e44');
 text(ctx,'动作细节',34,42,15,'#e4bc77',true);text(ctx,'参考游戏尺寸',741,42,15,'#e4bc77',true);
 text(ctx,'放大查看材质、姿态和烟尘',34,68,13,'#9aacae');text(ctx,'缩小后检查动作是否仍然清楚',741,68,13,'#9aacae');
 const large=(c.id==='build'||c.id==='ruin')?560:540;
 if(['walk','fire','repair','fall'].includes(c.id))contact(ctx,355,534,33);
 sprite(ctx,c,f,355,534,large);
 if(['walk','fire','repair','fall'].includes(c.id))contact(ctx,884,326,c.game*.22);sprite(ctx,c,f,884,326,c.game);
 text(ctx,c.phases[f],34,590,20,'#ede2c6',true);
 text(ctx,'蓝本比例 · 会随游戏镜头缩放',741,408,13,'#97aaad');
 text(ctx,'本轮为代表方向动作样片',741,432,13,'#97aaad');
 text(ctx,'12 张关键帧 / 逐帧查看可暂停',741,456,13,'#97aaad');
 $('readout').textContent=String(f+1).padStart(2,'0')+' / 12　'+c.phases[f];
 $('clock').textContent=(t%c.seconds).toFixed(2)+'s';$('duration').textContent=c.seconds.toFixed(2)+'s';$('timeline').value=Math.floor(t%c.seconds/c.seconds*1000);
}
function renderOverview(t){
 const ctx=overview.getContext('2d');ctx.fillStyle='#122027';ctx.fillRect(0,0,1440,950);
 text(ctx,'断线前沿 / 动画样片',30,40,25,'#ebe5d6',true);text(ctx,'灰港美术蓝本 · 代表视角 · 待验收',30,68,14,'#a1b0b1');text(ctx,'ART REVIEW 01',1220,44,15,'#dfb36a',true);
 for(let i=0;i<8;i++){
  const x=22+i%4*354,y=96+Math.floor(i/4)*416,w=334,h=396;
  ctx.fillStyle='#1d2d34';ctx.fillRect(x,y,w,h);ctx.strokeStyle='#52605c';ctx.strokeRect(x+.5,y+.5,w-1,h-1);
  if(i===7){text(ctx,'这一轮看什么',x+23,y+42,21,'#e2b675',true);const rows=['动作读得清','材质与原画一致','脚底与建筑地基稳定','烟尘不吞掉单位轮廓'];rows.forEach((s,k)=>{text(ctx,String(k+1).padStart(2,'0'),x+24,y+94+k*43,13,'#bd9d66');text(ctx,s,x+55,y+94+k*43,16)});line(ctx,[[x+24,y+281],[x+w-24,y+281]],'#62706b66');text(ctx,'7 组动作 · 84 张关键帧',x+24,y+314,16,'#d6d8cf');text(ctx,'游戏 v0.14.0 尚未接入',x+24,y+342,13,'#9caeae');continue}
  const c=clips[i],f=frameAt(c,t);text(ctx,'0'+(i+1),x+17,y+27,12,'#c09b62');text(ctx,c.title,x+47,y+29,18,'#ece3ce',true);
  terrain(ctx,x+1,y+45,w-2,276,true);ctx.save();ctx.beginPath();ctx.rect(x+1,y+45,w-2,276);ctx.clip();
  sprite(ctx,c,f,x+w/2,y+306,c.id==='walk'||c.id==='fire'||c.id==='repair'||c.id==='fall'?284:322);ctx.restore();
  text(ctx,c.phases[f],x+17,y+347,14,'#d9dfd4');text(ctx,String(f+1).padStart(2,'0')+' / 12',x+263,y+347,12,'#aab7b5');
  ctx.fillStyle='#394b50';ctx.fillRect(x+17,y+367,w-34,3);ctx.fillStyle='#d2ae6c';ctx.fillRect(x+17,y+367,(w-34)*(t%c.seconds/c.seconds),3);
 }
 text(ctx,'关键帧绘制与播放预览 · 放大效果用于验收，不代表游戏中的实际单位大小',26,936,12,'#9cacaa');
}
function select(c){active=c;time=0;$('title').textContent=c.title;$('category').textContent=c.tag;$('description').textContent=c.desc;$('phases').textContent=c.phases.filter((s,i,a)=>i===0||s!==a[i-1]).join(' → ');document.querySelectorAll('#library button').forEach(b=>b.classList.toggle('active',b.dataset.id===c.id));renderStage(time)}
for(const c of clips){const b=document.createElement('button');b.dataset.id=c.id;b.innerHTML=c.title+'<span>'+c.seconds.toFixed(2)+'s</span>';b.onclick=()=>select(c);$('library').append(b)}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'暂停':'播放'};
$('restart').onclick=()=>{time=0;renderStage(time);renderOverview(time)};
$('step').onclick=()=>{playing=false;$('play').textContent='播放';const f=(frameAt(active,time)+1)%12;time=active.hold.slice(0,f).reduce((a,b)=>a+b,0)+.0001;renderStage(time);renderOverview(time)};
$('speed').onchange=e=>speed=+e.target.value;$('ground').onchange=e=>{ground=e.target.value;renderStage(time);renderOverview(time)};
$('grid').onclick=()=>{grid=!grid;$('grid').textContent='地面格线：'+(grid?'开':'关');$('grid').setAttribute('aria-pressed',String(grid));renderStage(time);renderOverview(time)};
$('timeline').oninput=e=>{playing=false;$('play').textContent='播放';time=Math.min(.99999,e.target.value/1000)*active.seconds;renderStage(time);renderOverview(time)};
overview.onclick=e=>{const r=overview.getBoundingClientRect(),x=(e.clientX-r.left)*1440/r.width,y=(e.clientY-r.top)*950/r.height;const i=Math.floor((x-22)/354)+Math.floor((y-96)/416)*4;if(x>=22&&y>=96&&i>=0&&i<7)select(clips[i])};
window.ready=Promise.all(clips.map(c=>c.image.decode())).then(()=>{select(active);renderOverview(0);window.renderAt=t=>{time=t;renderStage(t);renderOverview(t)};window.previewState=()=>({active:active.id,frame:frameAt(active,time),playing,grid,ground,time});window.selectClip=id=>select(clips.find(c=>c.id===id));const tick=now=>{if(playing)time+=Math.min(.05,(now-(last||now))/1000)*speed;last=now;renderStage(time);renderOverview(time);requestAnimationFrame(tick)};requestAnimationFrame(tick)}).catch(e=>{document.body.insertAdjacentHTML('afterbegin','<p>素材未能加载，请保留 sheets 文件夹并重新打开。</p>');throw e});
