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
  if(o.ForceMoving&&o.T.Key=="tank")foreach(var victim in Units.Where(u=>u.Deployed&&u.Side!=o.Side&&u.T.Foot&&Dist(u.P,next)<Clearance(o)+Clearance(u)).ToArray())Kill(victim);
  var obstacle=Units.FirstOrDefault(u=>u!=o&&u.Deployed&&!u.T.Building&&Dist(u.P,next)<(Clearance(u)+Clearance(o))*.78f&&Dist(u.P,next)<Dist(u.P,o.P));
  if(obstacle==null){o.TrafficBlocked=0;return next;}
  o.TrafficBlocked+=dt;
  if(obstacle.Path.Count>0&&o.TrafficBlocked<.8f){
   var ahead=obstacle.Path.Peek();float dot=(ahead.X-obstacle.P.X)*(next.X-o.P.X)+(ahead.Y-obstacle.P.Y)*(next.Y-o.P.Y);
   if(dot>0)return o.P;
  }
  if(o.TrafficWait<=0){
   o.TrafficWait=.45f+(o.Id%4)*.07f;
   if(!TrafficDetour(o,obstacle,waypoint)&&o.Mining&&o.TrafficBlocked>1.6f){
    var destination=o.Path.Last();PlanRoute(o,destination,MineTraffic(o,destination));o.TrafficBlocked=0;
   }
  }return o.P;
 }
 public static RectangleF Muster(RectangleF lot){var gate=Gate(lot);return new RectangleF(gate.X,gate.Bottom,gate.Width,2*Tile);}
 public static RectangleF UnloadingLane(RectangleF lot){return new RectangleF(lot.Left,lot.Bottom,lot.Width,2*Tile);}
 static RectangleF ReservedLot(TypeInfo t,RectangleF r){if(HasGate(t))r.Height+=Tile;if(t.Key=="barracks")r.Height+=2*Tile;if(t.Key=="refinery")r.Height+=Tile;return r;}
 bool ParkingAllowed(int side,PointF p){
  return !Units.Any(o=>o.Alive&&o.Side==side&&o.T.Key=="barracks"&&Muster(Footprint(o.T,o.P)).Contains(p));
 }
 bool BerthClear(Piece src,PointF p){return !Blocked(p,6,null)&&!Units.Any(u=>u.Deployed&&!u.T.Building&&Dist(u.P,p)<16);}
 PointF? FreeBerth(Piece src){
  var r=Muster(Footprint(src.T,src.P));
  for(int row=0;row<2;row++)for(int col=0;col<3;col++){
   var p=new PointF(r.X+(col+.5f)*Tile,r.Y+(1.5f-row)*Tile);
   if(!src.Berths.Contains(p)&&BerthClear(src,p))return p;
  }return null;
 }
 public void Halt(Piece o){ClearMission(o);o.Mining=false;o.Dock=null;o.DockReserved=false;o.DockTicket=0;o.DockRetryAfter=0;o.Unload=0;o.Path.Clear();o.FirePoint=null;o.Target=null;o.Contact=ContactOrder.None;o.ResumeMarch=false;o.Formation=null;o.Advancing=false;}
 public bool Suppress(Piece o,PointF p){
  if(!o.Deployed||o.T.Hit==0||p.X<0||p.Y<0||p.X>=Width||p.Y>=Height)return false;
  Halt(o);o.FirePoint=p;o.Replan=0;return true;
 }
 bool GroundFireTick(Piece o,float dt){
  var p=o.FirePoint.Value;if(Dist(o.P,p)>o.T.Range){
   if(o.Replan<=0&&!o.T.Building){ApproachPoint(o,p);o.Replan=1.8f;}return false;
  }
  if(!o.T.Building)Face(o,p,dt);
  if(o.Cool<=0){
   var hit=Units.Where(e=>e!=o&&e.Deployed&&Dist(e.P,p)<(e.T.Building?Radius(e.T):14)).OrderBy(e=>Dist(e.P,p)).FirstOrDefault();
   if(hit!=null)Fire(o,hit);else{o.Cool=o.T.Reload;Shots.Add(new Shot{A=o.P,B=p,Side=o.Side,Life=.23f,Shell=o.T.Key=="arty"});}
  }return true;
 }
}
}
