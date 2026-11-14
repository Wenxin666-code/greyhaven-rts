using System;
using System.Drawing;
using System.Linq;
using Greyhaven;
class PerformanceAndIFVChecks {
 static int Main(){try{Campaign.CheckIFVPassengers();Campaign.CheckDeferredNavigation();Campaign.CheckThirdTerrain();Campaign.CheckNavigationAcceleration();Campaign.CheckThirdOpening();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void CheckIFV(bool ok,string reason){if(!ok)throw new Exception(reason);}
 internal static void CheckIFVPassengers(){
  var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();
  var car=w.Spawn("ifv",0,600,900);var foot=w.Spawn("squad",0,627,900);var eng=w.Spawn("engineer",0,560,900);var at=w.Spawn("at",0,560,940);var tank=w.Spawn("tank",0,800,900);
  CheckIFV(!w.CanBoard(foot,tank),"main battle tanks cannot carry infantry");
  CheckIFV(ReferenceBalance.Weapon(car).RuleId=="HoverMissile","empty IFV missile mode");
  w.Load(car);CheckIFV(foot.Carrier==null,"empty-car deploy must not teleport nearby infantry");
  foot.Entering=car;CheckIFV(w.TickBoard(foot)&&car.Passenger==foot&&foot.Carrier==car,"boarding must link passenger and vehicle");
  CheckIFV(!w.CanBoard(eng,car),"IFV has exactly one passenger slot");
  var gun=ReferenceBalance.Weapon(car);CheckIFV(gun.RuleId=="CRM60"&&gun.Damage==25&&gun.Frames==15&&gun.Range==6*Tile&&gun.Burst==1,"GI machinegun profile");
  var enemy=w.Spawn("squad",1,720,900);w.Fire(car,enemy);CheckIFV(enemy.Hp==100&&!w.Shots.Last().Rocket,"GI fires one machinegun round, not an empty-car missile volley");
  float cool=car.Cool;w.Load(car);CheckIFV(foot.Carrier==null&&car.Passenger==null&&ReferenceBalance.Weapon(car).RuleId=="HoverMissile"&&car.Cool==cool,"unload restores missile without resetting cooldown");
  at.Carrier=car;var missile=ReferenceBalance.Weapon(car);
  CheckIFV(missile.RuleId=="CRMissileLauncher"&&missile.Damage==30&&missile.Frames==40&&missile.Range==8*Tile&&missile.Burst==2,"GGI missile profile");
  enemy.P=new PointF(car.P.X+7*Tile,car.P.Y);CheckIFV(CanShootAt(car,enemy)&&ReferenceBalance.Range(car)==8*Tile,"GGI effective range must include cells six to eight");
  w.Guard(car);CheckIFV(ScanRange(car)==16*Tile,"guard range follows passenger weapon");w.Fire(car,enemy);CheckIFV(car.BurstRemaining==1&&w.Shots.Last().Rocket,"GGI two-round rocket volley");
  at.Carrier=null;eng.Carrier=car;CheckIFV(car.BurstRemaining==0&&ReferenceBalance.Weapon(car)==null&&!w.Armed(car)&&w.RepairVehicle(car),"engineer cancels old volley and disables attack");
  tank.P=new PointF(640,900);tank.Hp=200;car.Cool=0;CheckIFV(w.RepairOrder(car,tank)&&w.TickRepairVehicle(car,.1f)&&tank.Hp==250,"repair mode heals 50 vehicle HP");
  CheckIFV(Math.Abs(car.Cool-80/15f)<.001f&&!w.CanRepairVehicle(car,foot),"repair cadence and mechanical-only targeting");
  w.Load(car);CheckIFV(eng.Alive&&eng.Carrier==null&&car.SupportTarget==null&&w.Armed(car),"engineer survives unloading and repair order clears");
  foot.Carrier=car;w.Kill(car);CheckIFV(!foot.Alive&&foot.Carrier==null&&car.Passenger==null,"destroyed carrier consumes passenger without a stale gunner");
  w=new Campaign(true);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",1,2400,1300);
  car=w.Spawn("ifv",0,600,900);eng=w.Spawn("engineer",0,600,900);eng.Carrier=car;tank=w.Spawn("tank",0,665,900);tank.Hp=200;w.Guard(car);
  for(int i=0;i<600;i++)w.Tick(.05f);
  CheckIFV(tank.Hp==tank.T.Hp&&Dist(car.P,new PointF(600,900))<12&&car.SupportTarget==null,"guard repair must heal then return to anchor");
  tank.Hp=200;CheckIFV(w.EscortUnit(car,tank),"repair IFV accepts escort");
  for(int i=0;i<400;i++)w.Tick(.05f);
  CheckIFV(tank.Hp==tank.T.Hp&&car.Escort==tank,"escort must retain its assignment while repairing");
  Console.WriteLine("PASS IFV: one passenger, GI/GGI/engineer modes, actual damage, range, guard, repair, unload/cooldown, destruction; tanks cannot carry.");
 }
 internal static void CheckDeferredNavigation(){
  var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();var f=w.Spawn("barracks",0,600,650);var u=w.Spawn("squad",0,600,700);
  f.Rally=new PointF(102*Tile,30*Tile);w.RallyNewUnit(f,u);float retry=u.RallyRetryAt;int slot=u.RallySlot;
  if(u.RallySource!=null){w.RallyNewUnit(f,u);CheckIFV(u.RallyRetryAt==retry&&u.RallySlot==slot,"same-frame blocked exit repeated its search");}
  w.Halt(u);CheckIFV(u.RallySource==null,"manual stop must cancel deferred rally");
  var island=new Campaign(false,false,true,true);var truck=island.Live(0).First();int before=island.SurveyRouteAttempts;
  island.ExploreGround(truck);island.ExploreGround(truck);island.ExploreGround(truck);
  CheckIFV(island.SurveyRouteAttempts-before<=2,"autonomous exploration exceeded per-update route budget");
  Console.WriteLine("PASS deferred navigation: bounded exploration and retryable exit search respect manual orders.");
 }
}
}
