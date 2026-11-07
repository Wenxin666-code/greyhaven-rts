using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
namespace Greyhaven {
public partial class Campaign {
 // A road headway, not a larger collision body. Only applies behind a friendly
 // moving vehicle in the same lane. Mining retains its dock/queue behaviour.
 float FollowingSpeed(Piece unit,PointF waypoint,float speed,float dt){
  if(unit.T.Foot||unit.Mining||unit.T.Key=="miner")return speed;
  float requested=speed;
  float length=Dist(unit.P,waypoint);if(length<.01f)return speed;
  float fx=(waypoint.X-unit.P.X)/length,fy=(waypoint.Y-unit.P.Y)/length;
  foreach(var lead in Units){
   if(lead==unit||lead.Side!=unit.Side||!lead.Deployed||lead.T.Building||lead.T.Foot||lead.Path.Count==0||lead.TrafficBlocked>.8f||lead.NoProgressFor>.6f||lead.FollowingWait>.8f)continue;
   var next=lead.Path.Peek();float run=Dist(lead.P,next);if(run<.01f)continue;
   float dot=((next.X-lead.P.X)*fx+(next.Y-lead.P.Y)*fy)/run;if(dot<.90f)continue;
   float dx=lead.P.X-unit.P.X,dy=lead.P.Y-unit.P.Y,ahead=dx*fx+dy*fy,lateral=Math.Abs(dx*fy-dy*fx);
   if(ahead<=0||lateral>Math.Min(Tile*.42f,Clearance(unit)+Clearance(lead)))continue;
   // Ease into a short 4-unit following buffer, never an extra full cell.
   float room=Math.Max(0,ahead-Tile);
   speed=Math.Min(speed,Math.Min(room/Math.Max(.001f,dt),lead.T.Speed*Math.Min(1,room/4)));
  }
  unit.FollowingWait=speed<requested*.1f?unit.FollowingWait+dt:0;
  // Near a turn or another vehicle's destination, release the comfort gap and
  // let TrackStep's existing collision/wait/detour rules resolve congestion.
  return unit.FollowingWait>1.2f?requested:speed;
 }
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
  // Never splice a detour in front of yesterday's detour. That accumulated
  // repeated U-turns indefinitely when two miners met at a working position.
  waypoint=unit.Path.Last();
  var temporary=MineTraffic(unit,waypoint);var blocks=Grid(Clearance(unit)).Barriers.Concat(temporary??new RectangleF[0]).ToArray();
  float length=Math.Max(1,Dist(unit.P,waypoint)),fx=(waypoint.X-unit.P.X)/length,fy=(waypoint.Y-unit.P.Y)/length;
  foreach(float gap in new[]{26f,42f,64f})foreach(float side in new[]{1f,-1f}){
   float forward=Math.Min(length,Dist(unit.P,blocker.P)+gap);
   var pass=new PointF(unit.P.X+fx*forward-fy*gap*side,unit.P.Y+fy*forward+fx*gap*side);
   var shoulder=new PointF(unit.P.X-fy*gap*side,unit.P.Y+fx*gap*side);
   if(Blocked(pass,Clearance(unit),null))continue;
   var route=new Queue<PointF>();
   if(!TrackLegs(unit.P,shoulder,blocks,route)||!TrackLegs(shoulder,pass,blocks,route)||!TrackLegs(pass,waypoint,blocks,route))continue;
   unit.Path=route;unit.RouteVersion=topology;return true;
  }
  return false;
 }
}
}
