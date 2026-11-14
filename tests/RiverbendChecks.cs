using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 internal void CheckRiverbend(string folder){
  riverMissionSelected=true;islandMissionSelected=relayMissionSelected=false;
  OpenMapReview();var m=riverMap;
  if(m.WidthCells!=92||m.HeightCells!=68||m.Bridges.Count!=1)throw new Exception("compact riverbend dimensions");
  var bridge=m.Bridges.Single();
  int westEdge=Enumerable.Range(m.TerrainBounds.Left,m.WidthCells).First(x=>m.WithinRadar(x,34));
  if(westEdge>=0||Enumerable.Range(westEdge,m.WestStart.X-westEdge).Any(x=>{
   var cell=m.At(x,34);
   return cell==null||cell.Ground!=RiverbendMap.Surface.Road&&cell.Ground!=RiverbendMap.Surface.Square;
  }))throw new Exception("old-port approach stops before the preview edge");
  if(bridge.Height!=7||m.WestStart.X>=bridge.Left||m.Starts.Skip(1).Any(p=>p.X<bridge.Right))throw new Exception("bridge approaches or spawn banks");
  if(m.Starts.Skip(1).Any(p=>!m.Connected(m.WestStart,p)))throw new Exception("new map disconnected bases");
  foreach(var h in m.Houses){
   if(m.Houses.Any(other=>other!=h&&other.Lot.IntersectsWith(h.Lot)))throw new Exception("overlapping house "+h.Name);
   if(!m.Walkable(h.Door.X,h.Door.Y)||!m.Connected(m.WestStart,h.Door))throw new Exception("inaccessible house exit "+h.Name+" "+h.Door);
   int exits=0;for(int y=h.Lot.Top-1;y<=h.Lot.Bottom;y++)for(int x=h.Lot.Left-1;x<=h.Lot.Right;x++)if(m.Walkable(x,y))exits++;
   if(exits<h.Capacity)throw new Exception("insufficient evacuation ring "+h.Name);
  }
  if(m.At(31,13).Height<1||!m.Connected(m.WestStart,new Point(31,13)))throw new Exception("unreachable highland ramp");
  var a=new Point(bridge.Left-1,bridge.Top+3);var z=new Point(bridge.Right,bridge.Top+3);
  if(!m.Connected(a,z))throw new Exception("disconnected bridge approach");
  for(int y=bridge.Top;y<bridge.Bottom;y++)for(int x=bridge.Left;x<bridge.Right;x++)m.At(x,y).Ground=RiverbendMap.Surface.Water;
  if(m.Starts.Skip(1).Any(p=>m.Connected(m.WestStart,p))||!m.Connected(m.EastStart,m.SouthStart))throw new Exception("river crossing bypass or separated eastern bases");
  for(int y=bridge.Top;y<bridge.Bottom;y++)for(int x=bridge.Left;x<bridge.Right;x++)m.At(x,y).Ground=RiverbendMap.Surface.Bridge;
  Console.WriteLine("92x68 cells, one 7-cell-wide crossing, both eastern bases, highland ramp and {0} house entrances passed.",m.Houses.Count);
  var center=new PointF(620,400);var original=RiverWorld(center);RiverWheel(center,120);if(Campaign.Dist(original,RiverWorld(center))>.001)throw new Exception("map cursor zoom anchor");
  CloseMapReview();if(mapReview||!missionPicker)throw new Exception("preview return");
  LaunchMission();if(!world.Tutorial)throw new Exception("only tutorial must launch");
  if(folder==null)return;
  Directory.CreateDirectory(folder);var data=Path.GetFullPath(Path.Combine(folder,"../maps"));Directory.CreateDirectory(data);m.Export(Path.Combine(data,"riverbend-v02.json"));
  ClientSize=new Size(1920,1080);OpenMapReview();
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   RiverFit();OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"河湾镇-单桥全图.png"),ImageFormat.Png);
   hits.Last(h=>h.Id=="河湾大桥").Run();OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"河湾镇-单桥桥头.png"),ImageFormat.Png);
   var times=new System.Collections.Generic.List<double>();
   for(int i=0;i<48;i++){var timer=System.Diagnostics.Stopwatch.StartNew();RiverPan(3,1);OnPaint(new PaintEventArgs(g,ClientRectangle));times.Add(timer.Elapsed.TotalMilliseconds);}
   times.Sort();Console.WriteLine("Native map pan 1920x1080: median {0:F1}ms p95 {1:F1}ms; viewport buffer {2:F1} MiB.",times[24],times[45],riverView.Width*riverView.Height*4/1048576.0);
   var oldCenter=mapCenter;var screen=new PointF(600,410);var client=new Point((int)(ox+screen.X*scale),(int)(oy+screen.Y*scale));
   OnMouseDown(new MouseEventArgs(MouseButtons.Middle,1,client.X,client.Y,0));OnMouseMove(new MouseEventArgs(MouseButtons.Middle,0,client.X+24,client.Y,0));OnMouseUp(new MouseEventArgs(MouseButtons.Middle,1,client.X+24,client.Y,0));
   if(mapDragging||mapCenter==oldCenter)throw new Exception("native map drag/release");
   CloseMapReview();riverMissionSelected=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"河湾镇-单桥选关.png"),ImageFormat.Png);
  }
  Console.WriteLine("Native map previews + authored map data exported.");
 }
}
}
