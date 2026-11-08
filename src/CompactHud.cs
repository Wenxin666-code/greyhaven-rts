using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
partial class CommandWindow {
 const int SideInset=8,SideWidth=W-MW-2*SideInset;
 void DrawHud(Graphics g){
  float x=MW+SideInset;Metal(g,MW,0,W-MW,H);Line(g,MW,0,MW,H,gold,2);
  Txt(g,"军费",x+2,11,13,dim,true);Txt(g,"$ "+world.Funds[0],x+40,6,23,gold,true);
  Button(g,x,44,72,29,"任务目标",()=>{paused=true;help=true;});Button(g,x+80,44,72,29,"选项 Esc",OpenOptions);
  Rect(g,x,86,SideWidth,104,ink);DrawRadar(g,x+2);
  int available=world.Power(0);Rect(g,MW+2,199,3,Math.Min(120,35+Math.Abs(available)*4),available<0?red:teal);
  Button(g,x,197,72,29,"维修 [K]",()=>StructureTool(StructureMode.Repair),structureMode==StructureMode.Repair);Button(g,x+80,197,72,29,"出售 [L]",()=>StructureTool(StructureMode.Sell),structureMode==StructureMode.Sell);
  Button(g,x,234,36,31,"建筑",()=>ProductionTab(4),!army&&productionFilter!=1&&!queueOpen);
  Button(g,x+39,234,36,31,"防御",()=>ProductionTab(1),productionFilter==1&&!queueOpen);
  Button(g,x+78,234,36,31,"步兵",()=>ProductionTab(2),productionFilter==2&&!queueOpen);
  Button(g,x+117,234,36,31,"车辆",()=>ProductionTab(3),productionFilter==3&&!queueOpen);
  if(queueOpen){DrawQueue(g,x);Button(g,x,688,SideWidth,30,"返回生产栏",()=>queueOpen=false);}
  else{var cards=ProductionCards();sidebarRow=Math.Max(0,Math.Min(Math.Max(0,(cards.Length+1)/2-6),sidebarRow));
   for(int i=0;i<12;i++){int index=sidebarRow*2+i;if(index<cards.Length)Card(g,cards[index],x+i%2*80,274+i/2*75);else Plate(g,new RectangleF(x+i%2*80,274+i/2*75,72,71),C("#1b292d"),C("#35464a"));}
   int rows=(cards.Length+1)/2;Button(g,x,740,72,27,"↑",sidebarRow>0?(Action)(()=>SidebarScroll(-1)):null);Button(g,x+80,740,72,27,"↓",sidebarRow+6<rows?(Action)(()=>SidebarScroll(1)):null);
  }
  Txt(g,"电力 "+available.ToString("+#;-#;0"),x+3,778,12,available<0?red:teal,true);Txt(g,TimeSpan.FromSeconds(DisplayBattleSeconds).ToString(@"mm\:ss"),x+101,778,12,dim,true);
  Txt(g,world.River==null?"灰港战区":world.River.Title,x+3,800,11,dim);Txt(g,MatchSpeedLabel,x+112,799,11,gold,true);
 }
 void Card(Graphics g,TypeInfo t,float x,float y){
  var job=t.Building?world.Construction(t):null;var producer=t.Building?world.PrimaryFactory(0,t.Producer):CardProducer(t);
  int running=t.Building?0:world.Live(0).Count(p=>p.T.Key==t.Producer&&!p.ProductionPaused&&p.Queue.Count>0&&p.Queue.Peek()==t);
  bool active=producer!=null&&producer.Queue.Count>0&&producer.Queue.Peek()==t;
  string reason=t.Building?world.ConstructionReason(t):world.Require(t,0);
  bool enabled=reason==null||active;var r=new RectangleF(x,y,72,71);
  Plate(g,r,enabled?C("#2c383c"):C("#1c282d"),place==t||r.Contains(mouse)||world.Tutorial&&world.LessonKey==t.Key?gold:C("#536367"));
  var portrait=new RectangleF(x+3,y+3,66,38);Portrait(g,t,portrait);
  if(!enabled)Rect(g,x+3,y+3,66,39,Color.FromArgb(160,ink));
  float progress=job!=null?job.Work/t.Seconds:active?producer.Work/t.Seconds:0;
  if(job!=null||active){ProductionSweep(g,portrait,progress);Rect(g,x+3,y+68,66*Math.Min(1,progress),2,gold);}
  Txt(g,t.Name,x+4,y+42,11,enabled?pale:dim,true);
  string status=job!=null?world.ConstructionState(job,0,speed):active?(running>1&&!producer.ProductionPaused&&world.Funds[0]>0?running+" 厂并行":world.QueueState(producer,speed)):enabled?"$ "+t.Price:"锁 · "+reason.Replace("需要","需").Replace("先建","需");
  Txt(g,status.Length>7?status.Substring(0,6)+"…":status,x+4,y+57,9,job!=null&&job.Ready?teal:enabled?gold:dim);
  if(job!=null&&job.Ready){Rect(g,x+3,y+17,66,21,Color.FromArgb(225,ink));Txt(g,"就 绪",x+16,y+18,15,teal,true);}
  if(!t.Building&&producer!=null){int count=world.Live(0).Where(p=>p.T.Key==t.Producer).Sum(p=>p.Queue.Count(q=>q==t));if(count>0)Txt(g,count.ToString(),x+54,y+4,13,gold,true);}
  if(enabled)hits.Add(new HitBox{Box=r,Id="card-"+t.Key,Run=()=>BuildCardClick(t,(ModifierKeys&System.Windows.Forms.Keys.Shift)!=0?5:1),Right=()=>BuildCardRight(t,(ModifierKeys&System.Windows.Forms.Keys.Shift)!=0?5:1)});
 }
 void DrawCardHint(Graphics g){
  if(briefing||paused||queueOpen||mouse.X<MW+SideInset||mouse.Y<274||mouse.Y>=724)return;
  int col=(int)(mouse.X-MW-SideInset)/80,row=(int)(mouse.Y-274)/75;
  var cards=ProductionCards();int index=(row+sidebarRow)*2+col;
  if(col<0||col>1||index>=cards.Length)return;
  var t=cards[index];string reason=t.Building?world.ConstructionReason(t):world.Require(t,0);float x=MW-250,y=Math.Min(H-82,mouse.Y+12);
  Plate(g,new RectangleF(x,y,238,64),panel,gold);Txt(g,t.Name+"  $"+t.Price,x+12,y+8,16,pale,true);
  Txt(g,reason??(t.Building?"左键生产／放置 · 右键暂停／取消":"自动分配排产 · 右键暂停／取消"),x+12,y+35,13,reason==null?teal:gold);
 }
 void DrawQueue(Graphics g,float x){
  var list=Producers();if(list.Count==0){Txt(g,"展开指挥部后查看排产",x,284,12,dim);return;}
  queueIndex=list.FindIndex(o=>o.Id==queueId);if(queueIndex<0)queueIndex=0;var src=list[queueIndex];queueId=src.Id;
  Txt(g,src.T.Name+" #"+src.Id,x+2,284,11,pale,true);
  Button(g,x+98,277,24,29,"‹",()=>queueId=list[(queueIndex+list.Count-1)%list.Count].Id);
  Button(g,x+128,277,24,29,"›",()=>queueId=list[(queueIndex+1)%list.Count].Id);
  if(queuePageSource!=src.Id){queuePageSource=src.Id;queuePage=0;}
  var jobs=src.Queue.ToArray();int pages=Math.Max(1,(jobs.Length+3)/4);queuePage=Math.Max(0,Math.Min(queuePage,pages-1));
  int first=queuePage*4;float seconds=-src.Work;
  for(int j=0;j<first;j++)seconds+=jobs[j].Seconds;
  for(int row=0;row<4;row++){
   int i=first+row;float y=316+row*60;Plate(g,new RectangleF(x,y,SideWidth,55),panel,C("#526267"));
   if(i>=jobs.Length){Txt(g,jobs.Length==0&&row==0?"暂无订单":"—",x+8,y+17,13,dim);continue;}
   seconds+=jobs[i].Seconds;Portrait(g,jobs[i],new RectangleF(x+3,y+6,38,31));Txt(g,(i+1)+" "+jobs[i].Name,x+44,y+7,10,pale,true);
   string state=i==0?world.QueueState(src,speed):src.ProductionPaused?"等待继续":world.Funds[0]==0?"等待资金":"约 "+Math.Ceiling(seconds/(world.ProductionRate(src)*speed))+" 秒";
   Txt(g,state.Length>8?state.Substring(0,8):state,x+44,y+31,10,gold);
   int slot=i;Button(g,x+128,y+12,24,29,"×",()=>{if(src.Queue.SequenceEqual(jobs))world.CancelOrder(src,slot);});hits.Last().Id="cancel-"+i;
  }
  Button(g,x,558,24,21,"‹",queuePage>0?(Action)(()=>queuePage--):null);if(queuePage>0)hits.Last().Id="queue-prev-page";
  Txt(g,(queuePage+1)+"/"+pages+"  "+jobs.Length+"/"+Campaign.ProductionQueueLimit,x+31,561,11,dim);
  Button(g,x+128,558,24,21,"›",queuePage+1<pages?(Action)(()=>queuePage++):null);if(queuePage+1<pages)hits.Last().Id="queue-next-page";
 }
 bool QueueWheel(PointF p,int delta){
  if(!queueOpen||briefing||paused||p.X<MW||p.Y<274||p.Y>=580)return false;
  var source=Producers().FirstOrDefault(u=>u.Id==queueId);int last=source==null?0:Math.Max(0,(source.Queue.Count-1)/4);
  queuePage=Math.Max(0,Math.Min(last,queuePage-Math.Sign(delta)));hudDirty=true;Invalidate();return true;
 }

 void DrawQueueStrip(Graphics g){
  float x=MW+SideInset;var src=world.Live(0).Where(o=>o.Queue.Count>0).OrderByDescending(o=>selection.Contains(o)).ThenByDescending(o=>o==world.LastOrder).FirstOrDefault();
  Line(g,x,581,W-SideInset,581,C("#56686e"));int working=world.Live(0).Count(p=>p.Queue.Count>0&&!p.ProductionPaused);Txt(g,"排产 "+Pending()+" / "+working+"厂",x,590,11,pale,true);Button(g,x+104,586,48,27,"总览",OpenQueue);
  Plate(g,new RectangleF(x,619,SideWidth,57),panel,C("#637277"));
  var building=army?null:world.Construction(0,productionFilter==1?1:0);
  if(building!=null){Portrait(g,building.Type,new RectangleF(x+3,625,38,31));Txt(g,building.Type.Name,x+44,623,11,pale,true);
   Rect(g,x+44,648,76,5,ink);Rect(g,x+44,648,76*building.Work/building.Type.Seconds,5,gold);Txt(g,world.ConstructionState(building,0,speed),x+44,657,10,gold);
   Button(g,x+128,624,24,29,building.Ready?"放":building.Paused?"续":"停",()=>{if(building.Ready||building.Paused)BuildCardClick(building.Type);else BuildCardRight(building.Type);});}
  else if(src==null)Txt(g,"生产线空闲",x+9,638,14,dim);
  else{var job=src.Queue.Peek();Portrait(g,job,new RectangleF(x+3,625,38,31));Txt(g,job.Name,x+44,623,11,pale,true);
   Rect(g,x+44,648,76,5,ink);Rect(g,x+44,648,76*Math.Min(1,src.Work/job.Seconds),5,gold);
   string state=world.QueueState(src,speed);Txt(g,state.Length>9?state.Substring(0,9):state,x+44,657,10,gold);
   var snapshot=src.Queue.ToArray();Button(g,x+128,624,24,29,"×",()=>{if(src.Queue.SequenceEqual(snapshot))world.CancelOrder(src,0);});}
  var ready=world.LastReady;Button(g,x,682,SideWidth,29,"定位最新单位",ready!=null&&ready.Deployed?(Action)(()=>{selection.Clear();selection.Add(ready);CenterAt(ready.P);}):null);
  Txt(g,world.Radio.Length>13?world.Radio.Substring(0,12)+"…":world.Radio,x,715,10,teal);
 }
}
}
