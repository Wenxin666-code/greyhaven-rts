using System;
using System.Drawing;
using System.Linq;
using Greyhaven;

class PerformanceChecks {
 static int Main(){try{Campaign.CheckPerformanceEquivalence();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void PerformanceAssert(bool value,string message){if(!value)throw new Exception(message);}
 internal static void CheckPerformanceEquivalence(){
  CheckIndexedBarriers();CheckIncrementalExploration();
 }
 static void CheckIndexedBarriers(){
  var random=new Random(20261108);int compared=0,closed=0,open=0;
  foreach(bool island in new[]{false,true}){
   var w=new Campaign(false,false,true,island);
   foreach(float pad in new[]{5f,5.25f,8.8f}){
    var grid=w.Grid(pad);
    // Sample near foundations, negative-coordinate coastal cells and arbitrary
    // map cells, including corner/tangent intersections in traffic-only grids.
    var areas=w.Obstacles().ToList();areas.Add(new RectangleF(-3,5,27,35));areas.Add(new RectangleF(w.Home(0).X+15,w.Home(0).Y+15,60,60));
    grid=new NavigationGrid{Barriers=areas.Select(r=>Inflated(r,pad)).ToArray()};
    for(int i=0;i<5000;i++){
     PointF p=i<2500?Middle(areas[i%areas.Count]):new PointF(w.FogOriginX+random.Next(w.MapWidth+w.MapHeight),w.FogOriginY+random.Next(w.MapWidth+w.MapHeight));
     int x=Math.Max(1,Math.Min(w.N-2,w.NavX(p.X)+random.Next(-5,6))),y=Math.Max(1,Math.Min(w.M-2,w.NavY(p.Y)+random.Next(-5,6)));
     int dx=random.Next(-1,2),dy=random.Next(-1,2);if(dx==0&&dy==0)dx=1;int a=y*w.N+x,b=(y+dy)*w.N+x+dx;
     var from=w.Center(a);var to=w.Center(b);bool expected=w.TerrainLine(from,to,pad);
     if(expected)foreach(var r in grid.Barriers){float lo=0,hi=1;if(Slab(from.X,to.X-from.X,r.Left,r.Right,ref lo,ref hi)&&Slab(from.Y,to.Y-from.Y,r.Top,r.Bottom,ref lo,ref hi)){expected=false;break;}}
     bool actual=w.NavigationLink(grid,a,b,pad);PerformanceAssert(actual==expected,"Indexed route link differs from full swept-obstacle check");
     PerformanceAssert(w.NavigationLink(grid,a,b,pad)==expected,"Cached route link changed");compared++;if(expected)open++;else closed++;
    }
   }
  }
  PerformanceAssert(open>500&&closed>500,"Both passable and blocked route links exercised");Console.WriteLine("Indexed obstacles: "+compared+" links match original full scan, including cached repeats.");
 }
 void ReferenceExploration(){
  UpdateSatellites();foreach(var o in Units){if(!o.Deployed||o.Side<0)continue;float r=Sight(o);
   for(int y=Math.Max(0,FogRow(o.P.Y-r));y<=Math.Min(MapFogRows-1,FogRow(o.P.Y+r));y++)
    for(int x=Math.Max(0,FogColumn(o.P.X-r));x<=Math.Min(MapFogCols-1,FogColumn(o.P.X+r));x++){
     if(discovered[o.Side,x,y])continue;float dx=FogOriginX+(x+.5f)*FogSize-o.P.X,dy=FogOriginY+(y+.5f)*FogSize-o.P.Y;
     if(dx*dx+dy*dy<=r*r)Discover(o.Side,x,y);
    }
  }
 }
 static void CheckIncrementalExploration(){
  var a=new Campaign(false,false,true,true);var b=new Campaign(false,false,true,true);
  for(int side=0;side<6;side++)for(int i=0;i<24;i++){
   var home=a.Home(side);foreach(var w in new[]{a,b})w.Spawn(i%2==0?"squad":"tank",side,home.X+i%6*24,home.Y+i/6*24);
  }
  var left=new byte[Buffer.ByteLength(a.discovered)];var right=new byte[left.Length];
  for(int frame=0;frame<60;frame++){
   for(int i=0;i<a.Units.Count;i++){
    foreach(var w in new[]{a,b}){var p=w.Units[i];if(p.T.Building)continue;if(i%3!=0)p.P=new PointF(p.P.X+(i%2==0?6:-6),p.P.Y+3);
     if(frame==20&&i%7==0)p.Side=(p.Side+1)%6;
     if(frame==30&&i%9==0)p.T=Info("arty");
     if(frame==40&&i%11==0)p.Ready=.5f;
     if(frame==45)p.Ready=1;
    }
   }
   a.Reveal();b.ReferenceExploration();Buffer.BlockCopy(a.discovered,0,left,0,left.Length);Buffer.BlockCopy(b.discovered,0,right,0,right.Length);
   PerformanceAssert(left.SequenceEqual(right)&&a.FogRevision==b.FogRevision,"Exploration changed at frame "+frame);
  }
  Console.WriteLine("Incremental exploration matches full scans across movement, stationary units, capture, sight changes and completion.");
 }
}
}
