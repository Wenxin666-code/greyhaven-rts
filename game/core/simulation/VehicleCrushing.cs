using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 const float CrushContactRadius=Tile*.5f;
 const float AiCrushRange=Tile*1.8f;

 internal static bool CanCrush(Piece vehicle,Piece victim){
  return vehicle.Side>=0&&vehicle.T.CrushesInfantry&&victim.T.Foot&&victim.Deployed&&
   HostileTo(vehicle,victim)&&Crushable(victim);
 }

 static RectangleF GroundCell(PointF point){
  return new RectangleF(MathF.Floor(point.X/Tile)*Tile,MathF.Floor(point.Y/Tile)*Tile,Tile,Tile);
 }

 void CrushOnTrack(Piece vehicle,PointF next){
  var from=vehicle.P;
  float dx=next.X-from.X,dy=next.Y-from.Y,lengthSquared=dx*dx+dy*dy;
  if(lengthSquared<.000001f)return;
  float reachX=Math.Abs(dx)+CrushContactRadius,reachY=Math.Abs(dy)+CrushContactRadius;
  // The original checks occupiers of the crossing cell within half a tile.
  // Sweep the movement segment so a fast vehicle cannot skip that contact.
  foreach(var victim in Units){
   if(!victim.T.Foot||Math.Abs(victim.P.X-from.X)>reachX||Math.Abs(victim.P.Y-from.Y)>reachY||
      !CanCrush(vehicle,victim))continue;
   var cell=GroundCell(victim.P);
   float entry=0,exit=1;
   if(!Slab(from.X,dx,cell.Left,cell.Right,ref entry,ref exit)||
      !Slab(from.Y,dy,cell.Top,cell.Bottom,ref entry,ref exit))continue;
   float projection=((victim.P.X-from.X)*dx+(victim.P.Y-from.Y)*dy)/lengthSquared;
   float t=Math.Clamp(projection,entry,exit);
   var contact=new PointF(from.X+t*dx,from.Y+t*dy);
   if(Dist(contact,victim.P)<CrushContactRadius)CombatKill(victim,vehicle);
  }
 }

 bool CloseEnoughToPursueCrush(Piece vehicle,Piece target){
  return vehicle.Side>0&&CanCrush(vehicle,target)&&Dist(vehicle.P,target.P)<AiCrushRange;
 }

 bool PursueCrush(Piece vehicle,Piece target){
  if(!CloseEnoughToPursueCrush(vehicle,target))return false;
  if(vehicle.Path.Count>0&&Dist(vehicle.Path.Last(),target.P)<Tile*.25f&&
     vehicle.TrafficBlocked<VehicleRepathDelay)return true;
  if(Time<vehicle.RouteRetryAt)return false;
  vehicle.RouteRetryAt=Time+VehicleRepathDelay;
  return PlanRoute(vehicle,target.P,MineTraffic(vehicle,target.P));
 }
}
}
