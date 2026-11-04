'use strict';
const $=id=>document.getElementById(id), map=$('map'),ctx=map.getContext('2d'),inspect=$('preview').getContext('2d');
const names={squad:'步兵班',ifv:'步兵战车',tank:'主战坦克',hq:'指挥部',power:'电站',factory:'车辆工厂'};
const sizes={squad:78,ifv:100,tank:115,hq:174,power:155,factory:206};
const colors=['#5dcfff','#ff7867','#ffd565'],teams=['海蓝','砖红','琥珀'],pics={};
const building=k=>['hq','power','factory'].includes(k),hasAction=k=>k==='factory'||k==='squad';
let objects=[],selected,angle=2,spin=false,placing=false,cursor=null,serial=0,last=0,clock=0,spinTime=0;
function say(s){$('msg').textContent=s}
function unit(k,x,y,team,dir){
 const o={id:++serial,k,x,y,team,dir,route:[],turn:dir,fall:null,built:1,job:null,exiting:null};objects.push(o);return o;
}
function reset(){
 objects=[];serial=0;placing=false;spin=false;$('spin').textContent='连续转动';
 unit('hq',138,520,0,2);unit('power',287,522,0,2);unit('factory',463,495,0,1);
 unit('squad',430,374,0,4);unit('ifv',542,363,0,4);const tank=unit('tank',631,355,0,3);
 unit('squad',860,255,1,12);unit('tank',918,284,1,12);unit('hq',1091,452,1,14);
 choose(tank);say('蓝军在西南停机坪，红军守东侧码头。先选一辆车，点十字路口试走。');
}
function choose(o){
 selected=o;angle=o.dir;placing=false;spin=false;$('spin').textContent='连续转动';$('angle').value=angle;$('team').selectedIndex=o.team;
 $('object').replaceChildren(...objects.map(a=>new Option(teams[a.team]+' / '+names[a.k]+'　'+a.id,a.id)));
 $('object').value=o.id;controls();
}
function controls(){
 const o=selected,fallen=o.fall!==null,busy=o.built<1||!!o.exiting;
 $('place').disabled=!building(o.k)||fallen||busy;$('damage').disabled=fallen||busy||!!o.job;$('restore').disabled=!fallen;
 $('produce').disabled=o.k!=='factory'||!!o.job||fallen||busy;
 $('team').disabled=busy||!!o.job||fallen;$('angleOut').value=(angle*22.5)+'°';
}
const obstacles=[[186,126,260,180],[245,163,301,209],[267,127,296,163],[925,111,1038,191],[1064,88,1199,197]];
function radius(o){return {hq:43,power:39,factory:58}[o.k]||0}
function blocked(x,y,pad=10,skip=null){
 if(x<pad||x>1200-pad||y<99+pad||y>664-pad)return true;
 if(obstacles.some(([a,b,c,d])=>x>a-pad&&x<c+pad&&y>b-pad&&y<d+pad))return true;
 return objects.some(o=>o!==skip&&building(o.k)&&Math.abs(x-o.x)<radius(o)+pad&&Math.abs(y-o.y)<radius(o)*.68+pad);
}
// Shared map mask for route finding, factory rally points and placement.
function pathTo(o,x,y){
 if(blocked(x,y))return null;
 const step=15,cols=80,rows=45,key=(x,y)=>Math.floor(y/step)*cols+Math.floor(x/step),start=key(o.x,o.y),goal=key(x,y);
 const prev=new Int32Array(cols*rows).fill(-1),queue=[start];prev[start]=start;
 for(let j=0;j<queue.length&&prev[goal]===-1;j++){
  const v=queue[j],cx=v%cols,cy=Math.floor(v/cols);
  for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]]){
   const nx=cx+dx,ny=cy+dy,i=ny*cols+nx,px=(nx+.5)*step,py=(ny+.5)*step;
   if(nx<0||ny<0||nx>=cols||ny>=rows||prev[i]!==-1||blocked(px,py))continue;
   if(dx&&dy&&(blocked(px,(cy+.5)*step)||blocked((cx+.5)*step,py)))continue;
   prev[i]=v;queue.push(i);
  }
 }
 if(prev[goal]<0)return null;
 const points=[];for(let i=goal;i!==start;i=prev[i])points.push([(i%cols+.5)*step,(Math.floor(i/cols)+.5)*step]);
 return points.reverse().concat([[x,y]]);
}
function dispatch(x,y){
 const o=selected;if(building(o.k)||o.fall!==null||o.exiting)return say('请选择一支可移动的部队。');
 const route=pathTo(o,x,y);if(!route)return say('此处不可抵达。水面、吊机、货柜和建筑占地会阻挡地面部队。');
 o.route=route;say(names[o.k]+' 正在前往指定位置。');
}
function facing(dx,dy){return (Math.round(Math.atan2(dx,dy/.8)*8/Math.PI)+16)%16}
function produce(){
 const f=selected;if(f.k!=='factory'||f.job||f.fall!==null||f.built<1)return;
 const yaw=f.dir*Math.PI/8,dx=Math.sin(yaw),dy=Math.cos(yaw)*.82;
 const end=[f.x+dx*116,f.y+dy*116],start=[f.x+dx*24,f.y+dy*24];
 if(Array.from({length:8},(_,i)=>24+i*13).some(d=>blocked(f.x+dx*d,f.y+dy*d,10,f)))return say('出厂通道受阻，请从另一座工厂出车。');
 if(objects.some(o=>!building(o.k)&&Math.hypot(o.x-end[0],o.y-end[1])<23))return say('请先把出口集结点的部队移开。');
 f.job={t:0,start,end,k:$('product').value,child:null};controls();say('车间开启卷门，车辆将从正门驶出。');
}
function animate(dt){
 for(const o of objects){
  o.built=Math.min(1,o.built+dt*.65);
  if(o.fall!==null){o.fall=Math.min(1,o.fall+dt*1.4);continue}
  if(o.job){const j=o.job;j.t+=dt;
   if(j.t>=.45&&!j.child){j.child=unit(j.k,...j.start,o.team,o.dir);j.child.exiting=j;
    $('object').add(new Option(teams[o.team]+' / '+names[j.k]+'　'+j.child.id,j.child.id))}
   if(j.child){const t=Math.min(1,(j.t-.45)/1.5);j.child.x=j.start[0]+(j.end[0]-j.start[0])*t;j.child.y=j.start[1]+(j.end[1]-j.start[1])*t}
   if(j.t>=2.15){j.child.exiting=null;o.job=null;say('车辆已到集结点，可选中并下达移动指令。')}
  }
  if(!o.route.length||o.exiting)continue;
  const [x,y]=o.route[0],dx=x-o.x,dy=y-o.y,len=Math.hypot(dx,dy),dist=dt*(o.k==='squad'?49:75);
  if(len>.3){
   const desired=facing(dx,dy),diff=(desired-o.turn+24)%16-8,amount=dt*(o.k==='squad'?25:13);
   o.turn=(o.turn+Math.sign(diff)*Math.min(Math.abs(diff),amount)+16)%16;o.dir=Math.round(o.turn)%16;
   if(Math.abs(diff)>1.3)continue;
  }
  if(len<=dist){o.x=x;o.y=y;o.route.shift()}else{o.x+=dist*dx/len;o.y+=dist*dy/len}
 }
}
function sprite(c,o,x,y,size,dir){
 const action=hasAction(o.k),dead=o.fall!==null?o.fall:0;
 const busy=o.job||o.k==='squad'&&o.route.length&&Math.floor(clock*5)%2;
 function frame(group,alpha,scaleY=1){
  c.save();c.globalAlpha=alpha;c.drawImage(pics[o.k],dir%8*192,(group*2+(dir>>3))*192,192,192,x-size/2,y-size*.59*scaleY,size,size*scaleY);c.restore();
 }
 let normal=o.team*(action?2:1)+(action&&busy?1:0);
 if(o.k==='factory'&&o.job&&(o.job.t<.28||o.job.t>1.91))normal=7+o.team;
 if(o.k==='squad'&&dead>.12&&dead<1)normal=7+o.team;
 if(dead<1)frame(normal,1-dead,o.built<1?.15+.85*o.built:1-dead*.24);
 if(dead>0)frame(action?6:3,dead);
 if(dead>0&&dead<1&&o.k!=='squad'){
  c.save();c.globalAlpha=Math.sin(dead*Math.PI)*.76;c.fillStyle='#dbbc85';
  for(let i=0;i<7;i++){const a=i*2.4;c.beginPath();c.arc(x+Math.cos(a)*dead*size*.28,y-Math.sin(a)*dead*size*.14-size*dead*.16,size*(.025+.054*dead),0,Math.PI*2);c.fill()}c.restore();
 }
}
function badge(c,o,x,y){
 c.fillStyle=colors[o.team];c.strokeStyle='#172725';c.lineWidth=1.5;c.beginPath();
 if(o.team===0)c.arc(x,y,4.5,0,Math.PI*2);else if(o.team===1){c.moveTo(x,y-6);c.lineTo(x+6,y);c.lineTo(x,y+6);c.lineTo(x-6,y);c.closePath()}else c.rect(x-4,y-4,8,8);c.fill();c.stroke();
}
function draw(){
 ctx.drawImage(pics.port,0,0,1200,675);
 ctx.font='600 13px "Microsoft YaHei",sans-serif';
 for(const [txt,x,y] of [['01　登陆基地',40,631],['02　沿岸公路',50,342],['03　码头货场',946,214]]){
  ctx.fillStyle='#263631cb';ctx.fillRect(x,y,112,24);ctx.fillStyle='#f2eedc';ctx.fillText(txt,x+9,y+17)}
 if(selected.route.length){ctx.beginPath();ctx.moveTo(selected.x,selected.y);for(const p of selected.route)ctx.lineTo(...p);
  ctx.strokeStyle='#dbffd7';ctx.lineWidth=2;ctx.setLineDash([5,5]);ctx.stroke();ctx.setLineDash([])}
 for(const o of [...objects].sort((a,b)=>a.y-b.y)){
  const sz=sizes[o.k],sel=o===selected;
  if(sel){ctx.beginPath();ctx.ellipse(o.x,o.y+sz*.09,sz*.35,sz*.19,0,0,Math.PI*2);ctx.lineWidth=2.5;ctx.strokeStyle='#fff3a5';ctx.stroke()}
  sprite(ctx,o,o.x,o.y,sz,o.dir);
  const y=o.y+sz*.26;badge(ctx,o,o.x,y);
  if($('labels').checked||sel){const text=names[o.k]+(o.fall!==null?' · 损毁':'');const w=ctx.measureText(text).width+14;
   ctx.fillStyle=sel?'#24352df2':'#26342bd9';ctx.fillRect(o.x-w/2,y+7,w,20);ctx.fillStyle=sel?'#fff4bb':'#fff';ctx.textAlign='center';ctx.fillText(text,o.x,y+22);ctx.textAlign='left'}
 }
 if(placing&&cursor){const [x,y]=cursor;ctx.save();ctx.globalAlpha=.65;sprite(ctx,{...selected,dir:angle},x,y,sizes[selected.k],angle);ctx.restore();ctx.strokeStyle=canPlace(x,y)?'#84f296':'#ff5c52';const r=radius(selected);ctx.strokeRect(x-r,y-r*.68,2*r,1.36*r)}
 inspect.clearRect(0,0,280,215);sprite(inspect,selected,140,120,290,angle);controls();
}
function canPlace(x,y){const r=radius(selected);return x<715&&y>414&&y<643&&x>r&&x+r<715&&!blocked(x,y,r+5)&&!objects.some(o=>!building(o.k)&&Math.abs(o.x-x)<r+12&&Math.abs(o.y-y)<r*.68+12)}
function coords(e){const b=map.getBoundingClientRect();return [(e.clientX-b.left)*1200/b.width,(e.clientY-b.top)*675/b.height]}
function hit(x,y){return [...objects].sort((a,b)=>b.y-a.y).find(o=>{const s=sizes[o.k];return Math.abs(x-o.x)<s*.35&&y>o.y-s*.42&&y<o.y+s*.30+19})}
map.onpointermove=e=>{cursor=coords(e)};map.onpointerleave=()=>cursor=null;
map.onclick=e=>{const [x,y]=coords(e);
 if(placing){if(!canPlace(x,y))return say('请在西南水泥坪留出足够的部署间距。');const b=unit(selected.k,x,y,selected.team,angle);b.built=0;for(const a of objects){if(a.route.length){const end=a.route.at(-1);a.route=pathTo(a,...end)||[]}}choose(b);say('建筑正在展开。');return}
 const o=hit(x,y);if(o){choose(o);say(names[o.k]+' 已选中。'+(building(o.k)?'可检阅外观或部署同型建筑。':'点击空地移动。'))}else dispatch(x,y)};
map.oncontextmenu=e=>{e.preventDefault();if(placing){placing=false;say('已取消部署。')}else dispatch(...coords(e))};
$('object').onchange=e=>choose(objects.find(o=>o.id===+e.target.value));
$('angle').oninput=e=>{angle=+e.target.value;spin=false;$('spin').textContent='连续转动'};
$('spin').onclick=()=>{spin=!spin;$('spin').textContent=spin?'暂停转动':'连续转动'};
$('team').onchange=e=>{selected.team=e.target.selectedIndex;choose(selected)};
$('damage').onclick=()=>{selected.fall=0;selected.route=[];placing=false;say('播放'+(selected.k==='squad'?'倒地':building(selected.k)?'坍塌':'报废')+'过程。')};
$('restore').onclick=()=>{selected.fall=null;say('已复原。')};$('produce').onclick=produce;
$('place').onclick=()=>{placing=!placing;say(placing?'用检阅滑杆选朝向，再点西南基地空地部署。右键取消。':'已取消部署。')};$('reset').onclick=reset;
function loop(now){const dt=Math.min(.08,(now-last)/1000);last=now;clock+=dt;animate(dt);
 if(spin){spinTime+=dt;if(spinTime>=.14){spinTime=0;angle=(angle+1)%16;$('angle').value=angle}}
 draw();requestAnimationFrame(loop)}
Promise.all(['port',...Object.keys(names)].map(k=>new Promise((ok,fail)=>{const img=new Image();pics[k]=img;img.onload=ok;img.onerror=()=>fail(Error(k));img.src='../source/v02/'+(k==='port'?'port':'sprites/'+k)+'.png'}))).then(()=>{reset();requestAnimationFrame(loop)}).catch(e=>say('无法载入素材 '+e.message+'，请保留项目目录结构。'));
