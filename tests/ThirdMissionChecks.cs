using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class ThirdMissionChecks {
 [STAThread]static int Main(string[] args){try{
  Campaign.CheckThirdTerrain();Campaign.CheckNavigationAcceleration();Campaign.CheckThirdOpening();Campaign.CheckSmartAiRules();
  using(var f=new CommandWindow())f.CheckThirdInterface(args.Length>0?args[0]:null);
  Campaign.CheckAiDevelopment(false,900);Campaign.CheckAiDevelopment(true,900);
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void ThirdAssert(bool ok,string message){if(!ok)throw new Exception(message);}
 internal static void CheckThirdTerrain(){
  var m=new RiverbendMap(true);ThirdAssert(m.WidthCells==300&&m.HeightCells==200&&m.Starts.Length==6,"third map dimensions/factions");
  ThirdAssert(m.Cells.Length>new RiverbendMap().Cells.Length*1.8,"larger map");
  for(int side=1;side<6;side++){
   ThirdAssert(m.Connected(m.Starts[0],m.Starts[side]),"disconnected faction "+side);
   ThirdAssert(m.Starts.Skip(1).All(p=>p.X>=112||p.Y>=94),"enemies must be outside resource island");
   for(int other=side+1;other<6;other++)ThirdAssert(Dist(new PointF(m.Starts[side].X,m.Starts[side].Y),new PointF(m.Starts[other].X,m.Starts[other].Y))>60,"crowded enemy starts");
  }
  int resources=m.Ore.Count(p=>p.X<90&&p.Y<90);ThirdAssert(resources>=170,"island resource reserve too small: "+resources);
  foreach(var h in m.Houses){ThirdAssert(m.Walkable(h.Door.X,h.Door.Y)&&m.Connected(m.Starts[0],h.Door),"house door unreachable: "+h.Name+" "+h.Door);}
  foreach(var surface in new[]{RiverbendMap.Surface.Water,RiverbendMap.Surface.Marsh,RiverbendMap.Surface.Ramp,RiverbendMap.Surface.Rock,RiverbendMap.Surface.Bridge})ThirdAssert(m.Cells.Any(c=>c.Ground==surface),"missing diverse terrain "+surface);
  var gate=m.IslandGate;for(int y=gate.Top;y<gate.Bottom;y++)for(int x=gate.Left;x<gate.Right;x++)m.At(x,y).Ground=RiverbendMap.Surface.Water;
  for(int side=1;side<6;side++)ThirdAssert(!m.Connected(m.Starts[0],m.Starts[side]),"hidden second island exit "+side);
  Console.WriteLine("Third terrain: 300x200, six separated starts, all 33 house doors, multiple biomes and a single 6-cell-wide island gate passed; island ore cells="+resources);
 }
 internal static void CheckNavigationAcceleration(){
  var w=new Campaign(false,false,true,true);var rng=new Random(5729);int checkedEdges=0;
  foreach(float pad in new[]{5f,5.25f,8.8f}){
   var grid=w.Grid(pad);w.OpenGrid(grid,pad);
   for(int sample=0;sample<1200;sample++){
    int x=rng.Next(1,w.N-1),y=rng.Next(1,w.M-1),index=y*w.N+x;
    bool expected=w.TerrainOpen(w.Center(index),pad)&&!grid.Barriers.Any(r=>r.Contains(w.Center(index)));
    ThirdAssert(grid.Open[index]==expected,"rasterized obstacle/terrain mismatch");
    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
     if(dx==0&&dy==0)continue;int next=index+dy*w.N+dx;
     ThirdAssert(w.NavigationLink(grid,index,next,pad)==w.Clear(w.Center(index),w.Center(next),grid.Barriers,pad),"memoized swept connection mismatch");checkedEdges++;
    }
   }
  }
  var old=w.Grid(8.8f);var obstacle=w.Spawn("factory",0,850,650);var fresh=w.Grid(8.8f);
  ThirdAssert(!Object.ReferenceEquals(old,fresh),"building must invalidate obstacle grid");w.OpenGrid(fresh,8.8f);
  ThirdAssert(fresh.Open.Where((value,i)=>value&&Footprint(obstacle.T,obstacle.P).Contains(w.Center(i))).Count()==0,"new building left stale open cells");
  Console.WriteLine("Navigation acceleration: "+checkedEdges+" exact swept connections, three footprints, rasterized obstacles and building invalidation passed.");
 }
 internal static void CheckThirdOpening(){
  var w=new Campaign(true,false,true,true);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",1,7600,4600);
  var home=w.Home(0);var units=new[]{w.Spawn("squad",0,home.X,home.Y),w.Spawn("tank",0,home.X+24,home.Y),w.Spawn("miner",0,home.X+48,home.Y)};
  for(int i=0;i<units.Length;i++){
   var point=new PointF(126*Tile,50*Tile+i*24);var path=units[i].Path.ToArray();int population=w.Units.Count;
   ThirdAssert(w.CanMoveTo(units[i],point)&&w.CanMoveTo(units[i],point),"cached third-mission movement preview");
   ThirdAssert(w.Units.Count==population&&units[i].Path.SequenceEqual(path),"hover query changed unit orders or population");
   ThirdAssert(!w.CanMoveTo(units[i],new PointF(90*Tile,35*Tile)),"sea must show unreachable");
   ThirdAssert(w.Move(units[i],point,false),"island gate route for "+units[i].T.Key);
  }
  for(int tick=0;tick<7200;tick++)w.Tick(.05f);
  ThirdAssert(units.All(p=>Dist(p.P,p.Goal)<12),"a real unit failed to leave the island via the gate");
  w=new Campaign(true,false,true,true);w.Funds[0]=ReferenceBalance.StartingCredits;var mcv=w.Live(0).First(p=>p.T.Key=="mcv");ThirdAssert(w.Deploy(mcv),"third opening deployment");for(int i=0;i<40;i++)w.Tick(.1f);
  foreach(string key in new[]{"power","refinery"}){
   var type=Info(key);PointF site;ThirdAssert(w.AiBuildSite(0,type,w.Home(0),out site),"player has room for "+key);
   ThirdAssert(w.StartConstruction(type),"player construction "+key);for(int i=0;i<(int)Math.Ceiling(type.Seconds*10)+20;i++)w.Tick(.1f);
   ThirdAssert(w.PlaceConstruction(type,site,0),"player placement "+key);for(int i=0;i<15;i++)w.Tick(.1f);
  }
  for(int i=0;i<2400&&w.OreReceipts[0]==0;i++)w.Tick(.1f);
  ThirdAssert(w.OreReceipts[0]>0&&w.Live(0).Any(p=>p.T.Key=="miner"),"resource island must support a real paid player economy");
  ThirdAssert(w.Funds[0]==ReferenceBalance.StartingCredits-Info("power").Price-Info("refinery").Price+w.OreReceipts[0],"normal starting funds and player income conservation");
  Console.WriteLine("Third opening: real infantry, tank and miner crossed sole gate; player deploy, paid construction and first ore income passed.");
 }
 internal static void CheckSmartAiRules(){
  var w=new Campaign(false,false,true,true);ThirdAssert(w.FactionCount==6,"six-faction simulation");
  for(int s=0;s<6;s++)ThirdAssert(w.Funds[s]==ReferenceBalance.StartingCredits&&w.Live(s).Count()==4&&w.Explored(s,w.Home(s)),"fair start, fog and funds "+s);
  foreach(var u in w.Units.Where(p=>p.Side==1||p.Side==2).ToArray())u.Hp=0;
  w.CheckEnemySurrender(4);ThirdAssert(!w.Won&&w.Remaining(5)>0,"fifth enemy must prevent premature victory");
  w=new Campaign(false,false,true,true);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",0,900,700);w.Spawn("mcv",5,7600,4500);
  var house=w.Spawn("residence",-1,2000,2000);var dead=w.Spawn("power",3,6000,2500);w.CheckEnemySurrender(3.1f);
  ThirdAssert(w.HasSurrendered(3)&&!dead.Alive&&!w.HasSurrendered(5)&&house.Side==-1,"independent fifth-faction surrender");
  w=new Campaign(false,false,true,true);w.Units.Clear();w.Ore.Clear();w.Funds[4]=5000;var home=w.Home(4);
  w.Spawn("hq",4,home.X,home.Y);w.Spawn("power",4,home.X+100,home.Y-60);w.Spawn("factory",4,home.X+100,home.Y+120);w.Spawn("barracks",4,home.X-100,home.Y+100);
  for(int i=0;i<4;i++)w.Spawn("miner",4,home.X+140+i*30,home.Y+200).Mining=false;
  var brain=w.Brain(4);w.AiRecruit(4,brain,new Piece[0]);
  ThirdAssert(w.Live(4).Where(p=>p.T.Key=="barracks"||p.T.Key=="factory").All(p=>p.Queue.Count>0),"parallel infantry and vehicle production decisions");
  var defender=w.Spawn("tank",4,home.X-50,home.Y+40);var intruder=w.Spawn("tank",2,home.X-100,home.Y+40);w.Reveal();w.SmartTactics(4,false);
  ThirdAssert(defender.Target==intruder,"immediate base defense");w.RememberAiAttack(defender,intruder);ThirdAssert(w.Brain(4).Attacks[2]==w.Time,"attacker memory");
  var secrets=w.Spawn("tank",1,500,300);ThirdAssert(!w.AiKnown(4).Contains(secrets),"AI must not inspect unseen military units");
  Console.WriteLine("Six-faction funds/fog/surrender, fair starts, concurrent recruitment, threat memory, immediate base defense and hidden-unit isolation passed.");
 }
 internal static void CheckAiDevelopment(bool island,int seconds){
  var w=new Campaign(false,false,true,island);var watch=Stopwatch.StartNew();double maxStep=0;var income=new bool[MaxFactions];var developed=new bool[MaxFactions];var minerPeak=new int[MaxFactions];
  for(int tick=0;tick<seconds*10&&!w.Lost&&!w.Won;tick++){
   var step=Stopwatch.StartNew();w.Tick(.1f);maxStep=Math.Max(maxStep,step.Elapsed.TotalMilliseconds);
   for(int side=1;side<w.FactionCount;side++){income[side]|=w.OreReceipts[side]>0;developed[side]|=w.Live(side).Any(p=>p.T.Key=="factory");minerPeak[side]=Math.Max(minerPeak[side],w.AiCount(side,"miner"));}
   if(tick==599)for(int debugSide=1;debugSide<w.FactionCount;debugSide++){
    foreach(var plant in w.Live(debugSide).Where(p=>p.T.Key=="refinery")){var exit=Middle(ExitApron(plant.T,Footprint(plant.T,plant.P)));Console.WriteLine("Bootstrap "+debugSide+" refinery="+plant.P+" exit="+exit+" clear="+w.ExitClear(plant,Info("miner"),exit)+" starter="+plant.StarterDelivered);}
   }
   if(tick%600==599){Console.WriteLine((island?"Island":"River")+" at "+(int)w.Time+"s: "+String.Join(" | ",Enumerable.Range(1,w.FactionCount-1).Select(w.AiSummary)));}
  }
  for(int side=1;side<w.FactionCount;side++){
   ThirdAssert(income[side]&&developed[side],"AI failed to bootstrap: "+w.AiSummary(side));
   ThirdAssert(minerPeak[side]>=2,"AI did not expand mining: "+w.AiSummary(side));
   ThirdAssert(w.Brain(side).Orders>=8&&w.Brain(side).Launches>0,"no sustained production or squad attack: "+w.AiSummary(side));
  }
  ThirdAssert(w.Funds.All(n=>n>=0),"AI creates debt");
  Console.WriteLine((island?"Third":"Second")+" mission AI development passed; simulation seconds="+(int)w.Time+", wall ms="+watch.ElapsedMilliseconds+", max single step ms="+maxStep.ToString("F1"));
 }
}
partial class CommandWindow {
 internal void CheckThirdInterface(string folder){
  if(folder!=null)Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);OpenMissions();riverMissionSelected=false;islandMissionSelected=false;
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action<string> capture=name=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));if(folder!=null)frame.Save(Path.Combine(folder,name),ImageFormat.Png);};
   Action<string> tap=id=>{
    hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));var hit=hits.Last(control=>control.Id==id);var p=new PointF(hit.Box.X+hit.Box.Width/2,hit.Box.Y+hit.Box.Height/2);
    var ev=new MouseEventArgs(MouseButtons.Left,1,(int)(ox+p.X*scale),(int)(oy+p.Y*scale),0);OnMouseDown(ev);OnMouseUp(ev);
   };
   tap("03    孤岛要塞");if(!islandMissionSelected)throw new Exception("third native selection button");
   capture("孤岛要塞-选关.png");tap("查看地图");if(!mapReview||!riverMap.IsIsland)throw new Exception("third native map-preview button");RiverFit();capture("孤岛要塞-全图.png");
   RiverFocus(64,42,.55f);capture("孤岛要塞-资源岛与堤桥.png");CloseMapReview();tap("进入孤岛要塞");
   if(!world.IslandMission||world.FactionCount!=6||briefing||zoom<MinBattleZoom||zoom>MaxBattleZoom)throw new Exception("native third launch and tactical zoom");
   if(world.Live(0).Count()!=4||!selection.Contains(world.Units[0]))throw new Exception("native third spawn/selection");
   capture("孤岛要塞-正式开局.png");
   var home=world.Home(0);world.Spawn("power",0,home.X+90,home.Y);world.Spawn("radar",0,home.X+90,home.Y+70);world.Spawn("satellite",0,home.X-90,home.Y);
   world.Spawn("power",0,home.X+90,home.Y-70);world.Tick(.1f);capture("孤岛要塞-六方雷达.png");
   if(!RadarOnline()||!world.SatelliteOnline(0))throw new Exception("third radar/satellite");
   for(int side=0;side<6;side++)if(!world.Explored(0,world.Home(side)))throw new Exception("satellite misses faction "+side);
   if(Enumerable.Range(0,6).Select(s=>UnitColor(s).ToArgb()).Distinct().Count()!=6)throw new Exception("six faction colors");
   var timings=new System.Collections.Generic.List<double>();for(int i=0;i<48;i++){
    var clock=Stopwatch.StartNew();Pan(4,2);hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));timings.Add(clock.Elapsed.TotalMilliseconds);
   }
   timings.Sort();if(riverGroundBytes>RiverTileBudget)throw new Exception("third terrain cache exceeds memory bound");
   Console.WriteLine("Third native 1920x1080 pan: median {0:F1}ms, p95 {1:F1}ms; bounded visible terrain cache {2:F1}MiB.",timings[24],timings[45],riverGroundBytes/1048576.0);
   if(folder!=null)world.River.Export(Path.Combine(Path.GetDirectoryName(folder),"maps","island-fortress-v02.json"));
   StartRiverMission();if(world.IslandMission||world.FactionCount!=3)throw new Exception("second mission changed shape");
   LaunchMission();if(!world.Tutorial||world.FactionCount!=2)throw new Exception("training changed");
  }
  Console.WriteLine("Native mission picker, third preview/play, six colors, radar/satellite and second/training entry passed.");
 }
}
}
