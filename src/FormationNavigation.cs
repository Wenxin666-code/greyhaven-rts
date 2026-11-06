using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
namespace Greyhaven {
public partial class Campaign {
 // Minimum-cost bipartite assignment, O(n^3), at most 55 units per army.
 static int[] MatchSlots(Piece[] units,PointF[] slots){
  int n=units.Length;var u=new double[n+1];var v=new double[n+1];var owner=new int[n+1];var previous=new int[n+1];
  for(int i=1;i<=n;i++){
   owner[0]=i;int column=0;var minimum=Enumerable.Repeat(double.PositiveInfinity,n+1).ToArray();var visited=new bool[n+1];
   do{
    visited[column]=true;int row=owner[column],next=0;double delta=double.PositiveInfinity;
    for(int j=1;j<=n;j++)if(!visited[j]){
     double dx=units[row-1].P.X-slots[j-1].X,dy=units[row-1].P.Y-slots[j-1].Y;
     double cost=dx*dx+dy*dy-u[row]-v[j];
     if(cost<minimum[j]){minimum[j]=cost;previous[j]=column;}
     if(minimum[j]<delta){delta=minimum[j];next=j;}
    }
    for(int j=0;j<=n;j++)if(visited[j]){u[owner[j]]+=delta;v[j]-=delta;}else minimum[j]-=delta;
    column=next;
   }while(owner[column]!=0);
   do{int next=previous[column];owner[column]=owner[next];column=next;}while(column!=0);
  }
  var result=new int[n];for(int j=1;j<=n;j++)result[owner[j]-1]=j-1;return result;
 }
 bool TrafficDetour(Piece unit,Piece blocker,PointF waypoint){
  var temporary=MineTraffic(unit,waypoint);var blocks=Grid(Clearance(unit)).Barriers.Concat(temporary??new RectangleF[0]).ToArray();
  float length=Math.Max(1,Dist(unit.P,waypoint)),fx=(waypoint.X-unit.P.X)/length,fy=(waypoint.Y-unit.P.Y)/length;
  foreach(float gap in new[]{26f,42f,64f})foreach(float side in new[]{1f,-1f}){
   float forward=Math.Min(length,Dist(unit.P,blocker.P)+gap);
   var pass=new PointF(unit.P.X+fx*forward-fy*gap*side,unit.P.Y+fy*forward+fx*gap*side);
   var shoulder=new PointF(unit.P.X-fy*gap*side,unit.P.Y+fx*gap*side);
   if(Blocked(pass,Clearance(unit),null))continue;
   var route=new Queue<PointF>();
   if(!TrackLegs(unit.P,shoulder,blocks,route)||!TrackLegs(shoulder,pass,blocks,route)||!TrackLegs(pass,waypoint,blocks,route))continue;
   foreach(var point in unit.Path.Skip(1))route.Enqueue(point);
   unit.Path=route;unit.RouteVersion=topology;return true;
  }
  return false;
 }
}
}
