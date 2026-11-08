using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace Greyhaven {
partial class CommandWindow {
 // Cache only immutable terrain, at physical display resolution. The extra
 // border lets small pans reuse the same pixels without rerasterizing the map.
 const int TerrainMargin=144;
 Bitmap terrainView,radarGround;
 PointF terrainCamera;float terrainZoom,terrainScale;
 const float TerrainDensity=2;
 sealed class TerrainPatch {public Bitmap Image,Raster;public Rectangle Bounds,Source;public int Used;}
 float terrainRasterScale;long terrainRasterBytes;int terrainFrame;
 const long TerrainBudget=64L*1024*1024;
 void ClearTerrainRasters(){foreach(var patch in terrainPatches)if(patch.Raster!=null){patch.Raster.Dispose();patch.Raster=null;}terrainRasterBytes=0;}
 Bitmap TerrainRaster(TerrainPatch patch,float k){
  patch.Used=terrainFrame;if(patch.Raster!=null)return patch.Raster;
  int w=(int)Math.Round(patch.Bounds.Right*k)-(int)Math.Round(patch.Bounds.Left*k);
  int h=(int)Math.Round(patch.Bounds.Bottom*k)-(int)Math.Round(patch.Bounds.Top*k);long bytes=4L*w*h;
  while(terrainRasterBytes+bytes>TerrainBudget){
   var old=terrainPatches.Where(p=>p.Raster!=null&&p.Used!=terrainFrame).OrderBy(p=>p.Used).FirstOrDefault();
   if(old==null)break;
   terrainRasterBytes-=4L*old.Raster.Width*old.Raster.Height;old.Raster.Dispose();old.Raster=null;
  }
  patch.Raster=new Bitmap(w,h,PixelFormat.Format32bppPArgb);terrainRasterBytes+=bytes;
  using(var raster=Graphics.FromImage(patch.Raster))using(var sampling=new ImageAttributes()){
   raster.CompositingMode=CompositingMode.SourceCopy;raster.InterpolationMode=InterpolationMode.Bilinear;
   sampling.SetWrapMode(WrapMode.TileFlipXY);
   raster.DrawImage(patch.Image,new Rectangle(0,0,w,h),patch.Source.X,patch.Source.Y,patch.Source.Width,patch.Source.Height,GraphicsUnit.Pixel,sampling);
  }
  return patch.Raster;
 }
 readonly List<TerrainPatch> terrainPatches=new List<TerrainPatch>();
 void ProjectTerrain(){
  using(var map=new Bitmap((int)Math.Ceiling((Campaign.Width+Campaign.Height)*Axis*TerrainDensity)+2,
   (int)Math.Ceiling((Campaign.Width+Campaign.Height)*Rise*TerrainDensity)+2,PixelFormat.Format32bppPArgb)){
  using(var g=Graphics.FromImage(map))
  using(var m=new Matrix(Axis*TerrainDensity,Rise*TerrainDensity,-Axis*TerrainDensity,Rise*TerrainDensity,Campaign.Height*Axis*TerrainDensity,0)){
   g.Clear(C("#132122"));g.Transform=m;g.InterpolationMode=InterpolationMode.Bilinear;g.DrawImage(ground,0,0,ground.Width,ground.Height);
  }
   // GDI+ resamples a large source expensively even when only a small source
   // rectangle is requested. Independent tiles bound that work to the viewport.
   // A one-pixel gutter preserves bilinear sampling at tile boundaries.
   const int tile=256;
   var source=map.LockBits(new Rectangle(Point.Empty,map.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
   try{
   for(int y=0;y<map.Height;y+=tile)for(int x=0;x<map.Width;x+=tile){
    var core=new Rectangle(x,y,Math.Min(tile,map.Width-x),Math.Min(tile,map.Height-y));
    var padded=Rectangle.Inflate(core,1,1);padded.Intersect(new Rectangle(Point.Empty,map.Size));
    var image=new Bitmap(padded.Width,padded.Height,PixelFormat.Format32bppPArgb);
    var target=image.LockBits(new Rectangle(Point.Empty,image.Size),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
    try{var row=new byte[padded.Width*4];for(int line=0;line<padded.Height;line++){
     System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(source.Scan0,(padded.Y+line)*source.Stride+padded.X*4),row,0,row.Length);
     System.Runtime.InteropServices.Marshal.Copy(row,0,IntPtr.Add(target.Scan0,line*target.Stride),row.Length);
    }}finally{image.UnlockBits(target);}
    terrainPatches.Add(new TerrainPatch{Image=image,Bounds=core,
     Source=new Rectangle(core.X-padded.X,core.Y-padded.Y,core.Width,core.Height)});
   }
   }finally{map.UnlockBits(source);}
  }
 }
 void DrawGroundView(Graphics g){
  terrainFrame++;
  int w=(int)Math.Ceiling(MW*scale)+2*TerrainMargin,h=(int)Math.Ceiling(MH*scale)+2*TerrainMargin;
  var anchor=ScreenOf(terrainCamera);
  int dx=(int)Math.Round((anchor.X-MW*.5f)*scale),dy=(int)Math.Round((anchor.Y-MapTop-MH*.5f)*scale);
  if(terrainView==null||terrainView.Width!=w||terrainView.Height!=h||zoom!=terrainZoom||scale!=terrainScale||Math.Abs(dx)>=TerrainMargin||Math.Abs(dy)>=TerrainMargin){
   if(terrainView==null||terrainView.Width!=w||terrainView.Height!=h){
    if(terrainView!=null)terrainView.Dispose();terrainView=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
   }
   terrainCamera=camera;terrainScale=scale;terrainZoom=zoom;dx=dy=0;
   using(var bg=Graphics.FromImage(terrainView)){
    bg.Clear(C("#132122"));bg.CompositingMode=CompositingMode.SourceCopy;
    var origin=ScreenOf(new PointF(0,Campaign.Height));
    var top=ScreenOf(PointF.Empty);
    float k=zoom*scale/TerrainDensity;
    if(terrainRasterScale!=k){ClearTerrainRasters();terrainRasterScale=k;}
    float left=TerrainMargin+origin.X*scale,upper=TerrainMargin+(top.Y-MapTop)*scale;
    foreach(var patch in terrainPatches){
     int x=(int)Math.Round(patch.Bounds.Left*k),y=(int)Math.Round(patch.Bounds.Top*k);
     var size=new Size((int)Math.Round(patch.Bounds.Right*k)-x,(int)Math.Round(patch.Bounds.Bottom*k)-y);
     var dst=new Rectangle((int)Math.Round(left)+x,(int)Math.Round(upper)+y,size.Width,size.Height);
     if(dst.Right<=0||dst.Bottom<=0||dst.Left>=w||dst.Top>=h)continue;
     bg.DrawImageUnscaled(TerrainRaster(patch,k),dst.X,dst.Y);
    }
   }
  }
  var state=g.Save();g.ResetTransform();g.CompositingMode=CompositingMode.SourceCopy;
  g.DrawImageUnscaled(terrainView,(int)Math.Round(ox)+dx-TerrainMargin,(int)Math.Round(oy+MapTop*scale)+dy-TerrainMargin);g.Restore(state);
  // Prepare one nearby tile per ordinary frame instead of a whole new strip
  // at the next viewport-cache boundary. UI input never waits for that strip.
  if(dx!=0||dy!=0){
   float k=zoom*scale/TerrainDensity;var origin=ScreenOf(new PointF(0,Campaign.Height));var top=ScreenOf(PointF.Empty);
   float cx=(MW*.5f-origin.X)*scale/k,cy=(MapTop+MH*.5f-top.Y)*scale/k;
   float rx=(MW*scale*.5f+TerrainMargin+512)/k,ry=(MH*scale*.5f+TerrainMargin+512)/k;
   var next=terrainPatches.Where(p=>p.Raster==null&&p.Bounds.Right>cx-rx&&p.Bounds.Left<cx+rx&&p.Bounds.Bottom>cy-ry&&p.Bounds.Top<cy+ry)
    .OrderBy(p=>Math.Abs(p.Bounds.Left+p.Bounds.Width/2-cx)+Math.Abs(p.Bounds.Top+p.Bounds.Height/2-cy)).FirstOrDefault();
   if(next!=null)TerrainRaster(next,k);
  }
 }
}
}
