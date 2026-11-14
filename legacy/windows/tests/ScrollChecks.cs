using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class ScrollChecks {
 [STAThread]static int Main(string[] args){try{using(var f=new CommandWindow()){f.CheckRiverTilePixels();f.CheckFogCullingPixels();if(args.Length==0)f.CheckScrollPerformance();}return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 internal void ScrollReveal(bool full=true){
 if(!full){var sides=Units.Select(u=>u.Side).ToArray();for(int i=0;i<Units.Count;i++)Units[i].Side=0;Reveal();for(int i=0;i<Units.Count;i++)Units[i].Side=sides[i];return;}
 for(int x=0;x<MapFogCols;x++)for(int y=0;y<MapFogRows;y++)discovered[0,x,y]=true;FogRevision++;
}
}
partial class CommandWindow {
 static void ScrollReport(string label,List<double> values){if(values.Count==0)return;values.Sort();Console.WriteLine("{0}: n={1} median={2:F2}ms p95={3:F2}ms max={4:F2}ms",label,values.Count,values[values.Count/2],values[(int)(values.Count*.95)],values.Last());}
 internal void CheckScrollPerformance(){
  ClientSize=new Size(1920,1080);
  using(var target=new Bitmap(1920,1080))using(var g=Graphics.FromImage(target)){
   foreach(int scene in new[]{0,1,2}){bool river=scene>0;
    if(river)StartTerrainMission(false);else StartMission(true,true);
    selection.Clear();world.ScrollReveal(scene!=2);zoom=DefaultZoom;
    var center=scene==2?new PointF(43*24,34*24):river?new PointF(31*24,13*24):new PointF(1100,800);CenterAt(center);
    for(int i=0;i<24;i++)world.Spawn(i%3==0?"squad":"tank",0,center.X+i%6*25-75,center.Y+i/6*25);
    mouse=new PointF(MW+10,700);hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));
    var cached=new List<double>();var refresh=new List<double>();
    for(int i=0;i<96;i++){
     Pan(i<48?8:-8,i<48?4:-4);var previous=river?riverViewCenter:terrainCamera;
     var t=Stopwatch.StartNew();OnPaint(new PaintEventArgs(g,ClientRectangle));g.Flush(FlushIntention.Sync);
     if(i>=4)((previous==(river?riverViewCenter:terrainCamera))?cached:refresh).Add(t.Elapsed.TotalMilliseconds);
    }
    ScrollReport((scene==2?"river-partial-fog":river?"river":"training")+" full cached",cached);ScrollReport((scene==2?"river-partial-fog":river?"river":"training")+" full refresh",refresh);
    Action<string,Action> measure=(name,draw)=>{var values=new List<double>();for(int i=0;i<14;i++){var t=Stopwatch.StartNew();var state=frameGraphics.Save();draw();frameGraphics.Restore(state);frameGraphics.Flush(FlushIntention.Sync);if(i>2)values.Add(t.Elapsed.TotalMilliseconds);}ScrollReport((river?"river ":"training ")+name,values);};
    drawingClient=true;
    measure("ground-only",()=>{if(river)DrawRiverBattleGround(frameGraphics);else DrawGroundView(frameGraphics);});
    measure("fog-only",()=>{GroundSpace(frameGraphics);Shroud(frameGraphics);});
    measure("battle-only",()=>DrawBattle(frameGraphics));
    measure("HUD-only",()=>{hits.Clear();DrawHud(frameGraphics);});drawingClient=false;
   }
  }
 }
}
}

namespace Greyhaven {
partial class CommandWindow {
 internal void CheckRiverTilePixels(){
  ClientSize=new Size(1920,1080);StartTerrainMission(false);world.ScrollReveal();
  using(var frame=new Bitmap(1920,1080))using(var canvas=Graphics.FromImage(frame))OnPaint(new PaintEventArgs(canvas,ClientRectangle));
  var views=new[]{new PointF(8,23),new PointF(47,34),new PointF(31,13)};
  float[] levels={1.05f,1.55f,2.4f};
  for(int scene=0;scene<views.Length;scene++){
   riverBattleRender=true;riverMap=world.River;mapCenter=views[scene];mapZoom=levels[scene];
   var area=riverDrawArea;var origin=RiverScreen(0,0);float dx=(float)Math.Round((origin.X-area.Left)*scale)-(origin.X-area.Left)*scale,dy=(float)Math.Round((origin.Y-area.Top)*scale)-(origin.Y-area.Top)*scale;
   var size=new Size((int)Math.Ceiling(area.Width*scale),(int)Math.Ceiling(area.Height*scale));
   using(var tiled=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
   using(var reference=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)){
    using(var g=Graphics.FromImage(tiled))DrawRiverGroundTiles(g);
    using(var g=Graphics.FromImage(reference)){
     g.ScaleTransform(scale,scale);g.TranslateTransform(-area.Left+dx/scale,-area.Top+dy/scale);g.InterpolationMode=InterpolationMode.Bilinear;
     bool prior=drawingClient;drawingClient=false;try{DrawRiverMap(g);}finally{drawingClient=prior;}
    }
    var rect=new Rectangle(Point.Empty,size);var a=tiled.LockBits(rect,System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);var b=reference.LockBits(rect,System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
    long different=0,total=0;try{
     var left=new byte[a.Stride*size.Height];var right=new byte[b.Stride*size.Height];System.Runtime.InteropServices.Marshal.Copy(a.Scan0,left,0,left.Length);System.Runtime.InteropServices.Marshal.Copy(b.Scan0,right,0,right.Length);
     for(int y=8;y<size.Height-8;y+=2)for(int x=8;x<size.Width-8;x+=2){int index=y*a.Stride+x*4;total++;if(Math.Abs(left[index]-right[index])>3||Math.Abs(left[index+1]-right[index+1])>3||Math.Abs(left[index+2]-right[index+2])>3)different++;}
    }finally{tiled.UnlockBits(a);reference.UnlockBits(b);}
    double error=different/(double)total;Console.WriteLine("River tile/reference zoom {0}: differing samples {1:P3}",mapZoom,error);
    if(error>.01){tiled.Save(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"greyhaven-tile-actual.png"));reference.Save(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"greyhaven-tile-reference.png"));throw new Exception("tile/reference seam or detail mismatch");}
   }
  }
  // Force reuse, eviction, zoom and mission switches rather than only one camera.
  mapZoom=DefaultZoom;
  for(int i=0;i<18;i++){
   mapCenter=new PointF(12+i%6*14,12+i/6*21);using(var image=new Bitmap(2100,1450))using(var g=Graphics.FromImage(image))DrawRiverGroundTiles(g);
   if(riverGroundBytes>RiverTileBudget)throw new Exception("river tile budget exceeded");
  }
  var previous=riverGroundMap;StartTerrainMission(false);riverBattleRender=true;riverMap=world.River;mapZoom=DefaultZoom;EnsureRiverGroundCache();
  if(riverGroundTiles.Count!=0||riverGroundMap==previous)throw new Exception("new mission retained stale terrain tiles");riverBattleRender=false;
  Console.WriteLine("River tile overlaps, terrain detail, three tactical zooms, 64 MiB eviction and new-mission invalidation passed.");
 }
}
}

namespace Greyhaven {
partial class CommandWindow {
 internal void CheckFogCullingPixels(){
  StartTerrainMission(false);world.ScrollReveal(false);ClientSize=new Size(1920,1080);
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame))OnPaint(new PaintEventArgs(g,ClientRectangle));
  for(int scene=0;scene<3;scene++){
   zoom=new[]{1.05f,1.55f,2.4f}[scene];camera=new PointF((20+scene*24)*24,(18+scene*11)*24);
   using(var a=new Bitmap(1920,1080))using(var b=new Bitmap(1920,1080)){
    for(int variant=0;variant<2;variant++)using(var g=Graphics.FromImage(variant==0?a:b)){
     g.TranslateTransform(ox,oy);g.ScaleTransform(scale,scale);g.SetClip(new RectangleF(0,MapTop,MW,MH));GroundSpace(g);g.SmoothingMode=SmoothingMode.None;
     if(variant==0)Shroud(g);else if(fogRuns.Length>0)using(var brush=new SolidBrush(C("#152323")))g.FillRectangles(brush,fogRuns);
    }
    for(int y=0;y<1080;y+=3)for(int x=0;x<1920;x+=3)if(a.GetPixel(x,y)!=b.GetPixel(x,y))throw new Exception("fog culling changes visible concealment");
   }
  }
  Console.WriteLine("Fog culling preserves the visible concealment at three zooms and camera positions.");
 }
}
}
