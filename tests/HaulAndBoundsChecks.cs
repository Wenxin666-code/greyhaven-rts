using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class HaulAndBoundsChecks {
 [STAThread]static int Main(string[] args){try{
  Campaign.CheckHaulDecisions();using(var f=new CommandWindow())f.CheckSeaAndRadar(args.Length>0?args[0]:null);
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void HaulAssert(bool ok,string message){if(!ok)throw new Exception(message);}
 static Campaign HaulWorld(){var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",1,3000,1800);for(int x=0;x<w.MapFogCols;x++)for(int y=0;y<w.MapFogRows;y++)w.discovered[0,x,y]=true;return w;}
 internal void HaulReveal(){Reveal();}
 internal static void CheckHaulDecisions(){
  HaulAssert(Math.Abs(Info("miner").Speed/Info("tank").Speed-4f/6)<.0001,"miner/tank relative speed");
  var w=HaulWorld();var truck=w.Spawn("miner",0,450,600);
  for(int y=156;y<=1212;y+=24)w.Spawn("post",-1,612,y);
  for(int i=0;i<6;i++)w.Ore.Add(new OrePatch(690,576+i*24,480));
  var nearby=new OrePatch(450,930,480);w.Ore.Add(nearby);var timer=Stopwatch.StartNew();
  bool oreChosen=w.ChooseOre(truck);Console.WriteLine("Ore fixture choice "+oreChosen+" / "+(truck.Patch==null?"none":truck.Patch.P.ToString()));
  HaulAssert(oreChosen&&truck.Patch==nearby,"shorter legal ore route must beat six straight-line-near detours");
  Console.WriteLine("Ore route: same-bank vein selected beyond six detour candidates, "+timer.ElapsedMilliseconds+"ms.");
  w=HaulWorld();var plant=w.Spawn("refinery",0,600,650);plant.StarterDelivered=true;var gate=DockPoint(plant);
  truck=w.Spawn("miner",0,gate.X,gate.Y+360);truck.Cargo=LoadValue;truck.Returning=true;w.RunMiner(truck,.1f);
  HaulAssert(truck.Dock==plant&&truck.Path.Count>0&&Dist(truck.Path.Last(),gate)<.01,"free refinery must be approached directly, without a side queue detour");
  HaulAssert(!truck.DockReserved,"a distant truck cannot reserve the service bay");
  w=HaulWorld();var sealedPlant=w.Spawn("refinery",0,600,650);sealedPlant.StarterDelivered=true;gate=DockPoint(sealedPlant);
  var ring=new PointF(((int)(gate.X/Tile)+.5f)*Tile,((int)(gate.Y/Tile)+.5f)*Tile);
  for(int a=-48;a<=48;a+=24)for(int b=-48;b<=48;b+=24)if(Math.Abs(a)==48||Math.Abs(b)==48)w.Spawn("post",-1,ring.X+a,ring.Y+b);
  var reachable=w.Spawn("refinery",0,1100,900);reachable.StarterDelivered=true;
  truck=w.Spawn("miner",0,gate.X,gate.Y+190);truck.Cargo=LoadValue;truck.Returning=true;w.RunMiner(truck,.1f);
  HaulAssert(truck.Dock==reachable&&truck.Path.Count>0,"skip the nearest refinery when its actual unloading bay is unreachable");
  Console.WriteLine("Refinery selection: direct free-bay approach, no distant reservation and statically sealed-bay rejection passed.");
  foreach(bool sealedVein in new[]{false,true}){
   w=HaulWorld();var vein=new OrePatch(660,900,480);w.Ore.Add(vein);
   var busy=w.Spawn("miner",0,660,900);busy.Patch=vein;
   truck=w.Spawn("miner",0,sealedVein?450:630,900);
   if(sealedVein)for(int a=-48;a<=48;a+=24)for(int b=-48;b<=48;b+=24)if(Math.Abs(a)==48||Math.Abs(b)==48)w.Spawn("post",-1,660+a,900+b);
   w.RunMiner(truck,.1f);
   HaulAssert(sealedVein?truck.Dispatch>=3.9f:truck.Dispatch>0&&truck.Dispatch<1,"busy waiting must distinguish temporary occupancy from an unreachable field");
  }
  Console.WriteLine("Busy ore: reachable temporary occupancy waits; statically sealed ore does not stall unrelated miners.");
  w=HaulWorld();var miner=w.Spawn("miner",0,1000,1400);var tank=w.Spawn("tank",0,1000,1520);
  HaulAssert(w.Move(miner,new PointF(2500,1400),false)&&w.Move(tank,new PointF(2500,1520),false),"speed comparison routes");
  for(int i=0;i<40;i++)w.Tick(.05f);var mp=miner.P;var tp=tank.P;
  for(int i=0;i<100;i++)w.Tick(.05f);
  float md=Dist(mp,miner.P),td=Dist(tp,tank.P);HaulAssert(Math.Abs(md/td-4f/6)<.03f,"actual steady travel speed ratio");
  Console.WriteLine("Five-second steady movement: miner "+md.ToString("F1")+", tank "+td.ToString("F1")+" world units.");
  foreach(bool terrain in new[]{false,true}){
   w=new Campaign(true,false,terrain);w.Units.Clear();w.Spawn("squad",0,12,500);w.Reveal();
   var sea=new PointF(-36,500);HaulAssert(w.Battlefield.Contains(sea)&&w.Explored(0,sea),"coastal sight must reveal extended negative-coordinate terrain");
   HaulAssert(!w.Explored(1,sea),"coastal fog belongs to its own faction");
   HaulAssert(!w.Explored(0,new PointF(-w.MapHeight,-w.MapWidth)),"off-map corners must remain outside the battlefield");
  }
  Console.WriteLine("Rectangular battlefield: procedural sea, shifted fog, coastal reveal and faction isolation passed.");
 }
}
partial class CommandWindow {
 internal void CheckSeaAndRadar(string folder){
  CheckCoastalFogPixels();
  ClientSize=new Size(1920,1080);StartMission(true,true);world.Units.Clear();world.Ore.Clear();selection.Clear();
  world.Spawn("mcv",1,3000,1800);world.Spawn("hq",0,500,650);for(int i=0;i<4;i++)world.Spawn("power",0,700+i*85,650);
  world.Spawn("radar",0,500,800);world.Spawn("satellite",0,650,800);world.HaulReveal();
  var tank=world.Spawn("tank",0,600,1100);selection.Add(tank);CenterAt(tank.P);
  var first=new PointF(1100,1250);var next=new PointF(1200,1450);var oldCamera=camera;
  RadarAction(first,false);RadarAction(next,false);
  if(!selection.Contains(tank)||Campaign.Dist(tank.Goal,next)>Campaign.Tile||camera!=oldCamera)throw new Exception("repeat radar click must update the selected unit's destination");
  var water=new PointF(32*Campaign.Tile,24*Campaign.Tile);var goal=tank.Goal;
  if(!world.WaterAt(water))throw new Exception("lake fixture is not water");
  RadarAction(water,false);if(tank.Goal!=goal||!selection.Contains(tank))throw new Exception("sea command must preserve the existing valid order");
  RadarAction(new PointF(water.X+Campaign.Tile,water.Y),false);if(tank.Goal!=goal||!selection.Contains(tank))throw new Exception("second water command must preserve the existing valid order");
  RadarAction(water,true);if(Campaign.Dist(camera,water)>1||tank.Goal!=goal||!selection.Contains(tank))throw new Exception("right radar sea focus must preserve selection and order");
  RefreshProjectedRadar();foreach(var p in new[]{new Point(2,2),new Point(145,2),new Point(2,97),new Point(145,97)})
   if(projectedRadar.GetPixel(p.X,p.Y).ToArgb()==Color.FromArgb(16,27,32).ToArgb())throw new Exception("satellite radar has an unfilled corner");
  foreach(var p in new[]{new PointF(-10000,-10000),new PointF(10000,-10000),new PointF(-10000,10000),new PointF(10000,10000)}){
   CenterAt(p);var corners=new[]{new PointF(0,0),new PointF(MW,0),new PointF(0,MH),new PointF(MW,MH)};
   foreach(var s in corners){var q=WorldOf(s);float u=q.X-q.Y,v=q.X+q.Y;if(u< -world.MapHeight-1||u>world.MapWidth+1||v< -1||v>world.MapWidth+world.MapHeight+1)throw new Exception("camera exposes outside the rectangular battlefield");}
  }
  Console.WriteLine("Native radar: repeated destination, invalid sea preservation, independent camera focus, four filled radar corners and four viewport-edge clamps passed.");
  if(folder==null)return;Directory.CreateDirectory(folder);
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action<string> capture=name=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,name+".png"),ImageFormat.Png);};
   CenterAt(water);capture("海域边界与雷达");
   for(int level=0;level<3;level++){
    OpenMissions();riverMissionSelected=level>0;islandMissionSelected=level==2;capture("矩形选关-0"+(level+1));
    if(level>0){OpenMapReview();capture("矩形地图-0"+(level+1));}
   }
  }
 }
 void CheckCoastalFogPixels(){
  StartMission(true,true);world.Units.Clear();world.Spawn("squad",0,12,500);world.HaulReveal();
  foreach(float z in new[]{MinBattleZoom,DefaultZoom,MaxBattleZoom})foreach(var point in new[]{new PointF(-60,520),new PointF(600,600)}){
   zoom=z;CenterAt(point);var full=new System.Collections.Generic.List<RectangleF>();var previous=new System.Collections.Generic.Dictionary<int,int>();
   for(int y=0;y<world.MapFogRows;y++){
    var next=new System.Collections.Generic.Dictionary<int,int>();
    for(int x=0;x<world.MapFogCols;){
     if(world.Seen(0,x,y)){x++;continue;}int begin=x++;while(x<world.MapFogCols&&!world.Seen(0,x,y))x++;
     int key=begin*(world.MapFogCols+1)+x,index;
     if(previous.TryGetValue(key,out index)){var r=full[index];r.Height+=Campaign.FogSize;full[index]=r;}
     else{index=full.Count;full.Add(new RectangleF(world.FogOriginX+begin*Campaign.FogSize,world.FogOriginY+y*Campaign.FogSize,(x-begin)*Campaign.FogSize+.45f,Campaign.FogSize+.45f));}
     next[key]=index;
    }previous=next;
   }
   using(var actual=new Bitmap(MW,MH))using(var expected=new Bitmap(MW,MH)){
    for(int pass=0;pass<2;pass++)using(var g=Graphics.FromImage(pass==0?actual:expected)){
     g.Clear(Color.Magenta);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;GroundSpace(g);
     if(pass==0)Shroud(g);else using(var brush=new SolidBrush(C("#152323")))g.FillRectangles(brush,full.ToArray());
    }
    for(int y=1;y<MH-1;y+=3)for(int x=1;x<MW-1;x+=3)if(actual.GetPixel(x,y)!=expected.GetPixel(x,y)){
     // The original .45-world-unit seam overlap rasterizes by up to one
     // device pixel when clipped runs merge. Interior coverage must be exact.
     bool edge=false;for(int yy=y-1;yy<=y+1;yy++)for(int xx=x-1;xx<=x+1;xx++)
      if(expected.GetPixel(xx,yy)==actual.GetPixel(x,y))edge=true;
     if(!edge)throw new Exception("viewport fog interior differs from full-grid reference at "+x+","+y+", zoom "+z);
    }
   }
  }
  Console.WriteLine("Coastal fog: full-grid reference matches viewport-cached coverage at three zooms and two camera positions; native seam raster edges tolerated within one device pixel.");
 }
}
}
