using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 internal static float MinimumRange(Piece p){var w=ReferenceBalance.Weapon(p);return w==null?0:w.MinimumRange;}
 internal static float WeaponDistance(Piece p,Piece target){return p.Carrier!=null&&p.Carrier.T.Key=="bunker"?Dist(p.Carrier.P,TargetPoint(p.Carrier.P,target)):p.Carrier!=null&&p.Carrier.T.Garrison?Dist(TargetPoint(target.P,p.Carrier),TargetPoint(p.Carrier.P,target)):TargetDistance(p,target);}
 internal static bool CanShootAt(Piece p,Piece target){var w=ReferenceBalance.Weapon(p);float d=WeaponDistance(p,target);return !ChangingStance(p)&&w!=null&&d<=w.Range+.001f&&d>=w.MinimumRange;}
 void Fire(Piece source,Piece target){BeginVolley(source,target,TargetPoint(source.P,target));}
 void FireGround(Piece source,PointF point){BeginVolley(source,null,point);}
 void BeginVolley(Piece source,Piece target,PointF point){
  var weapon=ReferenceBalance.Weapon(source);if(weapon==null||RepairVehicle(source)||ChangingStance(source))return;
  source.Cool=ReferenceBalance.Reload(source,weapon);source.BurstTarget=target;source.BurstPoint=point;source.BurstWeapon=weapon;
  source.BurstRemaining=weapon.Burst-1;source.BurstAt=ReferenceBalance.BurstSpacing;
  Impact(source,target,point,weapon);
 }
 void TickBurst(Piece source,float dt){
  if(source.BurstRemaining<=0)return;
  if(!Armed(source)||source.BurstTarget!=null&&(!Targetable(source.BurstTarget)||!CanShootAt(source,source.BurstTarget))){source.BurstRemaining=0;return;}
  source.BurstAt-=dt;
  while(source.BurstAt<=.0001f&&source.BurstRemaining>0){
   var target=source.BurstTarget;
   if(target!=null&&!target.Alive){source.BurstRemaining=0;break;}
   var point=target==null?source.BurstPoint:TargetPoint(source.P,target);
   if(source.T.Key=="tank"&&!AimWeapon(source,point,dt))return;
   Impact(source,target,point,source.BurstWeapon);source.BurstRemaining--;source.BurstAt+=ReferenceBalance.BurstSpacing;
  }
 }
 void Impact(Piece source,Piece target,PointF point,WeaponProfile weapon){
  Piece intercepted=null;
  if(weapon.BlockedByWalls){
   var origin=source.Carrier?.P??source.P;
   intercepted=FirstWall(origin,target?.T.IsWall==true?target.P:point,out var impact);
   if(intercepted!=null){target=intercepted;point=impact;}
  }
  RecordShot(source,point,target);
  if(target!=null)DamageTarget(source,target,ReferenceBalance.Damage(source,target,weapon));
  if(weapon.Spread<=0||intercepted!=null)return;
  // Small warhead spread, same armor table as direct impact. No duplicate damage
  // to the primary target; carried infantry are protected by their carrier.
  foreach(var other in Units.Where(u=>u!=source&&u!=target&&Targetable(u)&&Dist(point,TargetPoint(point,u))<weapon.Spread).ToArray()){
   float distance=Dist(point,TargetPoint(point,other));
   DamageTarget(source,other,ReferenceBalance.Damage(source,other,weapon)*(1-.5f*distance/weapon.Spread));
  }
 }
 void DamageTarget(Piece source,Piece target,float amount){
  if(!target.Alive||amount<=0||BasinOperation?.IsStoryTruck(target)==true)return;
  if(!target.T.IsWall){if(target.Side==0)ReportStrike(target);RememberAiAttack(target,source);}
  target.HitAt=VisualTime;target.Hp-=amount;
  if(target.Hp<=0)CombatKill(target,source);
  else OnIncomingFire(target,source.Carrier!=null&&source.Carrier.T.Garrison?source.Carrier:source);
 }
}
}
