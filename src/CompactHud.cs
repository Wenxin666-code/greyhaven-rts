using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
partial class CommandWindow {
 const int SideInset=8,SideWidth=W-MW-2*SideInset;
 void DrawHud(Graphics g){
  Metal(g,0,0,W,MapTop);Emblem(g,31,26,14);Rect(g,64,1,7,49,gold);
  Txt(g,"断线前沿",87,8,28,pale,true);Txt(g," / 灰港登陆",216,14,20,dim,true);
  Txt(g,world.Tutorial?"野外训练":"歼灭敌方 · 剩余 "+world.Remaining(1),450,18,14,dim,true);
  Txt(g,"军费",700,17,15,dim,true);Txt(g,world.Funds[0].ToString(),753,9,24,gold,true);
  Txt(g,"电力",887,17,15,dim,true);Txt(g,world.Power(0).ToString("+#;-#;0"),939,9,24,world.Power(0)<0?red:teal,true);
  Txt(g,TimeSpan.FromSeconds(world.Time).ToString(@"mm\:ss"),1070,10,23,pale,true);
  Button(g,1205,8,105,36,"速度 ×"+speed,()=>speed=speed==1?2:1);Button(g,1323,8,130,36,"暂停",()=>paused=true);
  float x=MW+SideInset;Metal(g,MW,MapTop,W-MW,H-MapTop);Line(g,MW,MapTop,MW,H,gold,2);
  Txt(g,"// 战区雷达",x,62,15,pale,true);Rect(g,x,86,SideWidth,104,ink);DrawRadar(g,x+2);
  Button(g,x,197,72,29,"基地 H",GoHome);Button(g,x+80,197,72,29,"事件",world.LastEvent.HasValue?(Action)GoEvent:null);
  Button(g,x,234,48,31,"建设",()=>{army=false;queueOpen=false;place=null;},!army&&!queueOpen);
  Button(g,x+52,234,48,31,"生产",()=>{army=true;queueOpen=false;place=null;},army&&!queueOpen);
  Button(g,x+104,234,48,31,"队列",OpenQueue,queueOpen);
  if(queueOpen)DrawQueue(g,x);
  else{var cards=Campaign.Types.Where(t=>army?!t.Building:t.Building&&t.Key!="hq").ToArray();
   for(int i=0;i<cards.Length;i++)Card(g,cards[i],x+i%2*80,274+i/2*75);}
  DrawQueueStrip(g);
  Button(g,x,730,SideWidth,31,world.Upgraded[0]?"装甲已升级":!world.Live(0).Any(o=>o.T.Key=="factory")?"升级需车辆工厂":"装甲升级 $260",
   world.Upgraded[0]||world.Funds[0]<260||!world.Live(0).Any(o=>o.T.Key=="factory")?(Action)null:()=>world.Upgrade());
  Button(g,x,771,72,31,"手册 F5",()=>{paused=true;help=true;});
  Button(g,x+80,771,72,31,syncMarch?"同速开 F":"同速关 F",()=>syncMarch=!syncMarch,syncMarch);
 }
 void Card(Graphics g,TypeInfo t,float x,float y){
  string reason=world.Require(t,0);var r=new RectangleF(x,y,72,71);bool ready=reason==null;
  bool hot=place==t||world.Tutorial&&world.LessonKey==t.Key||r.Contains(mouse);
  Plate(g,r,ready?C("#2c383c"):C("#1c282d"),hot?gold:C("#536367"));
  Portrait(g,t,new RectangleF(x+3,y+3,66,38));if(!ready)Rect(g,x+3,y+3,66,39,Color.FromArgb(160,ink));
  Txt(g,t.Name,x+4,y+42,11,ready?pale:dim,true);
  string status=ready?"$ "+t.Price:"锁 · "+reason.Replace("需要","需").Replace("先建", "需");
  Txt(g,status.Length>7?status.Substring(0,6)+"…":status,x+4,y+57,9,ready?gold:dim);
  var active=world.Live(0).FirstOrDefault(o=>o.Queue.Count>0&&o.Queue.Peek()==t);
  if(active!=null){Rect(g,x+3,y+68,66,2,ink);Rect(g,x+3,y+68,66*Math.Min(1,active.Work/t.Seconds),2,gold);}
  if(!ready)return;
  hits.Add(new HitBox{Box=r,Id="card-"+t.Key,Run=()=>{
   if(world.Require(t,0)!=null)return;if(t.Building){place=t;attack=false;world.Radio="部署"+t.Name+"：左键确认绿色位置。";}
   else world.Train(t,0,selection.FirstOrDefault());}});
 }
 void DrawCardHint(Graphics g){
  if(briefing||paused||queueOpen||mouse.X<MW+SideInset||mouse.Y<274||mouse.Y>=570)return;
  int col=(int)(mouse.X-MW-SideInset)/80,row=(int)(mouse.Y-274)/75;
  var cards=Campaign.Types.Where(type=>army?!type.Building:type.Building&&type.Key!="hq").ToArray();int index=row*2+col;
  if(col<0||col>1||index>=cards.Length)return;
  var t=cards[index];string reason=world.Require(t,0);float x=MW-250,y=Math.Min(H-82,mouse.Y+12);
  Plate(g,new RectangleF(x,y,238,64),panel,gold);Txt(g,t.Name+"  $"+t.Price,x+12,y+8,16,pale,true);
  Txt(g,reason??"点击下单 · "+t.Seconds+" 秒",x+12,y+35,13,reason==null?teal:gold);
 }
 void DrawQueue(Graphics g,float x){
  var list=Producers();if(list.Count==0){Txt(g,"展开指挥部后查看排产",x,284,12,dim);return;}
  queueIndex=list.FindIndex(o=>o.Id==queueId);if(queueIndex<0)queueIndex=0;var src=list[queueIndex];queueId=src.Id;
  Txt(g,src.T.Name+" #"+src.Id,x+2,284,11,pale,true);
  Button(g,x+98,277,24,29,"‹",()=>queueId=list[(queueIndex+list.Count-1)%list.Count].Id);
  Button(g,x+128,277,24,29,"›",()=>queueId=list[(queueIndex+1)%list.Count].Id);
  var jobs=src.Queue.ToArray();float seconds=-src.Work;
  for(int i=0;i<4;i++){
   float y=316+i*60;Plate(g,new RectangleF(x,y,SideWidth,55),panel,C("#526267"));
   if(i>=jobs.Length){Txt(g,"0"+(i+1)+"  空位",x+8,y+17,13,dim);continue;}
   seconds+=jobs[i].Seconds;Portrait(g,jobs[i],new RectangleF(x+3,y+6,38,31));Txt(g,jobs[i].Name,x+44,y+7,11,pale,true);
   string state=i==0?world.QueueState(src):"约 "+Math.Ceiling(seconds/(world.Power(0)<0?.45:1))+" 秒";
   Txt(g,state.Length>8?state.Substring(0,8):state,x+44,y+31,10,gold);
   int slot=i;Button(g,x+128,y+12,24,29,"×",()=>{if(src.Queue.SequenceEqual(jobs))world.CancelOrder(src,slot);});hits.Last().Id="cancel-"+i;
  }
 }
 void DrawQueueStrip(Graphics g){
  float x=MW+SideInset;var src=world.Live(0).Where(o=>o.Queue.Count>0).OrderByDescending(o=>selection.Contains(o)).ThenByDescending(o=>o==world.LastOrder).FirstOrDefault();
  Line(g,x,581,W-SideInset,581,C("#56686e"));Txt(g,"排产  "+Pending(),x,590,13,pale,true);Button(g,x+104,586,48,27,"总览",OpenQueue);
  Plate(g,new RectangleF(x,619,SideWidth,57),panel,C("#637277"));
  if(src==null)Txt(g,"生产线空闲",x+9,638,14,dim);
  else{var job=src.Queue.Peek();Portrait(g,job,new RectangleF(x+3,625,38,31));Txt(g,job.Name,x+44,623,11,pale,true);
   Rect(g,x+44,648,76,5,ink);Rect(g,x+44,648,76*Math.Min(1,src.Work/job.Seconds),5,gold);
   string state=world.QueueState(src);Txt(g,state.Length>9?state.Substring(0,9):state,x+44,657,10,gold);
   var snapshot=src.Queue.ToArray();Button(g,x+128,624,24,29,"×",()=>{if(src.Queue.SequenceEqual(snapshot))world.CancelOrder(src,0);});}
  var ready=world.LastReady;Button(g,x,682,SideWidth,29,"定位最新单位",ready!=null&&ready.Deployed?(Action)(()=>{selection.Clear();selection.Add(ready);CenterAt(ready.P);}):null);
  Txt(g,world.Radio.Length>13?world.Radio.Substring(0,12)+"…":world.Radio,x,715,10,teal);
 }
}
}
