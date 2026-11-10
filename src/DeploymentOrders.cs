using System;
using System.Linq;
using System.Drawing;
namespace Greyhaven {
public partial class Campaign {
 static bool Aligned(Piece o,PointF p){
  double target=Math.Atan2(p.X-o.P.X,p.Y-o.P.Y)*8/Math.PI;
  return Math.Abs((target-o.Turn+40)%16-8)<.04;
 }
 PointF TrackStep(Piece o,PointF waypoint,PointF next,float dt){
  if(Dist(o.P,waypoint)<.001f)return waypoint;
  if(o.ForceMoving&&o.T.Key=="tank")foreach(var victim in Units.Where(u=>u.Deployed&&u.Side!=o.Side&&u.T.Foot&&Crushable(u)&&TrafficContact(o.T,next,u.T,u.P)).ToArray())CombatKill(victim,o);
  var obstacle=Units.FirstOrDefault(u=>u!=o&&u.Deployed&&!u.T.Building&&TrafficContact(o.T,next,u.T,u.P)&&Dist(u.P,next)<Dist(u.P,o.P));
  if(obstacle==null){o.TrafficBlocked=0;return next;}
  o.TrafficBlocked+=dt;
  if(obstacle.Path.Count>0&&o.TrafficBlocked<.8f){
   var ahead=obstacle.Path.Peek();float dot=(ahead.X-obstacle.P.X)*(next.X-o.P.X)+(ahead.Y-obstacle.P.Y)*(next.Y-o.P.Y);
   if(dot>0)return o.P;
  }
  if(o.TrafficWait<=0){
   o.TrafficWait=.45f+(o.Id%4)*.07f;
   if(o.TrafficBlocked>.4f)NudgeIdleFriend(o,obstacle);
   if(o.Target!=null&&o.TrafficBlocked>.4f&&FindFiringPosition(o,o.Target.P))return o.P;
   if(o.TrafficBlocked>.6f&&!TrafficDetour(o,obstacle,waypoint)){
    // A stopped car can occupy an intermediate turn point. Retry the FINAL
    // destination around nearby traffic instead of repeatedly aiming at it.
    var destination=o.Path.Last();if(PlanRoute(o,destination,MineTraffic(o,destination)))o.TrafficBlocked=0;

   }
  }return o.P;
 }
 // Door and dock cells are operational destinations, not extra building foundation.
 public static RectangleF ExitApron(TypeInfo t,RectangleF lot){
  return UnloadingLane(lot);
 }
 public static RectangleF UnloadingLane(RectangleF lot){return new RectangleF(lot.Left+(float)Math.Floor(lot.Width/Tile/2)*Tile,lot.Bottom,Tile,Tile);}
 bool ParkingAllowed(int side,PointF p){return true;}
 public void Halt(Piece o){o.BurstRemaining=0;o.BurstTarget=null;o.Entering=null;ClearMission(o);o.Mining=false;o.Dock=null;o.DockReserved=false;o.DockTicket=0;o.DockRetryAfter=0;o.DockBlockedFor=0;o.Unload=0;o.Path.Clear();o.FirePoint=null;o.Target=null;o.Contact=ContactOrder.None;o.ResumeMarch=false;o.Formation=null;o.Advancing=false;}
 public bool Suppress(Piece o,PointF p){
  if(!o.Deployed||!Armed(o)||!Battlefield.Contains(p))return false;
  Halt(o);o.FirePoint=p;o.Replan=0;return true;
 }
 bool GroundFireTick(Piece o,float dt){
  if(ChangingStance(o))return true;var p=o.FirePoint.Value;if(Dist(o.P,p)>ReferenceBalance.Range(o)||Dist(o.P,p)<MinimumRange(o)){
   if(o.Replan<=0&&!o.T.Building&&!o.Entrenched){ApproachPoint(o,p);o.Replan=1.8f;}return false;
  }
  if(!o.T.Building)Face(o,p,dt);
  if(o.Cool<=0&&ReadyToAimFire(o)){
   var hit=Units.Where(e=>e!=o&&e.Deployed&&Dist(e.P,p)<(e.T.Building?Radius(e.T):14)).OrderBy(e=>Dist(e.P,p)).FirstOrDefault();
   if(hit!=null&&CanShootAt(o,hit))Fire(o,hit);else FireGround(o,p);
  }return true;
 }
}
}
