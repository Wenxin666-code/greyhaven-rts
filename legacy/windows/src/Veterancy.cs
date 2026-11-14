using System;

namespace Greyhaven {
public partial class Campaign {
 double selfHealFramePhase;int selfHealSteps;

 // One shared frame clock; rendering frequency and match speed do not change
 // healing per simulation second, and paused/carried units accrue no catch-up.
 void AdvanceSelfHealClock(float dt){
  selfHealFramePhase+=dt*ReferenceBalance.FramesPerGameSecond;
  selfHealSteps=(int)Math.Floor((selfHealFramePhase+1e-7)/ReferenceBalance.SelfHealFrames);
  selfHealFramePhase-=selfHealSteps*ReferenceBalance.SelfHealFrames;
 }
 void TickSelfHealing(Piece unit){
  if(selfHealSteps<=0||!unit.Deployed||unit.T.Building||unit.Hp>=unit.T.Hp)return;
  if(unit.T.SelfHealing||ReferenceBalance.HasAbility(unit,VeteranAbility.SelfHeal))
   unit.Hp=Math.Min(unit.T.Hp,unit.Hp+selfHealSteps*ReferenceBalance.SelfHealAmount);
 }
 void AwardKillExperience(Piece killer,Piece victim){
  if(killer==null||!killer.T.Trainable||killer.T.Price<=0||killer.Side<0||victim.Side<0||killer.Side==victim.Side||victim.T.Price<=0)return;
  killer.Experience=Math.Min(ReferenceBalance.VeteranCap,killer.Experience+victim.T.Price/(killer.T.Price*ReferenceBalance.VeteranRatio));
 }
 void CombatKill(Piece victim,Piece killer){
  AwardKillExperience(killer,victim);
  // Transport passengers die with the vehicle. House occupants evacuate and
  // must not award experience until they are actually killed.
  if(!victim.T.Civilian&&victim.Passenger!=null&&victim.Passenger.Alive)AwardKillExperience(killer,victim.Passenger);
  Kill(victim);
 }
}
}
