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
 static bool HostileTo(Piece unit,Piece target){return target.Side>=0&&target.Side!=unit.Side;}
 WeaponProfile ThreatWeapon(Piece target){
  var weapon=ReferenceBalance.Weapon(target);
  if(weapon!=null||!target.T.Garrison)return weapon;
  foreach(var occupant in Units)
   if(occupant.Alive&&occupant.Carrier==target&&ReferenceBalance.Weapon(occupant) is WeaponProfile profile)return profile;
  return null;
 }
 bool CanAutoTarget(Piece unit,Piece target){
  if(target.T.IsWall||!HostileTo(unit,target)||!Targetable(target)||!UsefulTarget(unit,target)||IsRelayStation(target))return false;
  // Player-controlled units passively acquire buildings that can fight.
  return unit.Side!=0||!target.T.Building||ThreatWeapon(target)!=null;
 }
 float TargetThreat(Piece unit,Piece target,WeaponProfile weapon){
  // YR 1.001's default threat coefficients, using this game's weapon and armor profiles.
  const float effectiveness=200, health=200, specialThreat=200, enemyHouseBonus=400, distancePenalty=1;
  var opposingWeapon=ThreatWeapon(target);
  float opposingEffect=opposingWeapon?.Verses[(int)unit.T.Armor]??0;
  if(target.Target==unit)opposingEffect=-opposingEffect;
  float healthRatio=target.Hp/target.T.Hp;
  float priority=target.T.RuleId is "ENGINEER" or "FV" or "AMCV" or "GASPYSAT"?specialThreat:0;
  if(unit.Side>=0&&brains[unit.Side]?.Opponent==target.Side)priority+=enemyHouseBonus;
  int distanceBeyondRange=Math.Max(0,(int)(Dist(unit.P,target.P)/Tile)-(int)(weapon.Range/Tile));
  return effectiveness*(weapon.Verses[(int)target.T.Armor]+opposingEffect)
   +health*healthRatio+priority-distancePenalty*distanceBeyondRange;
 }
 Piece FindContact(Piece o,float range){
  var weapon=ReferenceBalance.Weapon(o);
  var house=o.Carrier;
  float firingExtent=house!=null&&house.T.Garrison&&house.T.Key!="bunker"
   ?Math.Max(house.T.FoundationWidth,house.T.FoundationHeight)*Tile*.5f:0;
  Piece best=null;float bestScore=float.NegativeInfinity,bestDistance=float.MaxValue;
  foreach(var e in Units){
   float extent=firingExtent+(e.T.Building?Math.Max(e.T.FoundationWidth,e.T.FoundationHeight)*Tile*.5f:0);
   if(Math.Abs(e.P.X-o.P.X)>range+extent||Math.Abs(e.P.Y-o.P.Y)>range+extent)continue;
   float distance=WeaponDistance(o,e);
   if(distance>range||distance<weapon.MinimumRange||!VisibleTo(o.Side,e)||!GuardAllows(o,e)||!CanAutoTarget(o,e))continue;
   float score=TargetThreat(o,e,weapon);
   if(score>bestScore||score==bestScore&&distance<bestDistance){
    best=e;bestScore=score;bestDistance=distance;
   }
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
  if(!HostileTo(defender,attacker))return;
  if(defender.Path.Count>0&&!defender.Advancing&&!defender.GuardHome.HasValue)return;
  if(defender.GuardReturning||defender.GuardTravelling||!GuardAllows(defender,attacker))return;
  if(!defender.Deployed||defender.FirePoint.HasValue||defender.Contact==ContactOrder.Directed)return;
  if(defender.Side==0&&defender.Target!=null)return;
  var weapon=ReferenceBalance.Weapon(defender);
  if(weapon==null){
   if(!defender.Mining&&CloseEnoughToPursueCrush(defender,attacker))
    Move(defender,attacker.P,false,MineTraffic(defender,attacker.P));
   return;
  }
  if(weapon.Verses[(int)attacker.T.Armor]<=.01f)return;
  if((defender.T.Building||defender.Entrenched)&&TargetDistance(defender,attacker)>ReferenceBalance.Range(defender))return;
  if(defender.Target!=null&&Targetable(defender.Target)&&HostileTo(defender,defender.Target)&&
     TargetThreat(defender,defender.Target,weapon)>TargetThreat(defender,attacker,weapon))return;
  if(defender.Target==attacker){defender.LastContact=attacker.P;defender.Contact=ContactOrder.ReturnFire;return;}
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
 static bool AimWeapon(Piece o,PointF point,float dt){
  if(o.T.Key=="tank"){
   float heading=Bearing(o.P,point);
   RotateToward(ref o.TurretTurn,heading,dt);
   return o.TurretTurn==heading;
  }
  if(!o.T.Building)Face(o,point,dt);
  return true;
 }
 void MovingTankFire(Piece tank,float dt){
  var target=tank.MovingFireTarget;
  if(target!=null&&(!CanAutoTarget(tank,target)||!CanShootAt(tank,target)||!VisibleTo(tank.Side,target)))target=null;
  if(target==null&&tank.ScanIn<=0){
   tank.ScanIn=.18f;
   target=FindContact(tank,ReferenceBalance.Range(tank));
  }
  tank.MovingFireTarget=target;
  if(target==null){
   RotateToward(ref tank.TurretTurn,tank.Turn,dt);
   return;
  }
  if(AimWeapon(tank,TargetPoint(tank.P,target),dt)&&tank.Cool<=0)Fire(tank,target);
 }
 bool FightContact(Piece o,float dt){
  o.ScanIn-=dt;if(ChangingStance(o))return true;
  bool travelling=o.Target==null&&o.Path.Count>0&&!o.Advancing;
  if(travelling||o.GuardReturning){
   if(o.T.Key=="tank"){
    if(travelling&&!o.GuardReturning)MovingTankFire(o,dt);
    else RotateToward(ref o.TurretTurn,o.Turn,dt);
   }
   return false;
  }
  o.MovingFireTarget=null;
  var e=o.Target;
  if(e!=null){
   bool visible=VisibleTo(o.Side,e);
   if(!Targetable(e)||(!visible&&o.Contact!=ContactOrder.ReturnFire)||
      (o.Contact!=ContactOrder.Directed&&(!HostileTo(o,e)||!GuardAllows(o,e)))||
      (o.Contact==ContactOrder.Automatic&&(!CanAutoTarget(o,e)||TargetDistance(o,e)>ScanRange(o)))){EndContact(o);e=null;}
  }
  if(e==null&&!o.GuardReturning&&o.ScanIn<=0){
   o.ScanIn=.18f;var found=FindContact(o,ScanRange(o));
   if(found!=null){e=found;TakeContact(o,e,ContactOrder.Automatic);}
  }
  if(e==null){
   if(o.T.Key=="tank")RotateToward(ref o.TurretTurn,o.Turn,dt);
   if(o.Side>0&&o.Entrenched&&!o.GuardHome.HasValue)ReleaseInfantry(o);
   return false;
  }
  bool seen=VisibleTo(o.Side,e);
  if(seen){o.LastContact=e.P;PrepareAiInfantry(o,e);if(ChangingStance(o))return true;}
  if(seen&&PursueCrush(o,e))return false;
  if(seen&&CanShootAt(o,e)){
   o.Path.Clear();o.Replan=0;
   bool aimed=AimWeapon(o,o.T.Key=="tank"?TargetPoint(o.P,e):e.P,dt);
   if(o.Cool<=0&&aimed&&ReadyToAimFire(o))Fire(o,e);
   return true;
  }
  if(seen&&o.T.Key=="tank")AimWeapon(o,TargetPoint(o.P,e),dt);
  if(o.T.Building||o.Entrenched)return false;
  if(!seen&&Explored(o.Side,o.LastContact)){EndContact(o);return false;}
  if(o.Replan<=0){
   o.Replan=.6f;
   if(seen){if(ChaseRouteNeedsUpdate(o,e))FindFiringPosition(o,e.P);}
   else if(!Route(o,o.LastContact))ApproachPoint(o,o.LastContact);
  }
  return false;
 }
}
}
