using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 sealed class AiWing {public Piece[] Units;public int Opponent;public PointF Goal;public float OrderedAt;public bool Raid;}
 sealed class AiBrain {
  public float NextPush,NextScout,NextRaid;public int Opponent=-1,Orders,Launches,Scouts,Raids;public PointF Rally,BuildPoint;
  public readonly float[] LastDispatch=Enumerable.Repeat(-1000f,MaxFactions).ToArray();
  public float NextPlacementTry;public string PlacementFailure;
  public string Building;public readonly float[] Attacks=Enumerable.Repeat(-1000f,MaxFactions).ToArray();
  public readonly List<AiWing> Wings=new List<AiWing>();public readonly Dictionary<Piece,float> Evading=new Dictionary<Piece,float>();
 }
 readonly AiBrain[] brains=new AiBrain[MaxFactions];
 AiBrain Brain(int side){
  if(brains[side]==null)brains[side]=new AiBrain{NextPush=MatchTempo.FirstPush+side*3,NextScout=18+side*2,NextRaid=MatchTempo.FirstRaid+side*3,Rally=AiOpenPoint(new PointF(Home(side).X+100,Home(side).Y+180))};
  return brains[side];
 }
 public static string FactionName(int side){return side==0?"我方":"敌方"+(side>=1&&side<=5?new[]{"一","二","三","四","五"}[side-1]:side.ToString());}
 int AiCount(int side,string key){return Units.Count(u=>u.Side==side&&u.Alive&&u.T.Key==key);}
 int AiPending(int side,string key){return Live(side).Sum(u=>u.Queue.Count(t=>t.Key==key));}
 Piece[] AiKnown(int side){return Units.Where(u=>u.Alive&&u.Side>=0&&u.Side!=side&&u.Carrier==null&&VisibleTo(side,u)).ToArray();}
 PointF AiOpenPoint(PointF target){
  if(!Blocked(target,9,null))return target;
  for(int ring=1;ring<=22;ring++)for(int i=0;i<16;i++){double angle=i*Math.PI/8;var p=new PointF(target.X+(float)Math.Cos(angle)*ring*Tile,target.Y+(float)Math.Sin(angle)*ring*Tile);if(!Blocked(p,9,null))return p;}
  return target;
 }
 void RememberAiAttack(Piece victim,Piece source){
  if(River==null||victim.Side<=0||source.Side<0||source.Side==victim.Side)return;
  Brain(victim.Side).Attacks[source.Side]=Time;
 }
 bool AiBuildSite(int side,TypeInfo type,PointF preferred,out PointF result){
  var anchors=Live(side).Where(p=>p.T.Building&&!p.T.Civilian&&p.T.Key!="post").ToArray();var candidates=new List<PointF>{preferred};
  foreach(var anchor in anchors){var r=Footprint(anchor.T,anchor.P);float w=(r.Width+type.FoundationWidth*Tile)/2,h=(r.Height+type.FoundationHeight*Tile)/2;
   for(int gap=0;gap<3;gap++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){if(x==0&&y==0)continue;candidates.Add(Middle(Footprint(type,new PointF(anchor.P.X+x*(w+gap*Tile),anchor.P.Y+y*(h+gap*Tile)))));}
  }
  var doors=anchors.Where(p=>HasGate(p.T)).Select(p=>ExitApron(p.T,Footprint(p.T,p.P))).ToArray();
  foreach(var p in candidates.Distinct().OrderBy(p=>Dist(p,preferred)).Take(256)){
   var body=Footprint(type,p);if(doors.Any(d=>d.IntersectsWith(body))||!CanPlace(type,p,null,side))continue;
   if(HasGate(type)){
    var exit=Middle(ExitApron(type,body));var exiting=Info(type.Key=="barracks"?"squad":type.Key=="refinery"?"miner":"tank");
    if(Blocked(exit,exiting.Clearance,null)||Units.Any(u=>u.Deployed&&!u.T.Building&&TrafficContact(exiting,exit,u.T,u.P)))continue;
   }
   result=p;return true;
  }
  result=PointF.Empty;return false;
 }
 void AiPlaceReady(int side,AiBrain brain){
  for(int category=0;category<2;category++){
   var job=Construction(side,category);if(job==null||!job.Ready)continue;PointF site=brain.BuildPoint;
   if(brain.Building!=job.Type.Key||!CanPlace(job.Type,site,null,side))if(!AiBuildSite(side,job.Type,Home(side),out site))continue;
   if(PlaceConstruction(job.Type,site,side))brain.Building=null;
  }
 }
 bool AiPlanBuild(int side,AiBrain brain,string key,PointF preferred){
  var type=Info(key);if(Construction(side,BuildCategory(type))!=null||Require(type,side)!=null||Funds[side]<Math.Min(180,type.Price)+40)return false;
  if(brain.PlacementFailure==key&&Time<brain.NextPlacementTry)return false;
  PointF site;if(!AiBuildSite(side,type,preferred,out site)){brain.PlacementFailure=key;brain.NextPlacementTry=Time+6;return false;}
  if(!StartConstruction(type,side))return false;
  brain.PlacementFailure=null;brain.Building=key;brain.BuildPoint=site;return true;
 }
 void AiConstruction(int side,AiBrain brain,Piece[] known){
  AiPlaceReady(side,brain);if(!Live(side).Any(p=>p.T.Key=="hq")||Construction(side,0)!=null)return;
  var home=Home(side);int power=Power(side),refineries=AiCount(side,"refinery"),factories=AiCount(side,"factory");
  if(AiCount(side,"power")==0||power<3){AiPlanBuild(side,brain,"power",new PointF(home.X+95,home.Y-70));return;}
  var ore=Ore.Where(p=>p.Stock>30&&Explored(side,p.P)).OrderBy(p=>Dist(p.P,home)).FirstOrDefault();
  var mine=ore==null?new PointF(home.X,home.Y+180):ore.P;
  if(refineries==0){AiPlanBuild(side,brain,"refinery",mine);return;}
  if(AiCount(side,"barracks")==0){AiPlanBuild(side,brain,"barracks",new PointF(home.X+75,home.Y+20));return;}
  if(factories==0&&AiCount(side,"barracks")>0){AiPlanBuild(side,brain,"factory",new PointF(home.X,home.Y+120));return;}
  if(factories==0)return;
  if(refineries<2&&Time>MatchTempo.SecondRefinery&&AiCount(side,"miner")>=2&&Funds[side]>=260){AiPlanBuild(side,brain,"refinery",mine);return;}
  int army=Units.Count(p=>p.Alive&&p.Side==side&&!p.T.Building&&p.T.Hit>0);
  if(factories<2&&Time>MatchTempo.SecondFactory&&army>=6&&Funds[side]>=450){
   if(power<8){AiPlanBuild(side,brain,"power",home);return;}AiPlanBuild(side,brain,"factory",brain.Rally);return;
  }
  if(AiCount(side,"barracks")<2&&Time>MatchTempo.SecondBarracks&&army>=8&&Funds[side]>=340){AiPlanBuild(side,brain,"barracks",brain.Rally);return;}
  if(AiCount(side,"radar")==0&&Time>230&&Funds[side]>=800){AiPlanBuild(side,brain,"radar",home);return;}
  if(AiCount(side,"radar")>0&&AiCount(side,"satellite")==0&&Time>480&&Funds[side]>=1800){
   if(power<9){AiPlanBuild(side,brain,"power",home);return;}AiPlanBuild(side,brain,"satellite",home);return;
  }
  var danger=known.FirstOrDefault(p=>p.T.Hit>0&&Dist(p.P,home)<700);
  if(danger!=null&&AiCount(side,"post")<2&&Funds[side]>500)AiPlanBuild(side,brain,"post",AiOpenPoint(new PointF((home.X*2+danger.P.X)/3,(home.Y*2+danger.P.Y)/3)));
 }
 int AiReserve(int side){
  if(Time>MatchTempo.SecondRefinery&&AiCount(side,"refinery")<2&&AiCount(side,"miner")>=2)return Info("refinery").Price+40;
  if(Time>MatchTempo.SecondFactory&&AiCount(side,"factory")<2&&Units.Count(p=>p.Alive&&p.Side==side&&p.T.Hit>0&&!p.T.Building)>=6)return Info("factory").Price+(Power(side)<8?Info("power").Price:0)+40;
  return Time<100?30:70;
 }
 int AiBudget(int side,int reserve){
  int promised=Live(side).Sum(p=>p.Queue.Sum(t=>t.Price)-p.WorkPaid);
  for(int i=0;i<2;i++){var job=Construction(side,i);if(job!=null)promised+=job.Type.Price-job.Paid;}
  return Funds[side]-promised-reserve;
 }
 string AiVehicle(int side,Piece[] known){
  int tanks=AiCount(side,"tank")+AiPending(side,"tank"),cars=AiCount(side,"ifv")+AiPending(side,"ifv"),guns=AiCount(side,"arty")+AiPending(side,"arty");
  int enemyFoot=known.Count(p=>p.T.Foot),enemyArmor=known.Count(p=>!p.T.Building&&!p.T.Foot&&p.T.Hit>0);
  float carRatio=enemyFoot>enemyArmor*2?.5f:.28f,gunRatio=side%5==3?.30f:.18f,tankRatio=1-carRatio-gunRatio;
  return cars/carRatio<=Math.Min(tanks/tankRatio,guns/gunRatio)?"ifv":guns/gunRatio<tanks/tankRatio?"arty":"tank";
 }
 string AiInfantry(int side,Piece[] known){
  bool house=Units.Any(p=>p.Alive&&p.T.Civilian&&p.Side<0&&Explored(side,p.P)&&Dist(p.P,Home(side))<1400);
  if(house&&AiCount(side,"engineer")+AiPending(side,"engineer")==0)return "engineer";
  int rifles=AiCount(side,"squad")+AiPending(side,"squad"),anti=AiCount(side,"at")+AiPending(side,"at");
  bool armor=known.Count(p=>p.T.Key=="tank"||p.T.Key=="ifv")>known.Count(p=>p.T.Foot);
  return anti<(rifles+anti+1)*(armor?.6f:.35f)?"at":"squad";
 }
 void AiRecruit(int side,AiBrain brain,Piece[] known){
  var facilities=Live(side).Where(p=>p.T.Key=="factory"||p.T.Key=="barracks").ToArray();
  int desiredMiners=Math.Min(4,Math.Max(2,AiCount(side,"refinery")*2));
  int miners=AiCount(side,"miner")+AiPending(side,"miner")+Units.Count(p=>p.Alive&&p.Side==side&&p.T.Key=="refinery"&&!p.StarterDelivered);
  if(miners<desiredMiners&&facilities.Any(p=>p.T.Key=="factory")&&AiBudget(side,40)>=Info("miner").Price){if(Train(Info("miner"),side,null))brain.Orders++;}
  int army=Units.Count(p=>p.Alive&&p.Side==side&&!p.T.Building&&p.T.Hit>0)+facilities.Sum(p=>p.Queue.Count(t=>t.Hit>0));
  int desiredArmy=Math.Min(IslandMission?42:48,12+(int)(Time/45)*4);
  foreach(var facility in facilities.OrderBy(p=>p.Queue.Count)){
   facility.Rally=brain.Rally;if(facility.Queue.Count>=2||facility.ProductionPaused||army>=desiredArmy)continue;
   var type=Info(facility.T.Key=="factory"?AiVehicle(side,known):AiInfantry(side,known));
   if(AiBudget(side,AiReserve(side))<type.Price||Require(type,side)!=null)continue;
   if(Train(type,side,facility)){brain.Orders++;if(type.Hit>0)army++;}
  }
 }
 void SmartEconomy(int side){
  if(HasSurrendered(side))return;var brain=Brain(side);var known=AiKnown(side);
  if(!Live(side).Any(p=>p.T.Key=="hq")){
   var mcv=Live(side).FirstOrDefault(p=>p.T.Key=="mcv");if(mcv!=null)DeployEnemyBase(mcv);
   else if(!Units.Any(p=>p.Alive&&p.Side==side&&(p.T.Key=="hq"||p.T.Key=="mcv")))EnemyRecoveryProduction(side);
  }
  AiConstruction(side,brain,known);AiRecruit(side,brain,known);
  foreach(var building in Live(side).Where(p=>p.T.Building&&!p.T.Civilian)){if(building.Hp<building.T.Hp*.65f&&Funds[side]>AiReserve(side)+80)building.Repairing=true;else if(Funds[side]<40)building.Repairing=false;}
  SmartTactics(side,false);
 }
 int AiOpponent(int side,AiBrain brain,Piece[] known){
  int best=-1;float bestScore=float.MinValue;
  for(int enemy=0;enemy<FactionCount;enemy++){
   if(enemy==side||Remaining(enemy)==0||HasSurrendered(enemy))continue;
   var seen=known.Where(p=>p.Side==enemy).ToArray();float score=-Dist(Home(side),Home(enemy))/Tile;
   if(Time-brain.Attacks[enemy]<65)score+=240;
   score+=seen.Count(p=>p.T.Key=="miner")*18+seen.Count(p=>p.T.Building)*5-seen.Count(p=>p.T.Hit>0)*3;
   if(enemy==brain.Opponent)score+=28;
   if(score>bestScore){best=enemy;bestScore=score;}
  }return best;
 }
 PointF AiObjective(int side,int opponent,Piece[] known){
  var candidates=known.Where(p=>p.Side==opponent).ToArray();
  var target=candidates.OrderBy(p=>Dist(p.P,Home(side))-(p.T.Key=="refinery"?400:p.T.Key=="miner"?330:p.T.Key=="factory"?260:p.T.Key=="hq"?200:0)).FirstOrDefault();
  return AiOpenPoint(target==null?Home(opponent):target.P);
 }
 void SmartTactics(int side,bool periodic){
  if(HasSurrendered(side))return;var brain=Brain(side);var known=AiKnown(side);var home=Home(side);
  var bases=Live(side).Where(p=>p.T.Building&&!p.T.Civilian).ToArray();
  var troops=Live(side).Where(p=>!p.T.Building&&p.T.Hit>0&&p.Entering==null).ToArray();
  var threat=known.Where(p=>p.T.Hit>0&&bases.Any(b=>Dist(b.P,p.P)<420)).OrderBy(p=>Dist(home,p.P)).FirstOrDefault();
  var defenders=new HashSet<Piece>();
  if(threat!=null){
   brain.Attacks[threat.Side]=Time;
   foreach(var p in troops.OrderBy(p=>Dist(p.P,threat.P)).Take(Math.Min(6,Math.Max(2,troops.Length/2)))){
    defenders.Add(p);if(p.Target!=threat)Attack(p,threat);
   }
  }
  foreach(var miner in Live(side).Where(p=>p.T.Key=="miner").ToArray()){
   var danger=known.FirstOrDefault(p=>p.T.Hit>0&&Dist(p.P,miner.P)<175);
   if(danger!=null){if(!brain.Evading.ContainsKey(miner)){Move(miner,AiOpenPoint(new PointF(home.X-75,home.Y+90)),false);}brain.Evading[miner]=Time;}
   else if(brain.Evading.ContainsKey(miner)&&Time-brain.Evading[miner]>12){MineOrder(miner,null,null);brain.Evading.Remove(miner);}
  }
  foreach(var retired in brain.Evading.Keys.Where(p=>!p.Alive).ToArray())brain.Evading.Remove(retired);
  foreach(var wing in brain.Wings.ToArray()){
   wing.Units=wing.Units.Where(p=>p.Deployed&&!defenders.Contains(p)).ToArray();
   if(wing.Units.Length==0){brain.Wings.Remove(wing);continue;}
   bool fighting=wing.Units.Any(p=>p.Target!=null&&p.Target.Alive&&Dist(p.P,p.Target.P)<Math.Max(p.T.Range+3*Tile,200));
   bool arrived=wing.Units.All(p=>!aiMovesPending.Contains(p)&&(p.Path.Count==0||Dist(p.P,wing.Goal)<220));
   if(Remaining(wing.Opponent)==0||HasSurrendered(wing.Opponent)||!fighting&&(arrived||Time-wing.OrderedAt>65)){
    foreach(var unit in wing.Units)if(unit.Target!=null&&unit.Target.Alive&&Dist(unit.P,unit.Target.P)>Math.Max(unit.T.Range+3*Tile,200))Halt(unit);
    int opponent=wing.Raid?AiRaidOpponent(side,brain):AiOpponent(side,brain,known);if(opponent<0)continue;wing.Opponent=opponent;wing.Goal=AiObjective(side,opponent,known);wing.OrderedAt=Time;brain.LastDispatch[opponent]=Time;
    QueueAiMarch(wing.Units,wing.Goal);
   }
  }
  var assigned=new HashSet<Piece>(brain.Wings.SelectMany(w=>w.Units));
  var available=troops.Where(p=>!defenders.Contains(p)&&!assigned.Contains(p)&&p.Target==null).ToArray();
  int enemySide=AiOpponent(side,brain,known);brain.Opponent=enemySide;if(enemySide<0)return;
  int minimum=Time<180?3:5;bool waited=Time>=brain.NextPush+MatchTempo.UnderfilledWait&&available.Length>=2;
  if(Time>=brain.NextPush&&(available.Length>=minimum||waited)){
   var batch=available.OrderBy(p=>Dist(p.P,brain.Rally)).Take(8).ToArray();
   if(LaunchAiWing(side,brain,batch,enemySide,known,false))brain.NextPush=Time+MatchTempo.PushInterval+(side%3)*3;
  }else if(Time>=brain.NextScout&&available.Length>0&&threat==null){
   var scout=available.Where(p=>p.T.Key=="ifv"||p.T.Key=="squad").OrderBy(p=>p.T.Key=="ifv"?0:1).FirstOrDefault();
   if(scout!=null&&scout.Path.Count==0&&Move(scout,AiOpenPoint(Home(enemySide)),true)){brain.Scouts++;brain.NextScout=Time+35;}
  }
  assigned=new HashSet<Piece>(brain.Wings.SelectMany(w=>w.Units));
  AiPressureRaid(side,brain,troops.Where(p=>!defenders.Contains(p)&&!assigned.Contains(p)&&p.Target==null).ToArray(),known,threat!=null);
 }
 void SmartHousing(int side){
  var brain=Brain(side);var known=AiKnown(side);
  foreach(var engineer in Live(side).Where(p=>p.T.Key=="engineer"&&p.Entering==null).ToArray()){
   var house=Units.Where(h=>h.Alive&&h.T.Civilian&&h.Side<0&&!known.Any(e=>e.T.Hit>0&&Dist(e.P,h.P)<220))
    .OrderBy(h=>Dist(h.P,engineer.P)).FirstOrDefault();if(house!=null)EnterHouse(engineer,house);
  }
  int garrison=Units.Count(p=>p.Alive&&p.Side==side&&p.Carrier!=null&&p.Carrier.T.Civilian);
  int housingLimit=Time<180?2:4;if(garrison>=housingLimit)return;var assigned=new HashSet<Piece>(brain.Wings.SelectMany(w=>w.Units));
  foreach(var foot in Live(side).Where(p=>p.T.Foot&&p.T.Hit>0&&p.Entering==null&&p.Target==null&&!assigned.Contains(p)).Take(2).ToArray()){
   var house=Live(side).Where(h=>h.T.Civilian&&Dist(h.P,foot.P)<420&&HouseCount(h)<Math.Min(2,HouseCapacity(h))).OrderBy(h=>Dist(h.P,Home(side))).FirstOrDefault();
   if(house!=null&&garrison++<housingLimit)EnterHouse(foot,house);
  }
 }
 public string AiSummary(int side){var b=Brain(side);return FactionName(side)+" cash="+Funds[side]+" miners="+AiCount(side,"miner")+" refineries="+AiCount(side,"refinery")+" factories="+AiCount(side,"factory")+" orders="+b.Orders+" squads="+b.Launches+" raids="+b.Raids+" target="+b.Opponent;}
}
}
