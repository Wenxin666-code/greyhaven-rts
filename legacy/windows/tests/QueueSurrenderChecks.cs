using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class QueueSurrenderChecks {
 [STAThread]static int Main(string[] args){try{
  new Campaign(true).CheckProductionCapacity();new Campaign().CheckSurrenderCases();
  if(args.Length>0)using(var f=new CommandWindow())f.CheckQueuePages(args[0]);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void AssertQueue(bool ok,string message){if(!ok)throw new Exception(message);}
 internal void CheckProductionCapacity(){
  Units.Clear();Ore.Clear();Funds[0]=0;
  var b=Spawn("barracks",0,600,600);var f=Spawn("factory",0,900,600);Spawn("hq",0,500,500);Spawn("power",0,700,500);
  for(int i=0;i<30;i++)AssertQueue(Train(Info(i%2==0?"squad":"at"),0,null),"all 30 infantry orders accepted");
  AssertQueue(!Train(Info("engineer"),0,null)&&b.Queue.Count==30&&Require(Info("engineer"),0).Contains("30"),"31st rejected with reason");
  for(int i=0;i<30;i++)AssertQueue(Train(Info("ifv"),0,null),"independent 30 vehicle orders, no old 55 cap");
  var secondFactory=Spawn("factory",0,1080,600);
  AssertQueue(!Train(Info("miner"),0,secondFactory)&&secondFactory.Queue.Count==0,"shared 30-order vehicle queue");
  AssertQueue(Require(Info("refinery"),0)==null,"no global cap on refinery free miner");
  var replacement=Spawn("barracks",0,400,700);var jobs=b.Queue.ToArray();SetPrimary(replacement);
  AssertQueue(b.Queue.SequenceEqual(jobs)&&replacement.Queue.Count==0,"priority does not transfer any old queue");
  for(int i=0;i<30;i++)AssertQueue(Train(Info("engineer"),0,null),"new factory has its own 30 slots");
  AssertQueue(!Train(Info("engineer"),0,null),"both factories must be full before rejecting");
  AssertQueue(CancelOrder(replacement,29)&&replacement.Queue.Count==29&&Funds[0]==0,"cancel last order without bogus refund");
  AssertQueue(Train(Info("engineer"),0,null)&&replacement.Queue.Last().Key=="engineer","slot freed at tail");
  for(int i=0;i<60;i++)Spawn("squad",0,200+i*12,1100);
  CancelOrder(f,29);AssertQueue(Train(Info("miner"),0,null),"more than 55 alive units still accept orders");
  Funds[0]=1000;UnitTick(f,1,true);int paid=f.WorkPaid,cash=Funds[0];
  AssertQueue(paid>0&&CancelOrder(f,0)&&Funds[0]==cash+paid&&f.Queue.Count==29,"head refund with long tail");
  Console.WriteLine("Production: shared vehicle capacity, independent barracks capacity, no global 55 cap and exact cancellation refunds passed.");
 }
 static Campaign SurrenderWorld(bool river=false){var w=new Campaign(false,false,river);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",0,300,800);return w;}
 internal void CheckSurrenderCases(){
  foreach(string key in new[]{"hq","mcv","barracks","factory","squad","at","engineer","ifv","tank","arty"}){
   var w=SurrenderWorld();w.Spawn("power",1,1800,900);var guard=w.Spawn(key,1,1800,1100);w.Funds[1]=0;
   if(guard.T.Building)guard.Ready=.001f;
   w.CheckEnemySurrender(4);AssertQueue(!w.HasSurrendered(1),"asset prevents premature surrender: "+key);
  }
  {var w=SurrenderWorld();var h=w.Spawn("hq",1,1800,900);h.Packing=1;w.CheckEnemySurrender(4);AssertQueue(!w.HasSurrendered(1),"packing HQ counted");}
  foreach(string carrier in new[]{"ifv","residence"}){
   var w=SurrenderWorld();var c=w.Spawn(carrier,1,1800,900);var foot=w.Spawn("squad",1,1800,900);foot.Carrier=c;
   w.CheckEnemySurrender(4);AssertQueue(!w.HasSurrendered(1),"hidden passenger/garrison counted: "+carrier);
  }
  {var w=SurrenderWorld();var c=w.Spawn("residence",1,1800,900);w.Spawn("engineer",1,1800,900).Carrier=c;w.CheckEnemySurrender(4);AssertQueue(!w.HasSurrendered(1),"hidden engineer counted");}
  {var w=SurrenderWorld();var plant=w.Spawn("power",1,1800,900);w.CheckEnemySurrender(2);AssertQueue(!w.HasSurrendered(1),"confirmation delay");
   var unit=w.Spawn("squad",1,1900,900);w.CheckEnemySurrender(1);w.Kill(unit);w.CheckEnemySurrender(2);AssertQueue(!w.HasSurrendered(1),"capability recovery resets timer");
   w.CheckEnemySurrender(1.1f);AssertQueue(w.HasSurrendered(1)&&!plant.Alive,"new complete delay required");}
  {var w=SurrenderWorld(true);var neutral=w.Spawn("residence",-1,1200,1800);var other=w.Spawn("mcv",2,2200,1800);
   var h=w.Spawn("hq",1,1700,1800);AssertQueue(w.StartConstruction(Info("post"),1),"pending building before base destruction");w.Kill(h);
   var power=w.Spawn("power",1,1800,1800);var post=w.Spawn("post",1,2000,1800);var miner=w.Spawn("miner",1,1900,1900);w.Funds[1]=500;
   for(int i=0;i<32;i++)w.Tick(.1f);
   AssertQueue(w.HasSurrendered(1)&&!w.HasSurrendered(2)&&!w.Won,"third faction continues after first surrenders");
   AssertQueue(!power.Alive&&!post.Alive&&!miner.Alive&&power.Dead<1&&w.Remaining(1)==0,"self destruct all remaining owned assets with normal effects");
   AssertQueue(neutral.Alive&&neutral.Side==-1&&other.Alive&&w.Funds[1]==500&&w.Construction(1,1)==null,"no neutral damage, money refund or ghost orders");
   w.Kill(other);w.Spawn("radar",2,2200,1700);for(int i=0;i<32&&!w.Won;i++)w.Tick(.1f);
   AssertQueue(w.HasSurrendered(2)&&w.Won&&!w.OutcomeReady,"final surrender wins after animation starts");w.TickVisuals(2.7f);AssertQueue(w.OutcomeReady,"result waits for explosion");
  }
  {var w=SurrenderWorld();w.Spawn("power",0,200,700);foreach(var u in w.Units.Where(u=>u.T.Key=="mcv").ToArray())w.Kill(u);w.Spawn("mcv",1,1900,1000);w.CheckEnemySurrender(5);AssertQueue(w.Units.Any(u=>u.Alive&&u.Side==0),"player never auto surrenders");}
  {var w=SurrenderWorld();var f=w.Spawn("factory",1,1800,900);w.Funds[1]=500;w.EnemyEconomy(1);AssertQueue(f.Queue.Any(t=>t.Key=="mcv"),"factory rebuilds MCV without HQ");
   for(int i=0;i<500;i++){foreach(var u in w.Units.ToArray())w.UnitTick(u,.1f,true);w.EnemyEconomy(1);}AssertQueue(w.Units.Any(u=>u.Alive&&u.Side==1&&u.T.Key=="hq"),"AI redeploys rebuilt MCV");}
  {var w=SurrenderWorld();var b=w.Spawn("barracks",1,1800,900);w.EnemyEconomy(1);AssertQueue(b.Queue.Count>0,"barracks reinforces after HQ loss");}
  Console.WriteLine("Surrender: resistance assets, assembly/packing, passengers/garrison, timer reset, per-faction elimination, scuttling, neutral safety, outcome animation and AI recovery passed.");
 }
}
partial class CommandWindow {
 internal void CheckQueuePages(string folder){
  StartMission(true,false);ClientSize=new Size(1920,1080);world.Units.Clear();world.Spawn("mcv",1,2100,1000);
  var b=world.Spawn("barracks",0,500,600);world.Funds[0]=0;for(int i=0;i<30;i++)world.Train(Campaign.Info(i%2==0?"squad":"at"),0,null);
  selection.Clear();selection.Add(b);OpenQueue();
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action render=()=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));};
   Action<string> tap=id=>{render();var hit=hits.Last(h=>h.Id==id);var p=new PointF(hit.Box.X+hit.Box.Width/2,hit.Box.Y+hit.Box.Height/2);
    var e=new MouseEventArgs(MouseButtons.Left,1,(int)(ox+p.X*scale),(int)(oy+p.Y*scale),0);OnMouseDown(e);OnMouseUp(e);render();};
   render();if(!hits.Any(h=>h.Id=="cancel-3")||hits.Any(h=>h.Id=="queue-prev-page"))throw new Exception("first page boundary");
   for(int i=0;i<7;i++)tap("queue-next-page");if(queuePage!=7||!hits.Any(h=>h.Id=="cancel-29")||hits.Any(h=>h.Id=="queue-next-page"))throw new Exception("last page boundary");
   frame.Save(Path.Combine(folder,"生产队列分页.png"),ImageFormat.Png);
   var tail=b.Queue.Last();tap("cancel-29");if(b.Queue.Count!=29||b.Queue.Last()==tail)throw new Exception("tail click cancels exact mixed-type slot");
   float oldZoom=zoom;var mouseEvent=new MouseEventArgs(MouseButtons.None,0,(int)(ox+(MW+50)*scale),(int)(oy+400*scale),120);OnMouseWheel(mouseEvent);render();
   if(queuePage!=6||zoom!=oldZoom)throw new Exception("sidebar wheel changes page without zooming map");
   while(b.Queue.Count>0)world.CancelOrder(b,0);render();if(queuePage!=0||hits.Any(h=>h.Id!=null&&h.Id.StartsWith("cancel-")))throw new Exception("empty queue clamps safely");
  }
  Console.WriteLine("Native queue pages: all 30 entries reachable, exact tail cancellation, safe disabled buttons, wheel paging and empty reset passed.");
 }
}
}
