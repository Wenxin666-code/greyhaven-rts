using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {public partial class Campaign {
 static void RelayRun(Campaign w,float seconds){for(int i=0;i<(int)Math.Ceiling(seconds*30)&&!w.Won&&!w.Lost;i++)w.Tick(1f/30);}
 static Piece RelayBuild(Campaign w,string key,PointF preferred){
  var type=Info(key);RelayAssert(w.StartConstruction(type),"player cannot queue "+key);
  int steps=0;while(!w.Construction(type).Ready&&steps++<120*30)w.Tick(1f/30);
  RelayAssert(w.Construction(type).Ready,"player construction stalled "+key);
  PointF p;RelayAssert(w.AiBuildSite(0,type,preferred,out p)&&w.Build(type,p),"no legal player placement "+key);
  RelayRun(w,2);return w.Units.Last(u=>u.Side==0&&u.T==type);
 }
 internal static void CheckRelaySimulation(){
  var w=new Campaign(false,false,false,false,true);w.PrepareBattleNavigation();
  var h=w.Home(0);RelayAssert(w.Deploy(w.Live(0).First(u=>u.T.Key=="mcv")),"relay MCV deploy");RelayRun(w,4);
  RelayBuild(w,"power",new PointF(h.X-4*Tile,h.Y));
  var plant=RelayBuild(w,"refinery",new PointF(h.X-5*Tile,h.Y+4*Tile));
  RelayBuild(w,"barracks",new PointF(h.X+4*Tile,h.Y));
  float baselineStock=w.Ore.Sum(p=>p.Stock);int beforeReceipts=w.OreReceipts.Sum();float initialCargo=w.Units.Sum(u=>u.Cargo);
  RelayRun(w,180);
  RelayAssert(w.OreReceipts[0]>=LoadValue&&w.OreReceipts[1]>=LoadValue,"both bases need real mining income: t="+w.Time+" won="+w.Won+" lost="+w.Lost+" receipts="+String.Join("/",w.OreReceipts)+" trucks="+String.Join(";",w.Live(0).Concat(w.Live(1)).Where(u=>u.T.Key=="miner").Select(u=>u.Side+":"+u.P+":"+w.MiningStatus(u)+":dock="+(u.Dock==null?"none":u.Dock.Id.ToString())+":path="+u.Path.Count)));
  RelayAssert(w.Units.Count(u=>u.Side==1&&u.T.Key=="miner")>=2&&w.Brain(1).Orders>=4,"enemy must grow from real economy");
  double removed=baselineStock-w.Ore.Sum(p=>p.Stock),accounted=w.OreReceipts.Sum()-beforeReceipts+w.Units.Sum(u=>u.Cargo)-initialCargo;
  RelayAssert(Math.Abs(removed-accounted)<4,"relay ore conservation");
  RelayAssert(w.Funds.All(n=>n>=0)&&!w.Won&&!w.Lost,"opening result/negative funds");
  Console.WriteLine("Relay opening {0:F0}s: player receipts={1}, enemy receipts={2}, enemy production orders={3}; finite ore conserved.",w.Time,w.OreReceipts[0],w.OreReceipts[1],w.Brain(1).Orders);

  w=new Campaign(false,false,false,false,true);w.PrepareBattleNavigation();
  foreach(var e in w.Live(1).Where(u=>u.T.Key=="engineer").ToArray())w.Kill(e);
  int firstId=w.Units.Max(u=>u.Id);w.RelayStation.Side=0;w.Changed();w.Reveal();
  for(int i=0;i<(RelayDuration-1)*30&&w.RelayStation.Side==0&&!w.Won&&!w.Lost;i++)w.Tick(1f/30);
  RelayAssert(w.RelayStation.Side==1&&w.RelayCounterattacks>0,"paid AI engineer failed to recapture before objective completion: time="+w.Time+" waves="+w.RelayCounterattacks+" engineers="+String.Join(";",w.Live(1).Where(u=>u.T.Key=="engineer").Select(u=>u.P+":path="+u.Path.Count+":entry="+(u.Entering==null?"none":u.Entering.T.Key)+":distance="+Dist(u.P,w.RelayStation.P))));
  RelayAssert(w.Units.Any(u=>u.Id>firstId&&u.T.Key=="engineer"&&!u.Alive),"retake must use a produced, consumed engineer");
  RelayAssert(w.relayReserve.Count>=4,"enemy home reserve abandoned");
  float saved=w.RelayProgress;RelayRun(w,10);RelayAssert(w.RelayProgress==saved&&!w.Won,"live recapture must pause progress");
  Console.WriteLine("Relay AI: produced engineer retook station by {0:F1}s; {1} counterattack(s), {2} home guards, saved progress {3:F1}s.",w.Time,w.RelayCounterattacks,w.relayReserve.Count,saved);

  // Finite logistical denial: no barracks/HQ/MCV/factory/engineer means no scripted replacement.
  w=new Campaign(false,false,false,false,true);w.RelayStation.Side=0;
  foreach(var u in w.Live(1).Where(u=>new[]{"barracks","factory","hq","mcv","engineer"}.Contains(u.T.Key)).ToArray())w.Kill(u);
  RelayRun(w,361);RelayAssert(w.Won&&!w.Lost&&w.Live(1).Any(),"communications victory with surviving enemy combat units");
  Console.WriteLine("Relay logistics denial: 360s hold wins with enemy survivors; no free engineer reinforcement.");

  // A staffed objective must produce a real fight, not an engineer walking through idle escorts.
  w=new Campaign(false,false,false,false,true);w.RelayStation.Side=0;var center=w.RelayStation.P;
  foreach(var u in w.Live(1).Where(u=>!u.T.Building&&Dist(u.P,center)<20*Tile).ToArray())w.Kill(u);
  int deployed=0;foreach(var key in new[]{"tank","tank","tank","tank","squad","squad","squad","squad","squad","squad","at","at"}){
   int slot=deployed++;var point=w.AiOpenPoint(new PointF(center.X+(-5+slot%4*3)*Tile,center.Y+(4+slot/4*2)*Tile));
   var unit=w.Spawn(key,0,point.X,point.Y);w.Guard(unit);
  }
  var deadIds=new System.Collections.Generic.HashSet<int>();
  for(int i=0;i<361*30&&!w.Won&&!w.Lost;i++){w.Tick(1f/30);foreach(var u in w.Units.Where(u=>u.Side==1&&!u.Alive&&u.T.Hit>0))deadIds.Add(u.Id);}
  int enemyCasualties=deadIds.Count;
  Console.WriteLine("Relay defended hold fixture: result={0}, counterattacks={1}, enemy combat losses={2}, player survivors={3}, progress={4:F1}s.",w.Won?"won":w.Lost?"lost":"ongoing",w.RelayCounterattacks,enemyCasualties,w.Live(0).Count(u=>!u.T.Building),w.RelayProgress);
  RelayAssert(w.RelayCounterattacks>=2&&enemyCasualties>0,"defended station has no actual repeated combat");

  CheckRelayPhysicalRoutes();
 }
 internal static void CheckRelayPhysicalRoutes(){
  // Run physical movement, not only route queries, through the entire western approach.
  var w=new Campaign(false,false,false,false,true);w.PrepareBattleNavigation();w.nextAiDecision[1]=float.MaxValue;
  foreach(var u in w.Live(1).Where(u=>u.T.Hit>0).ToArray())w.Kill(u);
  var car=w.Live(0).First(u=>u.T.Key=="ifv");var eng=w.Live(0).First(u=>u.T.Key=="engineer");
  var route=RiverbendMap.RelayRoads.Single(r=>r.Id=="B");
  foreach(var waypoint in route.Points.Skip(1)){
   var p=w.AiOpenPoint(new PointF(waypoint.X*Tile,waypoint.Y*Tile));
   RelayAssert(w.Move(car,p,false),"western IFV order");
   for(int i=0;i<110*30&&Dist(car.P,p)>2*Tile;i++)w.Tick(1f/30);
   RelayAssert(Dist(car.P,p)<=2*Tile,"western road physical movement stalled at "+p+" got "+car.P);
  }
  RelayAssert(w.EngineerOrder(eng,w.RelayStation),"engineer full approach order");
  float length=0;var previous=eng.P;foreach(var point in eng.Path){length+=Dist(previous,point);previous=point;}
  float travelBudget=length/eng.T.Speed+60;
  for(int i=0;i<travelBudget*30&&w.RelayStation.Side!=0;i++)w.Tick(1f/30);
  RelayAssert(w.RelayStation.Side==0&&!eng.Alive,"engineer actual long-distance path/entry");
  Console.WriteLine("Relay physical routes: IFV completed western approach; engineer walked from the player start and entered the 3x3 station.");
 }
}}
