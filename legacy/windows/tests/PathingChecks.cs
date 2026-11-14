using System;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using Greyhaven;
class PathingChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static Campaign Empty(){var w=new Campaign(true);w.Units.Clear();w.Spawn("engineer",1,3100,2000);return w;}
 static void CrowdedAttack(float dt){
  var w=Empty();var target=w.Spawn("municipal",1,864,1008);target.Hp=100000;
  for(int i=0;i<6;i++){var front=w.Spawn("tank",0,760,944+i*24);w.Attack(front,target);}
  var rear=Enumerable.Range(0,18).Select(i=>w.Spawn(i%3==0?"at":"squad",0,620+i%3*12,944+i/3*12)).ToArray();
  foreach(var u in rear)w.Attack(u,target);
  for(int i=0;i<30/dt;i++)w.Tick(dt);
  int fired=rear.Count(u=>u.FiredAt>=0);Console.WriteLine("Rear firing around six tanks at {0:F0} Hz: {1}/{2}",1/dt,fired,rear.Length);
  Check(fired==rear.Length,"rear units never reached firing positions: "+String.Join(";",rear.Where(u=>u.FiredAt<0).Select(u=>u.P+" path="+String.Join("/",u.Path)+" stall="+u.StalledFor+" slow="+u.NoProgressFor)));
 }
 static void JammedMarch(float dt){
  var w=Empty();var troop=Enumerable.Range(0,12).Select(i=>w.Spawn(i%3==0?"squad":"tank",0,450+i%4*28,950+i/4*28)).ToArray();
  var starts=troop.Select(u=>u.P).ToArray();Check(w.MarchTo(troop,new PointF(1100,1000),false,true)==12,"jam scenario order");
  var blocker=w.Spawn("miner",0,610,950);blocker.Mining=false;
  for(int i=0;i<10/dt;i++)w.Tick(dt);
  var progress=troop.Select((u,i)=>Campaign.Dist(starts[i],u.P)).OrderBy(v=>v).ToArray();
  Console.WriteLine("Blocked mixed convoy at {0:F0} Hz: median travel {1:F1} in 10 seconds",1/dt,progress[6]);Check(progress[6]>300,"one stalled member slowed entire group");
  for(int i=0;i<30/dt;i++)w.Tick(dt);
  Check(troop.All(u=>Campaign.Dist(u.P,u.Goal)<12),"blocked convoy did not finish");
 }
 static void ParkedNeutralTraffic(float dt){
  foreach(string type in new[]{"tank","ifv","mcv"})for(int scenario=0;scenario<4;scenario++){
   var w=Empty();var start=new PointF(700,900);var goal=new PointF(850,900);
   bool parkedBeforeOrder=scenario<2, occupiedGoal=scenario%2==1;
   var driver=w.Spawn(type,0,start.X,start.Y);
   var parking=occupiedGoal?goal:new PointF(770,900);
   Piece truck=null;
   if(parkedBeforeOrder)truck=w.Spawn("miner",-1,parking.X,parking.Y);
   if(parkedBeforeOrder&&occupiedGoal){
    var existing=new PointF(700,975);
    Check(w.Move(driver,existing,false),"initial order");
    int pathCount=driver.Path.Count;
    Check(!w.CanMoveTo(driver,goal)&&!w.Move(driver,goal,false)&&driver.Goal==existing&&driver.Path.Count==pathCount,
     "occupied neutral goal must reject a new order and preserve the old path");
   }else{
    Check(w.CanMoveTo(driver,goal)&&w.Move(driver,goal,false),"the clear destination takes a move order");
   }
   if(!parkedBeforeOrder)truck=w.Spawn("miner",-1,parking.X,parking.Y);
   truck.Mining=false;
   float maxStall=0;
   for(int tick=0;tick<25/dt&&driver.Path.Count>0;tick++){
    w.Tick(dt);maxStall=Math.Max(maxStall,driver.TrafficBlocked);
    Check(!Campaign.TrafficContact(driver.T,driver.P,truck.T,truck.P),"vehicle overlaps neutral miner");
   }
   Check(driver.Path.Count==0&&truck.P==parking&&truck.Alive,"vehicle must finish its leg and leave the neutral miner untouched");
   if(!occupiedGoal)Check(Campaign.Dist(driver.P,goal)<1&&maxStall<.5f,"vehicle must promptly bypass neutral traffic");
   if(!parkedBeforeOrder&&occupiedGoal)Check(driver.Goal==driver.P&&Campaign.Dist(driver.P,goal)<2.25f*Campaign.Tile,
    "a previously ordered destination which becomes occupied must end the leg nearby");
  }
  Console.WriteLine("Parked neutral traffic at {0:F0} Hz: tanks, IFVs and MCVs passed 12 static and late-blocker cases.",1/dt);
 }
 [STAThread]static int Main(string[] args){try{
  foreach(float dt in new[]{.05f,1f/30,1f/60})ParkedNeutralTraffic(dt);
  foreach(float dt in new[]{.05f,1f/60}){CrowdedAttack(dt);JammedMarch(dt);}
  using(var window=new CommandWindow())window.CapturePathingReview(args[0]);Console.WriteLine("Congestion and neutral-house presentation checks passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
partial class CommandWindow {
 internal void CapturePathingReview(string folder){
  StartTerrainMission(false);if(world.Units.Where(u=>u.T.Civilian).Any(u=>u.Side!=-1))throw new Exception("house starts hostile");
  if(UnitColor(-1).R<210||UnitColor(-1).G<210||UnitColor(-1).B<210)throw new Exception("neutral color not white");
  ClientSize=new Size(1920,1080);var house=world.Units.First(u=>u.T.Key=="municipal");var engineer=world.Live(0).First(u=>u.T.Key=="engineer");engineer.P=new PointF(house.P.X,house.P.Y+100);
  world.Tick(.1f);selection.Clear();selection.Add(engineer);CenterAt(house.P);mouse=ScreenOf(house.P);
  using(var image=new Bitmap(1920,1080))using(var g=Graphics.FromImage(image)){
   OnPaint(new PaintEventArgs(g,ClientRectangle));image.Save(Path.Combine(folder,"河湾镇-白色中立房屋.png"));
   OpenMissions();riverMissionSelected=true;hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));image.Save(Path.Combine(folder,"关卡选择-第二关.png"));
   if(!hits.Any(h=>h.Id=="进入河湾镇"))throw new Exception("mission 2 primary entry missing");
  }
 }
}
}
