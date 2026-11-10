using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 public static PointF TargetPoint(PointF from,Piece target){
  if(!target.T.Building)return target.P;var r=Footprint(target.T,target.P);
  return new PointF(Math.Max(r.Left,Math.Min(r.Right,from.X)),Math.Max(r.Top,Math.Min(r.Bottom,from.Y)));
 }
 public static float TargetDistance(Piece unit,Piece target){return Dist(unit.P,TargetPoint(unit.P,target));}
 static bool Targetable(Piece e){return e.Alive&&e.Carrier==null&&(e.Ready>=1||e.T.Building);}
 Piece FindContact(Piece o,float range){
  Piece best=null;float bestDistance=float.MaxValue;
  foreach(var e in Units){
   if(e.Side<0||e.Side==o.Side||!Targetable(e)||!UsefulTarget(o,e)||IsRelayStation(e))continue;
   float extent=e.T.Building?Math.Max(e.T.FoundationWidth,e.T.FoundationHeight)*Tile*.5f:0;
   if(Math.Abs(e.P.X-o.P.X)>range+extent||Math.Abs(e.P.Y-o.P.Y)>range+extent)continue;
   float d=TargetDistance(o,e);if(d>range||d<MinimumRange(o)||!VisibleTo(o.Side,e)||!GuardAllows(o,e))continue;
   bool armed=e.Deployed&&Armed(e);
   bool bestArmed=best!=null&&best.Deployed&&Armed(best);
   bool inRange=d<=ReferenceBalance.Range(o),bestInRange=bestDistance<=ReferenceBalance.Range(o);
   if(best==null||inRange&&!bestInRange||inRange==bestInRange&&((armed&&!bestArmed)||(armed==bestArmed&&d<bestDistance))){best=e;bestDistance=d;}
  }
  return best;
 }
 void TakeContact(Piece o,Piece e,ContactOrder kind){
  if(o.Target==null)o.ResumeMarch=o.Path.Count!=0;
  o.Contact=kind;o.Target=e;o.LastContact=e.P;o.Replan=0;o.Path.Clear();
 }
 void EndContact(Piece o){
  o.Target=null;o.Contact=ContactOrder.None;o.Path.Clear();
  ReturnToMission(o);
  o.ResumeMarch=false;
 }
 void OnIncomingFire(Piece defender,Piece attacker){
  if(defender.Path.Count>0&&!defender.Advancing&&!defender.GuardHome.HasValue)return;
  if(defender.GuardReturning||defender.GuardTravelling||!GuardAllows(defender,attacker))return;
  if(!defender.Deployed||!Armed(defender)||defender.FirePoint.HasValue||defender.Contact==ContactOrder.Directed)return;
  if((defender.T.Building||defender.Entrenched)&&TargetDistance(defender,attacker)>ReferenceBalance.Range(defender))return;
  if(defender.Target==attacker){defender.LastContact=attacker.P;defender.Contact=ContactOrder.ReturnFire;return;}
  if(defender.Contact==ContactOrder.ReturnFire&&defender.Target!=null&&Targetable(defender.Target)&&VisibleTo(defender.Side,defender.Target)&&TargetDistance(defender,defender.Target)<=ReferenceBalance.Range(defender))return;
  TakeContact(defender,attacker,ContactOrder.ReturnFire);
 }
 // Reuse a valid long detour when a distant target shifts by a few pixels.
 // Close-range firing and genuinely blocked paths keep their original checks.
 bool ChaseRouteNeedsUpdate(Piece unit,Piece target){
  if(unit.Path.Count==0||unit.StalledFor>.4f||unit.NoProgressFor>.6f)return true;
  float drift=Dist(unit.Path.Last(),TargetPoint(unit.Path.Last(),target));if(drift<=ReferenceBalance.Range(unit)&&drift>=MinimumRange(unit))return false;
  float distance=0,limit=Math.Max(12*Tile,ReferenceBalance.Range(unit)*4);var from=unit.P;
  foreach(var point in unit.Path){distance+=Dist(from,point);from=point;if(distance>limit)break;}
  return drift<MinimumRange(unit)||drift>ReferenceBalance.Range(unit)+(distance>limit?4*Tile:0);
 }
 bool FightContact(Piece o,float dt){
  o.ScanIn-=dt;if(ChangingStance(o))return true;
  bool travelling=o.Target==null&&o.Path.Count>0&&!o.Advancing;
  if(travelling||o.GuardReturning){
   // Grizzly's OpportunityFire permits a shot without cancelling movement.
   // Infantry and artillery keep walking; an explicit attack-move stops to fight.
   if(o.T.Key=="tank"&&!o.GuardReturning&&o.ScanIn<=0){
    o.ScanIn=.18f;var opportunity=FindContact(o,ReferenceBalance.Range(o));
    if(opportunity!=null&&o.Cool<=0)Fire(o,opportunity);
   }
   return false;
  }
  var e=o.Target;
  if(e!=null){
   bool visible=VisibleTo(o.Side,e);
   if(!Targetable(e)||(!visible&&o.Contact!=ContactOrder.ReturnFire)||
      (o.Contact!=ContactOrder.Directed&&(!GuardAllows(o,e)||o.Contact==ContactOrder.Automatic&&TargetDistance(o,e)>ScanRange(o)))){EndContact(o);e=null;}
  }
  if(!o.GuardReturning&&o.ScanIn<=0&&(e==null||o.Contact==ContactOrder.Automatic&&TargetDistance(o,e)>ReferenceBalance.Range(o))){
   o.ScanIn=.18f;var found=FindContact(o,ScanRange(o));
   if(found!=null&&(e==null||TargetDistance(o,found)<=ReferenceBalance.Range(o))){e=found;TakeContact(o,e,ContactOrder.Automatic);}
  }
  if(e==null){if(o.Side>0&&o.Entrenched&&!o.GuardHome.HasValue)ReleaseInfantry(o);return false;}
  bool seen=VisibleTo(o.Side,e);
  if(seen){o.LastContact=e.P;PrepareAiInfantry(o,e);if(ChangingStance(o))return true;}
  if(seen&&CanShootAt(o,e)){
   o.Path.Clear();o.Replan=0;
   if(!o.T.Building)Face(o,e.P,dt);
   if(o.Cool<=0&&ReadyToAimFire(o)&&(o.T.Key!="tank"||Aligned(o,e.P)))Fire(o,e);
   return true;
  }
  if(o.T.Building||o.Entrenched)return false;
  if(!seen&&Explored(o.Side,o.LastContact)){EndContact(o);return false;}
  if(o.Replan<=0){
   o.Replan=1;
   if(seen){if(ChaseRouteNeedsUpdate(o,e))FindFiringPosition(o,e.P);}
   else if(!Route(o,o.LastContact))ApproachPoint(o,o.LastContact);
  }
  return false;
 }
}
}
