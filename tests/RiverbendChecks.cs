using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 internal void CheckRiverbend(string folder){
  OpenMapReview();var m=riverMap;
  if(m.Cells.Length!=33000||m.Houses.Count!=28||m.Bridges.Count!=3)throw new Exception("riverbend map counts");
  if(!m.Connected(m.WestStart,m.EastStart))throw new Exception("new map disconnected bases");
  foreach(var h in m.Houses){
   if(m.Houses.Any(other=>other!=h&&other.Lot.IntersectsWith(h.Lot)))throw new Exception("overlapping house "+h.Name);
   if(!m.Walkable(h.Door.X,h.Door.Y)||!m.Connected(m.WestStart,h.Door))throw new Exception("inaccessible house exit "+h.Name+" "+h.Door);
   int exits=0;for(int y=h.Lot.Top-1;y<=h.Lot.Bottom;y++)for(int x=h.Lot.Left-1;x<=h.Lot.Right;x++)if(m.Walkable(x,y))exits++;
   if(exits<h.Capacity)throw new Exception("insufficient evacuation ring "+h.Name);
  }
  foreach(var point in new[]{new Point(62,31),new Point(175,75)})if(!m.Connected(m.WestStart,point))throw new Exception("unreachable highland ramp "+point);
  foreach(var b in m.Bridges){var a=new Point(b.Left-1,b.Top+2);var z=new Point(b.Right,b.Top+2);if(!m.Connected(a,z))throw new Exception("disconnected bridge approach");}
  foreach(var b in m.Bridges)for(int y=b.Top;y<b.Bottom;y++)for(int x=b.Left;x<b.Right;x++)m.At(x,y).Ground=RiverbendMap.Surface.Water;
  if(m.Connected(m.WestStart,m.EastStart))throw new Exception("river can be crossed outside the three bridges");
  foreach(var b in m.Bridges)for(int y=b.Top;y<b.Bottom;y++)for(int x=b.Left;x<b.Right;x++)m.At(x,y).Ground=RiverbendMap.Surface.Bridge;
  Console.WriteLine("220x150 cells, three crossings, both highland ramps, 28 house entrances and evacuation rings passed; water separates both banks without bridges.");
  var center=new PointF(620,400);var original=RiverWorld(center);RiverWheel(center,120);if(Campaign.Dist(original,RiverWorld(center))>.001)throw new Exception("map cursor zoom anchor");
  CloseMapReview();if(mapReview||!missionPicker)throw new Exception("preview return");
  LaunchMission();if(!world.Tutorial)throw new Exception("only tutorial must launch");
  if(folder==null)return;
  Directory.CreateDirectory(folder);var data=Path.GetFullPath(Path.Combine(folder,"../maps"));Directory.CreateDirectory(data);m.Export(Path.Combine(data,"riverbend-v01.json"));
  ClientSize=new Size(1920,1080);OpenMapReview();
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   string[] files={"河湾镇-全图.png","河湾镇-老街与中央桥.png","河湾镇-高地坡道.png","河湾镇-河口湿地.png"};
   for(int i=0;i<files.Length;i++){
    if(i==0)RiverFit();if(i==1)RiverFocus(96,75,1.55f);if(i==2)RiverFocus(165,76,.80f);if(i==3)RiverFocus(111,123,1.15f);
    OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,files[i]),ImageFormat.Png);
   }
   RiverFocus(96,75,1.55f);OnPaint(new PaintEventArgs(g,ClientRectangle));
   var times=new System.Collections.Generic.List<double>();
   for(int i=0;i<48;i++){var timer=System.Diagnostics.Stopwatch.StartNew();RiverPan(3,1);OnPaint(new PaintEventArgs(g,ClientRectangle));times.Add(timer.Elapsed.TotalMilliseconds);}
   times.Sort();Console.WriteLine("Native map pan 1920x1080: median {0:F1}ms p95 {1:F1}ms; viewport buffer {2:F1} MiB.",times[24],times[45],riverView.Width*riverView.Height*4/1048576.0);
   var oldCenter=mapCenter;var screen=new PointF(600,410);var client=new Point((int)(ox+screen.X*scale),(int)(oy+screen.Y*scale));
   OnMouseDown(new MouseEventArgs(MouseButtons.Middle,1,client.X,client.Y,0));OnMouseMove(new MouseEventArgs(MouseButtons.Middle,0,client.X+24,client.Y,0));OnMouseUp(new MouseEventArgs(MouseButtons.Middle,1,client.X+24,client.Y,0));
   if(mapDragging||mapCenter==oldCenter)throw new Exception("native map drag/release");
   CloseMapReview();OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"关卡选择.png"),ImageFormat.Png);
  }
  Console.WriteLine("Native map previews + authored map data exported.");
 }
}
}
