using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Greyhaven {
partial class CommandWindow {
 const int OreStages=12;
 Bitmap OreSprites(){
  var sheet=new Bitmap(96*OreStages,64,PixelFormat.Format32bppPArgb);var rng=new Random(71);
  using(var g=Graphics.FromImage(sheet)){
   g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;
   for(int i=0;i<80;i++){
    float a=(float)rng.NextDouble()*1.86f-.93f,b=(float)rng.NextDouble()*1.86f-.93f;
    float x=48+(a-b)*23,y=37+(a+b)*11.5f,h=2+rng.Next(3),w=1.7f+(float)rng.NextDouble()*2;
    for(int stage=i*OreStages/80;stage<OreStages;stage++){
     float u=x+stage*96;using(var shade=new SolidBrush(C("#6f5732")))using(var face=new SolidBrush(i%3==0?C("#e6c272"):C("#bf9452"))){
      g.FillPolygon(shade,new[]{new PointF(u-w,y),new PointF(u-1,y-h),new PointF(u+w,y-h*.54f),new PointF(u+w*.8f,y+2),new PointF(u,y+4)});
      g.FillPolygon(face,new[]{new PointF(u-w,y),new PointF(u-1,y-h),new PointF(u+w,y-h*.54f),new PointF(u,y)});
     }
    }
   }
  }return sheet;
 }
 void DrawOre(Graphics g){
  foreach(var patch in world.Ore){
   if(patch.Stock<=.01f||!world.Explored(0,patch.P))continue;
   var p=ScreenOf(patch.P);int stage=Math.Min(OreStages-1,Math.Max(0,(int)Math.Ceiling(OreStages*patch.Stock/patch.Initial)-1));
   float k=Campaign.Tile*Axis*2*zoom/92;
   DrawStamp(g,"ore",new Rectangle(stage*96,0,96,64),new RectangleF(p.X-48*k,p.Y-37*k,96*k,64*k));
  }
 }
}
}
