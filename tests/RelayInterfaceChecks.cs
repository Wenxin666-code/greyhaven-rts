using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class RelayInterfaceChecks {
 [STAThread]static int Main(string[] args){try{using(var f=new CommandWindow())f.CheckRelayInterface(args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {partial class CommandWindow {
 internal void CheckRelayInterface(string folder){
  Directory.CreateDirectory(folder);pulse.Stop();ClientSize=new Size(1920,1080);OpenMissions();
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action<string> capture=name=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,name+".png"),ImageFormat.Png);};
   Action<string> tap=id=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));var hit=hits.Last(h=>h.Id==id);var p=new PointF(hit.Box.X+hit.Box.Width/2,hit.Box.Y+hit.Box.Height/2);var ev=new MouseEventArgs(MouseButtons.Left,1,(int)(ox+p.X*scale),(int)(oy+p.Y*scale),0);OnMouseDown(ev);OnMouseUp(ev);};
   tap("04    雾脊中继站");capture("01-行动选择");tap("查看地图");if(!riverMap.IsRelay)throw new Exception("relay preview selection");RiverFit();capture("02-地图全览");
   CloseMapReview();tap("进入雾脊中继站");if(!world.RelayMission||briefing||RadarOnline()||selection.Count!=1||selection[0].T.Key!="mcv")throw new Exception("relay launch/selection/radar");capture("03-正式开局");
   var station=world.RelayStation;var before=world.Explored(0,station.P);capture("03b-目标提示");if(world.Explored(0,station.P)!=before)throw new Exception("marker revealed fog");
   var selected=selection[0];var homeCamera=camera;float initialZoom=zoom;var car=world.Live(0).First(u=>u.T.Key=="ifv");
   if(!world.Move(car,new PointF(car.P.X+4*Campaign.Tile,car.P.Y),false))throw new Exception("locator order fixture");
   var route=car.Path.ToArray();var goal=car.Goal;followUnit=selected;
   tap("定位中继站");capture("03c-情报坐标定位");
   if(followUnit!=null||Campaign.Dist(camera,station.P)>Campaign.Tile||zoom!=initialZoom||selection.Count!=1||selection[0]!=selected||car.Goal!=goal||!route.SequenceEqual(car.Path))throw new Exception("locator changed selection/orders/zoom or failed to focus");
   if(world.Explored(0,station.P)!=before||RadarOnline())throw new Exception("locator revealed terrain or enabled radar");
   tap("返回基地");if(Campaign.Dist(camera,homeCamera)>Campaign.Tile)throw new Exception("return home locator");
   OnPaint(new PaintEventArgs(g,ClientRectangle));OnPaint(new PaintEventArgs(g,ClientRectangle));
   if(hits.Count(h=>h.Id=="定位中继站")!=1||hits.Count(h=>h.Id=="relay-locator")!=1)throw new Exception("cached HUD duplicated or removed relay controls");
   tap("relay-locator");if(Campaign.Dist(camera,station.P)>Campaign.Tile)throw new Exception("edge marker is not clickable");
   tap("返回基地");
   world.Spawn("squad",0,station.P.X-6*Campaign.Tile,station.P.Y);world.Tick(.05f);CenterAt(station.P);capture("04-中继站");
   var engineer=world.Live(0).First(u=>u.T.Key=="engineer");selection.Clear();selection.Add(engineer);ContextOrderAt(Keys.None,station.P,station);
   if(engineer.Entering!=station)throw new Exception("normal context click must issue engineer capture");
   station.Side=0;world.Tick(.05f);station.Hp=station.T.Hp*.42f;selection.Clear();selection.Add(station);world.TickVisuals(.25f);capture("05-夺控与受损");
   world.PreviewRelaySupport();world.Radio="步枪兵已从兵营出列。";capture("05a-半程经济支援");
   if(!hits.Any(h=>h.Id=="relay-support-bulletin")||!world.RelaySupportGranted)throw new Exception("50% bulletin lost under production radio");
   world.Time+=31;capture("05b-支援记录保留");if(hits.Any(h=>h.Id=="relay-support-bulletin")||!world.RelaySupportResolved)throw new Exception("bulletin expiry or retained record");
   var basePoint=world.Home(0);world.Spawn("power",0,basePoint.X-4*Campaign.Tile,basePoint.Y);world.Spawn("radar",0,basePoint.X+5*Campaign.Tile,basePoint.Y);
   if(!RadarOnline())throw new Exception("radar fixture");capture("05c-雷达目标标记");
   var timings=new System.Collections.Generic.List<double>();
   for(int i=0;i<48;i++){world.TickVisuals(1f/30);var clock=System.Diagnostics.Stopwatch.StartNew();Pan(i<24?3:-3,1);hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));timings.Add(clock.Elapsed.TotalMilliseconds);}
   timings.Sort();if(riverGroundBytes>RiverTileBudget||buildingFrameBytes>BuildingFrameBudget)throw new Exception("relay rendering cache exceeded its bound");
   Console.WriteLine("Relay 1920x1080 pan with animated damaged station: median {0:F1}ms, p95 {1:F1}ms; terrain cache {2:F1}MiB, building cache {3:F1}MiB.",timings[24],timings[45],riverGroundBytes/1048576.0,buildingFrameBytes/1048576.0);
   for(int i=0;i<120;i++){world.TickVisuals(1f/30);hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));}
   timings.Clear();for(int i=0;i<48;i++){world.TickVisuals(1f/30);var clock=System.Diagnostics.Stopwatch.StartNew();Pan(i<24?3:-3,0);hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));timings.Add(clock.Elapsed.TotalMilliseconds);}
   timings.Sort();Console.WriteLine("Relay warmed animated pan: median {0:F1}ms, p95 {1:F1}ms.",timings[24],timings[45]);
   paused=true;capture("06-任务简报");paused=false;
   world.Won=true;capture("07-恢复完成");world.Won=false;station.Hp=0;world.Tick(.1f);world.TickVisuals(2.5f);capture("08-任务失败");
   world.River.Export(Path.Combine(folder,"mist-ridge-relay-v01.json"));
   StartIslandMission();if(world.FactionCount!=6||!world.IslandMission)throw new Exception("third entry regression");
   StartRiverMission();if(world.FactionCount!=3||world.RelayMission)throw new Exception("second entry regression");
   LaunchMission();if(!world.Tutorial)throw new Exception("training entry regression");
  }
  Console.WriteLine("Relay native UI: fog-safe target locator and return, retained commands, cached controls, 50% bulletin/record, radar marker, mission entry/outcomes and legacy launches passed.");
 }
}
public partial class Campaign {
 internal void PreviewRelaySupport(){RelayProgress=179.5f;Time+=.5f;TickRelayObjective(.5f);}
}}
