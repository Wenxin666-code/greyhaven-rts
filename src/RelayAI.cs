using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 const int RelayGuardCount=4;
 const int RelayWingSize=6;
 const float RelayWingStallSeconds=34;

 sealed class RelayWing {
  public Piece[] Units;
  public PointF Approach;
  public int InitialCount;
  public float OrderedAt, ProgressAt, BestDistance;
  public bool Rerouted, Returning;
 }

 readonly HashSet<Piece> relayReserve=new HashSet<Piece>();
 readonly HashSet<Piece> relayForward=new HashSet<Piece>();
 readonly List<RelayWing> relayWings=new List<RelayWing>();
 Piece relayEngineer, relayCarrier;
 float relayDecisionAt, relayLaunchAt, relayStageAt, relayBoardDeadline, relayTransportAt;
 float relayScoutAt=80;
 bool relayAidMobilized;
 public int RelayCounterattacks {get;private set;}

 bool RelayAssigned(Piece unit){
  return unit==relayCarrier||relayReserve.Contains(unit)||relayWings.Any(wing=>wing.Units.Contains(unit));
 }

 Piece[] RelayAvailable(int side){
  return Live(side).Where(u=>!u.T.Building&&Armed(u)&&u.Entering==null&&u.Target==null&&!RelayAssigned(u)).ToArray();
 }

 int RelayRecruitSlots(){
  // One wave can advance while its replacement gathers. The support order
  // adds a second assault slot and funds its replacements independently.
  int formingTeams=RelaySupportGranted&&RelayStation.Side!=1?3:2;
  return RelayWingSize*formingTeams;
 }

 PointF RelayStagingPoint(){
  // The eastern ridge is already connected to the enemy base by road R.
  var junction=RiverbendMap.RelayWorld(58,23);
  return AiOpenPoint(new PointF(junction.X+2*Tile,junction.Y+3*Tile));
 }

 void MaintainRelayGuard(int side,Piece[] known){
  var home=Home(side);
  relayReserve.RemoveWhere(u=>!u.Deployed||u.Side!=side||u.Entering!=null);
  foreach(var unit in Live(side).Where(u=>u.T.Key=="squad"&&!RelayAssigned(u)&&Dist(u.P,home)<12*Tile).OrderBy(u=>Dist(u.P,home))){
   if(relayReserve.Count>=RelayGuardCount)break;
   relayReserve.Add(unit);
   CancelAiMove(unit);
   Guard(unit);
  }
  var threat=known.Where(e=>e.T.Hit>0&&Dist(e.P,home)<17*Tile).OrderBy(e=>Dist(e.P,home)).FirstOrDefault();
  if(threat==null)return;
  foreach(var guard in relayReserve){
   if(guard.Target==null&&GuardAllows(guard,threat))TakeContact(guard,threat,ContactOrder.Automatic);
  }
  foreach(var unit in RelayAvailable(side).Where(u=>!u.T.Foot&&Dist(u.P,home)<14*Tile).Take(3)){
   CancelAiMove(unit);
   Attack(unit,threat);
  }
 }

 void StageRelayVehicles(int side){
  relayForward.RemoveWhere(u=>!u.Deployed||u.Side!=side||RelayAssigned(u));
  if(Time<relayStageAt)return;
  relayStageAt=Time+12;
  var staging=RelayStagingPoint();
  var candidates=RelayAvailable(side).Where(u=>!u.T.Foot&&!relayForward.Contains(u)&&!aiMovesPending.Contains(u))
   .OrderBy(u=>Dist(u.P,staging)/ReferenceBalance.Speed(u)).Take(Math.Max(0,4-relayForward.Count)).ToArray();
  foreach(var unit in candidates)relayForward.Add(unit);
  var moving=relayForward.Where(u=>u.Target==null&&!aiMovesPending.Contains(u)&&Dist(u.P,staging)>3*Tile&&
   (u.Path.Count==0||u.NoProgressFor>8)).ToArray();
  QueueAiMarch(moving,staging,true,true,true);
 }

 bool RelayApproach(Piece leader,PointF? skip,out PointF approach){
  var p=RelayStation.P;
  var candidates=new[]{new PointF(p.X+7*Tile,p.Y+2*Tile),new PointF(p.X,p.Y+7*Tile),new PointF(p.X-7*Tile,p.Y),new PointF(p.X,p.Y-7*Tile)};
  foreach(var point in candidates.Select(AiOpenPoint).Distinct().OrderBy(g=>Dist(leader.P,g))){
   if(skip.HasValue&&Dist(point,skip.Value)<2*Tile)continue;
   if(CanMoveTo(leader,point)){approach=point;return true;}
  }
  approach=PointF.Empty;
  return false;
 }

 void LaunchRelayWing(int side){
  int limit=RelaySupportGranted?AssaultTeamLimit:1;
  if(Time<relayLaunchAt||relayWings.Count(existing=>!existing.Returning)>=limit)return;
  relayLaunchAt=Time+6;
  var ranked=RelayAvailable(side).OrderBy(u=>Dist(u.P,RelayStation.P)/ReferenceBalance.Speed(u)).ToArray();
  if(ranked.Length<4)return;
  PointF approach;
  if(!RelayApproach(ranked[0],null,out approach))return;
  var units=ranked.Take(RelayWingSize).ToArray();
  foreach(var unit in units){relayForward.Remove(unit);CancelAiMove(unit);}
  if(QueueAiMarch(units,approach,true,true,true)==0)return;
  var wing=new RelayWing{Units=units,Approach=approach,InitialCount=units.Length,OrderedAt=Time,ProgressAt=Time,BestDistance=float.MaxValue};
  relayWings.Add(wing);
  RelayCounterattacks++;
  relayLaunchAt=Time+(RelaySupportGranted?22:32);
 }

 void UpdateRelayWings(int side,Piece[] stationDefenders){
  foreach(var wing in relayWings.ToArray()){
   wing.Units=wing.Units.Where(u=>u.Deployed&&u.Side==side&&u.Entering==null).ToArray();
   if(wing.Units.Length==0){relayWings.Remove(wing);continue;}
   if(wing.Returning){
    if(Time-wing.OrderedAt>=100||wing.Units.All(u=>Dist(u.P,wing.Approach)<8*Tile))relayWings.Remove(wing);
    continue;
   }

   foreach(var unit in wing.Units.Where(u=>Dist(u.P,RelayStation.P)<19*Tile&&(u.Target==null||!u.Target.Alive))){
    var defender=stationDefenders.Where(e=>UsefulTarget(unit,e)).OrderBy(e=>TargetDistance(unit,e)).FirstOrDefault();
    if(defender!=null){CancelAiMove(unit);Attack(unit,defender);}
   }
   // Track the head of the column and actual firing, so a surviving but
   // immobile group cannot reserve the only route to the station forever.
   float distance=wing.Units.Select(u=>Dist(u.P,wing.Approach)).OrderBy(d=>d).Take(3).Average();
   if(distance+Tile<wing.BestDistance||wing.Units.Any(u=>VisualTime-u.FiredAt<4)){
    wing.BestDistance=distance;
    wing.ProgressAt=Time;
   }
   bool depleted=wing.Units.Length<=wing.InitialCount/2&&Time-wing.OrderedAt>6;
   if(!depleted&&Time-wing.ProgressAt<RelayWingStallSeconds)continue;
   PointF alternate;
   if(!depleted&&!wing.Rerouted&&RelayApproach(wing.Units.OrderBy(u=>Dist(u.P,RelayStation.P)).First(),wing.Approach,out alternate)){
    foreach(var unit in wing.Units)Halt(unit);
    wing.Rerouted=true;wing.Approach=alternate;wing.ProgressAt=Time;wing.BestDistance=float.MaxValue;
    QueueAiMarch(wing.Units,alternate,true,true,true);
   }else{
    foreach(var unit in wing.Units)Halt(unit);
    wing.Returning=true;wing.OrderedAt=Time;wing.Approach=Brain(side).Rally;
    QueueAiMarch(wing.Units,wing.Approach,false,true,true);
   }
  }
 }

 void MoveRelayEngineer(int side,bool retake,Piece[] defenders){
  if(relayEngineer==null||!relayEngineer.Alive||relayEngineer.Side!=side){
   relayEngineer=Units.Where(u=>u.Alive&&u.Side==side&&u.T.Key=="engineer").OrderBy(u=>Dist(u.P,RelayStation.P)).FirstOrDefault();
   relayCarrier=relayEngineer==null?null:relayEngineer.Carrier;
  }
  if(relayEngineer==null)return;
  var engineer=relayEngineer;
  var staging=RelayStagingPoint();
  bool escorted=relayWings.Any(wing=>!wing.Returning&&wing.Units.Any(u=>u.Deployed&&Dist(u.P,RelayStation.P)<13*Tile));
  bool canEnter=retake&&escorted&&defenders.Length==0;
  if(engineer.Carrier!=null){
   var carrier=engineer.Carrier;
   if(canEnter&&Dist(carrier.P,RelayStation.P)<18*Tile&&TryUnloadPassenger(carrier,engineer)){
    relayCarrier=null;
    EngineerOrder(engineer,RelayStation);
   }else if(Time>=relayTransportAt&&Dist(carrier.P,staging)>3*Tile&&(carrier.Path.Count==0||carrier.NoProgressFor>8)){
    relayTransportAt=Time+8;
    Move(carrier,staging,false);
   }
   return;
  }
  if(engineer.Entering!=null){
   if(engineer.Entering==RelayStation){if(retake&&defenders.Length>0)Halt(engineer);else return;}
   else if(engineer.Entering==relayCarrier){
    if(Time<relayBoardDeadline)return;
    Halt(engineer);
   }
  }
  relayCarrier=null;
  if(canEnter&&Dist(engineer.P,RelayStation.P)<22*Tile){
   if(Time>=relayTransportAt){relayTransportAt=Time+4;EngineerOrder(engineer,RelayStation);}
   return;
  }
  if(Time<relayTransportAt)return;
  relayTransportAt=Time+6;
  var transport=RelayAvailable(side).Where(u=>u.T.Key=="ifv"&&CanBoard(engineer,u)&&Dist(u.P,engineer.P)<12*Tile)
   .OrderBy(u=>Dist(u.P,engineer.P)).FirstOrDefault();
  if(transport!=null){
   CancelAiMove(transport);
   Halt(transport);
   if(Board(engineer,transport)){
    relayForward.Remove(transport);relayCarrier=transport;relayBoardDeadline=Time+30;return;
   }
  }
  // An engineer can advance on foot if no usable transport survived.
  var leader=relayWings.Where(wing=>!wing.Returning).SelectMany(wing=>wing.Units).OrderBy(u=>Dist(u.P,engineer.P)).FirstOrDefault();
  if(retake&&leader!=null&&engineer.Escort!=leader)EscortUnit(engineer,leader);
  if(canEnter&&engineer.Path.Count==0)EngineerOrder(engineer,RelayStation);
 }

 void RelayTactics(int side){
  if(side!=1||HasSurrendered(side)||RelayStation==null||!RelayStation.Alive||Time<relayDecisionAt)return;
  relayDecisionAt=Time+1;
  var known=AiKnown(side);
  RetreatAiMiners(side,Brain(side),known);
  MaintainRelayGuard(side,known);
  bool retake=RelayStation.Side!=side;
  var defenders=known.Where(e=>e.T.Hit>0&&Dist(e.P,RelayStation.P)<ReferenceBalance.Range(e)+5*Tile).ToArray();
  MoveRelayEngineer(side,retake,defenders);
  if(!retake){
   foreach(var wing in relayWings){
    foreach(var unit in wing.Units.Where(u=>u.Deployed&&u.Side==side)){
     CancelAiMove(unit);GuardAt(unit,wing.Approach);
    }
   }
   relayWings.Clear();
   StageRelayVehicles(side);
   var threat=known.Where(e=>e.T.Hit>0&&(Dist(e.P,RelayStation.P)<24*Tile||Dist(e.P,RiverbendMap.RelayWorld(53,48))<18*Tile))
    .OrderBy(e=>Dist(e.P,RelayStation.P)).FirstOrDefault();
   if(threat!=null){
    foreach(var unit in RelayAvailable(side).Where(u=>Dist(u.P,threat.P)<32*Tile).OrderBy(u=>Dist(u.P,threat.P)).Take(RelayWingSize)){
     CancelAiMove(unit);Attack(unit,threat);
    }
   }else if(Time>=relayScoutAt){
    relayScoutAt=Time+85;
    var patrol=RelayAvailable(side).Where(u=>!relayForward.Contains(u)&&!u.GuardHome.HasValue).OrderByDescending(ReferenceBalance.Speed).Take(3).ToArray();
    QueueAiMarch(patrol,RiverbendMap.RelayWorld(42,66),true,false,true);
   }
   return;
  }
  StageRelayVehicles(side);
  if(RelaySupportGranted&&!relayAidMobilized){relayAidMobilized=true;relayLaunchAt=Time;}
  UpdateRelayWings(side,defenders);
  LaunchRelayWing(side);
 }
}
}
