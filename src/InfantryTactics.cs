using System;
using System.Linq;
using System.Drawing;
namespace Greyhaven {public partial class Campaign {
 public const float StanceSeconds=1.1f;
 public static bool CombatInfantry(Piece p){return p!=null&&(p.T.Key=="squad"||p.T.Key=="at"||p.T.Key=="sniper");}
 internal bool ReadyToAimFire(Piece p){return !CombatInfantry(p)||p.Carrier!=null||p.WeaponAim>=.999f;}
 void TickWeaponAim(Piece p,float dt){
  if(!CombatInfantry(p))return;
  bool point=p.FirePoint.HasValue&&Dist(p.P,p.FirePoint.Value)<=ReferenceBalance.Range(p)&&Dist(p.P,p.FirePoint.Value)>=MinimumRange(p);
  bool target=p.Target!=null&&p.Target.Alive&&CanShootAt(p,p.Target)&&VisibleTo(p.Side,p.Target);
  bool hold=p.Path.Count==0&&VisualTime-p.FiredAt<.55f;
  bool aim=!ChangingStance(p)&&(target||point||hold);
  p.WeaponAim=Math.Max(0,Math.Min(1,p.WeaponAim+(aim?1:-1)*dt/.18f));
 }

 public static bool CanDeployInfantry(Piece p){return p!=null&&p.Deployed&&p.T.DeployableFoot;}
 public bool ToggleInfantry(Piece p){
  if(!CanDeployInfantry(p))return false;
  bool desired=!p.Entrenched;Halt(p);p.Entrenched=desired;p.ScanIn=0;
  if(p.Side==0)Radio=desired?(p.T.Key=="at"?"反装甲兵部署导弹阵地；D 收起，移动命令会先收起。":"步枪兵部署机枪阵地；D 收起，移动命令会先收起。"):"步兵收起阵地，恢复行进武器。";
  return true;
 }
 internal static void ReleaseInfantry(Piece p){p.Entrenched=false;p.BurstRemaining=0;p.BurstTarget=null;}
 internal static bool ChangingStance(Piece p){return p.T.DeployableFoot&&(p.Entrenched?p.Stance<1:p.Stance>0);}
 internal static bool Crushable(Piece p){return !(p.T.Key=="at"&&p.Entrenched&&p.Stance>=1);}
 void TickInfantryStance(Piece p,float dt){
  if(!p.T.DeployableFoot)return;
  if(ChangingStance(p)&&p.WeaponAim>.001f&&(p.Stance==0||p.Stance==1))return; // Lower the current weapon before reaching for equipment.
  p.Stance=Math.Max(0,Math.Min(1,p.Stance+(p.Entrenched?1:-1)*dt/StanceSeconds));
 }
 // Tiny armor coefficients in the source describe ineffective targets: do not
 // let snipers automatically waste their long cooldown on a tank or wall.
 internal static bool UsefulTarget(Piece p,Piece e){
  var w=ReferenceBalance.Weapon(p);return w!=null&&w.Verses[(int)e.T.Armor]>.02f;
 }
 void PrepareAiInfantry(Piece p,Piece e){
  if(p.Side<=0||!p.T.DeployableFoot||p.Entering!=null)return;
  float reach=p.T.Key=="at"?ReferenceBalance.WMissileLauncher.Range:ReferenceBalance.WPara.Range;
  float distance=TargetDistance(p,e);bool needed=p.T.Key=="squad"||!e.T.Foot;
  if(needed&&distance<=reach&&distance>=(p.T.Key=="at"?Tile:0)){
   if(!p.Entrenched){p.Entrenched=true;p.BurstRemaining=0;p.Path.Clear();}
  }else if(p.Entrenched)ReleaseInfantry(p);
 }
 void AiBunker(int side,AiBrain brain){
  if(Tutorial||AiCount(side,"barracks")==0||AiCount(side,"refinery")==0)return;
  var bunker=Live(side).FirstOrDefault(u=>u.T.Key=="bunker");
  if(bunker==null){
   if(Units.Any(u=>u.Alive&&u.Side==side&&u.T.Key=="bunker"))return;
   var job=Construction(side,1);var type=Info("bunker");
   if(job==null){if(AiCount(side,"post")<2||AiBudget(side,600)<type.Price)return;if(!AiPlanBuild(side,brain,"bunker",brain.Rally))return;job=Construction(side,1);}
   if(job!=null&&job.Type==type&&job.Ready){PointF site;if(AiBuildSite(side,type,brain.Rally,out site))PlaceConstruction(type,site,side);}
   return;
  }
  if(HouseCount(bunker)>=3)return;
  foreach(var foot in Live(side).Where(u=>u.T.Key=="squad"&&u.Entering==null&&u.Target==null&&Dist(u.P,bunker.P)<12*Tile).OrderBy(u=>Dist(u.P,bunker.P)).ToArray()){
   if(Units.Count(u=>u.Alive&&(u.Carrier==bunker||u.Entering==bunker))>=3)break;
   if(EnterHouse(foot,bunker))CancelAiMove(foot);
  }
 }
}}
