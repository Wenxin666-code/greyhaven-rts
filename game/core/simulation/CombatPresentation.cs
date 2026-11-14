using System;
using System.Drawing;
namespace Greyhaven {
public partial class Campaign {
 public float VisualTime,OutcomeAt=-100;
 int visualShotId;
 public const float BuildingDestructionSeconds=2.4f;
 public bool OutcomeReady {get{return (Won||Lost)&&VisualTime-OutcomeAt>=BuildingDestructionSeconds+.25f;}}
 public void TickVisuals(float dt){
  VisualTime+=dt;Shots.ForEach(s=>s.Life-=dt);Shots.RemoveAll(s=>s.Life<=0);
  foreach(var o in Units)if(!o.Alive)o.Dead+=dt;
  Units.RemoveAll(o=>!o.Alive&&o.Dead>24&&!Units.Exists(u=>u.Alive&&u.Carrier==o));
 }
 void RecordShot(Piece o,PointF point,Piece target){
  o.FiredAt=VisualTime;o.LastFirePoint=point;
  Shots.Add(new Shot{A=o.P,B=point,Side=o.Side,Life=1.15f,Born=VisualTime,Heading=o.Turn,TurretHeading=o.TurretTurn,
   Weapon=o.T.Key,Rocket=ReferenceBalance.Weapon(o)!=null&&(ReferenceBalance.Weapon(o).RuleId.Contains("Missile")||ReferenceBalance.Weapon(o).RuleId.Contains("Hover")),Id=++visualShotId,SourceId=o.Id,TargetId=target==null?0:target.Id,Shell=o.T.Key=="arty",HitFoot=target!=null&&target.T.Foot,
   Stance=o.Stance,Aim=o.WeaponAim,ImpactLift=target==null?0:target.T.Building?9:target.T.Foot?8:7});
  // Feedback is cosmetic: no RNG, ballistic delay, damage or fire-rate changes.
  if(Shots.Count>256)Shots.RemoveRange(0,Shots.Count-256);
 }
}
}
