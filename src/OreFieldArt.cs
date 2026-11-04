using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Greyhaven {
partial class CommandWindow {
 Bitmap OreSprites(){
  var sheet=new Bitmap(96*4,64,PixelFormat.Format32bppPArgb);var rng=new Random(71);
  using(var g=Graphics.FromImage(sheet)){
   g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   for(int i=0;i<38;i++){
    float a=(float)(i*2.4),r=(float)Math.Sqrt(i/38.0);
    float x=48+(float)Math.Cos(a)*34*r,y=36+(float)Math.Sin(a)*15*r,h=4+rng.Next(7),w=3+rng.Next(4);
    for(int stage=i/10;stage<4;stage++){
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
   var p=ScreenOf(patch.P);int stage=Math.Min(3,(int)(4*patch.Stock/patch.Initial));
   DrawStamp(g,"ore",new Rectangle(stage*96,0,96,64),new RectangleF(p.X-23*zoom,p.Y-17*zoom,46*zoom,31*zoom));
  }
 }
}
}
