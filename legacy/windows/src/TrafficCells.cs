using System;
using System.Drawing;
namespace Greyhaven {
public partial class Campaign {
 // Ground cells, not the circumcircle around the rendered turret/barrel. This
 // admits adjacent vehicle cells while preserving a small gap between bodies.
 public static bool TrafficContact(TypeInfo a,PointF ap,TypeInfo b,PointF bp){
  float x=Math.Abs(ap.X-bp.X),y=Math.Abs(ap.Y-bp.Y);
  if(!a.Foot&&!b.Foot)return x<a.TrafficRadius+b.TrafficRadius&&y<a.TrafficRadius+b.TrafficRadius;
  if(a.Foot&&b.Foot)return x*x+y*y<(a.TrafficRadius+b.TrafficRadius)*(a.TrafficRadius+b.TrafficRadius);
  float square=a.Foot?b.TrafficRadius:a.TrafficRadius,circle=a.Foot?a.TrafficRadius:b.TrafficRadius;
  x=Math.Max(0,x-square);y=Math.Max(0,y-square);return x*x+y*y<circle*circle;
 }
}
}
