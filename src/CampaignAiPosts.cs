using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
namespace Greyhaven {public partial class Campaign {
 // A small detachment holds a mission-relevant place. It is separate from both
 // the standing base reserve and the attack wings; all members are normally produced.
 bool StoryPostPoint(int side,out PointF result){
  result=PointF.Empty;if(!StoryBattle||side<=0)return false;var role=StoryRole(side);var home=Home(side);PointF preferred;
  if(!IslandMission){
   var bridge=River.Bridges[0];
   float shoulder=side==1?bridge.Top-2.5f:bridge.Bottom+2.5f;
   preferred=new PointF((bridge.Right+4.5f)*Tile,shoulder*Tile);
  }else if(role==CampaignRole.Mining){
   var refinery=Live(side).Where(u=>u.T.Key=="refinery").OrderByDescending(u=>Dist(u.P,home)).FirstOrDefault();
   if(refinery==null)return false;preferred=new PointF(refinery.P.X-6*Tile,refinery.P.Y);
  }else if(role==CampaignRole.LocalDefense){
   var house=River.Houses.OrderBy(h=>Dist(home,new PointF(h.Lot.X*Tile,h.Lot.Y*Tile))).FirstOrDefault();
   if(house==null)return false;preferred=new PointF((house.Lot.Right+4)*Tile,(house.Lot.Bottom+2)*Tile);
  }else if(role==CampaignRole.Frontier){
   int opponent=AiOpponent(side,Brain(side),AiKnown(side));if(opponent<0)return false;
   var enemy=Home(opponent);float distance=Math.Max(Tile,Dist(home,enemy)),advance=Math.Min(distance*.3f,22*Tile);
   preferred=new PointF(home.X+(enemy.X-home.X)*advance/distance,home.Y+(enemy.Y-home.Y)*advance/distance);
  }else{
   var bridge=River.Bridges.Where(b=>b!=River.IslandGate).OrderBy(b=>Dist(home,new PointF((b.X+b.Width*.5f)*Tile,(b.Y+b.Height*.5f)*Tile))).FirstOrDefault();
   if(bridge.IsEmpty)return false;
   preferred=new PointF((home.X/Tile>bridge.X+bridge.Width*.5f?bridge.Right+5:bridge.Left-5)*Tile,(bridge.Top+bridge.Height*.5f)*Tile);
  }
  // Keep posts off roads, ore and doorways so they do not become traffic barriers.
  for(int ring=0;ring<=8;ring++)for(int i=0;i<(ring==0?1:16);i++){
   double angle=i*Math.PI/8;var p=new PointF(preferred.X+(float)Math.Cos(angle)*ring*Tile,preferred.Y+(float)Math.Sin(angle)*ring*Tile);
   var cell=Terrain.At((int)Math.Floor(p.X/Tile),(int)Math.Floor(p.Y/Tile));
   if(cell==null||cell.Ground==RiverbendMap.Surface.Road||cell.Ground==RiverbendMap.Surface.Bridge||cell.Ground==RiverbendMap.Surface.Ramp||Blocked(p,9,null)||Ore.Any(o=>o.Stock>0&&Dist(o.P,p)<Tile*2))continue;
   if(Live(side).Where(u=>HasGate(u.T)).Any(u=>Inflated(ExitApron(u.T,Footprint(u.T,u.P)),Tile*2).Contains(p)))continue;
   result=p;return true;
  }return false;
 }
 void WithdrawStoryPost(int side,AiBrain brain){
  if(brain.FieldReturning||brain.FieldGuard.Count==0)return;
  brain.FieldReturning=true;brain.FieldOrderedAt=Time;brain.NextFieldAttempt=Time+70;
  foreach(var unit in brain.FieldGuard){CancelAiMove(unit);Halt(unit);}
  QueueAiMarch(brain.FieldGuard.ToArray(),brain.Rally,false,true);brain.Withdrawals++;
 }
 void MaintainStoryPost(int side,AiBrain brain,Piece[] troops,Piece[] known,HashSet<Piece> defenders){
  if(!StoryBattle||side<=0)return;
  brain.FieldGuard.RemoveWhere(u=>!u.Deployed||u.Side!=side||u.Entering!=null);
  var core=Live(side).Where(u=>u.T.Key=="hq"||u.T.Key=="factory").ToArray();
  bool homeDanger=known.Any(e=>e.T.Hit>0&&core.Any(a=>TargetDistance(e,a)<380));
  if(brain.FieldGuard.Count>0){
   if(brain.FieldReturning){
    if(brain.FieldGuard.All(u=>Dist(u.P,brain.Rally)<8*Tile)||Time-brain.FieldOrderedAt>100){
     foreach(var unit in brain.FieldGuard){CancelAiMove(unit);if(unit.Target==null)Guard(unit);}brain.FieldGuard.Clear();brain.FieldReturning=false;
    }return;
   }
   float strength=brain.FieldGuard.Sum(u=>u.T.Price*Math.Max(.2f,u.Hp/u.T.Hp));
   float danger=known.Where(e=>e.T.Hit>0&&Dist(e.P,brain.FieldPoint)<ReferenceBalance.Range(e)+6*Tile).Sum(e=>e.T.Price*Math.Max(.2f,e.Hp/e.T.Hp));
   if(core.Length==0||homeDanger||brain.FieldGuard.Count<=brain.FieldInitial/2||danger>strength*1.8f){WithdrawStoryPost(side,brain);return;}
   foreach(var unit in brain.FieldGuard.Where(u=>!u.GuardHome.HasValue&&Dist(u.P,brain.FieldPoint)<4*Tile).ToArray()){CancelAiMove(unit);Guard(unit);}
   if(Time-brain.FieldOrderedAt>140&&brain.FieldGuard.All(u=>Dist(u.P,brain.FieldPoint)>6*Tile)){WithdrawStoryPost(side,brain);return;}
   return;
  }
  if(Time<brain.NextFieldAttempt||homeDanger||core.Length==0||brain.HomeGuard.Count<HomeGuardTarget(side))return;
  brain.NextFieldAttempt=Time+18;var assigned=new HashSet<Piece>(brain.Wings.SelectMany(w=>w.Units));
  var surplus=troops.Where(u=>!defenders.Contains(u)&&!assigned.Contains(u)&&u.Target==null&&!aiMovesPending.Contains(u)&&Dist(u.P,Home(side))<900).ToArray();
  if(surplus.Length<4)return;
  var available=surplus
   .OrderBy(u=>StoryRole(side)==CampaignRole.LocalDefense?(u.T.Foot?0:1):(u.T.Key=="ifv"?0:u.T.Key=="tank"?1:2)).ThenBy(u=>Dist(u.P,brain.Rally)).Take(4).ToArray();
  if(available.Length<4)return;PointF point;if(!StoryPostPoint(side,out point))return;
  if(known.Where(e=>e.T.Hit>0&&Dist(e.P,point)<ReferenceBalance.Range(e)+6*Tile).Sum(e=>e.T.Price)>available.Sum(u=>u.T.Price)*1.8f)return;
  brain.FieldPoint=point;brain.FieldInitial=available.Length;brain.FieldReturning=false;brain.FieldOrderedAt=Time;
  foreach(var unit in available)brain.FieldGuard.Add(unit);
  QueueAiMarch(available,point,true,true);brain.FieldDeployments++;
 }
}}
