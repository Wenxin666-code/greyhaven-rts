using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
namespace Greyhaven {
public partial class Campaign {
 readonly HashSet<Piece> relayReserve=new HashSet<Piece>();
 Piece[] relayTeam=new Piece[0];Piece relayEscort;PointF relayRally;
 float relayDecisionAt,relayLaunchAt,relayOrderedAt,relayScoutAt=80,relayEngineerAt;
 int relayInitialTeam;
 public int RelayCounterattacks {get;private set;}
 void RelayTactics(int side){
  if(side!=1||HasSurrendered(side)||RelayStation==null||!RelayStation.Alive||Time<relayDecisionAt)return;
  relayDecisionAt=Time+1;var brain=Brain(side);var known=AiKnown(side);
  RetreatAiMiners(side,brain,known);
  relayReserve.RemoveWhere(u=>!u.Deployed);
  foreach(var u in Live(side).Where(u=>u.T.Key=="squad"&&!relayTeam.Contains(u)&&Dist(u.P,Home(side))<12*Tile).OrderBy(u=>Dist(u.P,Home(side)))){if(relayReserve.Count>=4)break;if(relayReserve.Add(u))Guard(u);}
  var homeThreat=known.Where(e=>e.T.Hit>0&&Dist(e.P,Home(side))<17*Tile).OrderBy(e=>Dist(e.P,Home(side))).FirstOrDefault();
  foreach(var guard in relayReserve){if(homeThreat!=null&&guard.Target==null&&GuardAllows(guard,homeThreat))TakeContact(guard,homeThreat,ContactOrder.Automatic);}
  bool retake=RelayStation.Side!=side;
  if(retake&&AiCount(side,"engineer")+AiPending(side,"engineer")==0&&AiBudget(side,0)>=Info("engineer").Price)Train(Info("engineer"),side,null);
  if(!retake){
   if(relayTeam.Length>0){foreach(var u in relayTeam.Where(u=>u.Deployed))GuardAt(u,AiOpenPoint(new PointF(RelayStation.P.X+(u.Id%3-1)*2*Tile,RelayStation.P.Y+5*Tile+(u.Id%2)*2*Tile)));relayTeam=new Piece[0];relayEscort=null;relayLaunchAt=Time+25;}
   var threatened=known.Where(e=>e.T.Hit>0&&(Dist(e.P,RelayStation.P)<24*Tile||Dist(e.P,RiverbendMap.RelayWorld(53,48))<18*Tile)).OrderBy(e=>Dist(e.P,RelayStation.P)).FirstOrDefault();
   if(threatened!=null){foreach(var u in Live(side).Where(u=>!u.T.Building&&Armed(u)&&!relayReserve.Contains(u)&&u.Target==null).OrderBy(u=>Dist(u.P,threatened.P)).Take(6))Attack(u,threatened);}
   else if(Time>=relayScoutAt){relayScoutAt=Time+100;var scouts=Live(side).Where(u=>!u.T.Building&&Armed(u)&&!relayReserve.Contains(u)&&u.Target==null&&!u.GuardHome.HasValue).Take(3).ToArray();if(scouts.Length>0)QueueAiMarch(scouts,RiverbendMap.RelayWorld(42,66),true,true);}
   return;
  }
  var living=relayTeam.Where(u=>u.Deployed&&u.Side==side).ToArray();
  if(relayTeam.Length>0&&living.Length<=Math.Max(1,relayInitialTeam/2)&&Time>relayOrderedAt+6){
   var retreat=living.Concat(relayEscort!=null&&relayEscort.Deployed?new[]{relayEscort}:new Piece[0]).ToArray();
   if(retreat.Length>0)QueueAiMarch(retreat,AiOpenPoint(new PointF(Home(side).X,Home(side).Y+6*Tile)),false,true);
   relayTeam=new Piece[0];relayEscort=null;relayLaunchAt=Time+25;return;
  }
  if(relayTeam.Length==0){
   if(Time<relayLaunchAt)return;
   var pool=Live(side).Where(u=>!u.T.Building&&Armed(u)&&!relayReserve.Contains(u)&&u.Entering==null).OrderBy(u=>Dist(u.P,RelayStation.P)).Take(6).ToArray();
   if(pool.Length<4)return;
   var p=RelayStation.P;var options=new[]{new PointF(p.X+7*Tile,p.Y+2*Tile),new PointF(p.X,p.Y+7*Tile),new PointF(p.X-7*Tile,p.Y),new PointF(p.X,p.Y-7*Tile)};
   var reachable=options.Select(AiOpenPoint).Where(g=>CanMoveTo(pool[0],g)).OrderBy(g=>known.Where(e=>e.T.Hit>0).Sum(e=>Math.Max(0,ReferenceBalance.Range(e)+3*Tile-Dist(e.P,g)))*3+Dist(pool[0].P,g)).ToArray();
   if(reachable.Length==0){relayLaunchAt=Time+4;return;}
   relayRally=reachable[0];relayTeam=pool;relayInitialTeam=pool.Length;relayOrderedAt=Time;RelayCounterattacks++;QueueAiMarch(pool,relayRally,true,true);living=pool;
  }
  if(relayEscort==null||!relayEscort.Deployed||relayEscort.Side!=side)relayEscort=Live(side).Where(u=>u.T.Key=="engineer").OrderBy(u=>Dist(u.P,RelayStation.P)).FirstOrDefault();
  var defenders=known.Where(e=>e.T.Hit>0&&Dist(e.P,RelayStation.P)<ReferenceBalance.Range(e)+5*Tile).ToArray();
  // A safe assembly point is not the final attack order. Engage observed defenders
  // explicitly so a squad cannot wait outside its automatic acquisition radius.
  if(defenders.Length>0)foreach(var u in living.Where(u=>u.Target==null)){
   var target=defenders.OrderBy(e=>Dist(e.P,u.P)).First();Attack(u,target);
  }
  if(relayEscort==null)return;
  if(defenders.Length==0&&living.Any(u=>Dist(u.P,RelayStation.P)<11*Tile)){
   if(relayEscort.Entering!=RelayStation&&Time>=relayEngineerAt){relayEngineerAt=Time+2;EngineerOrder(relayEscort,RelayStation);}return;
  }
  if(relayEscort.Entering==RelayStation)Halt(relayEscort);
  var leader=living.OrderBy(u=>Dist(u.P,Home(side))).FirstOrDefault();
  if(leader!=null&&relayEscort.Escort!=leader)EscortUnit(relayEscort,leader);
  if(Time>=relayOrderedAt+12){
   foreach(var u in living.Where(u=>u.Target==null&&u.Path.Count==0&&Dist(u.P,relayRally)>3*Tile))Move(u,relayRally,true);
   relayOrderedAt=Time;
  }
 }
}
}
