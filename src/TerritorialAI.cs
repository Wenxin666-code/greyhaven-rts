using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 bool AiKeepsHaulLanes(int side,TypeInfo type,RectangleF body){
  foreach(var refinery in Live(side).Where(p=>p.T.Key=="refinery")){
   var dock=DockPoint(refinery);var approach=new RectangleF(dock.X-2*Tile,dock.Y-Tile*.2f,4*Tile,3.5f*Tile);
   if(body.IntersectsWith(approach))return false;
  }
  if(type.Key=="refinery"){
   var dock=Middle(ExitApron(type,body));var approach=new RectangleF(dock.X-2*Tile,dock.Y-Tile*.2f,4*Tile,3.5f*Tile);
   if(Obstacles().Any(p=>p.IntersectsWith(approach)))return false;
  }
  return true;
 }
 PointF AiDefenseFront(int side,PointF anchor,int index){
  var enemies=Enumerable.Range(0,FactionCount).Where(s=>s!=side&&Remaining(s)>0&&!HasSurrendered(s)).OrderBy(s=>Dist(Home(side),Home(s))).ToArray();
  if(enemies.Length==0)return new PointF(anchor.X+1,anchor.Y+1);
  float nearest=Dist(Home(side),Home(enemies[0]));var fronts=enemies.Where(s=>Dist(Home(side),Home(s))<=nearest*1.3f).Take(2).ToArray();
  return Home(fronts[index%fronts.Length]);
 }
 bool AiGuardGround(PointF point,int side){
  if(Blocked(point,9,null)||Ore.Any(p=>p.Stock>0&&Dist(p.P,point)<Tile*1.5f)||Brain(side).HomeGuard.Any(p=>p.GuardHome.HasValue&&Dist(p.GuardHome.Value,point)<Tile*.95f))return false;
  var c=Terrain.At((int)Math.Floor(point.X/Tile),(int)Math.Floor(point.Y/Tile));
  if(c==null||c.Ground==RiverbendMap.Surface.Road||c.Ground==RiverbendMap.Surface.Bridge||c.Ground==RiverbendMap.Surface.Ramp)return false;
  return !Live(side).Where(p=>HasGate(p.T)).Any(p=>Inflated(ExitApron(p.T,Footprint(p.T,p.P)),Tile*2.5f).Contains(point));
 }
 PointF AiGuardStation(int side,int index){
  var assets=Live(side).Where(p=>p.T.Building&&!p.T.Civilian&&p.T.Key!="post").ToArray();
  var center=assets.Length==0?Home(side):new PointF(assets.Average(p=>p.P.X),assets.Average(p=>p.P.Y));
  center=StoryGuardCenter(side,index,center);
  var front=AiDefenseFront(side,center,index);float distance=Math.Max(1,Dist(center,front));float dx=(front.X-center.X)/distance,dy=(front.Y-center.Y)/distance;
  float flank=((index/2)%3-1)*2*Tile;
  float outward=(7+index%2+index/6*2)*Tile;
  var desired=new PointF(center.X+dx*outward-dy*flank,center.Y+dy*outward+dx*flank);
  for(int ring=0;ring<=8;ring++)for(int n=0;n<16;n++){
   double angle=n*Math.PI/8;var candidate=new PointF(desired.X+(float)Math.Cos(angle)*ring*Tile,desired.Y+(float)Math.Sin(angle)*ring*Tile);
   if(AiGuardGround(candidate,side))return candidate;
  }
  return AiOpenPoint(desired);
 }
 int HomeGuardTarget(int side){
  if(!Live(side).Any(p=>p.T.Building&&!p.T.Civilian))return 0;
  if(!Live(side).Any(p=>p.T.Key=="factory")||AiCount(side,"refinery")<2)return 6;
  var enemies=Enumerable.Range(0,FactionCount).Where(s=>s!=side&&Remaining(s)>0&&!HasSurrendered(s)).OrderBy(s=>Dist(Home(side),Home(s))).ToArray();
  bool twoFronts=enemies.Length>1&&Dist(Home(side),Home(enemies[1]))<=Dist(Home(side),Home(enemies[0]))*1.3f;
  int target=twoFronts?20:12;
  // Armoured guards carry more combat value than a rifle detachment. Apply the
  // modest discount only after those vehicles actually exist, never on a timer.
  if(StoryRole(side)==CampaignRole.NorthLine)target-=Math.Min(4,AiCount(side,"tank")+AiCount(side,"ifv")+AiCount(side,"arty"));
  return target;
 }
 HashSet<Piece> MaintainHomeGuard(int side,AiBrain brain,Piece[] troops,Piece[] known){
  brain.HomeGuard.RemoveWhere(p=>!p.Deployed||p.Side!=side||p.Entering!=null);
  var assets=Live(side).Where(p=>!p.Selling&&(p.T.Key=="hq"||p.T.Key=="refinery"||p.T.Key=="factory")).OrderBy(p=>p.T.Key=="hq"?0:p.T.Key=="refinery"?1:2).ToArray();
  if(assets.Length==0){brain.HomeGuard.Clear();return new HashSet<Piece>();}
  var nearby=known.Where(p=>p.T.Hit>0&&assets.Any(a=>TargetDistance(p,a)<420)).ToArray();
  if(StoryRole(side)==CampaignRole.NorthLine&&nearby.Length==0){
   int spare=brain.HomeGuard.Count-HomeGuardTarget(side);
   foreach(var unit in brain.HomeGuard.OrderBy(p=>p.T.Foot?0:1).ThenByDescending(p=>Dist(p.P,Home(side))).Take(Math.Max(0,spare)).ToArray()){brain.HomeGuard.Remove(unit);brain.GuardPosts.Remove(unit);CancelAiMove(unit);Halt(unit);}
  }
  foreach(var removed in brain.GuardPosts.Keys.Where(p=>!brain.HomeGuard.Contains(p)).ToArray())brain.GuardPosts.Remove(removed);
  var assigned=new HashSet<Piece>(brain.Wings.SelectMany(p=>p.Units));
  assigned.UnionWith(brain.FieldGuard);
  foreach(var unit in troops.Where(p=>!assigned.Contains(p)&&!aiMovesPending.Contains(p)&&!brain.HomeGuard.Contains(p))
   .OrderBy(p=>Dist(p.P,Home(side))+(p.T.Key=="tank"?-50:p.T.Key=="at"?-20:0))){
   if(brain.HomeGuard.Count>=HomeGuardTarget(side))break;
   brain.HomeGuard.Add(unit);CancelAiMove(unit);var station=AiGuardStation(side,brain.HomeGuard.Count-1);brain.GuardPosts[unit]=station;
   if(nearby.Any(p=>TargetDistance(unit,p)<=unit.T.LockRange))Guard(unit);
   else if(!GuardAt(unit,station))Guard(unit);
  }
  foreach(var unit in brain.HomeGuard.Where(p=>!p.GuardHome.HasValue).ToArray()){
   if(!GuardAt(unit,AiGuardStation(side,unit.Id)))Guard(unit);
  }
  var defenders=new HashSet<Piece>(brain.HomeGuard);
  if(nearby.Length>0){
   var threat=nearby.OrderBy(p=>Dist(p.P,Home(side))).First();brain.Attacks[threat.Side]=Time;
   foreach(var unit in brain.HomeGuard){
    var enemy=nearby.Where(p=>GuardAllows(unit,p)||TargetDistance(unit,p)<=unit.T.LockRange).OrderBy(p=>TargetDistance(unit,p)-(p.T.Key=="arty"&&!unit.T.Foot?160:0)).FirstOrDefault();
    if(enemy!=null&&(unit.Target==null||!unit.Target.Alive||enemy.T.Key=="arty"&&unit.Target.T.Key!="arty"&&!unit.T.Foot)){
     CancelAiMove(unit);if(!GuardAllows(unit,enemy))unit.GuardHome=unit.P;
     TakeContact(unit,enemy,ContactOrder.ReturnFire);
    }
   }
   // Local reserves reinforce the line; distant attackers do not all abandon
   // their mission in response to a single scout.
   foreach(var unit in troops.Where(p=>!defenders.Contains(p)&&assets.Any(a=>Dist(a.P,p.P)<480)).OrderBy(p=>Dist(p.P,threat.P)).Take(Math.Max(2,nearby.Length))){
    defenders.Add(unit);CancelAiMove(unit);if(unit.Target!=threat)Attack(unit,threat);
   }
  }else{
   foreach(var unit in brain.HomeGuard.Where(p=>p.Target==null&&!p.GuardTravelling)){
    PointF station;if(brain.GuardPosts.TryGetValue(unit,out station)&&(!unit.GuardHome.HasValue||Dist(unit.GuardHome.Value,station)>Tile)){
     if(!GuardAt(unit,station))Guard(unit);
    }
   }
  }
  return defenders;
 }
 void CancelAiMove(Piece unit){aiMovesPending.Remove(unit);aiMoveVersions.Remove(unit);}
 void ReturnAiWing(int side,AiBrain brain,AiWing wing){
  wing.Phase=3;wing.Goal=brain.Rally;wing.OrderedAt=Time;
  foreach(var unit in wing.Units){CancelAiMove(unit);Halt(unit);}
  QueueAiMarch(wing.Units,wing.Goal,false,true);brain.Withdrawals++;
 }
 PointF AiApproach(int side,int opponent,PointF objective){
  var home=Home(side);float dx=home.X-objective.X,dy=home.Y-objective.Y,len=Math.Max(1,Dist(home,objective));
  return AiOpenPoint(new PointF(objective.X+dx/len*20*Tile,objective.Y+dy/len*20*Tile));
 }
 void UpdateAiWings(int side,AiBrain brain,Piece[] known,HashSet<Piece> defenders){
  foreach(var wing in brain.Wings.ToArray()){
   wing.Units=wing.Units.Where(p=>p.Deployed&&!defenders.Contains(p)).ToArray();
   if(wing.Units.Length==0){brain.Wings.Remove(wing);continue;}
   float value=wing.Units.Sum(p=>p.T.Price*Math.Max(.25f,p.Hp/p.T.Hp));
   bool fighting=wing.Units.Any(p=>p.Target!=null&&p.Target.Alive&&TargetDistance(p,p.Target)<Math.Max(p.T.Range+3*Tile,200));
   int near=wing.Units.Count(p=>!aiMovesPending.Contains(p)&&Dist(p.P,wing.Goal)<180);
   if(wing.Phase==3){
    if(near>=Math.Max(1,(wing.Units.Length*3+3)/4)||Time-wing.OrderedAt>100){
     foreach(var unit in wing.Units){CancelAiMove(unit);if(unit.Target==null)Guard(unit);}brain.Wings.Remove(wing);
    }continue;
   }
   if(value<wing.InitialValue*StoryWithdrawal(side)||(!wing.Raid&&wing.Units.Length<3)||HasSurrendered(wing.Opponent)||Remaining(wing.Opponent)==0){ReturnAiWing(side,brain,wing);continue;}
   if(wing.Phase==0){
    if(near<Math.Max(1,(wing.Units.Length*3+3)/4)&&Time-wing.OrderedAt<18)continue;
    wing.Phase=wing.Raid?2:1;wing.Goal=wing.Raid?wing.Objective:AiApproach(side,wing.Opponent,wing.Objective);wing.OrderedAt=Time;
    QueueAiMarch(wing.Units,wing.Goal,true,true);continue;
   }
   if(wing.Phase==1){
    if(near>=Math.Max(3,(wing.Units.Length*2+2)/3)||Time-wing.OrderedAt>80&&near>=3){
     wing.Phase=2;wing.Goal=wing.Objective;wing.OrderedAt=Time;QueueAiMarch(wing.Units,wing.Goal,true,true);
    }else if(Time-wing.OrderedAt>125&&!fighting)ReturnAiWing(side,brain,wing);
    continue;
   }
   if(!fighting&&(near>0||Time-wing.OrderedAt>100)){
    var objective=AiObjective(side,wing.Opponent,known);
    if(Dist(objective,wing.Goal)>3*Tile){wing.Goal=objective;wing.Objective=objective;wing.OrderedAt=Time;QueueAiMarch(wing.Units,wing.Goal,true,true);}
    else if(Time-wing.OrderedAt>120)ReturnAiWing(side,brain,wing);
   }
  }
 }
 void RetreatAiMiners(int side,AiBrain brain,Piece[] known){
  foreach(var miner in Live(side).Where(p=>p.T.Key=="miner").ToArray()){
   var danger=known.FirstOrDefault(p=>p.T.Hit>0&&Dist(p.P,miner.P)<175);
   if(danger!=null){
    var safePlant=Live(side).Where(p=>p.T.Key=="refinery"&&!known.Any(e=>e.T.Hit>0&&Dist(e.P,DockPoint(p))<e.T.Range+Tile))
     .OrderBy(p=>Dist(miner.P,DockPoint(p))).FirstOrDefault();
    if(miner.Cargo>5&&safePlant!=null){
     if(!miner.Mining||!miner.Returning||miner.Dock!=safePlant)MineOrder(miner,null,safePlant);
     brain.Evading.Remove(miner);continue;
    }
    if(!brain.Evading.ContainsKey(miner)||miner.Path.Count==0&&Time>=miner.RouteRetryAt){
     var home=Home(side);float dx=home.X-danger.P.X,dy=home.Y-danger.P.Y,length=Math.Max(1,Dist(home,danger.P));
     float spread=(miner.Id%5-2)*Tile;
     Move(miner,AiOpenPoint(new PointF(home.X+dx/length*120-dy/length*spread,home.Y+dy/length*120+dx/length*spread)),false);miner.RouteRetryAt=Time+4;
    }brain.Evading[miner]=Time;
   }
   else if(brain.Evading.ContainsKey(miner)&&Time-brain.Evading[miner]>6){MineOrder(miner,null,null);brain.Evading.Remove(miner);}
  }
  foreach(var retired in brain.Evading.Keys.Where(p=>!p.Alive).ToArray())brain.Evading.Remove(retired);
 }
}
}
