using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Greyhaven {
partial class CommandWindow {
 // The weapon measures Euclidean distance on the ground. Project that circle,
 // not a union of cells or a screen-space circle, through the battle camera.
 void DrawDefenseRange(Graphics g,PointF origin,float range,bool valid){
  var state=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;
  var points=ReferenceBalance.Circle(origin,range);
  for(int i=0;i<points.Length;i++)points[i]=ScreenOf(points[i]);
  using(var dark=new Pen(Color.FromArgb(215,12,25,29),3/PixelScale))
  using(var edge=new Pen(Color.FromArgb(valid?235:170,gold),1/PixelScale)){
   g.DrawPolygon(dark,points);g.DrawPolygon(edge,points);
  }
  g.Restore(state);
 }
}
}
