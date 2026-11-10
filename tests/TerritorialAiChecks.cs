using System;
using System.Drawing;
using System.Linq;
using Greyhaven;
class TerritorialAiChecks {
 static int Main(){try{Campaign.CheckTerritorialAi();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void TerritoryAssert(bool ok,string text){if(!ok)throw new Exception(text);}
 internal static void CheckTerritorialAi(){
  var w=new Campaign(false,false,true,true);w.Units.Clear();w.Ore.Clear();w.Time=400;var home=w.Home(2);
  w.Spawn("mcv",0,w.Home(0).X,w.Home(0).Y);w.Spawn("mcv",1,w.Home(1).X,w.Home(1).Y);w.Spawn("mcv",3,w.Home(3).X,w.Home(3).Y);
  var hq=w.Spawn("hq",2,home.X,home.Y);w.Spawn("refinery",2,home.X-120,home.Y+120);w.Spawn("refinery",2,home.X+120,home.Y+144);w.Spawn("factory",2,home.X+120,home.Y);
  for(int i=0;i<30;i++)w.Spawn(i%3==0?"at":"tank",2,home.X-120+i%6*24,home.Y-96+i/6*24);
  var brain=w.Brain(2);w.SmartTactics(2,true);
  TerritoryAssert(brain.HomeGuard.Count==20,"Two exposed fronts retain standing defence and local reserves");
  TerritoryAssert(brain.HomeGuard.All(p=>p.GuardHome.HasValue),"Standing guards keep a return anchor");
  TerritoryAssert((brain.Wings.Count>0||brain.FieldGuard.Count>0)&&brain.Wings.All(g=>g.Units.All(p=>!brain.HomeGuard.Contains(p)))&&brain.FieldGuard.All(p=>!brain.HomeGuard.Contains(p)),"Only surplus units may join attack wings or field posts");
  TerritoryAssert(brain.Wings.All(g=>g.Phase==0&&g.InitialValue>0&&g.Objective!=g.Goal),"Attack teams muster before advancing");
  var guards=brain.HomeGuard.ToArray();var invader=w.Spawn("tank",1,guards[0].P.X+30,guards[0].P.Y);w.SmartTactics(2,false);
  TerritoryAssert(brain.HomeGuard.All(p=>p.GuardHome.HasValue),"Defensive combat preserves return orders");
  var placement=Footprint(Info("power"),new PointF(home.X-120,home.Y+180));
  TerritoryAssert(!w.AiKeepsHaulLanes(2,Info("power"),placement),"AI construction preserves refinery approach lanes");

  w=new Campaign(true);w.Units.Clear();w.Ore.Clear();var unit=w.Spawn("tank",1,900,1000);
  w.QueueAiMarch(new[]{unit},new PointF(1400,1000));var original=w.aiMoveVersions[unit];w.CancelAiMove(unit);w.Halt(unit);
  w.QueueAiMarch(new[]{unit},new PointF(1000,1100),false,true);TerritoryAssert(w.aiMoveVersions[unit]!=original,"Replacement orders get a fresh version");
  w.TickAiMarchRequests();TerritoryAssert(!w.aiMovesPending.Contains(unit)&&!unit.Advancing&&Dist(unit.Goal,new PointF(1000,1100))<1,"Cancelled advance cannot overwrite withdrawal");
  var team=Enumerable.Range(0,6).Select(i=>w.Spawn("squad",1,1000+i*24,1300)).ToArray();brain=w.Brain(1);
  var wing=new AiWing{Units=team,Phase=2,Opponent=0,InitialValue=team.Sum(p=>p.T.Price),Objective=new PointF(1500,1300),Goal=new PointF(1500,1300),OrderedAt=w.Time};brain.Wings.Add(wing);
  foreach(var casualty in team.Take(4))casualty.Hp=0;
  w.UpdateAiWings(1,brain,new Piece[0],new System.Collections.Generic.HashSet<Piece>());
  TerritoryAssert(wing.Phase==3&&brain.Withdrawals==1,"Heavily depleted team returns for reorganisation");
  TerritoryAssert(w.aiMoveRequests.Any(p=>!p.AttackMove),"Withdrawal uses movement rather than attack-move");
  Console.WriteLine("Territorial AI: standing guards, surplus-only departures, staging, return anchors, refinery access, stale-order cancellation and casualty withdrawal passed.");
 }
}
}
