using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class ParallelProductionChecks {
 [STAThread]static int Main(string[] args){try{
  Campaign.CheckParallelProduction();
  using(var form=new CommandWindow())form.CheckParallelProductionInput(args.Length>0?args[0]:null);
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void AssertParallel(bool value,string reason){if(!value)throw new Exception(reason);}
 static Campaign ParallelWorld(int count,int side=0,string key="factory"){
  var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();
  w.Spawn("mcv",0,200,1200);w.Spawn("mcv",1,2100,1400);
  for(int i=0;i<3;i++)w.Funds[i]=100000;
  for(int i=0;i<count;i++){
   w.Spawn("power",side,400+i*180,450);
   var f=w.Spawn(key,side,400+i*180,700);f.Rally=new PointF(f.P.X,f.P.Y+240);
  }
  return w;
 }
 static Piece[] ParallelFactories(Campaign w,int side=0,string key="factory"){return w.Live(side).Where(p=>p.T.Key==key).OrderBy(p=>p.Id).ToArray();}
 static void ParallelAdvance(Campaign w,float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/.05f);i++)w.Tick(.05f);}
 internal static void CheckParallelProduction(){
  var type=Info("ifv");
  foreach(string producer in new[]{"factory","barracks"})for(int side=0;side<3;side++){
   var results=new int[3];var measured=Info(producer=="factory"?"ifv":"squad");int perFactory=producer=="factory"?2:4;
   for(int count=1;count<=3;count++){
    var w=ParallelWorld(count,side,producer);for(int i=0;i<count*4;i++)AssertParallel(w.Train(measured,side,null),"parallel order accepted");
    AssertParallel(ParallelFactories(w,side,producer).All(p=>p.Queue.Count==4),"even automatic dispatch");
    ParallelAdvance(w,16);int delivered=w.Live(side).Count(p=>p.T==measured);results[count-1]=delivered;
    AssertParallel(delivered==count*perFactory,"real independent output: "+producer+" / "+side+" / "+count+" facilities: "+delivered);
    AssertParallel(w.Funds[side]+delivered*measured.Price+ParallelFactories(w,side,producer).Sum(p=>p.WorkPaid)==100000,"parallel cost conservation");
   }
   Console.WriteLine("{0}, faction {1}: actual 16-second output with 1 / 2 / 3 facilities = {2} / {3} / {4} units.",producer,side,results[0],results[1],results[2]);
  }
  {var w=ParallelWorld(2,0,"barracks");for(int i=0;i<8;i++)w.Train(Info("squad"),0,null);ParallelAdvance(w,8);
   AssertParallel(w.InfantryDelivered==4,"barracks also have real parallel lines");}
  {var w=ParallelWorld(1);var a=ParallelFactories(w)[0];for(int i=0;i<6;i++)w.Train(type,0,null);ParallelAdvance(w,1);
   float work=a.Work;int paid=a.WorkPaid;var b=w.Spawn("factory",0,800,700);b.Ready=.95f;b.Assembly=true;
   w.BalanceProduction();AssertParallel(b.Queue.Count==0,"unfinished factory receives no orders");ParallelAdvance(w,.15f);
   AssertParallel(b.Deployed&&b.Queue.Count==1&&a.Queue.Count==5&&a.Work>=work&&a.WorkPaid>=paid,"newly built factory takes only an unstarted order");
   float head=a.Work;w.SetPrimary(b);AssertParallel(a.Work==head&&a.Queue.Count==5&&b.Queue.Count==1,"priority change cannot move paid jobs");
   b.ProductionPaused=true;float paused=b.Work;ParallelAdvance(w,.5f);AssertParallel(b.Work==paused&&a.Work>head,"one paused factory does not pause others");}
  {var w=ParallelWorld(2);var f=ParallelFactories(w);f[0].ProductionPaused=true;w.Train(type,0,null);
   AssertParallel(f[1].Queue.Count==1&&f[0].Queue.Count==0,"automatic dispatch avoids a paused line");
   f[0].ProductionPaused=false;w.Train(type,0,null);f[0].Work=type.Seconds;f[0].WorkPaid=type.Price;
   var door=Middle(ExitApron(f[0].T,Footprint(f[0].T,f[0].P)));var blocker=w.Spawn("tank",1,door.X,door.Y);blocker.Cool=100;
   var exit=door;AssertParallel(!w.ExitClear(f[0],type,exit),"deliberately blocked outlet");
   for(int i=0;i<160;i++)foreach(var p in w.Units.ToArray())if(p!=blocker)w.UnitTick(p,.05f,true);
   AssertParallel(f[0].Queue.Count==1&&f[1].Queue.Count==0&&w.Live(0).Count(p=>p.T==type)==1,"blocked outlet never stalls another factory");}
  {var w=ParallelWorld(2);var f=ParallelFactories(w);for(int i=0;i<4;i++)w.Train(type,0,null);ParallelAdvance(w,1);
   float work=f[1].Work;int paid=f[1].WorkPaid,cash=w.Funds[0],lostPaid=f[0].WorkPaid;f[0].Hp=0;w.Kill(f[0]);
   AssertParallel(f[1].Work==work&&f[1].WorkPaid==paid&&f[1].Queue.Count==4&&w.Funds[0]==cash+lostPaid,"combat kill merges pending orders without overwriting a busy line");
   AssertParallel(f[0].Queue.Count==0&&f[0].WorkPaid==0,"destroyed production cleared");}
  {var w=ParallelWorld(2);var f=ParallelFactories(w);w.Train(type,0,f[0]);ParallelAdvance(w,1);float work=f[0].Work;int paid=f[0].WorkPaid;
   f[0].Hp=0;w.Kill(f[0]);AssertParallel(f[1].Queue.Count==1&&f[1].Work==work&&f[1].WorkPaid==paid,"idle survivor retains paid current progress");}
  {var w=ParallelWorld(2,1);var f=ParallelFactories(w,1);for(int i=0;i<4;i++)w.Train(type,1,null);ParallelAdvance(w,1);
   float work=f[1].Work;int paid=f[1].WorkPaid,cash=w.Funds[1],refund=f[0].WorkPaid;
   var door=Middle(ExitApron(f[0].T,Footprint(f[0].T,f[0].P)));var engineer=w.Spawn("engineer",0,door.X,door.Y);
   AssertParallel(w.EngineerOrder(engineer,f[0]),"capture occupied production facility");w.UnitTick(engineer,.1f,true);
   AssertParallel(f[0].Side==0&&f[0].Queue.Count==0&&f[1].Work==work&&f[1].WorkPaid==paid&&f[1].Queue.Count==4&&w.Funds[1]==cash+refund,"capture refunds old faction without overwriting survivor");}
  {var w=ParallelWorld(2);var f=ParallelFactories(w);for(int i=0;i<4;i++)w.Train(type,0,null);ParallelAdvance(w,1);int cash=w.Funds[0],refund=f[0].WorkPaid;float work=f[1].Work;
   AssertParallel(w.Sell(f[0])&&f[1].Work==work&&f[1].Queue.Count==4&&w.Funds[0]==cash+refund+Info("factory").Price/2,"selling one factory preserves the other line and exact refunds");}
  {var w=ParallelWorld(2);var f=ParallelFactories(w);w.Funds[0]=7;w.Train(type,0,null);w.Train(type,0,null);ParallelAdvance(w,1);
   AssertParallel(w.Funds[0]==0&&f.Sum(p=>p.WorkPaid)==7,"scarce money cannot create negative funds or free work");
   var progress=f.Select(p=>p.Work).ToArray();ParallelAdvance(w,1);AssertParallel(f.Select(p=>p.Work).SequenceEqual(progress),"unpaid work stays frozen");
   w.Funds[0]=1000;ParallelAdvance(w,1);AssertParallel(f.Select((p,i)=>p.Work>progress[i]).All(v=>v),"both lines resume after funds return");
   foreach(var power in w.Live(0).Where(p=>p.T.Key=="power"))power.Hp=0;
   var before=f.Select(p=>p.Work).ToArray();ParallelAdvance(w,1);
   AssertParallel(f.Select((p,i)=>Math.Abs(p.Work-before[i]-.45f)<.001f).All(v=>v),"low power applies equally without extra factory multiplier");}
  {var w=ParallelWorld(2);var f=ParallelFactories(w);for(int i=0;i<60;i++)AssertParallel(w.Train(type,0,null),"two independent full queues");
   AssertParallel(f.All(p=>p.Queue.Count==30)&&!w.Train(type,0,null),"30 orders per building, not per faction");
   w.UnitTick(f[0],1,true);int cash=w.Funds[0],paid=f[0].WorkPaid;var original=f[1].Queue.ToArray();f[0].Hp=0;w.Kill(f[0]);
   AssertParallel(f[1].Queue.SequenceEqual(original)&&f[1].Queue.Count==30&&w.Funds[0]==cash+paid,"loss with a full surviving line cannot overwrite it or exceed its limit");}
  Console.WriteLine("Parallel production: completion/backlog, independent pause, blocked outlet, combat kill, capture, sell, exact costs, low power, three factions and infantry passed.");
 }
}
partial class CommandWindow {
 internal void CheckParallelProductionInput(string folder){
  StartMission(true,false);ClientSize=new Size(1920,1080);world.Units.Clear();world.Ore.Clear();world.Funds[0]=5000;world.Spawn("mcv",1,2100,1400);
  world.Spawn("hq",0,450,420);world.Spawn("power",0,550,420);world.Spawn("power",0,650,420);
  var a=world.Spawn("factory",0,460,580);var b=world.Spawn("factory",0,640,580);var c=world.Spawn("factory",0,820,580);
  selection.Clear();selection.Add(a);ProductionTab(3);var t=Campaign.Info("ifv");
  for(int i=0;i<9;i++)BuildCardClick(t);
  if(a.Queue.Count!=3||b.Queue.Count!=3||c.Queue.Count!=3)throw new Exception("sidebar with selected factory must still automatically dispatch");
  for(int i=0;i<20;i++)world.Tick(.1f);
  float active=a.Work;int activePaid=a.WorkPaid,cash=world.Funds[0];BuildCardRight(t);
  if(a.Queue.Count!=2||a.ProductionPaused||a.Work!=active||a.WorkPaid!=activePaid||world.Funds[0]!=cash)throw new Exception("right click first removes one unpaid duplicate without pausing active work");
  BuildCardRight(t);if(a.Queue.Count!=1)throw new Exception("remove remaining duplicate");
  BuildCardRight(t);if(!a.ProductionPaused||b.ProductionPaused||c.ProductionPaused)throw new Exception("right click with no duplicates pauses only selected producer");
  float work=b.Work;world.Tick(.1f);if(b.Work<=work)throw new Exception("other factories continue behind sidebar pause");
  int total=Pending();BuildCardClick(t);if(a.ProductionPaused||Pending()!=total)throw new Exception("left click resumes same factory without adding an order");
  BuildCardRight(t);cash=world.Funds[0];int paid=a.WorkPaid;BuildCardRight(t);
  if(a.Queue.Count!=0||a.WorkPaid!=0||world.Funds[0]!=cash+paid)throw new Exception("second right click cancels only one paid head with exact refund");
  for(int i=0;i<3;i++)BuildCardClick(t);
  world.SetPrimary(b);if(a.Queue.Count!=3||b.Queue.Count!=3||c.Queue.Count!=3)throw new Exception("priority UI changes no existing orders");
  CenterAt(new PointF(640,580));
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));if(folder!=null)frame.Save(Path.Combine(folder,"多工厂并行生产.png"),ImageFormat.Png);
   foreach(var factory in new[]{a,b,c}){
    selection.Clear();selection.Add(factory);OpenQueue();hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));
    if(queueId!=factory.Id||!hits.Any(h=>h.Id=="cancel-0"))throw new Exception("individual factory queue is selectable and cancellable");
   }
  }
  var barracksA=world.Spawn("barracks",0,460,820);var barracksB=world.Spawn("barracks",0,640,820);var infantry=Campaign.Info("squad");
  selection.Clear();selection.Add(barracksA);ProductionTab(2);for(int i=0;i<6;i++)BuildCardClick(infantry);
  if(barracksA.Queue.Count!=3||barracksB.Queue.Count!=3)throw new Exception("infantry sidebar automatically uses both barracks");
  world.Tick(.1f);BuildCardRight(infantry);BuildCardRight(infantry);BuildCardRight(infantry);float soldierWork=barracksA.Work,otherWork=barracksB.Work;world.Tick(.1f);
  if(!barracksA.ProductionPaused||barracksB.ProductionPaused||barracksA.Work!=soldierWork||barracksB.Work<=otherWork)throw new Exception("barracks pause is independent");
  BuildCardClick(infantry);if(barracksA.ProductionPaused||barracksA.Queue.Count!=1||barracksB.Queue.Count!=3)throw new Exception("infantry resume does not duplicate an order");
  Console.WriteLine("Native infantry UI: two barracks, automatic dispatch and independent pause/resume passed.");
  Console.WriteLine("Native production UI: automatic dispatch, local pause/resume/cancel, priority isolation, totals, all three individual queues and rendering passed.");
 }
}
}
