using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
partial class CommandWindow {
 const int SideInset=8,SideWidth=W-MW-2*SideInset;
 void DrawHud(Graphics g){
  Metal(g,0,0,W,MapTop);Emblem(g,31,26,14);Rect(g,64,1,7,49,gold);
  Txt(g,"断线前沿",87,8,28,pale,true);Txt(g,world.River==null?" / 灰港登陆":" / 河湾镇",216,14,20,dim,true);
  Txt(g,world.Tutorial?"野外训练":world.River==null?"歼灭敌方 · 剩余 "+world.Remaining(1):"敌方一 "+world.Remaining(1)+"  /  敌方二 "+world.Remaining(2),450,18,14,dim,true);
  Txt(g,"军费",700,17,15,dim,true);Txt(g,world.Funds[0].ToString(),753,9,24,gold,true);
  Txt(g,"电力",887,17,15,dim,true);Txt(g,world.Power(0).ToString("+#;-#;0"),939,9,24,world.Power(0)<0?red:teal,true);
  Txt(g,TimeSpan.FromSeconds(world.Time).ToString(@"mm\:ss"),1070,10,23,pale,true);
  Button(g,1205,8,105,36,"速度 ×"+speed,()=>speed=speed==1?2:1);Button(g,1323,8,130,36,"暂停",()=>paused=true);
  float x=MW+SideInset;Metal(g,MW,MapTop,W-MW,H-MapTop);Line(g,MW,MapTop,MW,H,gold,2);
  Txt(g,"// 战区雷达",x,62,15,pale,true);Rect(g,x,86,SideWidth,104,ink);DrawRadar(g,x+2);
  Button(g,x,197,36,29,"基地",GoHome);Button(g,x+39,197,36,29,"事件",world.LastEvent.HasValue?(Action)GoEvent:null);
  Button(g,x+78,197,36,29,"维修",()=>StructureTool(StructureMode.Repair),structureMode==StructureMode.Repair);Button(g,x+117,197,36,29,"出售",()=>StructureTool(StructureMode.Sell),structureMode==StructureMode.Sell);
  Button(g,x,234,36,31,"建筑",()=>ProductionTab(4),!army&&productionFilter!=1&&!queueOpen);
  Button(g,x+39,234,36,31,"防御",()=>ProductionTab(1),productionFilter==1&&!queueOpen);
  Button(g,x+78,234,36,31,"步兵",()=>ProductionTab(2),productionFilter==2&&!queueOpen);
  Button(g,x+117,234,36,31,"车辆",()=>ProductionTab(3),productionFilter==3&&!queueOpen);
  if(queueOpen)DrawQueue(g,x);
  else{var cards=ProductionCards();
   for(int i=0;i<cards.Length;i++)Card(g,cards[i],x+i%2*80,274+i/2*75);}
  DrawQueueStrip(g);
  Button(g,x,730,SideWidth,31,world.Upgraded[0]?"装甲已升级":!world.Live(0).Any(o=>o.T.Key=="factory")?"升级需车辆工厂":"装甲升级 $260",
   world.Upgraded[0]||world.Funds[0]<260||!world.Live(0).Any(o=>o.T.Key=="factory")?(Action)null:()=>world.Upgrade());
  Button(g,x,771,72,31,"手册 F5",()=>{paused=true;help=true;});
  Button(g,x+80,771,72,31,"辅助范围",()=>{showRanges=!showRanges;showGrid=showRanges;},showRanges);
 }
 void Card(Graphics g,TypeInfo t,float x,float y){
  var job=t.Building?world.Construction(t):null;var producer=world.PrimaryFactory(0,t.Producer);
  bool active=producer!=null&&producer.Queue.Count>0&&producer.Queue.Peek()==t;
  string reason=t.Building?world.ConstructionReason(t):world.Require(t,0);
  bool enabled=reason==null||active;var r=new RectangleF(x,y,72,71);
  Plate(g,r,enabled?C("#2c383c"):C("#1c282d"),place==t||r.Contains(mouse)||world.Tutorial&&world.LessonKey==t.Key?gold:C("#536367"));
  var portrait=new RectangleF(x+3,y+3,66,38);Portrait(g,t,portrait);
  if(!enabled)Rect(g,x+3,y+3,66,39,Color.FromArgb(160,ink));
  float progress=job!=null?job.Work/t.Seconds:active?producer.Work/t.Seconds:0;
  if(job!=null||active){ProductionSweep(g,portrait,progress);Rect(g,x+3,y+68,66*Math.Min(1,progress),2,gold);}
  Txt(g,t.Name,x+4,y+42,11,enabled?pale:dim,true);
  string status=job!=null?world.ConstructionState(job):active?world.QueueState(producer):enabled?"$ "+t.Price:"锁 · "+reason.Replace("需要","需").Replace("先建","需");
  Txt(g,status.Length>7?status.Substring(0,6)+"…":status,x+4,y+57,9,job!=null&&job.Ready?teal:enabled?gold:dim);
  if(job!=null&&job.Ready){Rect(g,x+3,y+17,66,21,Color.FromArgb(225,ink));Txt(g,"就 绪",x+16,y+18,15,teal,true);}
  if(!t.Building&&producer!=null){int count=producer.Queue.Count(q=>q==t);if(count>0)Txt(g,count.ToString(),x+54,y+4,13,gold,true);}
  if(enabled)hits.Add(new HitBox{Box=r,Id="card-"+t.Key,Run=()=>BuildCardClick(t),Right=()=>BuildCardRight(t)});
 }
 void DrawCardHint(Graphics g){
  if(briefing||paused||queueOpen||mouse.X<MW+SideInset||mouse.Y<274||mouse.Y>=570)return;
  int col=(int)(mouse.X-MW-SideInset)/80,row=(int)(mouse.Y-274)/75;
  var cards=ProductionCards();int index=row*2+col;
  if(col<0||col>1||index>=cards.Length)return;
  var t=cards[index];string reason=t.Building?world.ConstructionReason(t):world.Require(t,0);float x=MW-250,y=Math.Min(H-82,mouse.Y+12);
  Plate(g,new RectangleF(x,y,238,64),panel,gold);Txt(g,t.Name+"  $"+t.Price,x+12,y+8,16,pale,true);
  Txt(g,reason??(t.Building?"左键生产／放置 · 右键暂停／取消":"左键排产 · 右键暂停／取消"),x+12,y+35,13,reason==null?teal:gold);
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
   string state=i==0?world.QueueState(src):"约 "+Math.Ceiling(seconds/world.ProductionRate(src))+" 秒";
   Txt(g,state.Length>8?state.Substring(0,8):state,x+44,y+31,10,gold);
   int slot=i;Button(g,x+128,y+12,24,29,"×",()=>{if(src.Queue.SequenceEqual(jobs))world.CancelOrder(src,slot);});hits.Last().Id="cancel-"+i;
  }
 }
 void DrawQueueStrip(Graphics g){
  float x=MW+SideInset;var src=world.Live(0).Where(o=>o.Queue.Count>0).OrderByDescending(o=>selection.Contains(o)).ThenByDescending(o=>o==world.LastOrder).FirstOrDefault();
  Line(g,x,581,W-SideInset,581,C("#56686e"));Txt(g,"排产  "+Pending(),x,590,13,pale,true);Button(g,x+104,586,48,27,"总览",OpenQueue);
  Plate(g,new RectangleF(x,619,SideWidth,57),panel,C("#637277"));
  var building=army?null:world.Construction(0,productionFilter==1?1:0);
  if(building!=null){Portrait(g,building.Type,new RectangleF(x+3,625,38,31));Txt(g,building.Type.Name,x+44,623,11,pale,true);
   Rect(g,x+44,648,76,5,ink);Rect(g,x+44,648,76*building.Work/building.Type.Seconds,5,gold);Txt(g,world.ConstructionState(building),x+44,657,10,gold);
   Button(g,x+128,624,24,29,building.Ready?"放":building.Paused?"续":"停",()=>{if(building.Ready||building.Paused)BuildCardClick(building.Type);else BuildCardRight(building.Type);});}
  else if(src==null)Txt(g,"生产线空闲",x+9,638,14,dim);
  else{var job=src.Queue.Peek();Portrait(g,job,new RectangleF(x+3,625,38,31));Txt(g,job.Name,x+44,623,11,pale,true);
   Rect(g,x+44,648,76,5,ink);Rect(g,x+44,648,76*Math.Min(1,src.Work/job.Seconds),5,gold);
   string state=world.QueueState(src);Txt(g,state.Length>9?state.Substring(0,9):state,x+44,657,10,gold);
   var snapshot=src.Queue.ToArray();Button(g,x+128,624,24,29,"×",()=>{if(src.Queue.SequenceEqual(snapshot))world.CancelOrder(src,0);});}
  var ready=world.LastReady;Button(g,x,682,SideWidth,29,"定位最新单位",ready!=null&&ready.Deployed?(Action)(()=>{selection.Clear();selection.Add(ready);CenterAt(ready.P);}):null);
  Txt(g,world.Radio.Length>13?world.Radio.Substring(0,12)+"…":world.Radio,x,715,10,teal);
 }
}
}
