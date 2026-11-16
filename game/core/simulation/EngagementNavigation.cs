using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
namespace Greyhaven {
public partial class Campaign {
 void TrackRouteProgress(Piece unit){
  if(Time-unit.ProgressAt<.6f)return;float elapsed=Time-unit.ProgressAt;
  if(unit.Path.Count>0&&Dist(unit.P,unit.ProgressAnchor)<unit.T.Speed*elapsed*.18f)unit.NoProgressFor+=elapsed;
  else unit.NoProgressFor=0;
  unit.ProgressAnchor=unit.P;unit.ProgressAt=Time;
 }
 bool FreeCombatSlot(Piece unit,PointF p){
  if(Blocked(p,Clearance(unit),null))return false;
  return !Units.Any(u=>u!=unit&&u.Deployed&&!u.T.Building&&
   (TrafficContact(unit.T,p,u.T,u.P)||u.Side==unit.Side&&u.Target!=null&&u.Path.Count>0&&TrafficContact(unit.T,p,u.T,u.Path.Last())));
 }
 bool FindFiringPosition(Piece unit,PointF aim){
  if(Time<unit.RouteRetryAt)return false;unit.RouteRetryAt=Time+.55f+(unit.Id%5)*.025f;
  var target=unit.Target;var candidates=new List<PointF>();
  float reach=Math.Max(18,ReferenceBalance.Range(unit)-4),extent=target!=null&&target.T.Building?Math.Max(target.T.FoundationWidth,target.T.FoundationHeight)*Tile*.55f:0;
  var center=target==null?aim:target.P;double toward=Math.Atan2(unit.P.Y-center.Y,unit.P.X-center.X);
  // All candidate firing positions lie inside this box. Gather obstacles and
  // occupied/reserved endpoints once, instead of rescanning every army 144 times.
  float pad=Clearance(unit),bound=extent+reach;
  var area=new RectangleF(center.X-bound,center.Y-bound,bound*2,bound*2);
  var exits=RiverbendOperation!=null&&unit.Side>0
   ?FriendlyExitAreas(unit).Where(exit=>exit.IntersectsWith(area)).ToArray():EmptyTerrain;
  var barriers=Obstacles().Select(r=>Inflated(r,pad)).Where(r=>r.IntersectsWith(Inflated(area,.01f))).ToArray();
  var nearby=Units.Where(u=>u!=unit&&u.Deployed&&!u.T.Building&&
   (Inflated(area,Tile*2).Contains(u.P)||u.Side==unit.Side&&u.Target!=null&&u.Path.Count>0&&Inflated(area,Tile*2).Contains(u.Path.Last()))).ToArray();
  for(int ring=0;ring<3;ring++)for(int i=0;i<48;i++){
   double a=toward+i*Math.PI/24;float radius=extent+reach*(1-ring*.22f);
   var p=new PointF(center.X+(float)Math.Cos(a)*radius,center.Y+(float)Math.Sin(a)*radius);
   float distance=target==null?Dist(p,aim):Dist(p,TargetPoint(p,target));
   if(distance>ReferenceBalance.Range(unit)-1||distance<MinimumRange(unit)+1||!Battlefield.ContainsFootprint(p,pad)||!TerrainOpen(p,pad)||barriers.Any(r=>r.Contains(p))||exits.Any(exit=>exit.Contains(p)))continue;
   if(nearby.Any(u=>TrafficContact(unit.T,p,u.T,u.P)||u.Side==unit.Side&&u.Target!=null&&u.Path.Count>0&&TrafficContact(unit.T,p,u.T,u.Path.Last())))continue;
   candidates.Add(p);
  }
  // Reserve the route endpoint through the existing Path; no invisible larger
  // collision body. Replan only on obstruction, target movement or path expiry.
  foreach(var p in candidates.OrderBy(p=>Dist(unit.P,p)).Take(12))
   if(PlanRoute(unit,p,MineTraffic(unit,p))){unit.TrafficBlocked=0;return true;}
  return false;
 }
 bool NudgeIdleFriend(Piece mover,Piece blocker){
  if(blocker==null||blocker.Side!=mover.Side||!blocker.Deployed||blocker.T.Building||blocker.Path.Count>0||blocker.Target!=null||blocker.GuardHome.HasValue||blocker.Mining||blocker.Entering!=null||blocker.FirePoint.HasValue)return false;
  double heading=Math.Atan2(blocker.P.Y-mover.P.Y,blocker.P.X-mover.P.X);
  foreach(double offset in new[]{Math.PI/2,-Math.PI/2,Math.PI/4,-Math.PI/4,0}){
   double a=heading+offset;var p=new PointF(blocker.P.X+(float)Math.Cos(a)*Tile*1.5f,blocker.P.Y+(float)Math.Sin(a)*Tile*1.5f);
   if(!FreeCombatSlot(blocker,p))continue;
   if(PlanRoute(blocker,p,MineTraffic(blocker,p))){blocker.Goal=p;blocker.Formation=null;return true;}
  }return false;
 }
 void RecoverFootTraffic(Piece unit){
  if(unit.Entering!=null||unit.Path.Count==0||unit.StalledFor<.35f&&unit.NoProgressFor<.6f||Time<unit.RouteRetryAt)return;
  if(unit.Target!=null){FindFiringPosition(unit,unit.Target.P);return;}
  unit.RouteRetryAt=Time+.65f+(unit.Id%5)*.04f;
  var blocker=Units.Where(u=>u!=unit&&u.Deployed&&!u.T.Building&&Dist(u.P,unit.P)<36).OrderBy(u=>Dist(u.P,unit.P)).FirstOrDefault();
  NudgeIdleFriend(unit,blocker);var destination=unit.Path.Last();PlanRoute(unit,destination,MineTraffic(unit,destination));
 }
}
}
