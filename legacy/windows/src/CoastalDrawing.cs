using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Greyhaven {
partial class CommandWindow {
 void DrawPlacementCell(Graphics g,RectangleF cell,bool valid){
  float height=world.GroundHeight(Campaign.Middle(cell));
  var p=new[]{new PointF(cell.Left,cell.Top),new PointF(cell.Right,cell.Top),new PointF(cell.Right,cell.Bottom),new PointF(cell.Left,cell.Bottom)}
   .Select(w=>{var q=ScreenOf(w);q.Y+=(world.GroundHeight(w)-height)*15*zoom;return q;}).ToArray();
  var color=valid?C("#67ee78"):C("#ff4938");
  using(var b=new SolidBrush(Color.FromArgb(valid?83:145,color)))g.FillPolygon(b,p);
  using(var edge=new Pen(C("#14252a"),3/PixelScale))g.DrawPolygon(edge,p);
  using(var edge=new Pen(color,1.25f/PixelScale))g.DrawPolygon(edge,p);
  if(!valid){var center=ScreenOf(Campaign.Middle(cell));float half=Math.Max(2,Math.Min(5,Campaign.Tile*zoom*.15f))/PixelScale;
   using(var edge=new Pen(Color.FromArgb(235,255,211,192),1/PixelScale)){g.DrawLine(edge,center.X-half,center.Y-half,center.X+half,center.Y+half);g.DrawLine(edge,center.X-half,center.Y+half,center.X+half,center.Y-half);}
  }
 }
}
}
