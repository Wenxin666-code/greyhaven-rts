using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
namespace Greyhaven {
public partial class Campaign {
 // Commands own their destination. Opportunistic fire must not overwrite it.
 static void ClearMission(Piece o){o.OreSearchToken++;o.OreSearching=false;o.RallySource=null;o.RallySlot=0;o.PendingDeploy=false;o.SupportTarget=null;o.SupportAutomatic=false;o.SupportScanAt=0;o.Entering=null;o.RouteRetryAt=0;o.NoProgressFor=0;o.StalledFor=0;o.DepartingDock=null;o.DockAccessAt=0;
  o.GuardHome=null;o.Escort=null;o.GuardTravelling=false;o.GuardReturning=false;
  o.MovingFireTarget=null;
  o.Waypoints.Clear();o.WaypointIndex=0;o.PatrolStart=0;o.Patrol=false;o.ForceMoving=false;
 }
 public static float ScanRange(Piece o){
  if(o.T.Building||o.Entrenched)return ReferenceBalance.Range(o);
  return o.GuardHome.HasValue||o.Advancing?ReferenceBalance.LockRange(o):ReferenceBalance.Range(o);
 }
 public void Guard(Piece o){
  if(!o.Deployed)return;Halt(o);o.GuardHome=o.P;o.ScanIn=0;
 }
 public bool GuardAt(Piece o,PointF p){
  if(!Move(o,p,false))return false;o.GuardHome=p;o.GuardTravelling=true;return true;
 }
 public bool EscortUnit(Piece o,Piece friend){
  if(!o.Deployed||o.T.Building||friend==null||!friend.Alive||friend.Side!=o.Side||friend==o)return false;
  Halt(o);ReleaseInfantry(o);o.Escort=friend;
  double angle=o.Id*Math.PI*.763932;float radius=friend.T.Building?Radius(friend.T)+38:45;
  o.EscortOffset=new PointF((float)Math.Cos(angle)*radius,(float)Math.Sin(angle)*radius);
  o.GuardHome=new PointF(friend.P.X+o.EscortOffset.X,friend.P.Y+o.EscortOffset.Y);o.Replan=0;return true;
 }
 public bool ForceMove(Piece o,PointF p){if(!Move(o,p,false))return false;o.ForceMoving=true;return true;}
 public void Scatter(Piece[] troops){
  if(troops.Length==0)return;var center=new PointF(troops.Average(u=>u.P.X),troops.Average(u=>u.P.Y));
  foreach(var u in troops){
   double angle=Dist(center,u.P)<1?u.Id*2.4:Math.Atan2(u.P.Y-center.Y,u.P.X-center.X);
   for(int i=0;i<8;i++){double a=angle+(i%2==0?1:-1)*(i/2)*Math.PI/4;
    if(Move(u,new PointF(u.P.X+(float)Math.Cos(a)*48,u.P.Y+(float)Math.Sin(a)*48),false))break;}
  }
 }
 public bool SetWaypoints(Piece o,PointF[] points,bool patrol,int loopStart=0){
  if(points.Length==0||!Move(o,points[0],true))return false;
  o.Waypoints.AddRange(points);o.Patrol=patrol&&points.Length>1;o.PatrolStart=Math.Max(0,Math.Min(points.Length-1,loopStart));return true;
 }
 void MissionTick(Piece o){
  if(o.RallySource!=null&&o.Path.Count==0&&o.Target==null)ContinueRally(o);
  if(o.Escort!=null){
   if(!o.Escort.Alive||o.Escort.Carrier!=null){o.Escort=null;o.GuardHome=o.P;}
   else o.GuardHome=new PointF(o.Escort.P.X+o.EscortOffset.X,o.Escort.P.Y+o.EscortOffset.Y);
  }
  if(o.Target!=null||o.FirePoint.HasValue)return;
  if(o.GuardHome.HasValue&&!o.T.Building){
   var home=o.GuardHome.Value;
   if(o.Path.Count==0&&Dist(o.P,home)<12){o.GuardTravelling=false;o.GuardReturning=false;}
   else if(o.Replan<=0&&(o.GuardTravelling||o.GuardReturning||o.Escort!=null&&Dist(o.P,home)>36)){
    // A blocked anchor should not produce an A* search every frame.
    o.Replan=1;Route(o,home);
   }
  }
  if(o.Path.Count==0&&o.Waypoints.Count>0&&Dist(o.P,o.Goal)<12){
   int next=o.WaypointIndex+1;
   if(next>=o.Waypoints.Count){if(!o.Patrol){o.Waypoints.Clear();o.Advancing=false;return;}next=o.PatrolStart;}
   if(o.Replan<=0){o.Replan=.7f;if(Route(o,o.Waypoints[next])){o.WaypointIndex=next;o.Goal=o.Waypoints[next];}}
  }
  if(o.Path.Count==0)o.ForceMoving=false;
 }
 bool GuardAllows(Piece o,Piece target){
  return !o.GuardHome.HasValue||Dist(o.GuardHome.Value,TargetPoint(o.GuardHome.Value,target))<=ReferenceBalance.LockRange(o);
 }
 void ReturnToMission(Piece o){
  if(o.Entrenched)return;
  if(o.GuardHome.HasValue&&!o.T.Building){o.GuardReturning=true;o.Replan=0;Route(o,o.GuardHome.Value);}
  else if(o.ResumeMarch)Route(o,o.Goal);
 }
}
}
