using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class ParallelProductionChecks {
 [STAThread]static int Main(string[] args){try{
  Campaign.CheckVehicleProduction();
  if(!args.Contains("--rules-only"))using(var form=new CommandWindow())form.CheckParallelProductionInput(args.Length>0?args[0]:null);
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
 internal static void CheckVehicleProduction(){
  var type=Info("ifv");
  foreach(int side in new[]{0,1,2})for(int count=1;count<=3;count++){
   var w=ParallelWorld(count,side);var factories=ParallelFactories(w,side);var line=w.VehicleQueue(side);
   AssertParallel(w.Train(type,side,factories.Last())&&w.Train(type,side,factories.Last()),"two vehicle orders accepted");
   AssertParallel(line.Queue.Count==2&&factories.Skip(1).All(f=>f.Queue.Count==0),"one vehicle queue per faction");
   float duration=type.Seconds*(float)Math.Pow(.8,count-1);
   foreach(var factory in factories)w.UnitTick(factory,duration-.05f,true);
   AssertParallel(w.Live(side).Count(p=>p.T==type)==0,"no premature delivery");
   foreach(var factory in factories)w.UnitTick(factory,.05f,true);
   AssertParallel(line.Queue.Count==1&&w.Live(side).Count(p=>p.T==type)==1&&w.Funds[side]==100000-type.Price,
    "one paid vehicle after "+duration+" seconds, side "+side+", factories "+count);
  }
  foreach(string producer in new[]{"barracks","factory"}){
   var w=ParallelWorld(1,1,producer);var line=ParallelFactories(w,1,producer).Single();
   var order=Info(producer=="barracks"?"at":"tank");
   var door=Middle(ExitApron(line.T,Footprint(line.T,line.P)));
   line.Rally=new PointF(door.X+4,door.Y);
   var engineer=w.Spawn("engineer",1,line.Rally.Value.X,line.Rally.Value.Y);
   line.Queue.Enqueue(order);line.Work=order.Seconds;line.WorkPaid=order.Price;
   int cash=w.Funds[1];
   AssertParallel(!w.ExitClear(line,order,door),"the rally position must really block the apron");
   ParallelAdvance(w,5);
   AssertParallel(line.Queue.Count==0&&w.Live(1).Count(p=>p.T==order)==1&&w.Funds[1]==cash&&
    !TrafficContact(engineer.T,engineer.P,order,door),"a near-door rally must yield a paid order without blocking or recharging it: "+producer);
  }
  {
   var w=ParallelWorld(2,0,"barracks");var infantry=Info("squad");
   var barracks=ParallelFactories(w,0,"barracks");
   w.Train(infantry,0,null);w.Train(infantry,0,null);
   foreach(var b in barracks)w.UnitTick(b,infantry.Seconds+.05f,true);
   AssertParallel(w.Live(0).Count(p=>p.T==infantry)==2&&w.Funds[0]==100000-2*infantry.Price,
    "separate barracks still produce paid infantry concurrently");
  }
  {
   var w=ParallelWorld(2);var factories=ParallelFactories(w);var host=factories[0];var other=factories[1];
   other.Ready=.5f;
   AssertParallel(Math.Abs(w.ProductionRate(host)-1)<.001f,"unfinished factory grants no bonus");
   other.Ready=1;
   for(int i=0;i<30;i++)AssertParallel(w.Train(type,0,null),"accept shared queue order");
   AssertParallel(!w.Train(type,0,other)&&other.Queue.Count==0,"second factory cannot bypass queue capacity");
   w.UnitTick(host,1,true);float work=host.Work;int paid=host.WorkPaid,cash=w.Funds[0];
   host.ProductionPaused=true;
   AssertParallel(w.SetPrimary(other)&&w.VehicleQueue(0)==host&&host.Work==work,"changing exit preserves active order");
   host.Hp=0;w.Kill(host);
   AssertParallel(w.VehicleQueue(0)==other&&other.Queue.Count==30&&other.Work==work&&other.WorkPaid==paid&&
    other.ProductionPaused&&w.Funds[0]==cash,"host loss preserves shared orders, payment and pause");
   AssertParallel(w.CancelOrder(other,0)&&other.Queue.Count==29&&!other.ProductionPaused&&w.Funds[0]==cash+paid,
    "head cancellation refunds exact paid funds");
  }
  {
   var w=ParallelWorld(2);var line=w.VehicleQueue(0);var other=ParallelFactories(w)[1];
   var door=Middle(ExitApron(line.T,Footprint(line.T,line.P)));
   var preferredDoor=Middle(ExitApron(other.T,Footprint(other.T,other.P)));
   w.Train(type,0,null);w.Train(type,0,null);
   // Start before switching the outlet so the first factory retains the queue.
   w.SetPrimary(other);w.UnitTick(line,type.Seconds*.8f,true);
   AssertParallel(w.Live(0).Single(p=>p.T==type).P==preferredDoor,"completed vehicle spawns at selected primary");
   w.UnitTick(line,type.Seconds*.8f,true);
   AssertParallel(w.Live(0).Any(p=>p.T==type&&p.P==door)&&w.Funds[0]==100000-2*type.Price,
    "one fully paid vehicle uses the alternate exit when primary is blocked");
  }
  {
   var w=ParallelWorld(2,1);var host=w.VehicleQueue(1);var survivor=ParallelFactories(w,1)[1];
   w.Train(type,1,null);w.UnitTick(host,1,true);int cash=w.Funds[1],paid=host.WorkPaid;float work=host.Work;
   var door=Middle(ExitApron(host.T,Footprint(host.T,host.P)));var engineer=w.Spawn("engineer",0,door.X,door.Y);
   AssertParallel(w.EngineerOrder(engineer,host),"capture vehicle queue host");w.UnitTick(engineer,.1f,true);
   AssertParallel(host.Side==0&&host.Queue.Count==0&&w.VehicleQueue(1)==survivor&&survivor.Queue.Count==1&&
    survivor.WorkPaid==paid&&survivor.Work==work&&w.Funds[1]==cash,"captured factory does not take old faction orders");
  }
  Console.WriteLine("Shared vehicle timing, costs, full queue, host loss, pause and exit fallback passed for player and AI.");
 }
}
partial class CommandWindow {
 internal void CheckParallelProductionInput(string folder){
  StartMission(true,false);ClientSize=new Size(1920,1080);world.Units.Clear();world.Ore.Clear();world.Funds[0]=5000;world.Spawn("mcv",1,2100,1400);
  world.Spawn("hq",0,450,420);world.Spawn("power",0,550,420);world.Spawn("power",0,650,420);
  var a=world.Spawn("factory",0,460,580);var b=world.Spawn("factory",0,640,580);var c=world.Spawn("factory",0,820,580);
  selection.Clear();selection.Add(a);ProductionTab(3);var t=Campaign.Info("ifv");
  for(int i=0;i<9;i++)BuildCardClick(t);
  if(a.Queue.Count!=9||b.Queue.Count!=0||c.Queue.Count!=0)throw new Exception("sidebar uses one vehicle queue");
  for(int i=0;i<20;i++)world.Tick(.1f);
  float active=a.Work;int activePaid=a.WorkPaid,cash=world.Funds[0];BuildCardRight(t);
  if(a.Queue.Count!=8||a.ProductionPaused||a.Work!=active||a.WorkPaid!=activePaid||world.Funds[0]!=cash)throw new Exception("right click first removes one unpaid duplicate without pausing active work");
  for(int i=0;i<7;i++)BuildCardRight(t);if(a.Queue.Count!=1)throw new Exception("remove remaining duplicates");
  BuildCardRight(t);if(!a.ProductionPaused)throw new Exception("right click with no duplicates pauses the shared vehicle queue");
  world.Tick(.1f);if(a.Work!=active||b.Work!=0||c.Work!=0)throw new Exception("no factory produces behind the sidebar pause");
  int total=Pending();BuildCardClick(t);if(a.ProductionPaused||Pending()!=total)throw new Exception("left click resumes same factory without adding an order");
  BuildCardRight(t);cash=world.Funds[0];int paid=a.WorkPaid;BuildCardRight(t);
  if(a.Queue.Count!=0||a.WorkPaid!=0||world.Funds[0]!=cash+paid)throw new Exception("second right click cancels only one paid head with exact refund");
  for(int i=0;i<3;i++)BuildCardClick(t);
  world.SetPrimary(b);if(a.Queue.Count!=3||b.Queue.Count!=0||c.Queue.Count!=0)throw new Exception("primary exit changes no existing orders");
  CenterAt(new PointF(640,580));
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));if(folder!=null)frame.Save(Path.Combine(folder,"车辆共享队列.png"),ImageFormat.Png);
   foreach(var factory in new[]{a,b,c}){
    selection.Clear();selection.Add(factory);OpenQueue();hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));
    if(queueId!=a.Id||!hits.Any(h=>h.Id=="cancel-0"))throw new Exception("every factory opens the same cancellable vehicle queue");
   }
  }
  var barracksA=world.Spawn("barracks",0,460,820);var barracksB=world.Spawn("barracks",0,640,820);var infantry=Campaign.Info("squad");
  selection.Clear();selection.Add(barracksA);ProductionTab(2);for(int i=0;i<6;i++)BuildCardClick(infantry);
  if(barracksA.Queue.Count!=3||barracksB.Queue.Count!=3)throw new Exception("infantry sidebar automatically uses both barracks");
  world.Tick(.1f);BuildCardRight(infantry);BuildCardRight(infantry);BuildCardRight(infantry);float soldierWork=barracksA.Work,otherWork=barracksB.Work;world.Tick(.1f);
  if(!barracksA.ProductionPaused||barracksB.ProductionPaused||barracksA.Work!=soldierWork||barracksB.Work<=otherWork)throw new Exception("barracks pause is independent");
  BuildCardClick(infantry);if(barracksA.ProductionPaused||barracksA.Queue.Count!=1||barracksB.Queue.Count!=3)throw new Exception("infantry resume does not duplicate an order");
  Console.WriteLine("Native infantry UI: two barracks, automatic dispatch and independent pause/resume passed.");
  Console.WriteLine("Native vehicle UI: shared queue, pause/resume/cancel and primary exit selection passed.");
 }
}
}
