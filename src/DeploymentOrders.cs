using System;
using System.Linq;
using System.Drawing;
namespace Greyhaven {
public partial class Campaign {
 static bool Aligned(Piece o,PointF p){
  double target=Math.Atan2(p.X-o.P.X,p.Y-o.P.Y)*8/Math.PI;
  return Math.Abs((target-o.Turn+40)%16-8)<.04;
 }
 PointF TrackStep(Piece o,PointF waypoint,PointF next){
  if(Dist(o.P,waypoint)<.001f)return waypoint;
  var obstacle=Units.FirstOrDefault(u=>u!=o&&u.Deployed&&!u.T.Building&&Dist(u.P,next)<(Clearance(u)+Clearance(o))*.78f&&Dist(u.P,next)<Dist(u.P,o.P));
  if(obstacle==null)return next;
  if(o.TrafficWait<=0){
   o.TrafficWait=.65f;
   if(o.Mining){
    var destination=o.Path.Last();PlanRoute(o,destination,MineTraffic(o,destination));return o.P;
   }
   // Reserve the blocking vehicle's immediate envelope. Keep the starting
   // contact outside the temporary rectangle so a tank can reverse out.
   float gap=Math.Max(Math.Abs(o.P.X-obstacle.P.X),Math.Abs(o.P.Y-obstacle.P.Y));
   float pad=Math.Min(gap-.25f,Clearance(obstacle)+Clearance(o)+3);
   if(pad>1)PlanRoute(o,o.Path.Last(),new[]{new RectangleF(obstacle.P.X-pad,obstacle.P.Y-pad,2*pad,2*pad)});
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
 public void Halt(Piece o){o.Mining=false;o.Dock=null;o.Unload=0;o.Path.Clear();o.FirePoint=null;o.Target=null;o.Contact=ContactOrder.None;o.ResumeMarch=false;o.Formation=null;o.Advancing=false;}
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
   var hit=Live(1-o.Side).Where(e=>Dist(e.P,p)<(e.T.Building?Radius(e.T):14)).OrderBy(e=>Dist(e.P,p)).FirstOrDefault();
   if(hit!=null)Fire(o,hit);else{o.Cool=o.T.Reload;Shots.Add(new Shot{A=o.P,B=p,Side=o.Side,Life=.23f,Shell=o.T.Key=="arty"});}
  }return true;
 }
}
}
