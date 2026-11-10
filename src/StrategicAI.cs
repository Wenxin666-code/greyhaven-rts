using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 sealed class AiWing {public Piece[] Units;public int Opponent;public PointF Goal;public float OrderedAt,InitialValue;public int Phase;public PointF Objective;public bool Raid;}
 sealed class AiBrain {
 public readonly HashSet<Piece> FieldGuard=new HashSet<Piece>();public PointF FieldPoint;public float FieldOrderedAt,NextFieldAttempt;public int FieldInitial,FieldDeployments;public bool FieldReturning;
 public float NextPush,NextRaid;public int Opponent=-1,Orders,Launches,Raids,Withdrawals;public readonly HashSet<Piece> HomeGuard=new HashSet<Piece>();public PointF Rally;public readonly PointF[] BuildPoints=new PointF[2];
  public readonly float[] LastDispatch=Enumerable.Repeat(-1000f,MaxFactions).ToArray();
  public float NextPlacementTry;public string PlacementFailure;
  public readonly string[] Buildings=new string[2];public readonly float[] Attacks=Enumerable.Repeat(-1000f,MaxFactions).ToArray();
  public readonly List<AiWing> Wings=new List<AiWing>();public readonly Dictionary<Piece,float> Evading=new Dictionary<Piece,float>();public readonly Dictionary<Piece,PointF> GuardPosts=new Dictionary<Piece,PointF>();
 }
 readonly AiBrain[] brains=new AiBrain[MaxFactions];
 AiBrain Brain(int side){
  if(brains[side]==null)brains[side]=new AiBrain{NextPush=MatchTempo.FirstPush+side*3,NextRaid=MatchTempo.FirstRaid+side*3,Rally=AiOpenPoint(new PointF(Home(side).X+100,Home(side).Y+180))};
  return brains[side];
 }
 public static string FactionName(int side){return side==0?"我方":"敌方"+(side>=1&&side<=5?new[]{"一","二","三","四","五"}[side-1]:side.ToString());}
 int AiCount(int side,string key){return Units.Count(u=>u.Side==side&&u.Alive&&u.T.Key==key);}
 int AiPending(int side,string key){return Live(side).Sum(u=>u.Queue.Count(t=>t.Key==key));}
 Piece[] AiKnown(int side){return Units.Where(u=>u.Alive&&u.Side>=0&&u.Side!=side&&u.Carrier==null&&!IsRelayStation(u)&&VisibleTo(side,u)).ToArray();}
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
  var anchors=Live(side).Where(p=>p.T.Building&&!p.T.Civilian&&!p.T.Defense&&!IsRelayStation(p)).ToArray();var candidates=new List<PointF>{preferred};
  foreach(var anchor in anchors){var r=Footprint(anchor.T,anchor.P);float w=(r.Width+type.FoundationWidth*Tile)/2,h=(r.Height+type.FoundationHeight*Tile)/2;
   for(int gap=0;gap<=BuildAdjacentCells(type);gap++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){if(x==0&&y==0)continue;candidates.Add(Middle(Footprint(type,new PointF(anchor.P.X+x*(w+gap*Tile),anchor.P.Y+y*(h+gap*Tile)))));}
  }
  var doors=anchors.Where(p=>HasGate(p.T)).Select(p=>ExitApron(p.T,Footprint(p.T,p.P))).ToArray();
  foreach(var p in candidates.Distinct().OrderBy(p=>type.Key=="post"?AiDefenseSiteScore(side,p,preferred):Dist(p,preferred)).Take(256)){
   var body=Footprint(type,p);if(type.Key=="post"&&!AiDefenseGround(body))continue;if(doors.Any(d=>d.IntersectsWith(body))||!AiKeepsHaulLanes(side,type,body)||!CanPlace(type,p,null,side))continue;
   if(HasGate(type)||type.Key=="bunker"){
    var exit=Middle(ExitApron(type,body));var exiting=Info(type.Key=="barracks"||type.Key=="bunker"?"squad":type.Key=="refinery"?"miner":"tank");
    if(Blocked(exit,exiting.Clearance,null)||Units.Any(u=>u.Deployed&&!u.T.Building&&TrafficContact(exiting,exit,u.T,u.P)))continue;
   }
   result=p;return true;
  }
  result=PointF.Empty;return false;
 }
 void AiPlaceReady(int side,AiBrain brain,int firstCategory=0){
  for(int category=firstCategory;category<2;category++){
   var job=Construction(side,category);if(job==null||!job.Ready)continue;PointF site=brain.BuildPoints[category];
   if(brain.Buildings[category]!=job.Type.Key||!CanPlace(job.Type,site,null,side))if(!AiBuildSite(side,job.Type,brain.Buildings[category]==job.Type.Key?site:Home(side),out site))continue;
   if(PlaceConstruction(job.Type,site,side))brain.Buildings[category]=null;
  }
 }
 bool AiPlanBuild(int side,AiBrain brain,string key,PointF preferred){
  var type=Info(key);if(Construction(side,BuildCategory(type))!=null||Require(type,side)!=null||Funds[side]<Math.Min(900,type.Price)+200)return false;
  if(brain.PlacementFailure==key&&Time<brain.NextPlacementTry)return false;
  PointF site;if(!AiBuildSite(side,type,preferred,out site)){brain.PlacementFailure=key;brain.NextPlacementTry=Time+6;return false;}
  if(!StartConstruction(type,side))return false;
  int category=BuildCategory(type);brain.PlacementFailure=null;brain.Buildings[category]=key;brain.BuildPoints[category]=site;return true;
 }
 void AiConstruction(int side,AiBrain brain,Piece[] known){
  AiPlaceReady(side,brain);if(!Live(side).Any(p=>p.T.Key=="hq")||Construction(side,0)!=null)return;
  var home=Home(side);int power=Power(side),refineries=AiCount(side,"refinery"),factories=AiCount(side,"factory");
  if(AiCount(side,"power")==0||power<10){AiPlanBuild(side,brain,"power",new PointF(home.X+95,home.Y-70));return;}
  var ore=Ore.Where(p=>p.Stock>30&&Explored(side,p.P)).OrderBy(p=>Dist(p.P,home)).FirstOrDefault();
  var mine=ore==null?new PointF(home.X,home.Y+180):ore.P;
  if(refineries==0){AiPlanBuild(side,brain,"refinery",mine);return;}
  if(AiCount(side,"barracks")==0){AiPlanBuild(side,brain,"barracks",new PointF(home.X+75,home.Y+20));return;}
  if(factories==0&&AiCount(side,"barracks")>0){AiPlanBuild(side,brain,"factory",new PointF(home.X,home.Y+120));return;}
  if(factories==0)return;
  if(refineries<2&&Time>StorySecondRefinery(side)&&AiCount(side,"miner")>=2&&Funds[side]>=1300){AiPlanBuild(side,brain,"refinery",mine);return;}
  int army=Units.Count(p=>p.Alive&&p.Side==side&&!p.T.Building&&p.T.Hit>0);
  if(factories<2&&Time>StorySecondFactory(side)&&army>=6&&Funds[side]>=2250){
   if(power<25){AiPlanBuild(side,brain,"power",home);return;}AiPlanBuild(side,brain,"factory",StoryBuildDirection(side,brain.Rally));return;
  }
  if(AiCount(side,"barracks")<2&&Time>StorySecondBarracks(side)&&army>=8&&Funds[side]>=1700){AiPlanBuild(side,brain,"barracks",StoryBuildDirection(side,brain.Rally));return;}
  if(AiCount(side,"radar")==0&&Time>230&&Funds[side]>=4000){AiPlanBuild(side,brain,"radar",home);return;}
  if(AiCount(side,"radar")>0&&AiCount(side,"satellite")==0&&Time>480&&Funds[side]>=9000){
   if(power<100){AiPlanBuild(side,brain,"power",home);return;}AiPlanBuild(side,brain,"satellite",home);return;
  }
 }
 int AiReserve(int side,bool includeDefense=true){
  int reserve=Time<100?150:350;
  if(Time>StorySecondRefinery(side)&&AiCount(side,"refinery")<2&&AiCount(side,"miner")>=2)reserve=Info("refinery").Price+200;
  else if(Time>StorySecondFactory(side)&&AiCount(side,"factory")<2&&Units.Count(p=>p.Alive&&p.Side==side&&p.T.Hit>0&&!p.T.Building)>=6)reserve=Info("factory").Price+(Power(side)<25?Info("power").Price:0)+200;
  // Recruitment leaves room to finance a planned defence. Its own budget
  // excludes this extra reservation so it never waits for its cost twice.
  if(includeDefense&&Construction(side,1)==null&&AiCount(side,"post")<DesiredDefenses(side))reserve+=Info("post").Price;
  return reserve;
 }
 int AiBudget(int side,int reserve){
  int promised=Live(side).Sum(p=>p.Queue.Sum(t=>t.Price)-p.WorkPaid);
  for(int i=0;i<2;i++){var job=Construction(side,i);if(job!=null)promised+=job.Type.Price-job.Paid;}
  return Funds[side]-promised-reserve;
 }
 string AiVehicle(int side,Piece[] known){
  int tanks=AiCount(side,"tank")+AiPending(side,"tank"),cars=AiCount(side,"ifv")+AiPending(side,"ifv"),guns=AiCount(side,"arty")+AiPending(side,"arty");
  int enemyFoot=known.Count(p=>p.T.Foot),enemyArmor=known.Count(p=>!p.T.Building&&!p.T.Foot&&p.T.Hit>0);
  bool fortified=known.Count(p=>p.T.Key=="post")>=2;
  float carRatio=.28f,gunRatio=side%5==3?.30f:.18f;StoryVehicleMix(side,ref carRatio,ref gunRatio);
  if(fortified){carRatio=Math.Min(carRatio,.20f);gunRatio=Math.Max(gunRatio,.32f);}
  else if(enemyFoot>enemyArmor*2){carRatio=Math.Max(carRatio,StoryRole(side)==CampaignRole.NorthLine?.35f:.5f);gunRatio=Math.Min(gunRatio,.2f);}
  float tankRatio=1-carRatio-gunRatio;
  return cars/carRatio<=Math.Min(tanks/tankRatio,guns/gunRatio)?"ifv":guns/gunRatio<tanks/tankRatio?"arty":"tank";
 }
 string AiInfantry(int side,Piece[] known){
  if(RelayMission&&RelayStation!=null&&RelayStation.Side!=side&&AiCount(side,"engineer")+AiPending(side,"engineer")==0)return "engineer";
  bool house=Units.Any(p=>p.Alive&&p.T.Civilian&&p.Side<0&&Explored(side,p.P)&&Dist(p.P,Home(side))<1400);
  if(house&&AiCount(side,"engineer")+AiPending(side,"engineer")==0)return "engineer";
  if(Require(Info("sniper"),side)==null&&known.Count(p=>p.T.Foot)>=3&&AiCount(side,"sniper")+AiPending(side,"sniper")<2)return "sniper";
  int rifles=AiCount(side,"squad")+AiPending(side,"squad"),anti=AiCount(side,"at")+AiPending(side,"at");
  bool armor=known.Count(p=>p.T.Key=="tank"||p.T.Key=="ifv")>known.Count(p=>p.T.Foot);
  return anti<(rifles+anti+1)*(armor?.6f:.35f)?"at":"squad";
 }
 void AiRecruit(int side,AiBrain brain,Piece[] known){
  var facilities=Live(side).Where(p=>p.T.Key=="factory"||p.T.Key=="barracks").ToArray();
  int desiredMiners=Math.Min(4,Math.Max(2,AiCount(side,"refinery")*2));
  int miners=AiCount(side,"miner")+AiPending(side,"miner")+Units.Count(p=>p.Alive&&p.Side==side&&p.T.Key=="refinery"&&!p.StarterDelivered);
  if(miners<desiredMiners&&facilities.Any(p=>p.T.Key=="factory")&&AiBudget(side,200)>=Info("miner").Price){if(Train(Info("miner"),side,null))brain.Orders++;}
  int army=Units.Count(p=>p.Alive&&p.Side==side&&!p.T.Building&&p.T.Hit>0)+facilities.Sum(p=>p.Queue.Count(t=>t.Hit>0));
  int desiredArmy=Math.Min(RelayMission?28:IslandMission?42:48,12+(int)(Time/45)*4);
  foreach(var facility in facilities.OrderBy(p=>p.Queue.Count).ThenBy(p=>StoryRole(side)!=CampaignRole.Standard&&p.T.Key=="factory"?0:1)){
   facility.Rally=brain.Rally;if(facility.Queue.Count>=2||facility.ProductionPaused||army>=desiredArmy)continue;
   var type=Info(facility.T.Key=="factory"?AiVehicle(side,known):AiInfantry(side,known));
   int coreUnitReserve=facility.T.Key=="barracks"&&type.Key!="engineer"?StoryVehicleReserve(side,army,desiredArmy,known):0;
   if(AiBudget(side,AiReserve(side))<type.Price+coreUnitReserve||Require(type,side)!=null)continue;
   if(Train(type,side,facility)){brain.Orders++;if(type.Hit>0)army++;}
  }
 }
 void SmartEconomy(int side){
  if(HasSurrendered(side))return;var brain=Brain(side);var known=AiKnown(side);
  if(!Live(side).Any(p=>p.T.Key=="hq")){
   var mcv=Live(side).FirstOrDefault(p=>p.T.Key=="mcv");if(mcv!=null)DeployEnemyBase(mcv);
   else if(!Units.Any(p=>p.Alive&&p.Side==side&&(p.T.Key=="hq"||p.T.Key=="mcv")))EnemyRecoveryProduction(side);
  }
  AiConstruction(side,brain,known);AiFortify(side,brain,known);AiBunker(side,brain);AiRecruit(side,brain,known);
  foreach(var building in Live(side).Where(p=>p.T.Building&&!p.T.Civilian)){if(building.Hp<building.T.Hp*StoryRepairThreshold(side)&&Funds[side]>(known.Any(e=>e.T.Hit>0&&TargetDistance(e,building)<420)?400:AiReserve(side)+400))building.Repairing=true;else if(Funds[side]<200)building.Repairing=false;}
  SmartTactics(side,false);
 }
 int AiOpponent(int side,AiBrain brain,Piece[] known){
  int best=-1;float bestScore=float.MinValue;
  for(int enemy=0;enemy<FactionCount;enemy++){
   if(enemy==side||Remaining(enemy)==0||HasSurrendered(enemy))continue;
   var seen=known.Where(p=>p.Side==enemy).ToArray();float score=-Dist(Home(side),Home(enemy))/Tile*2;
   if(Time-brain.Attacks[enemy]<65)score+=80;
   score+=seen.Count(p=>p.T.Key=="miner")*10+seen.Count(p=>p.T.Building)*3-seen.Count(p=>p.T.Hit>0)*2;
   if(enemy==brain.Opponent)score+=28;
   score+=StoryOpponentPressure(side,enemy,known);
   if(score>bestScore){best=enemy;bestScore=score;}
  }return best;
 }
 PointF AiObjective(int side,int opponent,Piece[] known){
  var candidates=known.Where(p=>p.Side==opponent).ToArray();
  var target=candidates.OrderBy(p=>Dist(p.P,Home(side))-StoryTargetPriority(side,p,known)).FirstOrDefault();
  return AiOpenPoint(target==null?Home(opponent):target.P);
 }
 void SmartTactics(int side,bool periodic){
  if(RelayMission){RelayTactics(side);return;}
  if(HasSurrendered(side))return;var brain=Brain(side);var known=AiKnown(side);
  var troops=Live(side).Where(p=>!p.T.Building&&p.T.Hit>0&&p.Entering==null).ToArray();
  var defenders=MaintainHomeGuard(side,brain,troops,known);
  MaintainStoryPost(side,brain,troops,known,defenders);defenders.UnionWith(brain.FieldGuard);
  RetreatAiMiners(side,brain,known);UpdateAiWings(side,brain,known,defenders);
  var assets=Live(side).Where(p=>p.T.Building&&!p.T.Civilian).ToArray();
  bool threatened=known.Any(p=>p.T.Hit>0&&assets.Any(a=>TargetDistance(p,a)<420));
  var assigned=new HashSet<Piece>(brain.Wings.SelectMany(w=>w.Units));
  var available=troops.Where(p=>!defenders.Contains(p)&&!assigned.Contains(p)&&p.Target==null&&!aiMovesPending.Contains(p)&&Dist(p.P,Home(side))<900).ToArray();
  int opponent=AiOpponent(side,brain,known);brain.Opponent=opponent;if(opponent<0)return;
  int minimum=StoryWingMinimum(side);
  bool waited=Time>=brain.NextPush+MatchTempo.UnderfilledWait&&available.Length>=minimum-1;
  if(!threatened&&brain.HomeGuard.Count>=Math.Min(HomeGuardTarget(side),troops.Length)&&Time>=brain.NextPush&&(available.Length>=minimum||waited)){
   var batch=available.OrderBy(p=>Dist(p.P,brain.Rally)).Take(StoryWingMaximum(side)).ToArray();
   if(LaunchAiWing(side,brain,batch,opponent,known,false))brain.NextPush=Time+MatchTempo.PushInterval+(side%3)*3;
  }
  assigned=new HashSet<Piece>(brain.Wings.SelectMany(w=>w.Units));
  AiPressureRaid(side,brain,troops.Where(p=>!defenders.Contains(p)&&!assigned.Contains(p)&&p.Target==null&&!aiMovesPending.Contains(p)).ToArray(),known,threatened);
 }
 void SmartHousing(int side){
  if(RelayMission)return;
  var brain=Brain(side);var known=AiKnown(side);
  foreach(var engineer in Live(side).Where(p=>p.T.Key=="engineer"&&p.Entering==null).ToArray()){
   var house=Units.Where(h=>h.Alive&&h.T.Civilian&&h.Side<0&&Explored(side,h.P)&&Dist(h.P,Home(side))<1400&&!known.Any(e=>e.T.Hit>0&&Dist(e.P,h.P)<220))
    .OrderBy(h=>Dist(h.P,engineer.P)).FirstOrDefault();if(house!=null)EnterHouse(engineer,house);
  }
  int garrison=Units.Count(p=>p.Alive&&p.Side==side&&p.Carrier!=null&&p.Carrier.T.Civilian);
  int housingLimit=StoryHousingLimit(side);if(garrison>=housingLimit)return;var assigned=new HashSet<Piece>(brain.Wings.SelectMany(w=>w.Units));
  assigned.UnionWith(brain.HomeGuard);
  if(StoryRole(side)!=CampaignRole.LocalDefense)assigned.UnionWith(brain.FieldGuard);
  foreach(var foot in Live(side).Where(p=>p.T.Foot&&p.T.Hit>0&&p.Entering==null&&p.Target==null&&!assigned.Contains(p)).Take(2).ToArray()){
   var house=Live(side).Where(h=>h.T.Civilian&&Dist(h.P,foot.P)<420&&HouseCount(h)<Math.Min(StoryRole(side)==CampaignRole.LocalDefense?3:2,HouseCapacity(h))).OrderBy(h=>Dist(h.P,Home(side))).FirstOrDefault();
   if(house!=null&&garrison++<housingLimit&&EnterHouse(foot,house))brain.FieldGuard.Remove(foot);
  }
 }
 public string AiSummary(int side){var b=Brain(side);return FactionName(side)+" cash="+Funds[side]+" miners="+AiCount(side,"miner")+" refineries="+AiCount(side,"refinery")+" factories="+AiCount(side,"factory")+" orders="+b.Orders+" squads="+b.Launches+" raids="+b.Raids+" target="+b.Opponent;}
}
}
