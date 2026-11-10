using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Greyhaven;
class InfantryChecks {
 static int Main(string[] args){try{Campaign.CheckInfantry(args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {public partial class Campaign {
 static int infantryAssertions;
 static void InfAssert(bool v,string why){infantryAssertions++;if(!v)throw new Exception(why);}
 static void InfNear(float a,float b,string why){InfAssert(Math.Abs(a-b)<.003f,why+": "+a+" != "+b);}
 static Campaign InfWorld(){var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",1,2700,1600);return w;}
 internal static void CheckInfantry(string output){
  infantryAssertions=0;
  foreach(int side in Enumerable.Range(0,6)){
   var w=InfWorld();w.Funds[side]=10000;
   InfAssert(w.Require(Info("sniper"),side)!=null,"sniper needs barracks");
   var baseUnit=w.Spawn("hq",side,700,900);var barracks=w.Spawn("barracks",side,800,900);
   InfAssert(w.Require(Info("sniper"),side)!=null,"sniper needs radar");
   var radar=w.Spawn("radar",side,900,900);radar.Ready=.5f;
   InfAssert(w.Require(Info("sniper"),side)!=null,"unfinished radar cannot unlock");radar.Ready=1;
   InfAssert(w.Require(Info("sniper"),side)==null,"shared unlock six sides");
   InfAssert(w.Train(Info("sniper"),side,barracks),"sniper order accepted");
   InfAssert(w.StartConstruction(Info("bunker"),side),"bunker unlocked by base");
   InfAssert(w.Construction(side,1).Type.Key=="bunker"&&w.Construction(side,0)==null,"defense category separate from buildings");
   radar.Hp=0;InfAssert(w.Require(Info("sniper"),side)!=null,"lost prerequisite blocks new orders");
  }
  {
   var w=InfWorld();var h=w.Spawn("bunker",0,800,1000);var r=Footprint(h.T,h.P);r.X+=r.Width;
   InfAssert(!w.InBuildRange(Info("power"),r,0),"bunker is not expansion anchor");
   InfAssert(BuildAdjacentCells(h.T)==2&&h.T.FoundationWidth==2&&h.T.FoundationHeight==2,"2x2 foundation and 2 empty-cell adjacency");
   InfAssert(!w.Armed(h)&&h.T.Energy==0,"empty bunker has no gun or power demand");
   var enemyEngineer=w.Spawn("engineer",1,850,1000);InfAssert(!w.CanEngineer(enemyEngineer,h),"bunker cannot be captured");
   h.Hp=400;var engineer=w.Spawn("engineer",0,850,1050);InfAssert(w.CanEngineer(engineer,h),"own damaged bunker can be repaired");
  }
  foreach(string key in new[]{"squad","at"}){
   var w=InfWorld();var u=w.Spawn(key,0,800,1100);var enemy=w.Spawn("tank",1,900,1100);
   InfAssert(ReferenceBalance.Weapon(u).RuleId=="M60","mobile infantry uses machinegun");
   u.WeaponAim=1;InfAssert(w.ToggleInfantry(u)&&u.Entrenched,"D deploy");
   w.TickInfantryStance(u,.1f);InfNear(u.Stance,0,"lower gun before deployment starts");
   w.TickWeaponAim(u,.18f);InfNear(u.WeaponAim,0,"deployment lowers gun smoothly");
   w.TickInfantryStance(u,StanceSeconds*.4f);float halfway=u.Stance;
   InfAssert(w.ToggleInfantry(u)&&!u.Entrenched&&u.Stance==halfway,"reverse preserves current pose");
   w.TickInfantryStance(u,StanceSeconds*.1f);InfNear(u.Stance,.3f,"reverse travels backwards continuously");
   w.ToggleInfantry(u);InfNear(u.Stance,.3f,"resume does not reset pose");
   InfAssert(ChangingStance(u)&&!CanShootAt(u,enemy),"no firing during transition");w.TickInfantryStance(u,StanceSeconds);
   InfAssert(!ChangingStance(u),"deploy finishes");InfNear(ReferenceBalance.Range(u),Tile*(key=="at"?8:5),"deployed range");
   InfAssert(Crushable(u)==(key!="at"),"only deployed AT resists crushing");
   enemy.P=new PointF(800+12*Tile,1100);w.Attack(u,enemy);w.Reveal();w.FightContact(u,.1f);
   InfAssert(u.Path.Count==0&&u.Entrenched,"deployed unit must not chase out of range");
   var at=u.P;InfAssert(!w.Move(u,new PointF(-100000,-100000),false)&&u.Entrenched,"invalid movement preserves deployment");
   InfAssert(w.Move(u,new PointF(800,1160),false)&&!u.Entrenched,"valid move requests pack up");
   w.UnitTick(u,.1f,true);InfNear(Dist(u.P,at),0,"does not slide while packing");
   for(int i=0;i<50;i++)w.UnitTick(u,.1f,true);
   InfAssert(Dist(u.P,at)>10&&u.Stance==0,"moves after packing");
   w.ToggleInfantry(u);w.TickInfantryStance(u,StanceSeconds);w.Halt(u);InfAssert(u.Entrenched,"stop preserves deployed posture");
   w.ToggleInfantry(u);w.TickInfantryStance(u,StanceSeconds);InfAssert(!u.Entrenched&&ReferenceBalance.Weapon(u).RuleId=="M60","D packs up");
  }
  {
   var w=InfWorld();var sniper=w.Spawn("sniper",0,800,1100);var foot=w.Spawn("squad",1,950,1100);
   w.Fire(sniper,foot);InfAssert(!foot.Alive,"sniper kills ordinary rifleman in one shot");InfNear(sniper.Cool,10,"normal sniper cycle");
   sniper.Experience=2;foot=w.Spawn("at",1,950,1120);w.Fire(sniper,foot);InfNear(sniper.Cool,2.4f,"elite weapon plus ROF");
   var tank=w.Spawn("tank",1,850,1100);InfAssert(!UsefulTarget(sniper,tank),"sniper ignores armor automatically");
   var car=w.Spawn("ifv",0,830,1100);sniper.Carrier=car;
   InfAssert(ReferenceBalance.Weapon(car).RuleId=="CRAWP","sniper IFV weapon");
   InfNear(ReferenceBalance.Damage(car,tank,ReferenceBalance.Weapon(car)),0,"sniper IFV ineffective vs tank");
   var ordinary=w.Spawn("squad",1,900,1100);InfAssert(UsefulTarget(car,ordinary),"sniper IFV accepts infantry");
   w.Load(car);InfAssert(sniper.Carrier==null&&car.Passenger==null&&ReferenceBalance.Weapon(car).RuleId=="HoverMissile","unload restores original weapon");
  }
  {
   var w=InfWorld();var bunker=w.Spawn("bunker",0,800,1100);var door=Middle(ExitApron(bunker.T,Footprint(bunker.T,bunker.P)));
   foreach(string bad in new[]{"engineer","at","sniper","tank"}){var u=w.Spawn(bad,0,door.X,door.Y);InfAssert(!w.CanEnterHouse(u,bunker),bad+" cannot garrison bunker");u.Hp=0;}
   for(int i=0;i<5;i++){var u=w.Spawn("squad",0,door.X,door.Y);InfAssert(w.EnterHouse(u,bunker),"accept infantry "+i);InfAssert(w.TickEntry(u,.1f)&&u.Carrier==bunker,"actually enter "+i);}
   var sixth=w.Spawn("squad",0,door.X+30,door.Y);InfAssert(!w.EnterHouse(sixth,bunker),"sixth rejected");
   var enemy=w.Spawn("squad",1,bunker.P.X+120,bunker.P.Y);w.Reveal();
   var shooter=w.Units.First(u=>u.Carrier==bunker);w.TickGarrison(shooter,.1f);InfAssert(enemy.Hp<enemy.T.Hp,"garrison independently fires");
   InfAssert(w.Shots.Last().Garrison&&w.Shots.Last().SourceId==bunker.Id,"muzzle belongs to bunker");
   w.EvacuateHouse(bunker);InfAssert(w.HouseCount(bunker)==0,"D evacuates all five when free");
   var survivors=w.Units.Where(u=>u.T.Key=="squad"&&u.Side==0).Take(5).ToArray();foreach(var u in survivors){u.Carrier=bunker;u.P=bunker.P;}
   var killer=w.Spawn("tank",1,1100,1100);w.CombatKill(bunker,killer);
   InfAssert(survivors.All(u=>u.Alive&&u.Carrier==null),"destroyed bunker evacuates living occupants");
   InfAssert(survivors.All(u=>!Footprint(bunker.T,bunker.P).Contains(u.P)),"survivors escape outside foundation");
  }
  {
   var w=InfWorld();var u=w.Spawn("at",1,800,1100);var enemy=w.Spawn("tank",0,900,1100);w.PrepareAiInfantry(u,enemy);
   InfAssert(u.Entrenched,"AI deploys AT against armor");enemy.P=new PointF(1300,1100);w.PrepareAiInfantry(u,enemy);InfAssert(!u.Entrenched,"AI packs to reposition");
   u=w.Spawn("squad",1,800,1120);enemy.P=new PointF(860,1120);w.PrepareAiInfantry(u,enemy);InfAssert(u.Entrenched,"AI deploys rifleman on contact");
  }
  {
   var w=new Campaign(false,false,true);w.Units.Clear();w.Funds[1]=20000;var home=w.Home(1);
   w.Spawn("hq",1,home.X,home.Y);w.Spawn("barracks",1,home.X+96,home.Y);w.Spawn("refinery",1,home.X,home.Y+96);
   w.Spawn("power",1,home.X-96,home.Y);w.Spawn("radar",1,home.X-96,home.Y+96);
   w.Spawn("post",1,home.X+96,home.Y+96);w.Spawn("post",1,home.X+96,home.Y-96);
   var brain=w.Brain(1);w.AiBunker(1,brain);var job=w.Construction(1,1);InfAssert(job!=null&&job.Type.Key=="bunker","AI plans its own bunker");
   float budget=w.Funds[1];w.TickConstruction(30);InfAssert(job.Ready&&w.Funds[1]==budget-500,"AI actually pays for bunker");
   w.AiBunker(1,brain);var bunker=w.Units.Single(u=>u.T.Key=="bunker");bunker.Ready=1;
   var door=Middle(ExitApron(bunker.T,Footprint(bunker.T,bunker.P)));
   for(int i=0;i<3;i++)w.Spawn("squad",1,door.X+i*10,door.Y+16);
   w.AiBunker(1,brain);InfAssert(w.Units.Count(u=>u.Entering==bunker)==3,"AI assigns three real infantry");
   foreach(var unit in w.Units.Where(u=>u.Entering==bunker).ToArray()){unit.P=door;w.TickEntry(unit,.1f);}
   InfAssert(w.HouseCount(bunker)==3,"AI troops occupy bunker");
   var known=Enumerable.Range(0,3).Select(i=>w.Spawn("squad",0,home.X+300+i*20,home.Y)).ToArray();
   InfAssert(w.AiInfantry(1,known)=="sniper","AI chooses counter-infantry sniper after radar");
   var shoot=w.Units.First(u=>u.Carrier==bunker);var target=known[0];target.P=new PointF(bunker.P.X+6*Tile,bunker.P.Y);
   InfAssert(CanShootAt(shoot,target),"bunker 6 cell circle boundary");target.P=new PointF(target.P.X+.02f,target.P.Y);
   InfAssert(!CanShootAt(shoot,target),"bunker ring matches actual range");
  }
  foreach(string key in new[]{"squad","sniper","at"}){
   var w=InfWorld();var shooter=w.Spawn(key,0,800,1100);var target=w.Spawn("tank",1,850,1100);w.Reveal();w.Attack(shooter,target);
   w.FightContact(shooter,.01f);InfAssert(w.Shots.Count==0,"must shoulder weapon before first shot "+key);
   w.TickWeaponAim(shooter,.09f);InfNear(shooter.WeaponAim,.5f,"half aim transition "+key);w.FightContact(shooter,.01f);InfAssert(w.Shots.Count==0,"no early flash "+key);
   w.TickWeaponAim(shooter,.09f);w.FightContact(shooter,.01f);InfAssert(w.Shots.Count==1&&w.Shots[0].Aim==1,"aim and muzzle synchronized "+key);
   float cooldown=shooter.Cool;w.FightContact(shooter,.01f);InfNear(shooter.Cool,cooldown,"animation does not reset cooldown "+key);
   InfAssert(w.Move(shooter,new PointF(800,1200),false),"march order "+key);w.TickWeaponAim(shooter,.2f);InfNear(shooter.WeaponAim,0,"lower weapon for march "+key);
  }
  Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"rules-verification.json"),"{\"passed\":true,\"assertions\":"+infantryAssertions+",\"scope\":\"unlocks, deployment, aim-before-fire, movement, IFV, bunker, paid AI construction/recruitment/garrison\"}");
  Console.WriteLine("PASS infantry integration: "+infantryAssertions+" assertions");
 }
}}
