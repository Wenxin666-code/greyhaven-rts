using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class CoastPlacementChecks {
 [STAThread]static int Main(string[] args){try{
  Campaign.CheckCoastAndPlacement();
  if(args.Length>0)using(var window=new CommandWindow())window.CheckCoastPictures(args[0]);
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 internal void CoastRevealAll(){for(int x=0;x<MapFogCols;x++)for(int y=0;y<MapFogRows;y++)discovered[0,x,y]=true;}
 static void CoastAssert(bool ok,string text){if(!ok)throw new Exception(text);}
 static HashSet<Point> ReachableTerrain(RiverbendMap map,Point start){
  var seen=new HashSet<Point>();var queue=new Queue<Point>();seen.Add(start);queue.Enqueue(start);
  var directions=new[]{new Point(1,0),new Point(-1,0),new Point(0,1),new Point(0,-1)};
  while(queue.Count>0){var p=queue.Dequeue();foreach(var d in directions){var n=new Point(p.X+d.X,p.Y+d.Y);if(!seen.Contains(n)&&map.CanStep(p,n)){seen.Add(n);queue.Enqueue(n);}}}
  return seen;
 }
 internal static void CheckCoastAndPlacement(){
  foreach(bool island in new[]{false,true}){
   var m=new RiverbendMap(island);var reached=ReachableTerrain(m,m.Starts[0]);
   foreach(var p in m.Starts)CoastAssert(reached.Contains(p),"Disconnected start "+p);
   foreach(var h in m.Houses)CoastAssert(reached.Contains(h.Door),"Disconnected house "+h.Name+" "+h.Door);
   int shore=0,outerLand=0,outerWater=0;
   var area=m.TerrainBounds;
   for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++){
    var c=m.At(x,y);if(c==null)continue;
    if(c.Ground==RiverbendMap.Surface.Sand)shore++;
    if(x<0||y<0||x>=m.WidthCells||y>=m.HeightCells){if(c.Ground==RiverbendMap.Surface.Water)outerWater++;else if(m.Walkable(x,y))outerLand++;}
   }
   CoastAssert(shore>400&&outerLand>1000&&outerWater>1000,"Radar domain must contain actual extended terrain and continuous coast");
   int stranded=m.Ore.Count(p=>!reached.Contains(p));CoastAssert(stranded==0,"Unreachable ore cells "+stranded);
   foreach(var b in m.Bridges)for(int y=b.Top;y<b.Bottom;y++)for(int x=b.Left;x<b.Right;x++)m.At(x,y).Ground=RiverbendMap.Surface.Water;
   reached=ReachableTerrain(m,m.Starts[0]);
   if(island)CoastAssert(m.Starts.Skip(1).All(p=>!reached.Contains(p)),"Extended terrain bypasses sole island gate");
   else CoastAssert(!reached.Contains(m.EastStart),"Extended terrain bypasses river crossings");
   Console.WriteLine((island?"Island":"River")+": natural shore="+shore+", extended land="+outerLand+", extended water="+outerWater+"; starts, houses, ore and bridge isolation passed.");
  }
  var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();for(int x=0;x<w.MapFogCols;x++)for(int y=0;y<w.MapFogRows;y++)w.discovered[0,x,y]=true;
  var hq=w.Spawn("hq",0,480,600);var type=Info("power");var point=new PointF(552,600);var box=Footprint(type,point);
  for(int y=(int)box.Top/Tile;y<(int)box.Bottom/Tile;y++)for(int x=(int)box.Left/Tile;x<(int)box.Right/Tile;x++){var c=w.Terrain.At(x,y);c.Ground=RiverbendMap.Surface.Meadow;c.Height=0;c.Tree=false;}
  CoastAssert(w.CanPlace(type,point,null),"Clear foundation fixture");var blocked=w.Terrain.At((int)box.Left/Tile,(int)box.Top/Tile);
  foreach(var reason in new[]{PlacementBlock.Water,PlacementBlock.Trees,PlacementBlock.Uneven}){
   blocked.Ground=reason==PlacementBlock.Water?RiverbendMap.Surface.Water:RiverbendMap.Surface.Meadow;
   blocked.Height=reason==PlacementBlock.Uneven?1:0;blocked.Tree=reason==PlacementBlock.Trees;
   var report=w.AssessPlacement(type,point,null,0,true);
   CoastAssert(!report.Valid&&!w.CanPlace(type,point,null)&&report.Tiles.Count(t=>t.Block==reason)==1&&report.Tiles.Count(t=>t.Valid)==3,"Only offending cell red: "+reason);
  }
  blocked.Ground=RiverbendMap.Surface.Meadow;blocked.Height=0;blocked.Tree=false;
  var unit=w.Spawn("squad",0,box.Left+12,box.Top+12);var occupied=w.AssessPlacement(type,point,null,0,true);
  CoastAssert(occupied.Tiles.Count(t=>t.Block==PlacementBlock.Unit)==1,"Individual unit obstruction");unit.Hp=0;
  var ore=new OrePatch(box.Left+12,box.Top+12,480);w.Ore.Add(ore);CoastAssert(w.AssessPlacement(type,point).Tiles.Count(t=>t.Block==PlacementBlock.Ore)==1,"Individual ore obstruction");ore.Stock=0;
  CoastAssert(w.CanPlace(type,point,null),"Clearing obstacle restores legal placement");
  hq.Hp=0;CoastAssert(w.AssessPlacement(type,point,null,0,true).Global==PlacementBlock.NoBase,"Missing HQ reason");
  w.Spawn("hq",0,800,800);CoastAssert(w.AssessPlacement(type,point,null,0,true).Global==PlacementBlock.OutOfRange,"Build extension range reason");
  var crossing=new Campaign(true,false,true);crossing.Units.Clear();crossing.Ore.Clear();crossing.Spawn("mcv",1,4800,900);
  var extension= crossing.Terrain.TerrainBounds;Point? anchor=null;
  for(int y=extension.Top+3;y<extension.Bottom-3&&!anchor.HasValue;y++)for(int x=extension.Left+3;x<extension.Right-3;x++){
   if(x>=0&&y>=0&&x<crossing.Terrain.WidthCells&&y<crossing.Terrain.HeightCells)continue;
   if(crossing.TerrainOpen(new PointF((x+.5f)*Tile,(y+.5f)*Tile),10)&&crossing.TerrainOpen(new PointF((x+3.5f)*Tile,(y+.5f)*Tile),10)){anchor=new Point(x,y);break;}
  }
  CoastAssert(anchor.HasValue,"No extension route fixture");var at=anchor.Value;var tank=crossing.Spawn("tank",0,(at.X+.5f)*Tile,(at.Y+.5f)*Tile);
  var to=new PointF((at.X+3.5f)*Tile,(at.Y+.5f)*Tile);CoastAssert(crossing.CanMoveTo(tank,to)&&crossing.Move(tank,to,false),"Negative/extended land navigation");
  for(int i=0;i<100;i++)crossing.Tick(.05f);CoastAssert(Dist(tank.P,to)<12,"Actual extended-domain movement");
  Console.WriteLine("Placement: one blocked cell stays red with three green; water/tree/height/unit/ore/base/range reasons match actual rules. Extended-domain movement passed.");
 }
}
partial class CommandWindow {
 internal void CheckCoastPictures(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);pulse.Stop();
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   foreach(bool island in new[]{false,true}){
    islandMissionSelected=island;riverMissionSelected=true;OpenMapReview();mapLabels=false;RiverFit();hudDirty=true;
    OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,island?"孤岛战区-自然海岸.png":"河湾战区-完整地形.png"),ImageFormat.Png);
    if(island){RiverFocus(73,32,1.55f);hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"海岸细节.png"),ImageFormat.Png);}
   }
   mapReview=false;StartMission(true,false);world.Units.Clear();world.Ore.Clear();selection.Clear();
   world.Spawn("hq",0,480,600);var type=Campaign.Info("power");var at=new PointF(552,600);var box=Campaign.Footprint(type,at);
   world.CoastRevealAll();
   for(int y=(int)box.Top/Campaign.Tile;y<(int)box.Bottom/Campaign.Tile;y++)for(int x=(int)box.Left/Campaign.Tile;x<(int)box.Right/Campaign.Tile;x++){var c=world.Terrain.At(x,y);c.Ground=RiverbendMap.Surface.Meadow;c.Tree=false;c.Height=0;}
   world.Spawn("squad",0,box.Left+12,box.Top+12);place=type;CenterAt(at);mouse=ScreenOf(at);hudDirty=true;
   OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"逐格建造提示.png"),ImageFormat.Png);
  }
  Console.WriteLine("Coast and mixed foundation native render previews saved.");
 }
}
}
