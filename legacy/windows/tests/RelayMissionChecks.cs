using System;
using System.Drawing;
using System.Linq;
using Greyhaven;
class RelayMissionChecks {
 static int Main(){try{Campaign.CheckRelaySupport();Campaign.CheckRelayMission();Campaign.CheckRelaySimulation();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void RelayAssert(bool value,string message){if(!value)throw new Exception(message);}
 internal static void CheckRelaySupport(){
  var w=new Campaign(false,false,false,false,true);w.RelayStation.Side=0;w.Funds[1]=0;
  var factory=w.Live(1).First(u=>u.T.Key=="factory");var tank=Info("tank");
  RelayAssert(w.Train(tank,1,factory),"support production fixture");w.UnitTick(factory,1,true);
  RelayAssert(factory.Work==0,"unfunded production must wait");
  int count=w.Units.Count,receipts=w.OreReceipts[1];float ore=w.Ore.Sum(p=>p.Stock);
  w.TickRelayObjective(179.5f);RelayAssert(!w.RelaySupportResolved&&w.Funds[1]==0,"support fired before 50%");
  w.Time=200;w.TickRelayObjective(.5f);
  RelayAssert(w.RelayProgress==180&&w.RelaySupportGranted&&w.Funds[1]==RelaySupportFunds,"50% grant missing or wrong amount");
  RelayAssert(w.Units.Count==count&&w.OreReceipts[1]==receipts&&w.Ore.Sum(p=>p.Stock)==ore,"support spawned units or mutated mining accounts");
  string notice=w.RelaySupportNotice;RelayAssert(w.Radio==notice&&w.EventLabel==notice&&w.RelaySupportAt==200,"support notice missing");
  w.Radio="普通生产提示";w.TickRelayObjective(1);RelayAssert(w.RelaySupportNotice==notice,"radio overwrote permanent support notice");
  w.RelayStation.Side=1;w.TickRelayObjective(40);w.RelayStation.Side=0;w.TickRelayObjective(3);
  RelayAssert(w.Funds[1]==RelaySupportFunds&&w.RelaySupportAt==200,"recapture duplicated aid");
  for(int i=0;i<30*30&&factory.Queue.Count>0;i++)w.UnitTick(factory,1f/30,true);
  RelayAssert(factory.Queue.Count==0&&w.Funds[1]==RelaySupportFunds-tank.Price&&w.Units.Count==count+1,"aid did not fund ordinary timed production");
  w.TickRelayObjective(1000);RelayAssert(w.Won&&w.Funds[1]==RelaySupportFunds-tank.Price,"completion duplicated support");
  w=new Campaign(false,false,false,false,true);w.RelayStation.Side=0;int funds=w.Funds[1];w.TickRelayObjective(400);
  RelayAssert(w.Won&&w.RelaySupportGranted&&w.Funds[1]==funds+RelaySupportFunds,"large step skipped or duplicated threshold");
  w=new Campaign(false,false,false,false,true);w.Surrender(1);w.RelayStation.Side=0;funds=w.Funds[1];count=w.Units.Count;w.TickRelayObjective(180);
  RelayAssert(w.RelaySupportResolved&&!w.RelaySupportGranted&&w.Funds[1]==funds&&w.Units.Count==count&&!w.Live(1).Any(),"aid resurrected surrendered enemy");
  w=new Campaign(false,false,false,false,true);w.RelayStation.Side=0;
  foreach(var u in w.Live(1).Where(u=>!IsRelayStation(u)).ToArray())w.Kill(u);
  funds=w.Funds[1];w.TickRelayObjective(180);RelayAssert(!w.RelaySupportGranted&&w.Funds[1]==funds,"aid granted to eliminated enemy");
  w=new Campaign(false,false,false,false,true);w.RelayStation.Side=0;w.TickRelayObjective(179);funds=w.Funds[1];w.RelayStation.Hp=0;w.TickRelayObjective(2);
  RelayAssert(w.Lost&&!w.RelaySupportResolved&&w.Funds[1]==funds,"destroyed station triggered aid");
  w=new Campaign();w.ResolveRelaySupport();RelayAssert(!w.RelaySupportResolved,"non-relay campaign received aid");
  Console.WriteLine("Relay support: exact 50% threshold, one scaled grant, paid tank production, retained notice, recapture/overshoot, defeat and destruction checks passed.");
 }
 internal static void CheckRelayMission(){
  var w=new Campaign(false,false,false,false,true);w.PrepareBattleNavigation();
  RelayAssert(w.FactionCount==2&&w.River.Houses.Count==14&&w.Ore.Sum(p=>p.Stock)==528000,"relay setup counts");
  RelayAssert(w.Live(0).Count()==5&&w.Live(0).All(u=>!u.T.Building)&&w.Live(1).Count(u=>u.T.Key=="miner")==1,"opening roster");
  foreach(var u in w.Units.Where(u=>u.Deployed)){
   if(u.T.Building)RelayAssert(w.TerrainBuildable(Footprint(u.T,u.P)),"Building on slope/water: "+u.T.Key+" "+u.P);
   else RelayAssert(!w.Blocked(u.P,u.T.Clearance,null),"Unit blocked at spawn: "+u.T.Key+" "+u.P);
  }
  foreach(var route in RiverbendMap.RelayRoads)foreach(var key in new[]{"tank","miner","mcv"}){
   var unit=new Piece{T=Info(key),Side=0,Hp=100,P=new PointF((route.Points[0].X+.5f)*Tile,(route.Points[0].Y+.5f)*Tile)};
   unit.P=w.AiOpenPoint(unit.P);
   foreach(var p in route.Points.Skip(1)){
    var target=w.AiOpenPoint(new PointF(p.X*Tile,p.Y*Tile));
    RelayAssert(w.PlanRoute(unit,target,null),"Unreachable "+route.Id+" / "+key+" to "+target);unit.P=target;
   }
  }
  Console.WriteLine("Relay terrain: full radar domain, 14 houses, 528000 ore; all three vehicle types can route through A/B/C/R/L.");
  var station=w.RelayStation;var engineer=w.Live(0).Single(u=>u.T.Key=="engineer");
  RelayAssert(!w.CanSell(station)&&!w.InBuildRange(Info("power"),new RectangleF(station.P.X+5*Tile,station.P.Y,2*Tile,2*Tile),0),"station ownership/anchor rules");
  engineer.P=Middle(ExitApron(station.T,Footprint(station.T,station.P)));RelayAssert(w.EngineerOrder(engineer,station),"engineer entry order");w.TickEngineerEntry(engineer,.05f);
  RelayAssert(station.Side==0&&!engineer.Alive,"capture consumes engineer");
  RelayAssert(!w.StartConstruction(RelayType)&&!w.CanSell(station)&&!w.CanRelocate(station,station.P),"objective is not buyable/sellable/packable");
  RelayAssert(!w.InBuildRange(Info("power"),new RectangleF(station.P.X+5*Tile,station.P.Y,2*Tile,2*Tile),0),"captured station must not become a build anchor");
  var attacker=w.Live(1).First(u=>u.T.Key=="tank");attacker.P=new PointF(station.P.X+5*Tile,station.P.Y);w.Reveal();
  RelayAssert(w.FindContact(attacker,ScanRange(attacker))!=station,"automatic fire must spare the objective");
  w.Attack(attacker,station);RelayAssert(attacker.Target!=station,"ordinary attack must spare objective");
  w.Attack(attacker,station,true);RelayAssert(attacker.Target==station,"explicit force fire still targets objective");w.Halt(attacker);
  station.Hp=600;w.Spawn("factory",0,w.Home(0).X+9*Tile,w.Home(0).Y);RelayAssert(w.Power(0)<0,"brownout fixture");
  w.TickRelayObjective(110);station.Side=1;w.TickRelayObjective(100);RelayAssert(w.RelayProgress==110&&!w.Won,"recapture pauses without resetting");
  station.Side=0;w.TickRelayObjective(250);RelayAssert(w.Won&&w.Live(1).Any(),"restore wins while enemy army remains");
  w=new Campaign(false,false,false,false,true);station=w.RelayStation;w.Surrender(1);RelayAssert(station.Alive&&station.Side==-1,"surrender preserves neutral infrastructure");
  w.TickRelayObjective(1);RelayAssert(!w.Won&&!w.Lost,"no early extermination victory");
  station.Side=0;w.TickRelayObjective(359);station.Hp=0;w.TickRelayObjective(1);RelayAssert(w.Lost&&!w.Won,"destruction precedes same-frame completion");
  w=new Campaign(false,false,false,false,true);engineer=w.Live(0).Single(u=>u.T.Key=="engineer");var car=w.Live(0).Single(u=>u.T.Key=="ifv");engineer.Carrier=car;
  foreach(var u in w.Units.Where(u=>u.Side==0&&u!=car&&u!=engineer).ToArray())w.Kill(u);w.TickRelayObjective(1);RelayAssert(!w.Lost,"passenger engineer is retained");
  w.Kill(car);w.TickRelayObjective(1);RelayAssert(w.Lost,"irreversible loss of capture ability");
  Console.WriteLine("Relay objectives: capture, saved progress, surrender, destruction priority and passenger capability checks passed.");
 }
}
}
