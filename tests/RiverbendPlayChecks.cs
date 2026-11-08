using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
public partial class Campaign {
 internal void CheckRiverRules(){
  if(TerrainLine(new PointF(4650.171f,2911.066f),new PointF(4522.5f,2557.5f),6.5f))throw new Exception("long route misses a thin terrain-corner collision");
  for(int side=0;side<3;side++){
   var start=Live(side).First(u=>u.T.Key=="mcv");if(!CanPlace(Info("hq"),start.P,start,side))throw new Exception("spawn cannot deploy "+side);
   for(int other=side+1;other<3;other++)if(Dist(Home(side),Home(other))<100*Tile)throw new Exception("starts too close");
   if(Ore.Min(p=>Dist(Home(side),p.P))>12*Tile)throw new Exception("opening mine too far "+side);
  }
  var scout=Live(0).First(u=>u.T.Foot);var timer=System.Diagnostics.Stopwatch.StartNew();
  if(!Route(scout,Home(1)))throw new Exception("no cross-river path");
  var prev=scout.P;foreach(var p in scout.Path){if(!TerrainLine(prev,p,Clearance(scout)))throw new Exception("path footprint crosses water/cliff");prev=p;}
  Console.WriteLine("Cross-map route: {0} legs, {1} ms",scout.Path.Count,timer.ElapsedMilliseconds);Halt(scout);
  foreach(int side in new[]{0,2}){var vehicle=Live(side).First(u=>u.T.Key=="mcv");if(!Route(vehicle,Home(1)))throw new Exception("vehicle cannot reach other spawn");Halt(vehicle);}
  var house=Units.First(u=>u.T.Key=="residence");var engineer=Live(0).First(u=>u.T.Key=="engineer");
  engineer.P=Middle(ExitApron(house.T,Footprint(house.T,house.P)));Reveal();
  if(!EnterHouse(engineer,house))throw new Exception("engineer cannot enter neutral house");
  for(int i=0;i<40;i++)UnitTick(engineer,.1f,true);
  if(house.Side!=0||engineer.Carrier!=null||engineer.Alive)throw new Exception("capture must recolor and consume engineer");
  for(int i=0;i<HouseCapacity(house);i++){
   var p=Middle(ExitApron(house.T,Footprint(house.T,house.P)));var foot=Spawn("squad",0,p.X,p.Y);
   if(!EnterHouse(foot,house))throw new Exception("infantry cannot garrison");UnitTick(foot,.1f,true);
  }
  if(HouseCount(house)!=4||CanEnterHouse(scout,house))throw new Exception("garrison capacity not enforced");
  var foe=Spawn("tank",2,house.P.X+100,house.P.Y+100);Reveal();float hp=foe.Hp;
  foreach(var unit in Units.Where(u=>u.Carrier==house).ToArray())UnitTick(unit,2,true);
  if(foe.Hp>=hp||!Shots.Any(s=>s.Garrison))throw new Exception("garrison did not attack third faction");
  if(foe.Target!=house)throw new Exception("return fire must target house, not hidden occupant");
  var occupants=Units.Where(u=>u.Carrier==house).ToArray();Kill(house);
  if(occupants.Any(u=>!u.Alive||u.Carrier!=null||Blocked(u.P,Clearance(u),null)))throw new Exception("destroyed house lost/trapped survivors");
  foreach(var u in Units.Where(u=>u.Side==1).ToArray())Kill(u);Tick(.1f);if(Won)throw new Exception("victory ignored faction 2");
  foreach(var u in Units.Where(u=>u.Side==2).ToArray())Kill(u);Tick(.1f);if(!Won)throw new Exception("elimination did not win");
  Console.WriteLine("3 valid separated starts; engineer capture, capacity, third-faction fire and evacuation passed.");
 }
 internal void RevealReview(){var sides=Units.Select(u=>u.Side).ToArray();for(int i=0;i<Units.Count;i++)Units[i].Side=0;Reveal();for(int i=0;i<Units.Count;i++)Units[i].Side=sides[i];}
 internal void PrepareGarrisonReview(){
  var house=Units.First(u=>u.T.Key=="municipal");house.Side=0;
  for(int i=0;i<4;i++){var u=Spawn("squad",0,house.P.X,house.P.Y);u.Carrier=house;}
  RevealReview();
 }
}
partial class CommandWindow {
 internal void CheckRiverPlayable(string folder){
  new Campaign(false,false,true).CheckRiverRules();
  StartRiverMission();if(briefing||world.River==null||world.Tutorial)throw new Exception("playable river mission launch");
  var watch=System.Diagnostics.Stopwatch.StartNew();
  // Let each independent AI build and earn money while the player remains idle.
  var captured=new bool[3];
  for(int i=0;i<1800&&!world.Lost;i++){
   world.Tick(.1f);
   // Occupied houses can be recaptured or destroyed later in the battle.
   for(int side=1;side<3;side++)captured[side]|=world.Live(side).Any(u=>u.T.Civilian);
  }
  for(int side=1;side<3;side++){
   if(!world.Live(side).Any(u=>u.T.Key=="refinery")||world.OreReceipts[side]==0||!world.Live(side).Any(u=>u.T.Key=="factory"))throw new Exception("AI economy stalled side "+side);
   if(!captured[side])throw new Exception("AI did not capture a house side "+side);
  }
  Console.WriteLine("180 simulated seconds: {0} ms; enemy ore receipts {1}/{2}.",watch.ElapsedMilliseconds,world.OreReceipts[1],world.OreReceipts[2]);
  StartRiverMission();ClientSize=new Size(1920,1080);
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"河湾镇-正式开局.png"),ImageFormat.Png);
   var pixel=new Point((int)(ox+600*scale),(int)(oy+400*scale));
   OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,pixel.X,pixel.Y,-12000));if(zoom!=MinBattleZoom)throw new Exception("minimum tactical zoom");
   OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,pixel.X,pixel.Y,12000));if(zoom!=MaxBattleZoom)throw new Exception("maximum tactical zoom");zoom=DefaultZoom;
   foreach(var p in new[]{new PointF(62.5f*24,31.5f*24),new PointF(175.5f*24,75.5f*24),world.Home(0)})if(Campaign.Dist(p,WorldOf(ScreenOf(p)))>1)throw new Exception("elevated ground picking mismatch");
   world.PrepareGarrisonReview();var house=world.Units.First(u=>u.T.Key=="municipal");selection.Clear();selection.Add(house);CenterAt(house.P);
   var home=world.Home(0);world.Spawn("power",0,home.X+120,home.Y);world.Spawn("radar",0,home.X-120,home.Y);world.RevealReview();
   foreach(var p in new[]{home,world.Home(1),world.Home(2),house.P})if(Campaign.Dist(p,RadarWorldPoint(RadarPoint(p)))>.01f)throw new Exception("radar inverse mismatch");
   hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));if(!RadarOnline()||!hits.Any(h=>h.Id=="radar-map"))throw new Exception("river radar unavailable");
   var radar=hits.Last(h=>h.Id=="radar-map");var pin=RadarPoint(world.Home(2));mouse=new PointF(radar.Box.Left+pin.X,radar.Box.Top+pin.Y);radar.Right();if(Campaign.Dist(camera,world.Home(2))>.01f||!selection.Contains(house))throw new Exception("radar right focus with selection failed");CenterAt(house.P);mouse=PointF.Empty;
   OnPaint(new PaintEventArgs(g,ClientRectangle));
   frame.Save(Path.Combine(folder,"河湾镇-驻防与雷达.png"),ImageFormat.Png);
   var samples=new System.Collections.Generic.List<double>();
   for(int i=0;i<50;i++){watch.Restart();Pan(3,1);OnPaint(new PaintEventArgs(g,ClientRectangle));samples.Add(watch.Elapsed.TotalMilliseconds);}samples.Sort();
   Console.WriteLine("River battle pan median {0:F1} ms p95 {1:F1} ms; tactical zoom {2}–{3}; projected radar roundtrip passed.",samples[25],samples[47],MinBattleZoom,MaxBattleZoom);
   paused=true;hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));hits.Last(h=>h.Id=="重开本关").Run();if(world.River==null||world.Time!=0||briefing)throw new Exception("restart changed mission");
   OpenMissions();OnPaint(new PaintEventArgs(g,ClientRectangle));hits.Last(h=>h.Id=="02    河湾镇").Run();OnPaint(new PaintEventArgs(g,ClientRectangle));hits.Last(h=>h.Id=="进入河湾镇").Run();if(world.River==null||briefing)throw new Exception("river menu launch");
  }
 }
}
}
