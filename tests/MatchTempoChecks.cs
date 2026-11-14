using System;
using System.Drawing;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class MatchTempoChecks {
 [STAThread]static int Main(string[] args){try{
  CheckClocks();Campaign.CheckPressureRules();
  using(var form=new CommandWindow()){form.CheckTempoInterface(args.Length>0?args[0]:null);form.CheckOffscreenShadowResolution();form.CheckTempoBattleLoad(args.Length>0?args[0]:null);}
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 static Campaign ClockWorld(){
  var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",1,2400,2000);w.Spawn("hq",0,450,600);w.Spawn("power",0,550,600);var factory=w.Spawn("factory",0,480,800);factory.Rally=new PointF(500,1000);w.Funds[0]=10000;
  w.Train(Campaign.Info("tank"),0,null);w.StartConstruction(Campaign.Info("radar"));var unit=w.Spawn("tank",0,700,1150);Assert(w.Move(unit,new PointF(1000,1150),false),"clock route");return w;
 }
 static void CheckClocks(){
  foreach(float rate in new[]{2f,4f,6f,8f}){
   double? total=null;int? funds=null;PointF? position=null;
   foreach(int hz in new[]{24,30,60,144}){
    var clock=new SimulationStepper();double time=0;var world=ClockWorld();
    for(int frame=0;frame<hz*8;frame++)clock.Advance(1.0/hz,rate,dt=>{time+=dt;world.Tick(dt);});
    Assert(Math.Abs(time-8*rate)<.001,"render cadence changed simulation time "+rate+" / "+hz);
    var at=world.Live(0).First(p=>p.T.Key=="tank").P;
    if(total.HasValue)Assert(Math.Abs(total.Value-time)<1e-6&&funds.Value==world.Funds[0]&&Campaign.Dist(position.Value,at)<.001,"frame-dependent movement, production or payment");
    total=time;funds=world.Funds[0];position=at;
   }
  }
  var fastest=new SimulationStepper();double fastestTime=0;
  for(int i=0;i<160;i++)fastest.Advance(.05,8f,dt=>fastestTime+=dt);
  Assert(Math.Abs(fastestTime-64)<.001,"4x falls behind at 20 render Hz");
  var stalled=new SimulationStepper();double progressed=0;int steps=stalled.Advance(.2,2,dt=>progressed+=dt);
  Assert(steps==MatchTempo.MaxStepsPerFrame&&stalled.Pending>0,"long frame must be budgeted");
  stalled.Advance(.01,2,dt=>progressed+=dt);Assert(Math.Abs(progressed+stalled.Pending-.42)<1e-6,"short stall lost time");
  stalled.Reset();Assert(stalled.Pending==0&&stalled.Advance(Double.NaN,2,_=>{})==0,"clock reset/invalid time");
  Console.WriteLine("Clock: 2/4/6/8 game seconds per real second at 24/30/60/144 render Hz have consistent time, paid production and movement; bounded catch-up preserves short stalls.");
 }
}
namespace Greyhaven {
public partial class Campaign {
 static void AssertTempo(bool condition,string message){if(!condition)throw new Exception(message);}
 internal static void CheckPressureRules(){
  var w=new Campaign(false,false,true,true);var b=w.Brain(3);
  for(int side=0;side<w.FactionCount;side++)AssertTempo(w.Funds[side]==ReferenceBalance.StartingCredits,"asymmetric starting money");
  b.LastDispatch[0]=30;b.LastDispatch[1]=20;b.LastDispatch[2]=10;b.LastDispatch[4]=40;b.LastDispatch[5]=50;
  AssertTempo(w.AiRaidOpponent(3,b)==2,"raids must cover least-pressured opponent, not force player");b.LastDispatch[2]=60;AssertTempo(w.AiRaidOpponent(3,b)==2,"neglected distant factions must not override nearby threats");
  w.Units.Clear();w.Ore.Clear();var home=w.Home(3);w.Spawn("mcv",0,w.Home(0).X,w.Home(0).Y);w.Spawn("hq",3,home.X,home.Y);w.Spawn("power",3,home.X+100,home.Y-60);w.Spawn("refinery",3,home.X+80,home.Y+140);w.Funds[3]=2000;
  AssertTempo(w.OreReceipts[3]==0,"early military test setup");w.AiConstruction(3,b,new Piece[0]);AssertTempo(w.Construction(Campaign.Info("barracks"),3)!=null,"barracks must not wait for first cash receipt");
  w.Units.Clear();w.Spawn("hq",3,home.X,home.Y);w.Spawn("mcv",0,w.Home(0).X,w.Home(0).Y);w.Time=220;b.Wings.Clear();b.NextPush=190;b.NextRaid=99999;
  var troops=new[]{w.Spawn("tank",3,home.X+200,home.Y),w.Spawn("ifv",3,home.X+230,home.Y)};w.Reveal();w.SmartTactics(3,false);
  AssertTempo(b.Wings.Count==0&&troops.All(p=>b.HomeGuard.Contains(p)),"understrength base must retain its guards");
  // Supply both the separate four-unit field post and a real surplus attack wing.
  for(int i=0;i<20;i++)w.Spawn(i%3==0?"tank":"squad",3,home.X-120+i%6*24,home.Y-96+i/6*24);w.SmartTactics(3,false);
  AssertTempo(b.Wings.Count>0&&b.Wings.All(team=>team.Units.All(p=>!b.HomeGuard.Contains(p))),"surplus attack must not draw standing guards");
  var wingAt=b.Wings.Last();wingAt.OrderedAt=w.Time;AssertTempo(b.LastDispatch[0]==w.Time,"dispatch not recorded");
  var gate=w.Spawn("factory",3,home.X+350,home.Y+200);w.Funds[3]=10000;w.Train(Info("tank"),3,gate);w.UnitTick(gate,1,true);
  AssertTempo(gate.Work>0&&gate.WorkPaid>0&&w.Funds[3]<10000,"AI production bypasses payment");
  w.aiMoveRequests.Clear();w.aiMovesPending.Clear();w.QueueAiMarch(new[]{troops[0]},new PointF(-1000,-1000));w.TickAiMarchRequests();
  AssertTempo(w.aiMovesPending.Contains(troops[0])&&w.aiMoveRequests.Count==1&&w.aiMoveRequests.Peek().Attempt==1,"AI retried every alternate destination in one update");
  for(int attempt=1;attempt<17;attempt++)w.TickAiMarchRequests();AssertTempo(!w.aiMovesPending.Contains(troops[0])&&w.aiMoveRequests.Count==0,"AI invalid route did not finish its bounded retry cycle");
  AssertTempo(w.NavigationPriority(0,1000,w.N*w.M-1)<w.NavigationPriority(1001,0,0),"A* tie key overrode a lower total path cost");
  AssertTempo(w.NavigationPriority(1000,0,w.N*w.M-1)<w.NavigationPriority(0,1000,0),"A* equal-cost tie did not prefer goal proximity");
  var routes=new Campaign(true);routes.Units.Clear();routes.Ore.Clear();var moving=routes.Spawn("tank",0,600,1100);var grid=routes.Grid(moving.T.Clearance);routes.OpenGrid(grid,moving.T.Clearance);var baseOpen=(bool[])grid.Open.Clone();
  foreach(float x in new[]{740f,810f,740f}){
   var temporary=new[]{new RectangleF(x,1080,42,42)};AssertTempo(routes.PlanRoute(moving,new PointF(1000,1100),temporary),"temporary traffic detour");
   var from=moving.P;foreach(var point in moving.Path){AssertTempo(routes.CanLink(true,from,point,grid.Barriers.Concat(temporary).ToArray(),moving.T.Clearance),"shortcut crossed a swept obstacle");from=point;}
   AssertTempo(grid.Open.SequenceEqual(baseOpen),"temporary traffic mutated persistent navigation");
  }
  var pursue=new Campaign(true);pursue.Units.Clear();var hunter=pursue.Spawn("squad",0,400,1100);var target=pursue.Spawn("squad",1,600,1100);hunter.Target=target;
  hunter.Path.Enqueue(new PointF(450,1500));hunter.Path.Enqueue(new PointF(500,1100));AssertTempo(!pursue.ChaseRouteNeedsUpdate(hunter,target),"small target movement discarded long detour");
  target.P=new PointF(900,1100);AssertTempo(pursue.ChaseRouteNeedsUpdate(hunter,target),"large target movement did not refresh chase");hunter.Path.Clear();hunter.Path.Enqueue(new PointF(500,1100));target.P=new PointF(600,1100);AssertTempo(pursue.ChaseRouteNeedsUpdate(hunter,target),"near firing position became unresponsive");
  Console.WriteLine("AI pressure: equal money, no player targeting preference, nearby opposition, early paid barracks, permanent guard reservation and surplus team launch and paid vehicle production passed.");
 }
}
partial class CommandWindow {
 internal void CheckOffscreenShadowResolution(){
  StartIslandMission();ClientSize=new Size(1920,1080);var home=world.Home(5);var far=world.Spawn("power",5,home.X,home.Y);far.Hp=far.T.Hp*.2f;CenterAt(world.Home(0));
  using(var frame=new Bitmap(MW,H))using(var g=Graphics.FromImage(frame)){
   int before=painted.Count;var at=ScreenOf(far.P);DrawGroundShadow(g,far,at,far.T.Size*zoom);
   if(painted.Count!=before)throw new Exception("offscreen damaged shadow decoded a hidden frame");
   if(BuildingInkHit(far,PickBox(far),PointF.Empty)||painted.Count!=before)throw new Exception("hover outside a building decoded an unused damaged frame");
   DrawUnitHealth(g,far,at,far.T.Size*zoom);if(painted.Count!=before)throw new Exception("offscreen health bar decoded unused damaged art");
  }
  Console.WriteLine("Offscreen damaged-building shadows are clipped before resolving their art; visible shadow bounds are shared unchanged.");
 }
 // A native opaque DIB matches the real window destination. An ARGB
 // Graphics.FromImage target adds per-frame GDI alpha conversion unrelated
 // to presenting the game's already opaque frame buffer.
 sealed class NativeTempoCanvas:IDisposable {
  IntPtr dc,dib,previous;internal Bitmap Image;internal Graphics Surface;
  [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern bool GdiFlush();
  internal NativeTempoCanvas(int width,int height){
   IntPtr pixels;var header=new DibHeader{Size=40,Width=width,Height=-height,Planes=1,BitCount=32};dc=CreateCompatibleDC(IntPtr.Zero);dib=CreateDIBSection(dc,ref header,0,out pixels,IntPtr.Zero,0);previous=SelectObject(dc,dib);
   if(dc==IntPtr.Zero||dib==IntPtr.Zero||previous==IntPtr.Zero||previous==new IntPtr(-1))throw new Exception("native benchmark surface");
   Image=new Bitmap(width,height,width*4,PixelFormat.Format32bppPArgb,pixels);Surface=Graphics.FromHdc(dc);
  }
  internal void Save(string path){Surface.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);GdiFlush();Image.Save(path,ImageFormat.Png);}
  public void Dispose(){if(Surface!=null)Surface.Dispose();if(Image!=null)Image.Dispose();if(previous!=IntPtr.Zero)SelectObject(dc,previous);if(dib!=IntPtr.Zero)DeleteObject(dib);if(dc!=IntPtr.Zero)DeleteDC(dc);}
 }
 internal void CheckTempoBattleLoad(string folder){
  StartIslandMission();ClientSize=new Size(1920,1080);world.Deploy(world.Live(0).First(p=>p.T.Key=="mcv"));
  for(int i=0;i<5200&&!world.Won&&!world.Lost;i++)world.Tick(.05f);
  if(world.Lost||world.Won)throw new Exception("tempo load scene ended during warmup");matchRealTime=world.Time/speed;
  var timings=new System.Collections.Generic.List<double>();var simTimes=new System.Collections.Generic.List<double>();var drawTimes=new System.Collections.Generic.List<double>();string realNote="";
  using(var canvas=new NativeTempoCanvas(1920,1080)){var g=canvas.Surface;
   for(int i=0;i<360;i++){
    var clock=Stopwatch.StartNew();AdvanceMatch(1.0/60);double simMs=clock.Elapsed.TotalMilliseconds;Pan(2,1);OnPaint(new PaintEventArgs(g,ClientRectangle));if(i>=24){timings.Add(clock.Elapsed.TotalMilliseconds);simTimes.Add(simMs);drawTimes.Add(clock.Elapsed.TotalMilliseconds-simMs);}
   }
   // Feed measured wall time back into the normal clock. This catches
   // overload that an ideal 60-Hz offline input alone could conceal.
   var real=Stopwatch.StartNew();double previous=0;float began=world.Time;int frames=0;var intervals=new System.Collections.Generic.List<double>();
   while(real.Elapsed.TotalSeconds<4){
    double now=real.Elapsed.TotalSeconds;if(now-previous<1.0/60){System.Threading.Thread.Sleep(1);continue;}
    intervals.Add((now-previous)*1000);AdvanceMatch(now-previous);previous=now;Pan(2,1);OnPaint(new PaintEventArgs(g,ClientRectangle));frames++;
   }
   intervals.Sort();realNote=String.Format(System.Globalization.CultureInfo.InvariantCulture,"Six-faction measured-time loop: {0:F2} game seconds / {1:F2} active real seconds = {2:F2}x; {3} frames, interval median {4:F1}ms p95 {5:F1}ms.",world.Time-began,previous,(world.Time-began)/Math.Max(.01,previous),frames,intervals[intervals.Count/2],intervals[(int)(intervals.Count*.95)]);
   if(folder!=null)canvas.Save(Path.Combine(folder,"六方运行与滚屏.png"));
  }
  timings.Sort();int count=timings.Count;
  string note=String.Format(System.Globalization.CultureInfo.InvariantCulture,"Six-faction 2x + native 1920x1080 scrolling: {0} samples, median {1:F1}ms, p95 {2:F1}ms, max {3:F1}ms, >33ms {4}; living entities {5}, game time {6:F1}.",count,timings[count/2],timings[(int)(count*.95)],timings[count-1],timings.Count(t=>t>33),world.Units.Count(p=>p.Alive),world.Time);
  simTimes.Sort();drawTimes.Sort();note+=String.Format(System.Globalization.CultureInfo.InvariantCulture," Simulation median/p95 {0:F1}/{1:F1}ms; rendering median/p95 {2:F1}/{3:F1}ms.",simTimes[count/2],simTimes[(int)(count*.95)],drawTimes[count/2],drawTimes[(int)(count*.95)]);
  Console.WriteLine(note);Console.WriteLine(realNote);if(folder!=null)File.WriteAllText(Path.Combine(folder,"运行测量.txt"),note+Environment.NewLine+realNote+Environment.NewLine);
 }
 internal void CheckTempoInterface(string folder){
  StartTerrainMission(false);ClientSize=new Size(1920,1080);if(speed!=2||MatchSpeedLabel!="×1")throw new Exception("default terrain match rate");
  var funds=world.Funds.ToArray();AdvanceMatch(.1);if(Math.Abs(world.Time-.2)>.001||Math.Abs(DisplayBattleSeconds-.1)>.001)throw new Exception("native shared clock");
  paused=true;AdvanceMatch(5);if(Math.Abs(world.Time-.2)>.001)throw new Exception("paused match advanced");paused=false;AdvanceMatch(.1);if(Math.Abs(world.Time-.4)>.001)throw new Exception("pause catch-up burst");
  var own=world.Live(0).First();world.Deploy(own);for(int i=0;i<20;i++)world.Tick(.1f);world.Spawn("power",0,world.Home(0).X+100,world.Home(0).Y-60);var source=world.Spawn("factory",0,world.Home(0).X+100,world.Home(0).Y+120);world.Funds[0]=10000;world.Train(Campaign.Info("tank"),0,null);world.StartConstruction(Campaign.Info("radar"));
  var job=world.Construction(Campaign.Info("radar"));int remaining=(int)Math.Ceiling(job.Type.Seconds/2);if(world.ConstructionState(job,0,2)!=remaining+" 秒")throw new Exception("construction ETA rate");
  remaining=(int)Math.Ceiling(Campaign.Info("tank").Seconds/2);if(world.QueueState(source,2)!=remaining+" 秒")throw new Exception("unit ETA rate");
  paused=true;
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action draw=()=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));};draw();
   if(hits.Count(h=>h.Id!=null&&h.Id.StartsWith("match-speed-",StringComparison.Ordinal))!=4)throw new Exception("expected four match speed buttons");
   for(int multiplier=1;multiplier<=4;multiplier++){
    var box=hits.Single(h=>h.Id=="match-speed-"+multiplier);float work=job.Work;box.Run();
    if(speed!=2*multiplier||MatchSpeedLabel!="×"+multiplier||job.Work!=work||!paused)throw new Exception("speed button applied an incorrect rate or changed the paused match");
    draw();
   }
   if(folder!=null){Directory.CreateDirectory(folder);frame.Save(Path.Combine(folder,"对局速度设置.png"),ImageFormat.Png);}
  }
  paused=false;float beforeFastest=world.Time;AdvanceMatch(.05);
  if(Math.Abs(world.Time-beforeFastest-.4)>.001)throw new Exception("4x battle did not advance by eight game seconds per real second");
  StartMission(true,true);if(speed!=2||MatchSpeedLabel!="×1")throw new Exception("tutorial must start at the standard rate");
  StartIslandMission();if(speed!=2||matchRealTime!=0||simulation.Pending!=0)throw new Exception("new match clock reset");
  Console.WriteLine("Native pace: four menu rates, default 1x for all missions, pause, real-time match clock, rate-aware ETAs and restart reset passed.");
 }
}
}
